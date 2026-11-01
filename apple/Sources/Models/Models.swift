import Foundation

/// Playlist source: either a plain M3U/XSPF URL or an Xtream Codes API account.
enum PlaylistSource: Codable, Hashable {
    case m3u(url: URL)
    case xtream(server: URL, username: String, password: String)
}

struct Playlist: Codable, Identifiable, Hashable {
    var id: UUID = UUID()
    var name: String
    var source: PlaylistSource
    /// Optional XMLTV URL (.xml or .xml.gz). Filled automatically for Xtream.
    var epgURL: URL?
    var addedAt: Date = .now
}

enum ContentKind: String, Codable, CaseIterable, Identifiable {
    case live, movie, series
    var id: String { rawValue }
    var title: String {
        switch self {
        case .live: String(localized: "Live TV")
        case .movie: String(localized: "Movies")
        case .series: String(localized: "Series")
        }
    }
    var symbol: String {
        switch self {
        case .live: "dot.radiowaves.left.and.right"
        case .movie: "film"
        case .series: "tv"
        }
    }
}

struct Channel: Identifiable, Hashable, Codable {
    /// Stable ID used for favorites and iCloud sync.
    var id: String
    var name: String
    var logo: URL?
    var group: String
    var streamURL: URL?          // nil for series (episodes are loaded separately)
    var tvgID: String?
    var kind: ContentKind
    var seriesID: String?        // Xtream series only
}

struct Episode: Identifiable, Hashable {
    var id: String
    var title: String
    var season: Int
    var number: Int
    var streamURL: URL
}

struct Programme: Identifiable, Hashable {
    var id = UUID()
    var channelID: String
    var title: String
    var desc: String?
    var start: Date
    var stop: Date

    var isLive: Bool { (start...stop).contains(.now) }
    var progress: Double {
        let total = stop.timeIntervalSince(start)
        guard total > 0 else { return 0 }
        return min(max(Date.now.timeIntervalSince(start) / total, 0), 1)
    }
}
