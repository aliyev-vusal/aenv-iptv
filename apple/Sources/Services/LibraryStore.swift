import Foundation
import Observation

/// Playlists, favorites and recently watched.
/// Stored in a local JSON file and synced across Apple devices via the iCloud key-value store.
@Observable
@MainActor
final class LibraryStore {
    private(set) var playlists: [Playlist] = []
    private(set) var favorites: Set<String> = []
    private(set) var recents: [String] = []          // channel IDs, newest first

    /// Loaded content (in memory only): playlist.id -> channels
    private(set) var channels: [UUID: [Channel]] = [:]
    private(set) var guides: [UUID: [String: [Programme]]] = [:]
    private(set) var loading: Set<UUID> = []
    var lastError: String?

    private let kvs = NSUbiquitousKeyValueStore.default
    private let fileURL: URL = {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("AenvIPTV", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir.appendingPathComponent("library.json")
    }()

    private struct Snapshot: Codable {
        var playlists: [Playlist]
        var favorites: [String]
        var recents: [String]
    }

    init() {
        loadLocal()
        mergeFromCloud()
        NotificationCenter.default.addObserver(
            forName: NSUbiquitousKeyValueStore.didChangeExternallyNotification,
            object: kvs, queue: .main) { [weak self] _ in
                Task { @MainActor in self?.mergeFromCloud() }
            }
        kvs.synchronize()
    }

    // MARK: - Playlists

    func add(_ playlist: Playlist) {
        playlists.append(playlist)
        save()
        Task { await refresh(playlist) }
    }

    func remove(_ playlist: Playlist) {
        playlists.removeAll { $0.id == playlist.id }
        channels[playlist.id] = nil
        guides[playlist.id] = nil
        save()
    }

    func refresh(_ playlist: Playlist) async {
        loading.insert(playlist.id)
        defer { loading.remove(playlist.id) }
        do {
            var epg = playlist.epgURL
            let items: [Channel]
            switch playlist.source {
            case .m3u(let url):
                let (data, _) = try await URLSession.shared.data(from: url)
                let text = String(decoding: data, as: UTF8.self)
                let parsed = await Task.detached { M3UParser.parse(text) }.value
                items = parsed.channels
                if epg == nil { epg = parsed.epgURL }
            case .xtream(let server, let user, let pass):
                let client = XtreamClient(server: server, username: user, password: pass)
                items = try await client.loadAll()
                if epg == nil { epg = client.epgURL }
            }
            channels[playlist.id] = items
            if let epg {
                Task { guides[playlist.id] = try? await EPGService.shared.guide(from: epg) }
            }
        } catch {
            lastError = "\(playlist.name): \(error.localizedDescription)"
        }
    }

    func refreshAll() async {
        await withTaskGroup(of: Void.self) { group in
            for p in playlists { group.addTask { await self.refresh(p) } }
        }
    }

    // MARK: - Favorites and recents

    func isFavorite(_ c: Channel) -> Bool { favorites.contains(c.id) }

    func toggleFavorite(_ c: Channel) {
        if favorites.contains(c.id) { favorites.remove(c.id) } else { favorites.insert(c.id) }
        save()
    }

    func markWatched(_ c: Channel) {
        recents.removeAll { $0 == c.id }
        recents.insert(c.id, at: 0)
        recents = Array(recents.prefix(30))
        save()
    }

    var allChannels: [Channel] { channels.values.flatMap { $0 } }

    func favoriteChannels() -> [Channel] { allChannels.filter { favorites.contains($0.id) } }

    func recentChannels() -> [Channel] {
        let byID = Dictionary(allChannels.map { ($0.id, $0) }, uniquingKeysWith: { a, _ in a })
        return recents.compactMap { byID[$0] }
    }

    // MARK: - EPG

    func programmes(for channel: Channel) -> [Programme] {
        guard let key = channel.tvgID else { return [] }
        for guide in guides.values {
            if let list = guide[key] { return list }
        }
        return []
    }

    func nowPlaying(for channel: Channel) -> Programme? {
        programmes(for: channel).first { $0.isLive }
    }

    func upcoming(for channel: Channel, limit: Int = 5) -> [Programme] {
        Array(programmes(for: channel).filter { $0.stop > .now }.prefix(limit))
    }

    // MARK: - Persistence / iCloud

    private func save() {
        let snap = Snapshot(playlists: playlists, favorites: Array(favorites), recents: recents)
        guard let data = try? JSONEncoder().encode(snap) else { return }
        try? data.write(to: fileURL, options: .atomic)
        kvs.set(data, forKey: "library")
        kvs.set(Date.now.timeIntervalSince1970, forKey: "library.updatedAt")
        UserDefaults.standard.set(Date.now.timeIntervalSince1970, forKey: "library.updatedAt")
    }

    private func loadLocal() {
        guard let data = try? Data(contentsOf: fileURL),
              let snap = try? JSONDecoder().decode(Snapshot.self, from: data) else { return }
        apply(snap)
    }

    /// Adopts the iCloud copy if it is newer than the local one.
    private func mergeFromCloud() {
        let cloudTime = kvs.double(forKey: "library.updatedAt")
        let localTime = UserDefaults.standard.double(forKey: "library.updatedAt")
        guard cloudTime > localTime,
              let data = kvs.data(forKey: "library"),
              let snap = try? JSONDecoder().decode(Snapshot.self, from: data) else { return }
        let newPlaylists = snap.playlists.filter { p in !playlists.contains { $0.id == p.id } }
        apply(snap)
        try? data.write(to: fileURL, options: .atomic)
        UserDefaults.standard.set(cloudTime, forKey: "library.updatedAt")
        for p in newPlaylists { Task { await refresh(p) } }
    }

    private func apply(_ snap: Snapshot) {
        playlists = snap.playlists
        favorites = Set(snap.favorites)
        recents = snap.recents
    }
}
