using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Aenv.Models;

namespace Aenv.Services;

/// <summary>
/// Playlists, favorites, recently watched.
/// If OneDrive is installed the file lives in OneDrive\AenvIPTV\library.json,
/// so it syncs across all Windows PCs automatically (the Windows equivalent of iCloud).
/// </summary>
public class LibraryStore
{
    public static LibraryStore Shared { get; } = new();

    public List<Playlist> Playlists { get; private set; } = new();
    public HashSet<string> Favorites { get; private set; } = new();
    public List<string> Recents { get; private set; } = new();

    public Dictionary<Guid, List<Channel>> Channels { get; } = new();
    public Dictionary<Guid, Dictionary<string, List<Programme>>> Guides { get; } = new();
    public HashSet<Guid> Loading { get; } = new();
    /// <summary>Playlists whose last refresh failed, with the user-facing reason.</summary>
    public Dictionary<Guid, string> Failed { get; } = new();
    private readonly Dictionary<Guid, string?> _epgUrls = new();

    public event Action? Changed;
    public event Action<string>? Error;

    public string FilePath { get; }
    private readonly FileSystemWatcher? _watcher;
    private DateTime _lastOwnWrite;

    private record Snapshot(List<Playlist> Playlists, List<string> Favorites, List<string> Recents);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private LibraryStore()
    {
        var dir = AppPaths.DataDir;
        FilePath = AppPaths.LibraryFile;
        Load();
        LoadCachedChannels();

