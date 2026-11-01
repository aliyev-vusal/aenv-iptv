import SwiftUI

/// Xtream series: seasons and episodes.
struct SeriesDetailView: View {
    @Environment(LibraryStore.self) private var store
    let series: Channel

    @State private var episodes: [Episode] = []
    @State private var season = 1
    @State private var loading = true
    @State private var playing: PlayRequest?

    private var seasons: [Int] { Array(Set(episodes.map(\.season))).sorted() }

    var body: some View {
        List {
            HStack(alignment: .top, spacing: 16) {
                LogoImage(url: series.logo, fit: false)
                    .frame(width: 120, height: 180)
                    .clipShape(RoundedRectangle(cornerRadius: 10))
                VStack(alignment: .leading, spacing: 8) {
                    Text(series.name).font(.title2.bold())
                    Text(series.group).foregroundStyle(.secondary)
                    Button(store.isFavorite(series) ? String(localized: "Remove from favorites") : String(localized: "Add to favorites"),
                           systemImage: store.isFavorite(series) ? "star.fill" : "star") {
                        store.toggleFavorite(series)
                    }
                }
            }

            if seasons.count > 1 {
                Picker("Season", selection: $season) {
                    ForEach(seasons, id: \.self) { Text("Season \($0)").tag($0) }
                }
            }

            if loading {
                ProgressView()
            } else if episodes.isEmpty {
                Text("No episodes found.").foregroundStyle(.secondary)
            }

            ForEach(episodes.filter { $0.season == season }) { ep in
                Button { play(ep) } label: {
                    Label("\(ep.number). \(ep.title)", systemImage: "play.circle")
                }
            }
        }
        .navigationTitle(series.name)
        .task { await load() }
        #if os(macOS)
        .sheet(item: $playing) { PlayerView(request: $0).frame(minWidth: 960, minHeight: 560) }
        #else
        .fullScreenCover(item: $playing) { PlayerView(request: $0) }
        #endif
    }

    private func load() async {
        defer { loading = false }
        guard let sid = series.seriesID,
              let playlist = store.playlists.first(where: { p in store.channels[p.id]?.contains(series) == true }),
              case let .xtream(server, user, pass) = playlist.source else { return }
        episodes = (try? await XtreamClient(server: server, username: user, password: pass).episodes(seriesID: sid)) ?? []
        season = seasons.first ?? 1
    }

    private func play(_ ep: Episode) {
        let list = episodes.filter { $0.season == season }
        let queue = list.map {
            Channel(id: "ep-\($0.id)", name: "\(series.name) – S\($0.season)E\($0.number)",
                    logo: series.logo, group: series.group, streamURL: $0.streamURL,
                    tvgID: nil, kind: .movie)
        }
        playing = PlayRequest(queue: queue, index: list.firstIndex(of: ep) ?? 0)
    }
}
