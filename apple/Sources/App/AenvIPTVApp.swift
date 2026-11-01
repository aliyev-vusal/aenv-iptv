import SwiftUI
import AVFoundation

@main
struct AenvIPTVApp: App {
    @State private var store = LibraryStore()

    init() {
        #if os(iOS) || os(tvOS)
        try? AVAudioSession.sharedInstance().setCategory(.playback, mode: .moviePlayback)
        #endif
    }

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(store)
                .task { await store.refreshAll() }
                #if os(macOS)
                .frame(minWidth: 900, minHeight: 560)
                #endif
        }
        #if os(macOS)
        .commands {
            CommandGroup(after: .newItem) {
                Button("Refresh playlists") { Task { await store.refreshAll() } }
                    .keyboardShortcut("r")
            }
        }
        #endif
    }
}
