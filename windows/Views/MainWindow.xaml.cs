using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Aenv.Models;
using Aenv.Services;

namespace Aenv.Views;

public class SidebarEntry
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Glyph { get; init; } = "";
    public string Badge { get; init; } = "";
    public Playlist? Playlist { get; init; }
    public string? Tip { get; init; }
}

public partial class MainWindow : Window
{
    private static string AllGroups => Loc.T("all");
    private readonly LibraryStore _store = LibraryStore.Shared;
    private readonly DispatcherTimer _searchDebounce = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private List<Channel> _source = new();
    private List<Channel> _filtered = new();
    private ContentKind _kind = ContentKind.Live;
    private string _group = "";
    private bool _gridMode = true;
    private string _selectedKey = "fav";
    private bool _suppress;

    public MainWindow()
    {
        InitializeComponent();
        _group = AllGroups;
        ApplyViewMode();

        // Language picker
        LanguageBox.ItemsSource = Loc.Languages.Select(l => new { l.Code, l.Name }).ToList();
        LanguageBox.SelectedValuePath = "Code";
        LanguageBox.SelectedValue = Loc.I.Lang;
        Loc.I.LanguageChanged += () =>
        {
            _group = AllGroups;
            RebuildSidebar();
            ReloadSource();
        };

        _store.Changed += () => Dispatcher.Invoke(() => { RebuildSidebar(); ReloadSource(); });
        _store.Error += msg => Dispatcher.Invoke(() =>
            MessageBox.Show(this, msg, Loc.T("error"), MessageBoxButton.OK, MessageBoxImage.Warning));

        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); ApplyFilter(); };

        // Refresh every minute so EPG progress moves
        var clock = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        clock.Tick += (_, _) => ApplyFilter(keepScroll: true);
        clock.Start();

        KeyDown += (_, e) =>
        {
            if (PlayerVisible) return;
            if (e.Key == Key.F5) { _ = _store.RefreshAllAsync(); e.Handled = true; }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { CloseSeries(); SearchBox.Focus(); e.Handled = true; }
        };

        Closing += (_, _) => _player?.Shutdown();

        Loaded += async (_, _) =>
        {
            RebuildSidebar();
            ReloadSource();
            if (_store.Playlists.Count == 0) AddPlaylist_Click(this, new RoutedEventArgs());
            // Start-up uses cached lists when they are fresh: no burst of requests to the provider
            await _store.RefreshAllAsync(force: false);
        };
    }

    // ================= Sidebar =================

    private void RebuildSidebar()
    {
        _suppress = true;
        var entries = new List<SidebarEntry>
        {
            new() { Key = "fav", Title = Loc.T("favorites"), Glyph = "", Badge = _store.Favorites.Count.ToString() },
            new() { Key = "recent", Title = Loc.T("recents"), Glyph = "" }
        };
        foreach (var p in _store.Playlists)
        {
            var count = _store.Channels.TryGetValue(p.Id, out var ch) ? ch.Count.ToString("N0") : "";
            var failed = _store.Failed.TryGetValue(p.Id, out var reason);
            var badge = _store.Loading.Contains(p.Id) ? Loc.T("loading") : failed ? $"⚠ {count}".Trim() : count;
            entries.Add(new SidebarEntry
            {
                Key = p.Id.ToString(), Title = p.Name, Playlist = p, Badge = badge,
                Tip = failed ? reason : null,
                Glyph = p.Type == SourceType.Xtream ? "" : ""
            });
        }
        Sidebar.ItemsSource = entries;
        Sidebar.SelectedItem = entries.FirstOrDefault(e => e.Key == _selectedKey) ?? entries[0];
        _suppress = false;
    }

    private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || Sidebar.SelectedItem is not SidebarEntry entry) return;
        _selectedKey = entry.Key;
        CloseSeries();
        _group = AllGroups;
        SearchBox.Text = "";
        ReloadSource(resetKind: true);
    }

    private SidebarEntry? SelectedEntry => Sidebar.SelectedItem as SidebarEntry;

    // ================= Content =================

    private void ReloadSource(bool resetKind = false)
    {
        var entry = SelectedEntry;
        TitleText.Text = entry?.Title ?? "";
        _source = entry?.Key switch
        {
            "fav" => _store.FavoriteChannels(),
            "recent" => _store.RecentChannels(),
            _ when entry?.Playlist != null && _store.Channels.TryGetValue(entry.Playlist.Id, out var list) => list,
            _ => new List<Channel>()
        };

        var kinds = _source.Select(c => c.Kind).Distinct().ToHashSet();
        KindLive.Visibility = kinds.Contains(ContentKind.Live) ? Visibility.Visible : Visibility.Collapsed;
        KindMovie.Visibility = kinds.Contains(ContentKind.Movie) ? Visibility.Visible : Visibility.Collapsed;
        KindSeries.Visibility = kinds.Contains(ContentKind.Series) ? Visibility.Visible : Visibility.Collapsed;

        if ((resetKind || !kinds.Contains(_kind)) && kinds.Count > 0)
        {
            var k = kinds.Contains(ContentKind.Live) ? ContentKind.Live : kinds.First();
            SetKind(k);
        }
        RebuildGroups();
        ApplyFilter(keepScroll: !resetKind);
    }

    private void SetKind(ContentKind k)
    {
        _suppress = true;
        _kind = k;
        KindLive.IsChecked = k == ContentKind.Live;
        KindMovie.IsChecked = k == ContentKind.Movie;
        KindSeries.IsChecked = k == ContentKind.Series;
        _suppress = false;
    }

    private void Kind_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppress || !IsLoaded) return;
        _kind = sender == KindMovie ? ContentKind.Movie : sender == KindSeries ? ContentKind.Series : ContentKind.Live;
        _group = AllGroups;
        RebuildGroups();
        ApplyFilter();
    }

    private void RebuildGroups()
    {
        _suppress = true;
        var groups = new List<string> { AllGroups };
        var seen = new HashSet<string>();
        foreach (var c in _source.Where(c => c.Kind == _kind))
            if (seen.Add(c.Group)) groups.Add(c.Group);
        if (!groups.Contains(_group)) _group = AllGroups;
        GroupList.ItemsSource = groups;
        GroupList.SelectedItem = _group;
        _suppress = false;
    }

    private void Group_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || GroupList.SelectedItem is not string g) return;
        _group = g;
        ApplyFilter();
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedValue is string code) Loc.I.SetLanguage(code);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void ApplyFilter(bool keepScroll = false)
    {
        var q = SearchBox.Text.Trim();
        _filtered = _source.Where(c =>
                c.Kind == _kind &&
                (_group == AllGroups || c.Group == _group) &&
                (q.Length == 0 || c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        foreach (var c in _filtered)
        {
            c.IsFavorite = _store.IsFavorite(c);
            var now = c.IsLive ? _store.NowPlaying(c) : null;
            c.Subtitle = now?.Title ?? c.Group;
            c.Progress = now?.Progress ?? 0;
        }

        var selected = ContentList.SelectedItem;
        ContentList.ItemsSource = null;
        ContentList.ItemsSource = _filtered;
        if (keepScroll && selected is Channel sc && _filtered.Contains(sc)) ContentList.SelectedItem = sc;

        var loading = SelectedEntry?.Playlist is { } p && _store.Loading.Contains(p.Id);
        EmptyState.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = loading ? Loc.T("loading")
            : _source.Count == 0 && _selectedKey == "fav" ? Loc.T("empty_fav")
            : _source.Count == 0 && _selectedKey == "recent" ? Loc.T("empty_recent")
            : Loc.T("nothing_found");
    }

    private void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _gridMode = !_gridMode;
        ApplyViewMode();
    }

    private void ApplyViewMode()
    {
        ContentList.ItemTemplate = (DataTemplate)FindResource(_gridMode ? "TileTemplate" : "RowTemplate");
        ContentList.ItemsPanel = (ItemsPanelTemplate)FindResource(_gridMode ? "GridPanel" : "ListPanel");
        ViewToggleIcon.Text = _gridMode ? "" : "";
    }

    // ================= Playback =================

    private PlayerView? _player;
    private SeriesView? _seriesView;

    private bool PlayerVisible => PlayerHost.Visibility == Visibility.Visible;

    private void Open(Channel c)
    {
        if (c.Kind == ContentKind.Series)
        {
            ShowSeries(c);
            return;
        }
        var queue = _filtered.Where(x => x.Kind != ContentKind.Series).ToList();
        ShowPlayer(queue, Math.Max(0, queue.IndexOf(c)));
    }

    private void ShowSeries(Channel series)
    {
        _seriesView = new SeriesView(series);
        _seriesView.CloseRequested += CloseSeries;
        _seriesView.PlayRequested += ShowPlayer;
        SeriesHost.Child = _seriesView;
        SeriesHost.Visibility = Visibility.Visible;
    }

    private void CloseSeries()
    {
        if (_seriesView == null) return;
        var series = _seriesView.Series;
        SeriesHost.Child = null;
        SeriesHost.Visibility = Visibility.Collapsed;
        _seriesView = null;
        FocusItem(series);
    }

    /// <summary>Plays inside this window; the player covers the window until the user goes back.</summary>
    private void ShowPlayer(List<Channel> queue, int index)
    {
        if (_player == null)
        {
            _player = new PlayerView();
            _player.CloseRequested += ClosePlayer;
            _player.ChannelChanged += c => { if (_filtered.Contains(c)) ContentList.SelectedItem = c; };
        }
        if (PlayerHost.Child == null) PlayerHost.Child = _player;
        PlayerHost.Visibility = Visibility.Visible;
        _player.Play(queue, index);
    }

    private void ClosePlayer()
    {
        if (_player == null) return;
        var last = _player.Current;
        _player.Stop();
        PlayerHost.Child = null; // detaching also hides VLC's overlay window
        PlayerHost.Visibility = Visibility.Collapsed;
        Title = "AENV IPTV";

        // Favorites and recents may have changed while watching
        RebuildSidebar();
        if (_selectedKey is "fav" or "recent") ReloadSource(); else ApplyFilter(keepScroll: true);

        if (_seriesView != null) _seriesView.Focus();
        else if (last != null) FocusItem(last);
    }

    /// <summary>Selects, scrolls to and focuses an item so keyboard navigation continues from it.</summary>
    private void FocusItem(Channel c)
    {
        var item = _filtered.FirstOrDefault(x => x.Id == c.Id);
        if (item == null) { ContentList.Focus(); return; }
        ContentList.SelectedItem = item;
        ContentList.ScrollIntoView(item);
        Dispatcher.InvokeAsync(() =>
        {
            if (ContentList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem li) li.Focus();
            else ContentList.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as ListBoxItem)?.DataContext is Channel c) Open(c);
    }

    private void ContentList_KeyDown(object sender, KeyEventArgs e)
    {
        if (ContentList.SelectedItem is not Channel c) return;
        if (e.Key == Key.Enter) { Open(c); e.Handled = true; }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None) { _store.ToggleFavorite(c); e.Handled = true; }
    }

    // Right-click selects the ListBoxItem, so the selected item is the menu target
    private Channel? MenuChannel(object sender) => ContentList.SelectedItem as Channel;

    private void PlayMenu_Click(object sender, RoutedEventArgs e) { if (MenuChannel(sender) is { } c) Open(c); }
    private void ToggleFav_Click(object sender, RoutedEventArgs e) { if (MenuChannel(sender) is { } c) _store.ToggleFavorite(c); }
    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (MenuChannel(sender)?.StreamUrl is { } url) Clipboard.SetText(url);
    }

    // ================= Playlist actions =================

    private void AddPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddPlaylistWindow { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            _selectedKey = dlg.Result.Id.ToString();
            _store.Add(dlg.Result);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private async void RefreshAll_Click(object sender, RoutedEventArgs e) => await _store.RefreshAllAsync();

    private async void RefreshPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry?.Playlist is { } p) await _store.RefreshAsync(p);
    }

    private void EditPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry?.Playlist is not { } p) return;
        var dlg = new AddPlaylistWindow(p) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            _store.Remove(p);
            dlg.Result.Id = p.Id;
            _store.Add(dlg.Result);
        }
    }

    private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry?.Playlist is not { } p) return;
        if (MessageBox.Show(this, Loc.F("delete_confirm", p.Name), "AENV IPTV", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _selectedKey = "fav";
            _store.Remove(p);
        }
    }
}
