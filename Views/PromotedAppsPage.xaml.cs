using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace TechnicallyReal.Promo;

/// <summary>
/// "Our other apps" - cross-promotes the publisher's Store and web apps. The page
/// displays the last successfully cached public manifest immediately, refreshes it
/// from GitHub, and retains bundled assets as an offline first-run fallback. The app
/// the page is running inside is filtered out by package family name.
/// </summary>
public sealed partial class PromotedAppsPage : Page
{
    private static readonly Uri ManifestUri = new(
        "https://raw.githubusercontent.com/RyanEwen/technicallyreal-promo/main/Assets/PromotedApps/apps.json");

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>One promoted-app card (bound by the ItemsControl template).</summary>
    public sealed class PromotedApp
    {
        public string Destination { get; init; } = "";
        public string ActionLabel { get; init; } = "";
        public string Name { get; init; } = "";
        public string Blurb { get; init; } = "";
        public ImageSource? Icon { get; init; }
    }

    public PromotedAppsPage()
    {
        InitializeComponent();

        string cacheDirectory = GetCacheDirectory();
        List<PromotedApp> apps = LoadApps(cacheDirectory);
        if (apps.Count == 0)
            apps = LoadApps(GetBundledDirectory());

        AppList.ItemsSource = apps;
        Loaded += PromotedAppsPage_Loaded;
    }

    /// <summary>
    /// Refreshes the central manifest once after the page loads. Existing cards stay
    /// visible if GitHub is unavailable or returns unusable data.
    /// </summary>
    private async void PromotedAppsPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PromotedAppsPage_Loaded;

        List<PromotedApp> apps = await RefreshCacheAsync();
        if (apps.Count > 0)
            AppList.ItemsSource = apps;
    }

    /// <summary>Returns the immutable promo assets shipped with the consuming app.</summary>
    private static string GetBundledDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "PromotedApps");
    }

    /// <summary>
    /// Returns app-local persistent storage for the last successful promo refresh.
    /// Unpackaged development runs fall back to the user's local application data.
    /// </summary>
    private static string GetCacheDirectory()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "PromotedApps");
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TechnicallyReal",
                "PromotedApps");
        }
    }

    /// <summary>
    /// Downloads a changed manifest and all of its relative icons, then publishes the
    /// manifest to the cache last so readers never observe a partially refreshed set.
    /// Returns an empty list on failure, leaving the currently displayed data intact.
    /// </summary>
    private static async Task<List<PromotedApp>> RefreshCacheAsync()
    {
        try
        {
            string cacheDirectory = GetCacheDirectory();
            string cachedManifestPath = Path.Combine(cacheDirectory, "apps.json");
            string manifest = await HttpClient.GetStringAsync(ManifestUri);

            if (File.Exists(cachedManifestPath) &&
                string.Equals(await File.ReadAllTextAsync(cachedManifestPath), manifest, StringComparison.Ordinal))
                return LoadApps(cacheDirectory);

            Directory.CreateDirectory(cacheDirectory);
            using var document = JsonDocument.Parse(manifest);

            foreach (JsonElement app in document.RootElement.GetProperty("apps").EnumerateArray())
            {
                string icon = app.GetProperty("icon").GetString() ?? "";
                if (icon.Length == 0 || !string.Equals(icon, Path.GetFileName(icon), StringComparison.Ordinal))
                    throw new InvalidDataException("Promo icon paths must be simple file names.");

                byte[] iconBytes = await HttpClient.GetByteArrayAsync(new Uri(ManifestUri, icon));
                await WriteBytesAtomicallyAsync(Path.Combine(cacheDirectory, icon), iconBytes);
            }

            await WriteTextAtomicallyAsync(cachedManifestPath, manifest);
            return LoadApps(cacheDirectory);
        }
        catch
        {
            return new List<PromotedApp>();
        }
    }

    /// <summary>Writes bytes through a unique temporary file, then replaces the destination.</summary>
    private static async Task WriteBytesAtomicallyAsync(string destination, byte[] content)
    {
        string temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content);
            File.Move(temporaryPath, destination, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>Writes text through a unique temporary file, then replaces the destination.</summary>
    private static async Task WriteTextAtomicallyAsync(string destination, string content)
    {
        string temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content);
            File.Move(temporaryPath, destination, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>Loads and filters one local manifest and its colocated icons.</summary>
    private static List<PromotedApp> LoadApps(string directory)
    {
        var apps = new List<PromotedApp>();
        try
        {
            string jsonPath = Path.Combine(directory, "apps.json");
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

                string iconPath = Path.Combine(directory, app.GetProperty("icon").GetString() ?? "");
                apps.Add(new PromotedApp
                {
                    Destination = GetDestination(app),
                    ActionLabel = app.TryGetProperty("actionLabel", out var actionLabel)
                        ? actionLabel.GetString() ?? "Open"
                        : "View in Store",
                    Name = app.GetProperty("name").GetString() ?? "",
                    Blurb = app.GetProperty("blurb").GetString() ?? "",
                    Icon = File.Exists(iconPath) ? new BitmapImage(new Uri(iconPath)) : null,
                });
            }
        }
        catch { /* a broken promo page must never break the app */ }
        return apps;
    }

    /// <summary>
    /// Reads the destination URI while retaining compatibility with manifests that only
    /// contain the original Microsoft Store product ID.
    /// </summary>
    private static string GetDestination(JsonElement app)
    {
        if (app.TryGetProperty("destination", out var destination))
            return destination.GetString() ?? "";

        if (app.TryGetProperty("id", out var productId))
            return "ms-windows-store://pdp/?ProductId=" + productId.GetString();

        return "";
    }

    /// <summary>Opens a promoted app's Store listing or website.</summary>
    private async void OpenDestination_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string destination ||
            !Uri.TryCreate(destination, UriKind.Absolute, out var uri))
            return;

        _ = await global::Windows.System.Launcher.LaunchUriAsync(uri);
    }
}
