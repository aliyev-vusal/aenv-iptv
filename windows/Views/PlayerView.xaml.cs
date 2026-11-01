using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aenv.Models;
using Aenv.Services;
using LibVLCSharp.Shared;

namespace Aenv.Views;

/// <summary>
/// Player embedded in the main window. One instance (and one VLC MediaPlayer) is reused for
/// the whole session: the host attaches it to show the player and detaches it to go back.
/// </summary>
public partial class PlayerView : UserControl
{
    private readonly LibraryStore _store = LibraryStore.Shared;
    private List<Channel> _queue = new();
    private int _index;

    private readonly MediaPlayer _mp;
    private Media? _media;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastActivity = DateTime.Now;
    private bool _seeking;
    private bool _pendingPlay;
    private Task? _stopTask;
    private bool _fullscreen;
    private WindowState _prevState;
    private WindowStyle _prevStyle;
    private ResizeMode _prevResize;

    /// <summary>Raised when the user wants to leave the player (Back button or Esc).</summary>
    public event Action? CloseRequested;

    /// <summary>Raised whenever another channel starts, so the host can sync its selection.</summary>
    public event Action<Channel>? ChannelChanged;

    public Channel? Current => _queue.Count > 0 ? _queue[_index] : null;
    public bool IsFullscreen => _fullscreen;
    private bool IsLive => Current?.Kind == ContentKind.Live;

    public PlayerView()
    {
        InitializeComponent();
        _mp = new MediaPlayer(App.Vlc) { Volume = (int)VolumeSlider.Value };

        // VLC raises events on its own thread → marshal to UI with BeginInvoke
        _mp.Playing += (_, _) => Ui(() => { StatusText.Visibility = Visibility.Collapsed; PlayButton.Content = ""; });
        _mp.Paused += (_, _) => Ui(() => PlayButton.Content = "");
        _mp.Buffering += (_, e) => Ui(() =>
        {
            StatusText.Visibility = e.Cache < 100 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = Loc.F("buffering", e.Cache.ToString("0"));
        });
        _mp.EncounteredError += (_, _) => Ui(() =>
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = Loc.T("stream_error");
        });
        _mp.EndReached += (_, _) => Ui(() => { if (!IsLive && _index < _queue.Count - 1) Select(_index + 1); });

        // The video surface needs a window handle, so attach VLC only once the view is in a window
        Video.Loaded += (_, _) =>
        {
            Video.MediaPlayer ??= _mp;
            if (_pendingPlay) { _pendingPlay = false; PlayCurrent(); }
        };

