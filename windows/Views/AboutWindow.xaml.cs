using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Aenv.Views;

/// <summary>Version, content disclaimer and links to the privacy policy and licenses.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        VersionText.Text = $"{Loc.T("version")} {v.Major}.{v.Minor}.{v.Build}";
    }

    private void Privacy_Click(object sender, RoutedEventArgs e) => OpenLegal("PRIVACY.txt");
    private void License_Click(object sender, RoutedEventArgs e) => OpenLegal("LICENSE.txt");
    private void Notices_Click(object sender, RoutedEventArgs e) => OpenLegal("THIRD-PARTY-NOTICES.txt");

    /// <summary>Opens a bundled legal document with the user's default text viewer.</summary>
    private void OpenLegal(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Legal", file);
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Loc.T("error"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
