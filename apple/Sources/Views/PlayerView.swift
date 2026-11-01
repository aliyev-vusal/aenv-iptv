import SwiftUI
import AVKit

/// Playback logic: channel switching within a queue, automatic .ts → .m3u8 fallback.
@Observable
@MainActor
final class PlayerController {
    let player = AVPlayer()
    var queue: [Channel]
    var index: Int
    var errorText: String?
    private var statusObservation: NSKeyValueObservation?

    init(queue: [Channel], index: Int) {
        self.queue = queue
        self.index = index
        player.automaticallyWaitsToMinimizeStalling = true
    }

    var current: Channel { queue[index] }

    func start() { load(current.streamURL, allowFallback: true) }

    func select(_ newIndex: Int) {
        guard queue.indices.contains(newIndex) else { return }
        index = newIndex
        start()
    }

    func next() { select((index + 1) % queue.count) }
    func previous() { select((index - 1 + queue.count) % queue.count) }
    func stop() { player.pause(); player.replaceCurrentItem(with: nil); statusObservation = nil }

    private func load(_ url: URL?, allowFallback: Bool) {
        guard let url else { errorText = String(localized: "No link for this item."); return }
        errorText = nil
        let asset = AVURLAsset(url: url, options: [
            "AVURLAssetHTTPHeaderFieldsKey": ["User-Agent": "AENV-IPTV/1.0"]
        ])
        let item = AVPlayerItem(asset: asset)
        item.preferredForwardBufferDuration = 4
        statusObservation = item.observe(\.status) { [weak self] item, _ in
            let status = item.status
            let message = item.error?.localizedDescription
            Task { @MainActor in
                guard let self, status == .failed else { return }
                if allowFallback, let alt = Self.alternate(for: url) {
                    self.load(alt, allowFallback: false)
                } else {
                    self.errorText = message ?? String(localized: "The stream could not be opened.")
                }
            }
        }
        player.replaceCurrentItem(with: item)
        player.play()
    }

    /// AVPlayer cannot play raw MPEG-TS; most IPTV servers also serve the same stream as HLS (.m3u8).
    private static func alternate(for url: URL) -> URL? {
        switch url.pathExtension.lowercased() {
        case "ts": url.deletingPathExtension().appendingPathExtension("m3u8")
        case "": url.appendingPathExtension("m3u8")
        default: nil
        }
    }
}

