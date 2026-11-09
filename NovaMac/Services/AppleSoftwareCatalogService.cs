using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NovaMac.Models;

namespace NovaMac.Services;

// Apple Software Update catalogs contain full InstallAssistant.pkg products.
// This is independent of the Apple Internet Recovery endpoint.
public sealed class AppleSoftwareCatalogService
{
    private static readonly string[] CatalogUrls =
    {
        "https://swscan.apple.com/content/catalogs/others/index-27-26-15-14-13-12-10.16-10.15-10.14-10.13-10.12-10.11-10.10-10.9-mountainlion-lion-snowleopard-leopard.merged-1.sucatalog",
        "https://swscan.apple.com/content/catalogs/others/index-26-15-14-13-12-10.16-10.15-10.14-10.13-10.12-10.11-10.10-10.9-mountainlion-lion-snowleopard-leopard.merged-1.sucatalog",
        "https://swscan.apple.com/content/catalogs/others/index-15-14-13-12-10.16-10.15-10.14-10.13-10.12-10.11-10.10-10.9-mountainlion-lion-snowleopard-leopard.merged-1.sucatalog"
    };
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<MacOSSubVersion>? _cache;
    private DateTimeOffset _cachedAt;
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromHours(6);

    public AppleSoftwareCatalogService(HttpClient? client = null)
    {
        _http = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public async Task<IReadOnlyList<MacOSSubVersion>> GetAllInstallersAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cache != null && DateTimeOffset.UtcNow - _cachedAt < CacheLifetime) return _cache;
        await _gate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _cache != null && DateTimeOffset.UtcNow - _cachedAt < CacheLifetime) return _cache;
            Exception? last = null;
            var installers = new List<MacOSSubVersion>();
            var seenCandidates = new HashSet<(string PackageUrl, string DistributionUrl)>();
            foreach (var url in CatalogUrls)
            {
                try
                {
                    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    // Catalogs can be large. Bound the uncompressed XML to 100 MiB.
                    var bytes = await ReadBoundedAsync(stream, 100 * 1024 * 1024, ct);
                    if (bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
                    {
                        using var compressed = new MemoryStream(bytes);
                        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                        bytes = await ReadBoundedAsync(gzip, 100 * 1024 * 1024, ct);
                    }
                    using var xmlReader = System.Xml.XmlReader.Create(new MemoryStream(bytes), new System.Xml.XmlReaderSettings
                    {
                        DtdProcessing = System.Xml.DtdProcessing.Ignore,
                        XmlResolver = null,
                        MaxCharactersInDocument = 100 * 1024 * 1024
                    });
                    var document = XDocument.Load(xmlReader, LoadOptions.None);
                    var products = DictValue(document.Root?.Element("dict"), "Products");
                    if (products == null) throw new InvalidDataException("Catálogo Apple sem Products.");
                    var candidates = new List<(string PackageUrl, string DistributionUrl)>();
                    foreach (var (_, value) in Entries(products))
                    {
                        if (value.Name.LocalName != "dict") continue;
                        var packages = DictValue(value, "Packages");
                        var isInstaller = packages?.Elements("dict").Any(pkg =>
                            (DictValue(pkg, "URL")?.Value ?? "").Contains("InstallAssistant.pkg", StringComparison.OrdinalIgnoreCase)) == true;
                        if (!isInstaller) continue;
                        var packageUrl = packages!.Elements("dict").Select(pkg => DictValue(pkg, "URL")?.Value)
                            .FirstOrDefault(u => u?.Contains("InstallAssistant.pkg", StringComparison.OrdinalIgnoreCase) == true);
                        var distributions = DictValue(value, "Distributions");
                        var distributionUrl = DictValue(distributions, "English")?.Value ?? distributions?.Elements("string").FirstOrDefault()?.Value;
                        if (IsAppleHttps(packageUrl) && IsAppleHttps(distributionUrl))
                            candidates.Add((packageUrl!, distributionUrl!));
                    }
                    if (candidates.Count == 0) throw new InvalidDataException("Nenhum InstallAssistant.pkg no catálogo.");
                    using var limiter = new SemaphoreSlim(8);
                    var tasks = candidates.Where(seenCandidates.Add).Select(async c =>
                    {
                        await limiter.WaitAsync(ct);
                        try { return await ReadDistributionAsync(c.PackageUrl, c.DistributionUrl, ct); }
                        catch (HttpRequestException) { return null; }
                        catch (InvalidDataException) { return null; }
                        finally { limiter.Release(); }
                    });
                    installers.AddRange((await Task.WhenAll(tasks)).OfType<MacOSSubVersion>());
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or System.Xml.XmlException)
                { last = ex; }
            }
            if (installers.Count == 0)
                throw new InvalidOperationException("Não foi possível consultar o catálogo de instaladores da Apple.", last);

            _cache = installers
                .GroupBy(x => (x.Version, x.BuildNumber))
                .Select(group => group.First())
                .OrderByDescending(x => ParseVersion(x.Version)).ToList();
            _cachedAt = DateTimeOffset.UtcNow;
            return _cache;
        }
        finally { _gate.Release(); }
    }

    public async Task<List<MacOSSubVersion>> GetTahoeReleaseHistoryAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("https://support.apple.com/en-us/122868",
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var html = System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(stream, 4 * 1024 * 1024, ct));
        var headings = Regex.Matches(html, @"<h2\b[^>]*>(.*?)</h2\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        var versions = headings.Select(heading =>
            System.Net.WebUtility.HtmlDecode(Regex.Replace(heading.Groups[1].Value, "<[^>]+>", "")).Trim())
            .Select(heading => Match(heading, @"^macOS\s+Tahoe\s+(26(?:\.\d+){0,2})$"))
            .OfType<string>()
            .Select(version => version == "26" ? "26.0" : version)
            .Distinct()
            .OrderByDescending(ParseVersion)
            .Select(version => new MacOSSubVersion
            {
                Version = version,
                DisplayName = $"macOS Tahoe {version} • Histórico oficial",
                OsType = "history",
                Description = "Versão publicada pela Apple. Para instalar esta versão exata, selecione o instalador correspondente, quando disponível, ou use uma imagem local."
            }).ToList();

        if (versions.Count == 0)
            throw new InvalidDataException("Nenhuma versão do Tahoe encontrada no histórico da Apple.");

        return versions;
    }

    public async Task<List<MacOSSubVersion>> GetForReleaseAsync(MacOSRelease release, bool forceRefresh = false, CancellationToken ct = default)
    {
        var major = release.Id switch
        {
            "macos-27" => 27, "macos-26-tahoe" => 26, "macos-15-sequoia" => 15,
            "macos-14-sonoma" => 14, "macos-13-ventura" => 13,
            "macos-12-monterey" => 12, "macos-11-bigsur" => 11, _ => -1
        };
        var installers = await GetAllInstallersAsync(forceRefresh, ct);
        return installers.Where(x => ParseVersion(x.Version).Major == major)
            .Select(x => new MacOSSubVersion
            {
                Version = x.Version, BuildNumber = x.BuildNumber, DisplayName = x.DisplayName,
                Description = x.Description, OsType = x.OsType, IsBeta = x.IsBeta,
                IsVerifiedAvailable = x.IsVerifiedAvailable, InstallerPackageUrl = x.InstallerPackageUrl
            }).ToList();
    }

    private async Task<MacOSSubVersion?> ReadDistributionAsync(string packageUrl, string distUrl, CancellationToken ct)
    {
        using var response = await _http.GetAsync(distUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var xml = System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(stream, 2 * 1024 * 1024, ct));
        // Distribution scripts expose the installer's version/build in several known fields.
        var version = Match(xml, @"(?:VERSION|version|OSVersion|ProductVersion|CFBundleShortVersionString)\s*['""\s:=]+(\d{1,2}\.\d+(?:\.\d+)?)")
            ?? Match(xml, @"(?:macOS|Mac OS X)\s+(\d{1,2}\.\d+(?:\.\d+)?)");
        if (version == null) return null;
        var build = Match(xml, @"(?:BUILD|build|BuildVersion|ProductBuildVersion)\s*['""\s:=]+(\d{2}[A-Z]\d+[a-z]?)") ?? "";
        return new MacOSSubVersion
        {
            Version = version, BuildNumber = build, OsType = "installer", IsBeta = false,
            DisplayName = $"macOS {version}" + (build.Length > 0 ? $" • build {build}" : "") + " • Instalador Apple",
            Description = "InstallAssistant.pkg encontrado no catálogo oficial Apple; não é uma imagem BaseSystem.dmg.",
            IsVerifiedAvailable = true, InstallerPackageUrl = packageUrl
        };
    }

    private static string? Match(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups[1].Value : null;
    }
    private static Version ParseVersion(string value) => Version.TryParse(value, out var v) ? v : new Version(0, 0);
    private static bool IsAppleHttps(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var u) &&
        u.Scheme == Uri.UriSchemeHttps && (u.Host.EndsWith(".apple.com", StringComparison.OrdinalIgnoreCase) ||
        u.Host.EndsWith(".cdn-apple.com", StringComparison.OrdinalIgnoreCase));
    private static XElement? DictValue(XElement? dict, string key)
        => dict == null ? null : Entries(dict).FirstOrDefault(x => x.Key == key).Value;
    private static IEnumerable<(string Key, XElement Value)> Entries(XElement dict)
    {
        var nodes = dict.Elements().ToArray();
        for (var i = 0; i + 1 < nodes.Length; i++)
            if (nodes[i].Name.LocalName == "key") yield return (nodes[i].Value, nodes[++i]);
    }
    private static async Task<byte[]> ReadBoundedAsync(Stream input, int max, CancellationToken ct)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = await input.ReadAsync(buffer.AsMemory(), ct)) != 0)
        {
            if (output.Length + n > max) throw new InvalidDataException("Resposta Apple excede o limite de tamanho.");
            await output.WriteAsync(buffer.AsMemory(0, n), ct);
        }
        return output.ToArray();
    }
}
