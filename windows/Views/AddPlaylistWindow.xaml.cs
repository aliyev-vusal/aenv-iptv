using System;
using System.IO;
using System.Windows;
using Aenv.Models;
using Aenv.Services;
using Microsoft.Win32;

namespace Aenv.Views;

public partial class AddPlaylistWindow : Window
{
    public Playlist? Result { get; private set; }

    public AddPlaylistWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>Edit an existing playlist.</summary>
    public AddPlaylistWindow(Playlist existing) : this()
    {
        Header.Text = Loc.T("edit_playlist");
        OkButton.Content = Loc.T("save");
        NameBox.Text = existing.Name;
        if (existing.Type == SourceType.Xtream)
        {
            TypeXtream.IsChecked = true;
            ServerBox.Text = existing.Url;
            UserBox.Text = existing.Username ?? "";
            PassBox.Password = existing.Password ?? "";
        }
        else
        {
            UrlBox.Text = existing.Url;
            EpgBox.Text = existing.EpgUrl ?? "";
        }
    }

    private bool IsXtream => TypeXtream.IsChecked == true;

    private void Type_Checked(object sender, RoutedEventArgs e)
    {
        if (M3UPanel == null) return; // during InitializeComponent
        M3UPanel.Visibility = IsXtream ? Visibility.Collapsed : Visibility.Visible;
        XtreamPanel.Visibility = IsXtream ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = $"{Loc.T("playlist_files")} (*.m3u;*.m3u8;*.xspf)|*.m3u;*.m3u8;*.xspf|{Loc.T("all_files")}|*.*" };
        if (dlg.ShowDialog(this) == true)
        {
            UrlBox.Text = new Uri(dlg.FileName).AbsoluteUri; // file:///C:/...
            if (string.IsNullOrWhiteSpace(NameBox.Text)) NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            if (!IsXtream)
            {
                var url = UrlBox.Text.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) throw new Exception(Loc.T("err_link"));
                Result = new Playlist
                {
                    Name = Fallback(NameBox.Text, uri.IsFile ? Loc.T("local_playlist") : uri.Host),
                    Type = SourceType.M3U,
                    Url = url,
                    EpgUrl = string.IsNullOrWhiteSpace(EpgBox.Text) ? null : EpgBox.Text.Trim()
                };
            }
            else
            {
                var server = ServerBox.Text.Trim();
                if (!server.StartsWith("http", StringComparison.OrdinalIgnoreCase)) server = "http://" + server;
                if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)) throw new Exception(Loc.T("err_server"));
                if (UserBox.Text.Length == 0 || PassBox.Password.Length == 0) throw new Exception(Loc.T("err_creds"));

                OkButton.IsEnabled = false;
                OkButton.Content = Loc.T("checking");
                await new XtreamClient(server, UserBox.Text.Trim(), PassBox.Password).AuthenticateAsync();

                Result = new Playlist
                {
                    Name = Fallback(NameBox.Text, uri.Host),
                    Type = SourceType.Xtream,
                    Url = server,
                    Username = UserBox.Text.Trim(),
                    Password = PassBox.Password
                };
            }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
            OkButton.IsEnabled = true;
            OkButton.Content = Loc.T(Header.Text == Loc.T("edit_playlist") ? "save" : "add");
        }
    }

    private static string Fallback(string s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
}