        _timer.Tick += (_, _) => Tick();
        Loc.I.LanguageChanged += () => { if (Current != null) UpdateInfo(); };
    }

    private void Ui(Action a) => Dispatcher.BeginInvoke(a);

    // ================= Public API for the host =================

    /// <summary>Starts playing <paramref name="queue"/>[<paramref name="index"/>]; ↑/↓ moves within the queue.</summary>
    public void Play(List<Channel> queue, int index)
    {
        _queue = queue;
        _index = Math.Clamp(index, 0, Math.Max(0, queue.Count - 1));
        foreach (var c in _queue)
        {
            var now = c.IsLive ? _store.NowPlaying(c) : null;
            c.Subtitle = now?.Title ?? c.Group;
        }
        QueueList.ItemsSource = null;
        QueueList.ItemsSource = _queue;
        ListPanel.Visibility = Visibility.Collapsed;
        _timer.Start();

        if (Video.IsLoaded && Video.MediaPlayer != null) PlayCurrent();
        else _pendingPlay = true;

        Dispatcher.InvokeAsync(() => Overlay.Focus(), DispatcherPriority.Input);
    }

    /// <summary>Stops playback (off the UI thread) and leaves fullscreen.</summary>
    public void Stop()
    {
        _timer.Stop();
        _pendingPlay = false;
        if (_fullscreen) ToggleFullscreen();
        var media = _media;
        _media = null;
        _stopTask = Task.Run(() => { _mp.Stop(); media?.Dispose(); });
    }

    /// <summary>Releases VLC when the app closes.</summary>
    public void Shutdown()
    {
        _timer.Stop();
        var media = _media;
        Video.MediaPlayer = null;
        Task.Run(() => { _mp.Stop(); _mp.Dispose(); media?.Dispose(); });
    }

    // ================= Playback =================

    private void PlayCurrent()
    {
        // A background Stop() from the previous session must finish first, or it could stop this stream
        if (_stopTask is { IsCompleted: false })
        {
            _stopTask.ContinueWith(_ => Ui(PlayCurrent));
            return;
        }

        var c = Current;
        StatusText.Visibility = Visibility.Collapsed;
        if (c == null || string.IsNullOrEmpty(c.StreamUrl)) return;

        var old = _media;
        _media = new Media(App.Vlc, new Uri(c.StreamUrl));
        _media.AddOption(":http-user-agent=AENV-IPTV/1.0");
        _media.AddOption(IsLive ? ":network-caching=2000" : ":network-caching=3000");
        _mp.Play(_media);
        old?.Dispose();

        _store.MarkWatched(c);
        QueueList.SelectedIndex = _index;
        QueueList.ScrollIntoView(c);
        UpdateInfo();
        ShowControls();
        ChannelChanged?.Invoke(c);
    }

    private void Select(int i)
    {
        if (_queue.Count == 0) return;
        _index = (i % _queue.Count + _queue.Count) % _queue.Count;
        PlayCurrent();
    }

    private void UpdateInfo()
    {
        var c = Current;
        if (c == null) return;
        if (Window.GetWindow(this) is { } w) w.Title = $"{c.Name} – AENV IPTV";
        ChannelName.Text = c.Name;
        LogoImage.Source = Uri.TryCreate(c.Logo, UriKind.Absolute, out var u) ? SafeImage(u) : null;
        FavButton.Content = _store.IsFavorite(c) ? "" : "";
        SeekRow.Visibility = IsLive ? Visibility.Collapsed : Visibility.Visible;

        var upcoming = IsLive ? _store.Upcoming(c, 2) : new List<Programme>();
        if (upcoming.Count > 0 && upcoming[0].IsLive)
        {
            var now = upcoming[0];
            NowText.Text = $"{Loc.T("now")}  {now.Start.LocalDateTime:HH:mm}–{now.Stop.LocalDateTime:HH:mm}   {now.Title}";
            NowProgress.Value = now.Progress;
            NowProgress.Visibility = Visibility.Visible;
            NextText.Text = upcoming.Count > 1 ? $"{Loc.T("next")}  {upcoming[1].Start.LocalDateTime:HH:mm}   {upcoming[1].Title}" : "";
        }
        else
        {
            NowText.Text = c.Group;
            NowProgress.Visibility = Visibility.Collapsed;
            NextText.Text = "";
        }
    }

    private static BitmapImage? SafeImage(Uri u)
    {
        try { return new BitmapImage(u); } catch { return null; }
    }

    private void Tick()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm");

        if (!IsLive && !_seeking && _mp.Length > 0)
        {
            SeekSlider.Value = _mp.Position;
            TimeText.Text = Format(_mp.Time);
            DurationText.Text = Format(_mp.Length);
        }
        if (IsLive && DateTime.Now.Second == 0) UpdateInfo(); // EPG progress

        var idle = (DateTime.Now - _lastActivity).TotalSeconds > 4;
        if (idle && ListPanel.Visibility != Visibility.Visible && !BottomBar.IsMouseOver && !TopBar.IsMouseOver
            && TopBar.Visibility == Visibility.Visible)
        {
            TopBar.Visibility = BottomBar.Visibility = Visibility.Collapsed;
            Overlay.Cursor = Cursors.None;
        }
    }

    internal static string Format(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    private void ShowControls()
    {
        _lastActivity = DateTime.Now;
        TopBar.Visibility = BottomBar.Visibility = Visibility.Visible;
        Overlay.Cursor = null;
    }

    // ================= Controls =================

    private void TogglePlay()
    {
        if (_mp.IsPlaying) _mp.Pause(); else _mp.Play();
        ShowControls();
    }

    private void SeekBy(int seconds)
    {
        if (IsLive || _mp.Length <= 0) return;
        _mp.Time = Math.Clamp(_mp.Time + seconds * 1000L, 0, _mp.Length);
        ShowControls();
    }

    private void ToggleList()
    {
        ListPanel.Visibility = ListPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (ListPanel.Visibility == Visibility.Visible)
        {
            if (Current != null) QueueList.ScrollIntoView(Current);
            QueueList.Focus();
        }
        else Overlay.Focus();
        ShowControls();
    }

    /// <summary>Fullscreen applies to the main window that hosts this view.</summary>
    public void ToggleFullscreen()
    {
        if (Window.GetWindow(this) is not { } w) return;
        if (!_fullscreen)
        {
            _prevState = w.WindowState; _prevStyle = w.WindowStyle; _prevResize = w.ResizeMode;
            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.NoResize;
            if (w.WindowState == WindowState.Maximized) w.WindowState = WindowState.Normal; // so the taskbar is covered
            w.WindowState = WindowState.Maximized;
            FullButton.Content = "";
        }
        else
        {
            w.WindowStyle = _prevStyle;
            w.ResizeMode = _prevResize;
            w.WindowState = _prevState;
            FullButton.Content = "";
        }
        _fullscreen = !_fullscreen;
        ShowControls();
    }

    private void ChangeVolume(int delta) => VolumeSlider.Value = Math.Clamp(VolumeSlider.Value + delta, 0, 100);

    private void GoBack()
    {
        if (ListPanel.Visibility == Visibility.Visible) { ToggleList(); return; }
        CloseRequested?.Invoke();
    }

    private void Player_KeyDown(object sender, KeyEventArgs e)
    {
        // While the channel list is open, arrow keys navigate the list
        if (ListPanel.Visibility == Visibility.Visible && e.Key is Key.Up or Key.Down or Key.Enter)
        {
            if (e.Key == Key.Enter && QueueList.SelectedIndex >= 0) { Select(QueueList.SelectedIndex); ToggleList(); e.Handled = true; }
            return;
        }

        e.Handled = true;
        switch (e.Key)
        {
            case Key.Up or Key.PageUp: Select(_index - 1); break;
            case Key.Down or Key.PageDown: Select(_index + 1); break;
            case Key.Left: if (IsLive) ToggleList(); else SeekBy(-10); break;
            case Key.Right: if (IsLive) ToggleList(); else SeekBy(30); break;
            case Key.Space or Key.MediaPlayPause: TogglePlay(); break;
            case Key.F11: ToggleFullscreen(); break;
            case Key.F: if (Current != null) { _store.ToggleFavorite(Current); UpdateInfo(); } break;
            case Key.L: ToggleList(); break;
            case Key.M: Mute_Click(this, new RoutedEventArgs()); break;
            case Key.OemPlus or Key.Add: ChangeVolume(+5); break;
            case Key.OemMinus or Key.Subtract: ChangeVolume(-5); break;
            case Key.Back or Key.BrowserBack: GoBack(); break;
            case Key.Escape:
                if (ListPanel.Visibility == Visibility.Visible) ToggleList();
                else if (_fullscreen) ToggleFullscreen();
                else CloseRequested?.Invoke();
                break;
            default: e.Handled = false; ShowControls(); break;
        }
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e) => ShowControls();

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != Overlay) return; // click on a button
        Overlay.Focus();
        if (e.ClickCount == 2) ToggleFullscreen();
        else if (ListPanel.Visibility == Visibility.Visible) ToggleList();
        else ShowControls();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();
    private void Prev_Click(object sender, RoutedEventArgs e) => Select(_index - 1);
    private void Next_Click(object sender, RoutedEventArgs e) => Select(_index + 1);
    private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePlay();
    private void List_Click(object sender, RoutedEventArgs e) => ToggleList();
    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void Fav_Click(object sender, RoutedEventArgs e)
    {
        if (Current == null) return;
        _store.ToggleFavorite(Current);
        UpdateInfo();
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _mp.Mute = !_mp.Mute;
        MuteButton.Content = _mp.Mute ? "" : "";
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_mp == null) return; // during InitializeComponent
        _mp.Volume = (int)e.NewValue;
        if (_mp.Mute && e.NewValue > 0) Mute_Click(this, new RoutedEventArgs());
    }

    private void Seek_MouseDown(object sender, MouseButtonEventArgs e) => _seeking = true;

    private void Seek_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _mp.Position = (float)SeekSlider.Value;
        _seeking = false;
    }

    private void Queue_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QueueList.SelectedIndex >= 0) { Select(QueueList.SelectedIndex); ToggleList(); }
    }

    private void Queue_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && QueueList.SelectedIndex >= 0) { Select(QueueList.SelectedIndex); ToggleList(); e.Handled = true; }
    }
}
