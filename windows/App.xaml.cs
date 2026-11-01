using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using LibVLCSharp.Shared;

namespace Aenv;

public partial class App : Application
{
    /// <summary>Single VLC instance shared by all player windows.</summary>
    public static LibVLC Vlc { get; private set; } = null!;

    public static string LogPath => Services.AppPaths.CrashLog;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Never close silently on unexpected errors: show them and write to the log
        DispatcherUnhandledException += (_, ex) => { Report(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Report(ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Report(ex.Exception); ex.SetObserved(); };

        try
        {
            Core.Initialize();
            Vlc = new LibVLC("--network-caching=1500", "--http-reconnect");
        }
        catch (Exception ex)
        {
            Report(ex, Loc.T("vlc_failed"));
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Vlc?.Dispose();
        base.OnExit(e);
    }

    public static void Report(Exception? ex, string? title = null)
    {
        if (ex == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
        MessageBox.Show(ex.ToString(), title ?? Loc.T("app_error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
