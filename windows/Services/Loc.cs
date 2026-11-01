using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aenv;

/// <summary>
/// Simple localization: Azerbaijani, English, Turkish, German, Spanish.
/// XAML: Text="{l:T add_playlist}" · code: Loc.T("add_playlist")
/// All XAML bindings refresh automatically when the language changes.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    // Lazy so that the static arrays (Languages, Strings) are initialized first
    private static Loc? _instance;
    public static Loc I => _instance ??= new Loc();

    public static readonly (string Code, string Name)[] Languages =
    {
        ("az", "Azərbaycanca"),
        ("en", "English"),
        ("tr", "Türkçe"),
        ("de", "Deutsch"),
        ("es", "Español"),
    };

    public string Lang { get; private set; } = "en";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    private static string SettingsPath => Services.AppPaths.SettingsFile;

    private Loc()
    {
        string? saved = null;
        try
        {
            if (File.Exists(SettingsPath))
                saved = JsonDocument.Parse(File.ReadAllText(SettingsPath)).RootElement.GetProperty("lang").GetString();
        }
        catch { }
        var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Lang = Index(saved) >= 0 ? saved! : Index(system) >= 0 ? system : "en";
    }

    public string this[string key] => T(key);

    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var row)) return key;
        var i = Index(I.Lang);
        return row[i < 0 ? 1 : i];
    }

    public static string F(string key, params object[] args) => string.Format(T(key), args);

    public void SetLanguage(string code)
    {
        if (Index(code) < 0 || code == Lang) return;
        Lang = code;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { lang = code }));
        }
        catch { }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    private static int Index(string? code) => Array.FindIndex(Languages, l => l.Code == code);

    //                                   az, en, tr, de, es
    private static readonly Dictionary<string, string[]> Strings = new()
    {
        ["app_error"] = new[] { "AENV IPTV – xəta", "AENV IPTV – error", "AENV IPTV – hata", "AENV IPTV – Fehler", "AENV IPTV – error" },
        ["vlc_failed"] = new[] { "VLC başladıla bilmədi", "Could not start VLC", "VLC başlatılamadı", "VLC konnte nicht gestartet werden", "No se pudo iniciar VLC" },
        ["add_playlist"] = new[] { "Pleylist əlavə et", "Add playlist", "Liste ekle", "Playlist hinzufügen", "Añadir lista" },
        ["refresh_all"] = new[] { "Hamısını yenilə  (F5)", "Refresh all  (F5)", "Tümünü yenile  (F5)", "Alle aktualisieren  (F5)", "Actualizar todo  (F5)" },
        ["refresh"] = new[] { "Yenilə", "Refresh", "Yenile", "Aktualisieren", "Actualizar" },
        ["edit"] = new[] { "Redaktə et", "Edit", "Düzenle", "Bearbeiten", "Editar" },
        ["delete"] = new[] { "Sil", "Delete", "Sil", "Löschen", "Eliminar" },
        ["live"] = new[] { "Canlı TV", "Live TV", "Canlı TV", "Live-TV", "TV en directo" },
        ["movies"] = new[] { "Filmlər", "Movies", "Filmler", "Filme", "Películas" },
        ["series"] = new[] { "Seriallar", "Series", "Diziler", "Serien", "Series" },
        ["view_toggle"] = new[] { "Tile / siyahı rejimi", "Tile / list view", "Kutu / liste görünümü", "Kachel- / Listenansicht", "Vista de mosaico / lista" },
        ["play"] = new[] { "Oynat", "Play", "Oynat", "Abspielen", "Reproducir" },
        ["fav_toggle"] = new[] { "Favoritlərə əlavə et / çıxar   (F)", "Add to / remove from favorites   (F)", "Favorilere ekle / çıkar   (F)", "Zu Favoriten hinzufügen / entfernen   (F)", "Añadir a / quitar de favoritos   (F)" },
        ["copy_link"] = new[] { "Linki kopyala", "Copy link", "Bağlantıyı kopyala", "Link kopieren", "Copiar enlace" },
        ["favorites"] = new[] { "Favoritlər", "Favorites", "Favoriler", "Favoriten", "Favoritos" },
        ["recents"] = new[] { "Son baxılanlar", "Recently watched", "Son izlenenler", "Zuletzt gesehen", "Vistos recientemente" },
        ["loading"] = new[] { "Yüklənir…", "Loading…", "Yükleniyor…", "Wird geladen…", "Cargando…" },
        ["all"] = new[] { "Hamısı", "All", "Tümü", "Alle", "Todo" },
        ["empty_fav"] = new[] { "Kanalın üzərinə sağ klik edib favoritlərə əlavə edin.", "Right-click a channel to add it to favorites.", "Favorilere eklemek için bir kanala sağ tıklayın.", "Klicken Sie mit der rechten Maustaste auf einen Sender, um ihn zu den Favoriten hinzuzufügen.", "Haz clic derecho en un canal para añadirlo a favoritos." },
        ["empty_recent"] = new[] { "Hələ heç nəyə baxılmayıb.", "Nothing watched yet.", "Henüz hiçbir şey izlenmedi.", "Noch nichts angesehen.", "Aún no has visto nada." },
        ["nothing_found"] = new[] { "Heç nə tapılmadı.", "Nothing found.", "Hiçbir şey bulunamadı.", "Nichts gefunden.", "No se encontró nada." },
        ["delete_confirm"] = new[] { "\"{0}\" silinsin?", "Delete \"{0}\"?", "\"{0}\" silinsin mi?", "\"{0}\" löschen?", "¿Eliminar \"{0}\"?" },
        ["error"] = new[] { "Xəta", "Error", "Hata", "Fehler", "Error" },
        ["search"] = new[] { "Axtar…", "Search…", "Ara…", "Suchen…", "Buscar…" },
        ["language"] = new[] { "Dil", "Language", "Dil", "Sprache", "Idioma" },
        ["new_playlist"] = new[] { "Yeni pleylist", "New playlist", "Yeni liste", "Neue Playlist", "Nueva lista" },
        ["edit_playlist"] = new[] { "Pleylisti redaktə et", "Edit playlist", "Listeyi düzenle", "Playlist bearbeiten", "Editar lista" },
        ["type_m3u"] = new[] { "M3U / XSPF link", "M3U / XSPF URL", "M3U / XSPF bağlantısı", "M3U / XSPF-Link", "Enlace M3U / XSPF" },
        ["type_xtream"] = new[] { "Xtream Codes", "Xtream Codes", "Xtream Codes", "Xtream Codes", "Xtream Codes" },
        ["name"] = new[] { "Ad", "Name", "Ad", "Name", "Nombre" },
        ["playlist_url"] = new[] { "Pleylist linki (http/https) və ya fayl", "Playlist URL (http/https) or file", "Liste bağlantısı (http/https) veya dosya", "Playlist-URL (http/https) oder Datei", "URL de la lista (http/https) o archivo" },
        ["file_btn"] = new[] { "Fayl…", "File…", "Dosya…", "Datei…", "Archivo…" },
        ["epg_url"] = new[] { "EPG linki (istəyə bağlı, .xml / .xml.gz)", "EPG URL (optional, .xml / .xml.gz)", "EPG bağlantısı (isteğe bağlı, .xml / .xml.gz)", "EPG-URL (optional, .xml / .xml.gz)", "URL de EPG (opcional, .xml / .xml.gz)" },
        ["server_hint"] = new[] { "Server (məs. http://server.com:8080)", "Server (e.g. http://server.com:8080)", "Sunucu (örn. http://server.com:8080)", "Server (z. B. http://server.com:8080)", "Servidor (p. ej. http://server.com:8080)" },
        ["username"] = new[] { "İstifadəçi adı", "Username", "Kullanıcı adı", "Benutzername", "Usuario" },
        ["password"] = new[] { "Şifrə", "Password", "Şifre", "Passwort", "Contraseña" },
        ["cancel"] = new[] { "Ləğv et", "Cancel", "İptal", "Abbrechen", "Cancelar" },
        ["add"] = new[] { "Əlavə et", "Add", "Ekle", "Hinzufügen", "Añadir" },
        ["save"] = new[] { "Yadda saxla", "Save", "Kaydet", "Speichern", "Guardar" },
        ["checking"] = new[] { "Yoxlanılır…", "Checking…", "Kontrol ediliyor…", "Wird geprüft…", "Comprobando…" },
        ["err_link"] = new[] { "Link düzgün deyil.", "Invalid URL.", "Bağlantı geçersiz.", "Ungültige URL.", "URL no válida." },
        ["err_server"] = new[] { "Server ünvanı düzgün deyil.", "Invalid server address.", "Sunucu adresi geçersiz.", "Ungültige Serveradresse.", "Dirección de servidor no válida." },
        ["err_creds"] = new[] { "İstifadəçi adı və şifrə lazımdır.", "Username and password are required.", "Kullanıcı adı ve şifre gerekli.", "Benutzername und Passwort sind erforderlich.", "Se requieren usuario y contraseña." },
        ["local_playlist"] = new[] { "Lokal pleylist", "Local playlist", "Yerel liste", "Lokale Playlist", "Lista local" },
        ["playlist_files"] = new[] { "Pleylist", "Playlist", "Liste", "Playlist", "Lista" },
        ["all_files"] = new[] { "Bütün fayllar", "All files", "Tüm dosyalar", "Alle Dateien", "Todos los archivos" },
        ["season"] = new[] { "Mövsüm:", "Season:", "Sezon:", "Staffel:", "Temporada:" },
        ["season_n"] = new[] { "Mövsüm {0}", "Season {0}", "Sezon {0}", "Staffel {0}", "Temporada {0}" },
        ["episodes_n"] = new[] { "{0} epizod", "{0} episodes", "{0} bölüm", "{0} Folgen", "{0} episodios" },
        ["no_episodes"] = new[] { "Epizod tapılmadı.", "No episodes found.", "Bölüm bulunamadı.", "Keine Folgen gefunden.", "No se encontraron episodios." },
        ["fav_add"] = new[] { "☆  Favoritlərə əlavə et", "☆  Add to favorites", "☆  Favorilere ekle", "☆  Zu Favoriten hinzufügen", "☆  Añadir a favoritos" },
        ["fav_remove"] = new[] { "★  Favoritlərdən çıxar", "★  Remove from favorites", "★  Favorilerden çıkar", "★  Aus Favoriten entfernen", "★  Quitar de favoritos" },
        ["episode"] = new[] { "Epizod", "Episode", "Bölüm", "Folge", "Episodio" },
        ["channels"] = new[] { "Kanallar", "Channels", "Kanallar", "Sender", "Canales" },
        ["buffering"] = new[] { "Yüklənir… {0}%", "Loading… {0}%", "Yükleniyor… {0}%", "Wird geladen… {0}%", "Cargando… {0}%" },
        ["stream_error"] = new[]
        {
            "Yayım açılmadı.\nServer cavab vermir və ya link köhnəlib. ↑/↓ ilə başqa kanala keçin.",
            "The stream could not be opened.\nThe server is not responding or the link is outdated. Use ↑/↓ to switch channels.",
            "Yayın açılamadı.\nSunucu yanıt vermiyor veya bağlantı eskimiş. ↑/↓ ile başka kanala geçin.",
            "Stream konnte nicht geöffnet werden.\nDer Server antwortet nicht oder der Link ist veraltet. Mit ↑/↓ den Sender wechseln.",
            "No se pudo abrir la emisión.\nEl servidor no responde o el enlace está caducado. Usa ↑/↓ para cambiar de canal."
        },
        ["now"] = new[] { "İndi", "Now", "Şimdi", "Jetzt", "Ahora" },
        ["next"] = new[] { "Sonra", "Next", "Sonra", "Danach", "Después" },
        ["back"] = new[] { "Geri", "Back", "Geri", "Zurück", "Atrás" },
        ["about"] = new[] { "Haqqında", "About", "Hakkında", "Info", "Acerca de" },
        ["version"] = new[] { "Versiya", "Version", "Sürüm", "Version", "Versión" },
        ["about_disclaimer"] = new[]
        {
            "AENV IPTV yalnız media pleyerdir. Heç bir kanal, yayım və ya pleylist təqdim etmir. Yalnız izləmək hüququnuz olan məzmunu əlavə edin.",
            "AENV IPTV is a media player only. It does not provide any channels, streams or playlists. Only add content you have the right to watch.",
            "AENV IPTV yalnızca bir medya oynatıcıdır. Hiçbir kanal, yayın veya liste sağlamaz. Yalnızca izleme hakkınız olan içerikleri ekleyin.",
            "AENV IPTV ist ausschließlich ein Medienplayer. Es werden keine Sender, Streams oder Playlists bereitgestellt. Fügen Sie nur Inhalte hinzu, zu deren Nutzung Sie berechtigt sind.",
            "AENV IPTV es solo un reproductor multimedia. No proporciona canales, emisiones ni listas. Añade solo contenido que tengas derecho a ver."
        },
        ["privacy_policy"] = new[] { "Məxfilik siyasəti", "Privacy policy", "Gizlilik politikası", "Datenschutzerklärung", "Política de privacidad" },
        ["license"] = new[] { "Lisenziya", "License", "Lisans", "Lizenz", "Licencia" },
        ["third_party"] = new[] { "Üçüncü tərəf lisenziyaları", "Third-party notices", "Üçüncü taraf lisansları", "Lizenzen von Drittanbietern", "Avisos de terceros" },
        ["close"] = new[] { "Bağla", "Close", "Kapat", "Schließen", "Cerrar" },
        ["http_too_many"] = new[]
        {
            "Server hazırda çox sorğu qəbul etmir (429). Bir neçə dəqiqə gözləyib yeniləyin. Çox provayder hesab başına yalnız bir bağlantıya icazə verir — eyni hesabı istifadə edən digər cihaz və ya tətbiqləri bağlayın.",
            "The server is refusing requests for now (429 Too Many Requests). Wait a few minutes, then refresh. Many providers allow only one connection per account — close other devices or apps using the same account.",
            "Sunucu şu anda istekleri kabul etmiyor (429). Birkaç dakika bekleyip yenileyin. Birçok sağlayıcı hesap başına yalnızca bir bağlantıya izin verir — aynı hesabı kullanan diğer cihaz veya uygulamaları kapatın.",
            "Der Server lehnt derzeit Anfragen ab (429 – zu viele Anfragen). Warten Sie einige Minuten und aktualisieren Sie dann. Viele Anbieter erlauben nur eine Verbindung pro Konto – schließen Sie andere Geräte oder Apps mit demselben Konto.",
            "El servidor rechaza solicitudes por ahora (429). Espera unos minutos y actualiza. Muchos proveedores permiten solo una conexión por cuenta: cierra otros dispositivos o apps que usen la misma cuenta."
        },
        ["http_denied"] = new[]
        {
            "Giriş rədd edildi. İstifadəçi adını, şifrəni və abunəliyin aktiv olduğunu yoxlayın.",
            "Access denied. Check the username, password and that the subscription is active.",
            "Erişim reddedildi. Kullanıcı adını, şifreyi ve aboneliğin aktif olduğunu kontrol edin.",
            "Zugriff verweigert. Prüfen Sie Benutzername, Passwort und ob das Abonnement aktiv ist.",
            "Acceso denegado. Comprueba el usuario, la contraseña y que la suscripción esté activa."
        },
        ["http_not_found"] = new[]
        {
            "Ünvan tapılmadı (404). Linki yoxlayın.",
            "Address not found (404). Check the link.",
            "Adres bulunamadı (404). Bağlantıyı kontrol edin.",
            "Adresse nicht gefunden (404). Bitte den Link prüfen.",
            "Dirección no encontrada (404). Comprueba el enlace."
        },
        ["http_server_error"] = new[]
        {
            "Serverdə xəta baş verdi ({0}). Bir az sonra yenidən cəhd edin.",
            "The server reported an error ({0}). Try again later.",
            "Sunucuda bir hata oluştu ({0}). Daha sonra tekrar deneyin.",
            "Der Server hat einen Fehler gemeldet ({0}). Bitte später erneut versuchen.",
            "El servidor informó de un error ({0}). Inténtalo más tarde."
        },
        ["http_other"] = new[]
        {
            "Server sorğunu qəbul etmədi ({0}).",
            "The server did not accept the request ({0}).",
            "Sunucu isteği kabul etmedi ({0}).",
            "Der Server hat die Anfrage nicht angenommen ({0}).",
            "El servidor no aceptó la solicitud ({0})."
        },
        ["http_network"] = new[]
        {
            "Serverə qoşulmaq mümkün olmadı. Ünvanı və internet bağlantınızı yoxlayın.",
            "Could not connect to the server. Check the address and your internet connection.",
            "Sunucuya bağlanılamadı. Adresi ve internet bağlantınızı kontrol edin.",
            "Keine Verbindung zum Server. Prüfen Sie die Adresse und Ihre Internetverbindung.",
            "No se pudo conectar con el servidor. Comprueba la dirección y tu conexión a internet."
        },
        ["http_timeout"] = new[]
        {
            "Server vaxtında cavab vermədi. Bir az sonra yenidən cəhd edin.",
            "The server did not respond in time. Try again later.",
            "Sunucu zamanında yanıt vermedi. Daha sonra tekrar deneyin.",
            "Der Server hat nicht rechtzeitig geantwortet. Bitte später erneut versuchen.",
            "El servidor no respondió a tiempo. Inténtalo más tarde."
        },
        ["tip_back"] = new[] { "Geri (Esc)", "Back (Esc)", "Geri (Esc)", "Zurück (Esc)", "Atrás (Esc)" },
        ["tip_prev"] = new[] { "Əvvəlki (↑)", "Previous (↑)", "Önceki (↑)", "Vorheriger (↑)", "Anterior (↑)" },
        ["tip_play"] = new[] { "Oynat / Pauza (Space)", "Play / Pause (Space)", "Oynat / Duraklat (Space)", "Wiedergabe / Pause (Leertaste)", "Reproducir / Pausa (Espacio)" },
        ["tip_next"] = new[] { "Növbəti (↓)", "Next (↓)", "Sonraki (↓)", "Nächster (↓)", "Siguiente (↓)" },
        ["tip_list"] = new[] { "Kanal siyahısı (L)", "Channel list (L)", "Kanal listesi (L)", "Senderliste (L)", "Lista de canales (L)" },
        ["tip_fav"] = new[] { "Favorit (F)", "Favorite (F)", "Favori (F)", "Favorit (F)", "Favorito (F)" },
        ["tip_mute"] = new[] { "Səs (M)", "Mute (M)", "Sessiz (M)", "Stumm (M)", "Silenciar (M)" },
        ["tip_full"] = new[] { "Tam ekran (F11 / iki klik)", "Fullscreen (F11 / double-click)", "Tam ekran (F11 / çift tık)", "Vollbild (F11 / Doppelklick)", "Pantalla completa (F11 / doble clic)" },
        ["auth_failed"] = new[] { "İstifadəçi adı və ya şifrə yanlışdır.", "Wrong username or password.", "Kullanıcı adı veya şifre yanlış.", "Benutzername oder Passwort falsch.", "Usuario o contraseña incorrectos." },
        ["bad_response"] = new[] { "Server gözlənilməz cavab qaytardı.", "The server returned an unexpected response.", "Sunucu beklenmeyen bir yanıt döndürdü.", "Der Server hat eine unerwartete Antwort geliefert.", "El servidor devolvió una respuesta inesperada." },
        ["save_error"] = new[] { "Saxlama xətası: ", "Save error: ", "Kaydetme hatası: ", "Speicherfehler: ", "Error al guardar: " },
        ["other"] = new[] { "Digər", "Other", "Diğer", "Sonstige", "Otros" },
    };

    /// <summary>For tests: returns keys that do not have all 5 translations.</summary>
    internal static IReadOnlyDictionary<string, string[]> AllStrings => Strings;

    internal static IEnumerable<string> BrokenKeys() =>
        Strings.Where(kv => kv.Value.Length != Languages.Length).Select(kv => kv.Key);
}
