using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TechnicallyReal.Promo;

/// <summary>
/// "Our other apps" - cross-promotes the publisher's other Store apps. All data
/// (names, blurbs, icons) is generated at build time by refresh.ps1 and bundled
/// under Assets/PromotedApps, so this page never touches the network. The app the
/// page is running inside is filtered out by package family name.
/// </summary>
public sealed partial class PromotedAppsPage : Page
{
    /// <summary>One promoted-app card (bound by the ItemsControl template).</summary>
    public sealed class PromotedApp
    {
        public string ProductId { get; init; } = "";
        public string Name { get; init; } = "";
        public string Blurb { get; init; } = "";
        public ImageSource? Icon { get; init; }
    }

    public PromotedAppsPage()
    {
        InitializeComponent();
        AppList.ItemsSource = LoadApps();
    }

    private static List<PromotedApp> LoadApps()
    {
        var apps = new List<PromotedApp>();
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "PromotedApps");
            string jsonPath = Path.Combine(dir, "apps.json");
            if (!File.Exists(jsonPath)) return apps;

            string? currentPfn = null;
            try { currentPfn = global::Windows.ApplicationModel.Package.Current.Id.FamilyName; }
            catch { /* unpackaged dev run - show all apps */ }

            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            foreach (var app in doc.RootElement.GetProperty("apps").EnumerateArray())
            {
                string pfn = app.GetProperty("packageFamilyName").GetString() ?? "";
                if (currentPfn != null && string.Equals(pfn, currentPfn, StringComparison.OrdinalIgnoreCase))
                    continue;

                string iconPath = Path.Combine(dir, app.GetProperty("icon").GetString() ?? "");
                apps.Add(new PromotedApp
                {
                    ProductId = app.GetProperty("id").GetString() ?? "",
                    Name = app.GetProperty("name").GetString() ?? "",
                    Blurb = app.GetProperty("blurb").GetString() ?? "",
                    Icon = File.Exists(iconPath) ? new BitmapImage(new Uri(iconPath)) : null,
                });
            }
        }
        catch { /* a broken promo page must never break the app */ }
        return apps;
    }

    private async void ViewInStore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string productId || productId.Length == 0) return;
        // Opens the Store app's product page - a Store-app launch, not a network call from us.
        _ = await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=" + productId));
    }
}
