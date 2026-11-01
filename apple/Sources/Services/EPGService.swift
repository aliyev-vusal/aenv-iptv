import Foundation

/// XMLTV programme guide. Supports .xml and .xml.gz (GZIP).
/// To save memory only the window [now - 3 h, now + 24 h] is kept.
actor EPGService {
    static let shared = EPGService()

    private var cache: [URL: [String: [Programme]]] = [:]

    func guide(from url: URL) async throws -> [String: [Programme]] {
        if let cached = cache[url] { return cached }
        let (raw, _) = try await URLSession.shared.data(from: url)
        let data = try Gzip.isGzip(raw) ? Gzip.decompress(raw) : raw
        let result = XMLTVParser.parse(data, from: .now.addingTimeInterval(-3 * 3600),
                                       to: .now.addingTimeInterval(24 * 3600))
        cache[url] = result
        return result
    }

    func clear() { cache.removeAll() }
}

final class XMLTVParser: NSObject, XMLParserDelegate {
    private var result: [String: [Programme]] = [:]
    private let from: Date, to: Date
    private var current: (channel: String, start: Date, stop: Date)?
    private var title = "", desc = "", text = ""

    private static let formatter: DateFormatter = {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.dateFormat = "yyyyMMddHHmmss Z"
        return f
    }()

    private init(from: Date, to: Date) { self.from = from; self.to = to }

    static func parse(_ data: Data, from: Date, to: Date) -> [String: [Programme]] {
        let p = XMLTVParser(from: from, to: to)
        let parser = XMLParser(data: data)
        parser.delegate = p
        parser.parse()
        for key in p.result.keys { p.result[key]?.sort { $0.start < $1.start } }
        return p.result
    }

    private static func date(_ s: String?) -> Date? {
        guard let s else { return nil }
        if let d = formatter.date(from: s) { return d }
        return formatter.date(from: String(s.prefix(14)) + " +0000")
    }

    func parser(_ parser: XMLParser, didStartElement name: String, namespaceURI: String?,
                qualifiedName: String?, attributes a: [String: String] = [:]) {
        text = ""
        guard name == "programme" else { return }
        if let ch = a["channel"], let s = Self.date(a["start"]), let e = Self.date(a["stop"]),
           e > from, s < to {
            current = (ch, s, e); title = ""; desc = ""
        } else {
            current = nil
        }
    }

    func parser(_ parser: XMLParser, foundCharacters string: String) {
        if current != nil { text += string }
    }

    func parser(_ parser: XMLParser, didEndElement name: String, namespaceURI: String?, qualifiedName: String?) {
        guard let cur = current else { return }
        switch name {
        case "title": if title.isEmpty { title = text.trimmingCharacters(in: .whitespacesAndNewlines) }
        case "desc": if desc.isEmpty { desc = text.trimmingCharacters(in: .whitespacesAndNewlines) }
        case "programme":
            result[cur.channel, default: []].append(
                Programme(channelID: cur.channel, title: title, desc: desc.isEmpty ? nil : desc,
                          start: cur.start, stop: cur.stop))
            current = nil
        default: break
        }
    }
}

/// GZIP decoder built on Apple's Compression framework (raw DEFLATE), no third-party library.
enum Gzip {
    enum GzipError: Error { case invalid }

    static func isGzip(_ d: Data) -> Bool { d.count > 18 && d[d.startIndex] == 0x1f && d[d.startIndex + 1] == 0x8b }

    static func decompress(_ data: Data) throws -> Data {
        let d = [UInt8](data)
        guard d.count > 18 else { throw GzipError.invalid }
        let flags = d[3]
        var i = 10
        if flags & 0x04 != 0 { // FEXTRA
            guard i + 2 <= d.count else { throw GzipError.invalid }
            i += 2 + Int(d[i]) + Int(d[i + 1]) << 8
        }
        if flags & 0x08 != 0 { while i < d.count, d[i] != 0 { i += 1 }; i += 1 } // FNAME
        if flags & 0x10 != 0 { while i < d.count, d[i] != 0 { i += 1 }; i += 1 } // FCOMMENT
        if flags & 0x02 != 0 { i += 2 } // FHCRC
        guard i < d.count - 8 else { throw GzipError.invalid }
        let deflate = Data(d[i..<(d.count - 8)])
        return try (deflate as NSData).decompressed(using: .zlib) as Data
    }
}