struct PlayerView: View {
    @Environment(LibraryStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var controller: PlayerController
    @State private var showOverlay = true
    @State private var showList = false
    @State private var hideTask: Task<Void, Never>?

    @MainActor init(request: PlayRequest) {
        _controller = State(initialValue: PlayerController(queue: request.queue, index: request.index))
    }

    private var isLive: Bool { controller.current.kind == .live }

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()

            if isLive {
                PlayerLayerView(player: controller.player).ignoresSafeArea()
                    .contentShape(Rectangle())
                    .onTapGesture { revealOverlay() }
            } else {
                // Movies/series: full system controls (seeking, subtitles, AirPlay)
                VideoPlayer(player: controller.player).ignoresSafeArea()
            }

            if let err = controller.errorText {
                ContentUnavailableView("Playback error", systemImage: "exclamationmark.triangle",
                                       description: Text(err))
                    .foregroundStyle(.white)
            }

            if isLive && showOverlay { liveOverlay.transition(.opacity) }

            if showList { channelList.transition(.move(edge: .leading)) }

            #if !os(tvOS)
            if !isLive { closeButton.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading) }
            #endif
        }
        .animation(.easeInOut(duration: 0.25), value: showOverlay)
        .animation(.easeInOut(duration: 0.25), value: showList)
        .onAppear {
            controller.start()
            store.markWatched(controller.current)
            revealOverlay()
        }
        .onDisappear { controller.stop() }
        .onChange(of: controller.index) {
            store.markWatched(controller.current)
            revealOverlay()
        }
        #if os(tvOS)
        .focusable(isLive && !showList)
        .onMoveCommand { dir in
            guard isLive, !showList else { return }
            switch dir {
            case .up: controller.previous()
            case .down: controller.next()
            case .left, .right: showList = true
            @unknown default: break
            }
        }
        .onPlayPauseCommand { togglePlay() }
        .onExitCommand { if showList { showList = false } else { dismiss() } }
        #else
        .onKeyPress(.upArrow) { guard isLive else { return .ignored }; controller.previous(); return .handled }
        .onKeyPress(.downArrow) { guard isLive else { return .ignored }; controller.next(); return .handled }
        .onKeyPress(.space) { togglePlay(); return .handled }
        .onKeyPress(.escape) { dismiss(); return .handled }
        #endif
    }

    // MARK: - Live TV overlay

    private var liveOverlay: some View {
        let channel = controller.current
        let upcoming = store.upcoming(for: channel, limit: 2)
        return VStack {
            HStack(alignment: .top, spacing: 16) {
                #if !os(tvOS)
                closeButton
                #endif
                LogoImage(url: channel.logo).frame(width: 96, height: 60)
                    .background(.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 8))
                VStack(alignment: .leading, spacing: 4) {
                    Text(channel.name).font(.title3.bold())
                    if let now = upcoming.first, now.isLive {
                        Text("Now: \(now.title)").font(.callout)
                        ProgressView(value: now.progress).frame(maxWidth: 320)
                        if upcoming.count > 1 {
                            Text("Next \(upcoming[1].start.formatted(date: .omitted, time: .shortened)): \(upcoming[1].title)")
                                .font(.caption).foregroundStyle(.white.opacity(0.7))
                        }
                    } else {
                        Text(channel.group).font(.callout).foregroundStyle(.white.opacity(0.7))
                    }
                }
                Spacer()
            }
            .padding()
            .background(LinearGradient(colors: [.black.opacity(0.8), .clear], startPoint: .top, endPoint: .bottom))

            Spacer()

            HStack(spacing: 24) {
                controlButton("chevron.up", String(localized: "Previous")) { controller.previous() }
                controlButton("list.bullet", String(localized: "Channels")) { showList = true }
                controlButton("chevron.down", String(localized: "Next")) { controller.next() }
                controlButton(store.isFavorite(channel) ? "star.fill" : "star", String(localized: "Favorite")) {
                    store.toggleFavorite(channel)
                }
            }
            .padding()
            .background(.ultraThinMaterial, in: Capsule())
            .padding(.bottom, 32)
        }
        .foregroundStyle(.white)
    }

    private var channelList: some View {
        HStack(spacing: 0) {
            ScrollViewReader { proxy in
                List {
                    ForEach(Array(controller.queue.enumerated()), id: \.element.id) { i, c in
                        Button {
                            controller.select(i)
                            showList = false
                        } label: {
                            HStack {
                                LogoImage(url: c.logo).frame(width: 48, height: 30)
                                VStack(alignment: .leading) {
                                    Text(c.name).lineLimit(1)
                                    if let now = store.nowPlaying(for: c) {
                                        Text(now.title).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                                    }
                                }
                                Spacer()
                                if i == controller.index { Image(systemName: "play.fill") }
                            }
                        }
                        .id(c.id)
                    }
                }
                .onAppear { proxy.scrollTo(controller.current.id, anchor: .center) }
            }
            .frame(width: 420)
            .background(.regularMaterial)

            Color.black.opacity(0.001)
                .onTapGesture { showList = false }
        }
    }

    // MARK: - Helpers

    private var closeButton: some View {
        Button { dismiss() } label: {
            Image(systemName: "xmark").font(.title3.bold()).padding(12)
                .background(.ultraThinMaterial, in: Circle())
        }
        .buttonStyle(.plain)
        .foregroundStyle(.white)
        .padding()
    }

    private func controlButton(_ symbol: String, _ label: String, action: @escaping () -> Void) -> some View {
        Button(action: { action(); revealOverlay() }) {
            Image(systemName: symbol).font(.title2).frame(width: 44, height: 44)
        }
        .accessibilityLabel(label)
        #if !os(tvOS)
        .buttonStyle(.plain)
        #endif
    }

    private func togglePlay() {
        if controller.player.timeControlStatus == .paused { controller.player.play() } else { controller.player.pause() }
        revealOverlay()
    }

    private func revealOverlay() {
        showOverlay = true
        hideTask?.cancel()
        hideTask = Task {
            try? await Task.sleep(for: .seconds(5))
            if !Task.isCancelled { showOverlay = false }
        }
    }
}

// MARK: - AVPlayerLayer (no system controls, used for live TV)

#if os(macOS)
struct PlayerLayerView: NSViewRepresentable {
    let player: AVPlayer
    func makeNSView(context: Context) -> NSView {
        let v = NSView()
        let layer = AVPlayerLayer(player: player)
        layer.videoGravity = .resizeAspect
        v.layer = layer
        v.wantsLayer = true
        return v
    }
    func updateNSView(_ nsView: NSView, context: Context) {}
}
#else
struct PlayerLayerView: UIViewRepresentable {
    let player: AVPlayer

    final class LayerView: UIView {
        override class var layerClass: AnyClass { AVPlayerLayer.self }
        var playerLayer: AVPlayerLayer { layer as! AVPlayerLayer }
    }

    func makeUIView(context: Context) -> LayerView {
        let v = LayerView()
        v.playerLayer.player = player
        v.playerLayer.videoGravity = .resizeAspect
        v.backgroundColor = .black
        return v
    }
    func updateUIView(_ uiView: LayerView, context: Context) {}
}
#endif
