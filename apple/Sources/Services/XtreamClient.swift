import Foundation

/// Xtream Codes "player_api.php" client.
/// Servers return some fields as Int and others as String, so JSONSerialization is used.
struct XtreamClient {
    let server: URL
    let username: String
    let password: String

    enum XtreamError: LocalizedError {
        case badResponse, authFailed
        var errorDescription: String? {
            switch self {
            case .badResponse: String(localized: "The server returned an unexpected response.")
            case .authFailed: String(localized: "Wrong username or password.")
            }
        }
    }

    var epgURL: URL? { url(path: "xmltv.php") }

    func authenticate() async throws {
        let obj = try await request(action: nil)
        guard let dict = obj as? [String: Any],
              let info = dict["user_info"] as? [String: Any],
              int(info["auth"]) == 1 else { throw XtreamError.authFailed }
    }

    func loadAll() async throws -> [Channel] {
        async let live = load(kind: .live)
        async let movies = load(kind: .movie)
        async let series = load(kind: .series)
        return try await live + movies + series
    }

    func load(kind: ContentKind) async throws -> [Channel] {
        let (catAction, streamAction): (String, String) = switch kind {
        case .live: ("get_live_categories", "get_live_streams")
        case .movie: ("get_vod_categories", "get_vod_streams")
        case .series: ("get_series_categories", "get_series")
        }
        async let catsObj = request(action: catAction)
        async let itemsObj = request(action: streamAction)

        var categories: [String: String] = [:]
        for c in (try await catsObj as? [[String: Any]]) ?? [] {
            if let id = str(c["category_id"]) { categories[id] = str(c["category_name"]) ?? String(localized: "Other") }
        }

        let items = (try await itemsObj as? [[String: Any]]) ?? []
        return items.compactMap { item -> Channel? in
            let group = str(item["category_id"]).flatMap { categories[$0] } ?? String(localized: "Other")
            let name = str(item["name"]) ?? "?"
            switch kind {
            case .live:
                guard let sid = str(item["stream_id"]) else { return nil }
                let u = streamURL(type: "live", id: sid, ext: "m3u8")
                return Channel(id: "xt-live-\(server.host ?? "")-\(sid)", name: name,
                               logo: str(item["stream_icon"]).flatMap(URL.init(string:)),
                               group: group, streamURL: u,
                               tvgID: str(item["epg_channel_id"]), kind: .live)
            case .movie:
                guard let sid = str(item["stream_id"]) else { return nil }
                let ext = str(item["container_extension"]) ?? "mp4"
                return Channel(id: "xt-vod-\(server.host ?? "")-\(sid)", name: name,
                               logo: str(item["stream_icon"]).flatMap(URL.init(string:)),
                               group: group, streamURL: streamURL(type: "movie", id: sid, ext: ext),
                               tvgID: nil, kind: .movie)
            case .series:
                guard let sid = str(item["series_id"]) else { return nil }
                return Channel(id: "xt-series-\(server.host ?? "")-\(sid)", name: name,
                               logo: str(item["cover"]).flatMap(URL.init(string:)),
                               group: group, streamURL: nil, tvgID: nil,
                               kind: .series, seriesID: sid)
            }
        }
    }

    func episodes(seriesID: String) async throws -> [Episode] {
        let obj = try await request(action: "get_series_info", extra: [URLQueryItem(name: "series_id", value: seriesID)])
        guard let dict = obj as? [String: Any], let seasons = dict["episodes"] as? [String: Any] else { return [] }
        var result: [Episode] = []
        for (seasonKey, value) in seasons {
            for ep in (value as? [[String: Any]]) ?? [] {
                guard let id = str(ep["id"]) else { continue }
                let ext = str(ep["container_extension"]) ?? "mp4"
                result.append(Episode(id: id,
                                      title: str(ep["title"]) ?? String(localized: "Episode"),
                                      season: Int(seasonKey) ?? int(ep["season"]) ?? 1,
                                      number: int(ep["episode_num"]) ?? 0,
                                      streamURL: streamURL(type: "series", id: id, ext: ext)))
            }
        }
        return result.sorted { ($0.season, $0.number) < ($1.season, $1.number) }
    }

    // MARK: - Helpers

    private func streamURL(type: String, id: String, ext: String) -> URL {
        server.appendingPathComponent(type).appendingPathComponent(username)
            .appendingPathComponent(password).appendingPathComponent("\(id).\(ext)")
    }

    private func url(path: String, extra: [URLQueryItem] = []) -> URL? {
        var comps = URLComponents(url: server.appendingPathComponent(path), resolvingAgainstBaseURL: false)
        comps?.queryItems = [URLQueryItem(name: "username", value: username),
                             URLQueryItem(name: "password", value: password)] + extra
        return comps?.url
    }

    private func request(action: String?, extra: [URLQueryItem] = []) async throws -> Any {
        var items = extra
        if let action { items.insert(URLQueryItem(name: "action", value: action), at: 0) }
        guard let u = url(path: "player_api.php", extra: items) else { throw XtreamError.badResponse }
        var req = URLRequest(url: u)
        req.setValue("AENV-IPTV/1.0", forHTTPHeaderField: "User-Agent")
        let (data, _) = try await URLSession.shared.data(for: req)
        do { return try JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed]) }
        catch { throw XtreamError.badResponse }
    }

    private func str(_ v: Any?) -> String? {
        switch v {
        case let s as String: s.isEmpty ? nil : s
        case let n as NSNumber: n.stringValue
        default: nil
        }
    }

    private func int(_ v: Any?) -> Int? {
        switch v {
        case let n as NSNumber: n.intValue
        case let s as String: Int(s)
        default: nil
        }
    }
}
