import SwiftUI

/// Content browser: live / movie / series sections, groups, search, tile ↔ list view.
struct ChannelBrowserView: View {
    @Environment(LibraryStore.self) private var store
    let title: String
    let channels: [Channel]
    let emptyText: String

    @State private var kind: ContentKind = .live
    @State private var group: String = Self.allGroups
    @State private var query = ""
    @AppStorage("browser.gridMode") private var gridMode = true
    @State private var playing: PlayRequest?

    static let allGroups = String(localized: "All")

    private var availableKinds: [ContentKind] {
        ContentKind.allCases.filter { k in channels.contains { $0.kind == k } }
    }

    private var groups: [String] {
        var seen = Set<String>(), out = [Self.allGroups]
        for c in channels where c.kind == kind && seen.insert(c.group).inserted { out.append(c.group) }
        return out
    }

    private var filtered: [Channel] {
        channels.filter { c in
            c.kind == kind
            && (group == Self.allGroups || c.group == group)
            && (query.isEmpty || c.name.localizedCaseInsensitiveContains(query))
        }
    }

    var body: some View {
        Group {
            if channels.isEmpty {
                ContentUnavailableView(title, systemImage: "tv", description: Text(emptyText))
            } else {
                VStack(spacing: 0) {
                    filters
                    ScrollView {
                        if gridMode { grid } else { list }
                    }
                }
            }
        }
        .navigationTitle(title)
        .searchable(text: $query, prompt: "Search channels, movies, series")
        .toolbar {
            #if !os(tvOS)
            ToolbarItem {
                Button { gridMode.toggle() } label: {
                    Image(systemName: gridMode ? "list.bullet" : "square.grid.2x2")
                }
            }
            #endif
        }
        .onAppear { if !availableKinds.contains(kind), let k = availableKinds.first { kind = k } }
        .onChange(of: kind) { group = Self.allGroups }
        .navigationDestination(for: Channel.self) { SeriesDetailView(series: $0) }
        #if os(macOS)
        .sheet(item: $playing) { req in PlayerView(request: req).frame(minWidth: 960, minHeight: 560) }
        #else
        .fullScreenCover(item: $playing) { req in PlayerView(request: req) }
        #endif
    }

    private var filters: some View {
        VStack(spacing: 8) {
            if availableKinds.count > 1 {
                Picker("Section", selection: $kind) {
                    ForEach(availableKinds) { Label($0.title, systemImage: $0.symbol).tag($0) }
                }
                .pickerStyle(.segmented)
            }
            ScrollView(.horizontal, showsIndicators: false) {
                HStack {
                    ForEach(groups, id: \.self) { g in
                        Button(g) { group = g }
                            .buttonStyle(.bordered)
                            .tint(g == group ? .accentColor : .secondary)
                    }
                }
                .padding(.vertical, 4)
            }
        }
        .padding(.horizontal)
        .padding(.top, 8)
    }

    private var columns: [GridItem] {
        #if os(tvOS)
        [GridItem(.adaptive(minimum: 260), spacing: 40)]
        #else
        [GridItem(.adaptive(minimum: kind == .live ? 140 : 120), spacing: 16)]
        #endif
    }

    private var grid: some View {
        LazyVGrid(columns: columns, spacing: 20) {
            ForEach(filtered) { c in cell(c) { ChannelTile(channel: c) } }
        }
        .padding()
    }

    private var list: some View {
        LazyVStack(alignment: .leading, spacing: 0) {
            ForEach(filtered) { c in
                cell(c) { ChannelRow(channel: c) }
                Divider()
            }
        }
        .padding(.horizontal)
    }

    @ViewBuilder
    private func cell<Content: View>(_ c: Channel, @ViewBuilder label: () -> Content) -> some View {
        Group {
            if c.kind == .series {
                NavigationLink(value: c) { label() }
            } else {
                Button { open(c) } label: { label() }
            }
        }
        #if os(tvOS)
        .buttonStyle(.card)
        #else
        .buttonStyle(.plain)
        #endif
        .contextMenu {
            Button(store.isFavorite(c) ? String(localized: "Remove from favorites") : String(localized: "Add to favorites"),
                   systemImage: store.isFavorite(c) ? "star.slash" : "star") { store.toggleFavorite(c) }
        }
    }

    private func open(_ c: Channel) {
        let queue = filtered.filter { $0.kind != .series }
        playing = PlayRequest(queue: queue, index: queue.firstIndex(of: c) ?? 0)
    }
}

struct PlayRequest: Identifiable {
    let id = UUID()
    var queue: [Channel]
    var index: Int
}

struct ChannelTile: View {
    @Environment(LibraryStore.self) private var store
    let channel: Channel

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            ZStack(alignment: .topTrailing) {
                RoundedRectangle(cornerRadius: 12).fill(.quaternary)
                LogoImage(url: channel.logo, fit: channel.kind == .live)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                if store.isFavorite(channel) {
                    Image(systemName: "star.fill").foregroundStyle(.yellow).padding(6)
                }
            }
            .aspectRatio(channel.kind == .live ? 16 / 10 : 2 / 3, contentMode: .fit)

            Text(channel.name).font(.caption.weight(.semibold)).lineLimit(1)
            if let now = store.nowPlaying(for: channel) {
                Text(now.title).font(.caption2).foregroundStyle(.secondary).lineLimit(1)
                ProgressView(value: now.progress).tint(.accentColor)
            }
        }
        .contentShape(Rectangle())
    }
}

struct ChannelRow: View {
    @Environment(LibraryStore.self) private var store
    let channel: Channel

    var body: some View {
        HStack(spacing: 12) {
            LogoImage(url: channel.logo, fit: true)
                .frame(width: 64, height: 40)
                .background(.quaternary, in: RoundedRectangle(cornerRadius: 6))
            VStack(alignment: .leading, spacing: 2) {
                Text(channel.name).font(.body.weight(.medium)).lineLimit(1)
                Text(store.nowPlaying(for: channel)?.title ?? channel.group)
                    .font(.caption).foregroundStyle(.secondary).lineLimit(1)
            }
            Spacer()
            if store.isFavorite(channel) { Image(systemName: "star.fill").foregroundStyle(.yellow) }
        }
        .padding(.vertical, 8)
        .contentShape(Rectangle())
    }
}

struct LogoImage: View {
    let url: URL?
    var fit = true

    var body: some View {
        AsyncImage(url: url) { phase in
            if let image = phase.image {
                image.resizable().aspectRatio(contentMode: fit ? .fit : .fill).padding(fit ? 8 : 0)
            } else {
                Image(systemName: "tv").font(.title2).foregroundStyle(.secondary)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
