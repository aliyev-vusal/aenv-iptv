import SwiftUI

struct AddPlaylistView: View {
    @Environment(LibraryStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    enum Mode: String, CaseIterable, Identifiable {
        case m3u = "M3U / XSPF URL"
        case xtream = "Xtream Codes"
        var id: String { rawValue }
    }

    @State private var mode: Mode = .m3u
    @State private var name = ""
    @State private var m3uURL = ""
    @State private var epgURL = ""
    @State private var server = ""
    @State private var username = ""
    @State private var password = ""
    @State private var checking = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            Form {
                Picker("Type", selection: $mode) {
                    ForEach(Mode.allCases) { Text(LocalizedStringKey($0.rawValue)).tag($0) }
                }
                #if !os(tvOS)
                .pickerStyle(.segmented)
                #endif

                TextField("Name (e.g. Living room TV)", text: $name)

                switch mode {
                case .m3u:
                    Section("Playlist") {
                        urlField("https://…/playlist.m3u", text: $m3uURL)
                        urlField("EPG URL (optional, .xml / .xml.gz)", text: $epgURL)
                    }
                case .xtream:
                    Section("Server") {
                        urlField("http://server.com:8080", text: $server)
                        plainField("Username", text: $username)
                        SecureField("Password", text: $password)
                    }
                }

                if let error { Text(error).foregroundStyle(.red) }
            }
            .formStyle(.grouped)
            .navigationTitle("New playlist")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    if checking { ProgressView() } else { Button("Add") { Task { await submit() } }.disabled(!isValid) }
                }
            }
        }
        #if os(macOS)
        .frame(minWidth: 460, minHeight: 360)
        #endif
    }

    private var isValid: Bool {
        switch mode {
        case .m3u: URL(string: m3uURL.trimmed)?.scheme != nil
        case .xtream: URL(string: server.trimmed)?.scheme != nil && !username.isEmpty && !password.isEmpty
        }
    }

    private func submit() async {
        error = nil
        switch mode {
        case .m3u:
            guard let url = URL(string: m3uURL.trimmed) else { return }
            store.add(Playlist(name: name.isEmpty ? (url.host ?? String(localized: "Playlist")) : name,
                               source: .m3u(url: url),
                               epgURL: URL(string: epgURL.trimmed)))
            dismiss()
        case .xtream:
            guard let url = URL(string: server.trimmed) else { return }
            checking = true
            defer { checking = false }
            do {
                try await XtreamClient(server: url, username: username, password: password).authenticate()
                store.add(Playlist(name: name.isEmpty ? (url.host ?? "Xtream") : name,
                                   source: .xtream(server: url, username: username, password: password)))
                dismiss()
            } catch {
                self.error = error.localizedDescription
            }
        }
    }

    @ViewBuilder private func urlField(_ title: LocalizedStringKey, text: Binding<String>) -> some View {
        TextField(title, text: text)
            .autocorrectionDisabled()
            #if os(iOS)
            .keyboardType(.URL)
            .textInputAutocapitalization(.never)
            #endif
    }

    @ViewBuilder private func plainField(_ title: LocalizedStringKey, text: Binding<String>) -> some View {
        TextField(title, text: text)
            .autocorrectionDisabled()
            #if os(iOS) || os(tvOS)
            .textInputAutocapitalization(.never)
            #endif
    }
}

extension String {
    var trimmed: String { trimmingCharacters(in: .whitespacesAndNewlines) }
}
