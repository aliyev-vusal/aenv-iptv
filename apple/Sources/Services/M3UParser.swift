import Foundation

/// Parses M3U / M3U8 (EXTINF) and XSPF playlists.
enum M3UParser {
    struct Result {
        var channels: [Channel]
        var epgURL: URL?
    }

    static func parse(_ text: String) -> Result {
        if text.contains("<playlist") && text.contains("xspf") {
            return Result(channels: XSPFParser.parse(Data(text.utf8)), epgURL: nil)
        }

        var channels: [Channel] = []
        var epgURL: URL?
        var pendingName: String?
        var pendingAttrs: [String: String] = [:]
        var pendingGroup: String?

        for rawLine in text.split(whereSeparator: \.isNewline) {
            let line = rawLine.trimmingCharacters(in: .whitespaces)
            if line.isEmpty { continue }

            if line.hasPrefix("#EXTM3U") {
                let attrs = attributes(in: line)
                if let s = attrs["url-tvg"] ?? attrs["x-tvg-url"],
                   let first = s.split(separator: ",").first,
                   let url = URL(string: String(first)) {
                    epgURL = url
                }
            } else if line.hasPrefix("#EXTINF") {
                pendingAttrs = attributes(in: line)
                pendingName = displayName(in: line)
            } else if line.hasPrefix("#EXTGRP:") {
                pendingGroup = String(line.dropFirst("#EXTGRP:".count))
            } else if line.hasPrefix("#") {
                continue // #EXTVLCOPT etc. are ignored
            } else if let url = URL(string: line) {
                let name = pendingName ?? pendingAttrs["tvg-name"] ?? url.lastPathComponent
                let group = pendingAttrs["group-title"] ?? pendingGroup ?? String(localized: "Other")
                channels.append(Channel(
                    id: url.absoluteString,
                    name: name,
                    logo: pendingAttrs["tvg-logo"].flatMap(URL.init(string:)),
                    group: group.isEmpty ? String(localized: "Other") : group,
                    streamURL: url,
                    tvgID: pendingAttrs["tvg-id"].flatMap { $0.isEmpty ? nil : $0 },
                    kind: guessKind(url: url)
                ))
                pendingName = nil
                pendingAttrs = [:]
                pendingGroup = nil
            }
        }
        return Result(channels: channels, epgURL: epgURL)
    }

    /// Extracts key="value" pairs.
    private static func attributes(in line: String) -> [String: String] {
        var result: [String: String] = [:]
        let pattern = #"([A-Za-z0-9\-_]+)="([^"]*)""#
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return result }
        let ns = line as NSString
        for m in regex.matches(in: line, range: NSRange(location: 0, length: ns.length)) {
            result[ns.substring(with: m.range(at: 1)).lowercased()] = ns.substring(with: m.range(at: 2))
        }
        return result
    }

    /// The channel name is everything after the first comma outside quotes (names may contain commas).
    private static func displayName(in line: String) -> String? {
        var inQuotes = false
        var firstComma: String.Index?
        for idx in line.indices {
            let c = line[idx]
            if c == "\"" { inQuotes.toggle() }
            else if c == ",", !inQuotes { firstComma = idx; break }
        }
        guard let comma = firstComma else { return nil }
        let name = line[line.index(after: comma)...].trimmingCharacters(in: .whitespaces)
        return name.isEmpty ? nil : name
    }

    private static func guessKind(url: URL) -> ContentKind {
        let p = url.path.lowercased()
        if p.contains("/movie/") { return .movie }
        if p.contains("/series/") { return .movie } // single episode URL, played like a movie
        let videoExt = ["mp4", "mkv", "avi", "mov"]
        if videoExt.contains(url.pathExtension.lowercased()) { return .movie }
        return .live
    }
}

/// Minimal XSPF (XML Shareable Playlist Format) parser.
final class XSPFParser: NSObject, XMLParserDelegate {
    private var channels: [Channel] = []
    private var current: [String: String] = [:]
    private var text = ""
    private var inTrack = false

    static func parse(_ data: Data) -> [Channel] {
        let p = XSPFParser()
        let parser = XMLParser(data: data)
        parser.delegate = p
        parser.parse()
        return p.channels
    }

    func parser(_ parser: XMLParser, didStartElement name: String, namespaceURI: String?,
                qualifiedName: String?, attributes: [String: String] = [:]) {
        text = ""
        if name == "track" { inTrack = true; current = [:] }
    }

    func parser(_ parser: XMLParser, foundCharacters string: String) { text += string }

    func parser(_ parser: XMLParser, didEndElement name: String, namespaceURI: String?, qualifiedName: String?) {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if inTrack, ["location", "title", "image", "album"].contains(name) { current[name] = value }
        if name == "track" {
            inTrack = false
            if let loc = current["location"], let url = URL(string: loc) {
                channels.append(Channel(id: url.absoluteString,
                                        name: current["title"] ?? url.lastPathComponent,
                                        logo: current["image"].flatMap(URL.init(string:)),
                                        group: current["album"] ?? String(localized: "Other"),
                                        streamURL: url, tvgID: nil, kind: .live))
            }
        }
    }
}
