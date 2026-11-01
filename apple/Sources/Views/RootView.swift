import SwiftUI

enum SidebarItem: Hashable {
    case favorites
    case recents
    case playlist(UUID)
}

struct RootView: View {
    @Environment(LibraryStore.self) private var store
    @State private var selection: SidebarItem? = .favorites
    @State private var showAdd = false

    var body: some View {
        NavigationSplitView {
            List(selection: $selection) {
                Section("Library") {
                    NavigationLink(value: SidebarItem.favorites) {
                        Label("Favorites", systemImage: "star.fill")
                    }
                    NavigationLink(value: SidebarItem.recents) {
                        Label("Recently watched", systemImage: "clock")
                    }
                }
                Section("Playlists") {
                    ForEach(store.playlists) { p in
                        NavigationLink(value: SidebarItem.playlist(p.id)) {
                            HStack {
                                Label(p.name, systemImage: p.isXtream ? "server.rack" : "list.bullet.rectangle")
                                Spacer()
                                if store.loading.contains(p.id) { ProgressView().controlSize(.small) }
                            }
                        }
                        .contextMenu {
                            Button("Refresh", systemImage: "arrow.clockwise") { Task { await store.refresh(p) } }
                            Button("Delete", systemImage: "trash", role: .destructive) { store.remove(p) }
                        }
                    }
                    #if !os(tvOS)
                    .onDelete { idx in idx.map { store.playlists[$0] }.forEach(store.remove) }
                    #endif
                    Button { showAdd = true } label: { Label("Add playlist", systemImage: "plus") }
                }
            }
            .navigationTitle("AENV IPTV")
        } detail: {
            NavigationStack {
                switch selection {
                case .favorites, nil:
                    ChannelBrowserView(title: String(localized: "Favorites"), channels: store.favoriteChannels(),
                                       emptyText: String(localized: "Long-press a channel and tap ★ to add it to favorites."))
                case .recents:
                    ChannelBrowserView(title: String(localized: "Recently watched"), channels: store.recentChannels(),
                                       emptyText: String(localized: "Nothing watched yet."))
                case .playlist(let id):
                    if let p = store.playlists.first(where: { $0.id == id }) {
                        ChannelBrowserView(title: p.name, channels: store.channels[id] ?? [],
                                           emptyText: store.loading.contains(id) ? String(localized: "Loading…") : String(localized: "No content found."))
                            .id(id)
                    }
                }
            }
        }
        .sheet(isPresented: $showAdd) { AddPlaylistView() }
        .alert("Error", isPresented: Binding(get: { store.lastError != nil },
                                             set: { if !$0 { store.lastError = nil } })) {
            Button("OK") { store.lastError = nil }
        } message: { Text(store.lastError ?? "") }
        .onAppear { if store.playlists.isEmpty { showAdd = true } }
    }
}

extension Playlist {
    var isXtream: Bool { if case .xtream = source { true } else { false } }
}
