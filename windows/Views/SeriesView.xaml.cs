using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Aenv.Models;
using Aenv.Services;

namespace Aenv.Views;

/// <summary>Seasons and episodes of an Xtream series, shown inside the main window.</summary>
public partial class SeriesView : UserControl
{
    /// <summary>Host should start playback of the queue at the given index.</summary>
    public event Action<List<Channel>, int>? PlayRequested;
    /// <summary>Host should return to the channel list.</summary>
    public event Action? CloseRequested;

    public Channel Series => _series;

    private readonly Channel _series;
    private readonly LibraryStore _store = LibraryStore.Shared;
    private List<Episode> _episodes = new();

    public SeriesView(Channel series)
    {
        InitializeComponent();
        _series = series;
        NameText.Text = series.Name;
        GroupText.Text = series.Group;
        if (Uri.TryCreate(series.Logo, UriKind.Absolute, out var logo))
            try { Poster.Source = new BitmapImage(logo); } catch { }
        UpdateFavButton();
        Loaded += async (_, _) =>
        {
            if (_episodes.Count == 0) await LoadAsync();
            EpisodeList.Focus();
        };
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        StatusText.Text = Loc.T("loading");
        try
        {
            var p = _store.Playlists.FirstOrDefault(x => x.Id == _series.PlaylistId);
            if (p == null || p.Type != SourceType.Xtream || _series.SeriesId == null) { StatusText.Text = Loc.T("no_episodes"); return; }
            _episodes = await new XtreamClient(p.Url, p.Username ?? "", p.Password ?? "", p.Id).EpisodesAsync(_series.SeriesId);
            var seasons = _episodes.Select(e => e.Season).Distinct().OrderBy(s => s).ToList();
            SeasonBox.ItemsSource = seasons.Select(s => Loc.F("season_n", s)).ToList();
            SeasonBox.Tag = seasons;
            if (seasons.Count > 0) SeasonBox.SelectedIndex = 0;
            StatusText.Text = Loc.F("episodes_n", _episodes.Count);
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private int CurrentSeason =>
        SeasonBox.Tag is List<int> s && SeasonBox.SelectedIndex >= 0 ? s[SeasonBox.SelectedIndex] : 1;

    private void Season_Changed(object sender, SelectionChangedEventArgs e) =>
        EpisodeList.ItemsSource = _episodes.Where(x => x.Season == CurrentSeason).ToList();

    private void Play(Episode ep)
    {
        var list = _episodes.Where(x => x.Season == CurrentSeason).ToList();
        var queue = list.Select(x => new Channel
        {
            Id = "ep-" + x.Id,
            Name = $"{_series.Name} – S{x.Season:00}E{x.Number:00}  {x.Title}",
            Logo = _series.Logo, Group = _series.Group,
            StreamUrl = x.StreamUrl, Kind = ContentKind.Movie, PlaylistId = _series.PlaylistId
        }).ToList();
        PlayRequested?.Invoke(queue, Math.Max(0, list.IndexOf(ep)));
    }

    private void Episode_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EpisodeList.SelectedItem is Episode ep) Play(ep);
    }

    private void Episode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && EpisodeList.SelectedItem is Episode ep) Play(ep);
    }

    private void Fav_Click(object sender, RoutedEventArgs e)
    {
        _store.ToggleFavorite(_series);
        UpdateFavButton();
    }

    private void UpdateFavButton() =>
        FavButton.Content = _store.IsFavorite(_series) ? Loc.T("fav_remove") : Loc.T("fav_add");

    private void Back_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void View_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Back or Key.BrowserBack) { CloseRequested?.Invoke(); e.Handled = true; }
    }
}