        // Reload when another PC changes the file via OneDrive
        _watcher = new FileSystemWatcher(dir, "library.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size };
        _watcher.Changed += (_, _) =>
        {
            if ((DateTime.Now - _lastOwnWrite).TotalSeconds < 2) return;
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(500);
                var before = Playlists.Select(p => p.Id).ToHashSet();
                Load();
                LoadCachedChannels();
                foreach (var p in Playlists.Where(p => !before.Contains(p.Id))) await RefreshAsync(p, force: false);
                Changed?.Invoke();
            });
        };
        _watcher.EnableRaisingEvents = true;
    }

    // ---------- Playlists ----------

    public void Add(Playlist p)
    {
        Playlists.Add(p);
        Save();
        _ = RefreshAsync(p);
    }

    public void Remove(Playlist p)
    {
        Playlists.RemoveAll(x => x.Id == p.Id);
        Channels.Remove(p.Id);
        Guides.Remove(p.Id);
        Failed.Remove(p.Id);
        ChannelCache.Remove(p.Id);
        Save();
        Changed?.Invoke();
    }

    /// <summary>Shows the last known channel lists immediately, before any network request.</summary>
    private void LoadCachedChannels()
    {
        foreach (var p in Playlists)
        {
            if (Channels.ContainsKey(p.Id) || ChannelCache.Load(p.Id) is not { } entry) continue;
            foreach (var c in entry.Channels) c.PlaylistId = p.Id;
            Channels[p.Id] = entry.Channels;
            _epgUrls[p.Id] = entry.EpgUrl;
        }
    }

    /// <summary>
    /// Reloads a playlist. With <paramref name="force"/> = false (app start) a fresh cached list is used
    /// without contacting the server. Errors only interrupt the user when they asked for the refresh or
    /// when there is nothing cached to show; otherwise the playlist is marked with a warning badge.
    /// </summary>
    public async Task RefreshAsync(Playlist p, bool force = true)
    {
        if (Loading.Contains(p.Id)) return;

        if (!force && Channels.ContainsKey(p.Id) && ChannelCache.IsFresh(p.Id))
        {
            await LoadGuideAsync(p.Id, p.EpgUrl ?? _epgUrls.GetValueOrDefault(p.Id), force: false);
            return;
        }

        Loading.Add(p.Id);
        Changed?.Invoke();
        string? epg = p.EpgUrl;
        var ok = false;
        try
        {
            List<Channel> items;
            if (p.Type == SourceType.M3U)
            {
                var uri = new Uri(p.Url);
                var text = uri.IsFile
                    ? await File.ReadAllTextAsync(uri.LocalPath)
                    : await Http.GetStringAsync(p.Url);
                var parsed = await Task.Run(() => M3UParser.Parse(text, p.Id));
                items = parsed.Channels;
                epg ??= parsed.EpgUrl;
            }
            else
            {
                var client = new XtreamClient(p.Url, p.Username ?? "", p.Password ?? "", p.Id);
                items = await client.LoadAllAsync();
                epg ??= client.EpgUrl;
            }
            Channels[p.Id] = items;
            _epgUrls[p.Id] = epg;
            ChannelCache.Save(p.Id, items, epg);
            Failed.Remove(p.Id);
            ok = true;
        }
        catch (Exception ex)
        {
            Failed[p.Id] = ex.Message;
            if (force || !Channels.ContainsKey(p.Id)) Error?.Invoke($"{p.Name}: {ex.Message}");
        }
        finally
        {
            Loading.Remove(p.Id);
            Changed?.Invoke();
        }

        // The guide is loaded after the channel list, not in parallel, to stay within provider limits
        if (ok) await LoadGuideAsync(p.Id, epg, force);
    }

    private async Task LoadGuideAsync(Guid id, string? url, bool force)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Guides[id] = await EpgService.LoadAsync(url, force);
            Changed?.Invoke();
        }
        catch { /* the app works without EPG */ }
    }

    /// <summary>Refreshes playlists one after another (never in parallel, see <see cref="Http"/>).</summary>
    public async Task RefreshAllAsync(bool force = true)
    {
        foreach (var p in Playlists.ToList()) await RefreshAsync(p, force);
    }

    // ---------- Favorites / history ----------

    public bool IsFavorite(Channel c) => Favorites.Contains(c.Id);

    public void ToggleFavorite(Channel c)
    {
        if (!Favorites.Remove(c.Id)) Favorites.Add(c.Id);
        Save();
        Changed?.Invoke();
    }

    public void MarkWatched(Channel c)
    {
        Recents.Remove(c.Id);
        Recents.Insert(0, c.Id);
        if (Recents.Count > 30) Recents.RemoveRange(30, Recents.Count - 30);
        Save();
    }

    public IEnumerable<Channel> AllChannels => Channels.Values.SelectMany(x => x);

    public List<Channel> FavoriteChannels() => AllChannels.Where(c => Favorites.Contains(c.Id)).ToList();

    public List<Channel> RecentChannels()
    {
        var byId = new Dictionary<string, Channel>();
        foreach (var c in AllChannels) byId.TryAdd(c.Id, c);
        return Recents.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    // ---------- EPG ----------

    public List<Programme> ProgrammesFor(Channel c)
    {
        if (c.TvgId == null) return new();
        if (Guides.TryGetValue(c.PlaylistId, out var g) && g.TryGetValue(c.TvgId, out var list)) return list;
        foreach (var guide in Guides.Values)
            if (guide.TryGetValue(c.TvgId, out var l)) return l;
        return new();
    }

    public Programme? NowPlaying(Channel c) => ProgrammesFor(c).FirstOrDefault(p => p.IsLive);

    public List<Programme> Upcoming(Channel c, int limit = 5) =>
        ProgrammesFor(c).Where(p => p.Stop > DateTimeOffset.Now).Take(limit).ToList();

    // ---------- Persistence ----------

    private void Save()
    {
        try
        {
            _lastOwnWrite = DateTime.Now;
            var json = JsonSerializer.Serialize(new Snapshot(Playlists, Favorites.ToList(), Recents), JsonOpts);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex) { Error?.Invoke(Loc.T("save_error") + ex.Message); }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var snap = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(FilePath));
            if (snap == null) return;
            Playlists = snap.Playlists ?? new();
            Favorites = new HashSet<string>(snap.Favorites ?? new());
            Recents = snap.Recents ?? new();
        }
        catch { /* file was being written; retry next time */ }
    }
}
