using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace ScreenPaste.Settings;

public sealed record UpdateInfo(Version Version, string TagName, string? SetupUrl, string PageUrl, string Notes);

/// <summary>Checks GitHub Releases for a newer version and downloads the installer.</summary>
public static class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/taida957789/ScreenPaste/releases/latest";

    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenPaste-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version Current => To3(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0));

    /// <summary>Returns update info if a newer release exists, else null (also null on error).</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(ApiUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = Str(root, "tag_name");
            var page = Str(root, "html_url");
            var notes = Str(root, "body");
            var ver = ParseTag(tag);
            if (ver == null) return null;

            string? setup = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = Str(a, "name");
                    if (name.EndsWith("setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        setup = Str(a, "browser_download_url");
                        break;
                    }
                }
            }

            return ver > Current ? new UpdateInfo(ver, tag, setup, page, notes) : null;
        }
        catch { return null; }
    }

    /// <summary>Download the installer to a temp file. Returns the path, or null on failure.</summary>
    public static async Task<string?> DownloadAsync(string url)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "ScreenPaste-update-setup.exe");
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(path);
            await resp.Content.CopyToAsync(fs).ConfigureAwait(false);
            return path;
        }
        catch { return null; }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static Version? ParseTag(string tag)
    {
        var s = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(s, out var v) ? To3(v) : null;
    }

    private static Version To3(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
