using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using NovaMac.Models;

namespace NovaMac.Services;

public record DownloadProgressInfo(
    string Stage,
    double Percentage,
    long BytesDownloaded,
    long TotalBytes,
    double SpeedMbPerSec);

public class AppleRecoveryService
{
    private const string RecoveryBaseUrl = "https://osrecovery.apple.com";
    private const string UserAgent = "InternetRecovery/1.0";
    private readonly HttpClient _httpClient;

    public AppleRecoveryService()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = true
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromHours(2)
        };
    }

    private static string RandomHex(int length)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes((length + 1) / 2);
        return Convert.ToHexString(bytes)[..length];
    }

    public record LiveRecoveryInfo(
        string ApplePart,
        string DmgUrl,
        string AssetToken,
        string ChunklistUrl,
        bool IsAvailable);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset Updated, List<MacOSSubVersion> Versions)> _subVersionsCache = new();
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);

    public async Task<LiveRecoveryInfo?> QueryLiveChannelAsync(
        string boardId,
        string osType = "default",
        CancellationToken ct = default)
    {
        try
        {
            var fakeRelease = new MacOSRelease
            {
                BoardId = boardId,
                Mlb = "00000000000000000",
                OsType = osType
            };
            var (dmgUrl, token, chunklistUrl) = await QueryRecoveryUrlAsync(fakeRelease, ct);

            string part = "Oficial";
            var match = System.Text.RegularExpressions.Regex.Match(dmgUrl, @"/([0-9]{3}-[0-9]{5})/");
            if (match.Success)
            {
                part = match.Groups[1].Value;
            }

            return new LiveRecoveryInfo(part, dmgUrl, token, chunklistUrl, true);
        }
        catch
        {
            return null;
        }
    }

    private readonly AppleSoftwareCatalogService _softwareCatalog = new();

    public Task<List<MacOSSubVersion>> GetTahoeReleaseHistoryAsync(CancellationToken ct = default)
        => _softwareCatalog.GetTahoeReleaseHistoryAsync(ct);

    // Descobre instaladores reais para qualquer versão principal suportada.
    // Erros são propagados: não apresentar dados estáticos como atualizados.
    public Task<List<MacOSSubVersion>> GetAvailableSubVersionsAsync(
        MacOSRelease release, bool forceRefresh = false, CancellationToken ct = default)
        => _softwareCatalog.GetForReleaseAsync(release, forceRefresh, ct);

    public Task<IReadOnlyList<MacOSSubVersion>> GetAllAppleInstallersAsync(
        bool forceRefresh = false, CancellationToken ct = default)
        => _softwareCatalog.GetAllInstallersAsync(forceRefresh, ct);

    private static MacOSSubVersion CloneSubVersion(MacOSSubVersion sub) => new()
    {
        Version = sub.Version,
        DisplayName = sub.DisplayName,
        BuildNumber = sub.BuildNumber,
        OsType = sub.OsType,
        IsBeta = sub.IsBeta,
        Description = sub.Description,
        IsVerifiedAvailable = sub.IsVerifiedAvailable,
        InstallerPackageUrl = sub.InstallerPackageUrl
    };

    public async Task<(string DmgUrl, string AssetToken, string ChunklistUrl)> QueryRecoveryUrlAsync(
        MacOSRelease release,
        CancellationToken ct = default)
    {
        string boardId = !string.IsNullOrWhiteSpace(release.BoardId)
            ? release.BoardId
            : "Mac-937A206F2EE63C01";

        string mlb = !string.IsNullOrWhiteSpace(release.Mlb)
            ? release.Mlb
            : "00000000000000000";

        // Protocolo Apple Internet Recovery:
        // Testar via HTTP (porta 80 padrão de EFI Network Boot da Apple) e HTTPS como fallback
        string[] protocolBases = ["http://osrecovery.apple.com", "https://osrecovery.apple.com"];
        Exception? lastEx = null;

        foreach (var baseUrl in protocolBases)
        {
            try
            {
                // Step 1: Obter cookie de sessão de osrecovery.apple.com via GET /
                using var initReq = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/");
                initReq.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                initReq.Headers.Host = "osrecovery.apple.com";
                initReq.Headers.ConnectionClose = true;

                using var initResp = await _httpClient.SendAsync(initReq, ct);
                initResp.EnsureSuccessStatusCode();

                string? sessionCookie = null;
                if (initResp.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var c in cookies)
                    {
                        var part = c.Split(';').FirstOrDefault(p => p.Trim().StartsWith("session=", StringComparison.OrdinalIgnoreCase));
                        if (part != null)
                        {
                            sessionCookie = part.Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(sessionCookie))
                {
                    continue;
                }

                // Step 2: Query RecoveryImage via POST /InstallationPayload/RecoveryImage
                // Apple requer text/plain estrito e somente parâmetro 'os' se for canal específico (ex: 'latest' para betas).
                // 'default' e 'installer' NUNCA devem ser enviados como 'os=' pois a Apple rejeita com HTTP 400 (Malformed parameters).
                var payloadLines = new List<string>
                {
                    $"cid={RandomHex(16)}",
                    $"sn={mlb}",
                    $"bid={boardId}",
                    $"k={RandomHex(64)}",
                    $"fg={RandomHex(64)}"
                };

                if (!string.IsNullOrWhiteSpace(release.OsType) &&
                    !release.OsType.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                    !release.OsType.Equals("installer", StringComparison.OrdinalIgnoreCase))
                {
                    payloadLines.Add($"os={release.OsType}");
                }

                string payload = string.Join("\n", payloadLines);

                using var queryReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/InstallationPayload/RecoveryImage");
                queryReq.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                queryReq.Headers.Host = "osrecovery.apple.com";
                queryReq.Headers.ConnectionClose = true;
                queryReq.Headers.TryAddWithoutValidation("Cookie", sessionCookie);

                var queryContent = new ByteArrayContent(Encoding.ASCII.GetBytes(payload));
                queryContent.Headers.TryAddWithoutValidation("Content-Type", "text/plain");
                queryReq.Content = queryContent;

                using var queryResp = await _httpClient.SendAsync(queryReq, ct);
                queryResp.EnsureSuccessStatusCode();

                string body = await queryResp.Content.ReadAsStringAsync(ct);
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var rawLine in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    int idx = rawLine.IndexOf(": ", StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        string key = rawLine[..idx].Trim();
                        string val = rawLine[(idx + 2)..].Trim();
                        fields[key] = val;
                    }
                }

                if (!fields.TryGetValue("AU", out string? dmgUrl) || string.IsNullOrWhiteSpace(dmgUrl))
                {
                    throw new InvalidOperationException($"Servidor da Apple não retornou URL de imagem (AU) para BoardID {boardId}.");
                }

                fields.TryGetValue("AT", out string? token);
                fields.TryGetValue("CU", out string? chunklistUrl);

                return (dmgUrl, token ?? string.Empty, chunklistUrl ?? string.Empty);
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }

        // Se falhou com o BoardID atual e ele não for o padrão universal MacPro7,1 (Mac-937A206F2EE63C01),
        // tentar novamente com o BoardID padrão para evitar que a VM falhe ao criar.
        if (!boardId.Equals("Mac-937A206F2EE63C01", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackRelease = new MacOSRelease
            {
                Id = release.Id,
                Name = release.Name,
                Version = release.Version,
                BoardId = "Mac-937A206F2EE63C01",
                Mlb = mlb,
                OsType = release.OsType
            };
            return await QueryRecoveryUrlAsync(fallbackRelease, ct);
        }

        throw lastEx ?? new HttpRequestException("Falha ao conectar com os servidores de recuperação da Apple.");
    }

    public async Task<string> DownloadRecoveryImageAsync(
        MacOSRelease release,
        string destinationDirectory,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        if (release.OsType == "installer")
            throw new InvalidOperationException("InstallAssistant.pkg não é BaseSystem.dmg. Use DownloadInstallerPackageAsync para baixar a versão escolhida.");
        Directory.CreateDirectory(destinationDirectory);
        string versionSlug = string.IsNullOrWhiteSpace(release.Version)
            ? "default"
            : release.Version.Replace(" ", "-").ToLowerInvariant();
        string targetDmgPath = Path.Combine(destinationDirectory, $"BaseSystem-{release.Id}-{release.OsType}.dmg");


        string effectiveDmg = targetDmgPath;

        if (File.Exists(effectiveDmg) && new FileInfo(effectiveDmg).Length > 400 * 1024 * 1024 &&
            File.Exists(effectiveDmg + ".recovery-channel") &&
            File.ReadAllText(effectiveDmg + ".recovery-channel") == release.BoardId + "|" + release.OsType)
        {
            var fi = new FileInfo(effectiveDmg);
            progress?.Report(new DownloadProgressInfo(
                $"Imagem {release.Name} ({release.Version}) encontrada no cache local!",
                100,
                fi.Length,
                fi.Length,
                0));
            return effectiveDmg;
        }

        progress?.Report(new DownloadProgressInfo(
            $"Conectando ao servidor Apple Recovery ({release.Name} {release.Version} - {release.BoardId} • Canal:{release.OsType})...",
            2, 0, 0, 0));

        var (dmgUrl, token, _) = await QueryRecoveryUrlAsync(release, ct);

        using var dlReq = new HttpRequestMessage(HttpMethod.Get, dmgUrl);
        dlReq.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (!string.IsNullOrEmpty(token))
        {
            dlReq.Headers.TryAddWithoutValidation("Cookie", $"AssetToken={token}");
        }

        using var resp = await _httpClient.SendAsync(dlReq, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long totalBytes = resp.Content.Headers.ContentLength ?? (750L * 1024 * 1024);
        string tempFile = targetDmgPath + ".part";

        await using (var contentStream = await resp.Content.ReadAsStreamAsync(ct))
        await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            byte[] buffer = new byte[81920];
            long totalRead = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastReportMs = 0;

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (sw.ElapsedMilliseconds - lastReportMs >= 150)
                {
                    lastReportMs = sw.ElapsedMilliseconds;
                    double elapsedSec = Math.Max(0.1, sw.Elapsed.TotalSeconds);
                    double speedMb = (totalRead / (1024.0 * 1024.0)) / elapsedSec;
                    double pct = totalBytes > 0 ? Math.Min(99.5, (totalRead * 100.0) / totalBytes) : 50;

                    progress?.Report(new DownloadProgressInfo(
                        $"Baixando {release.Name} BaseSystem.dmg da Apple...",
                        pct,
                        totalRead,
                        totalBytes,
                        speedMb));
                }
            }
        }

        if (File.Exists(targetDmgPath))
        {
            File.Delete(targetDmgPath);
        }
        File.Move(tempFile, targetDmgPath);
        File.WriteAllText(targetDmgPath + ".recovery-channel", release.BoardId + "|" + release.OsType);

        var finalInfo = new FileInfo(targetDmgPath);
        progress?.Report(new DownloadProgressInfo(
            $"Download de {release.Name} concluído!",
            100,
            finalInfo.Length,
            finalInfo.Length,
            0));

        return targetDmgPath;
    }
    public async Task<string> DownloadInstallerPackageAsync(MacOSSubVersion version, string destinationDirectory,
        IProgress<DownloadProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (version.OsType != "installer" || !version.IsVerifiedAvailable ||
            !Uri.TryCreate(version.InstallerPackageUrl, UriKind.Absolute, out var url) ||
            url.Scheme != "https" || !(url.Host.EndsWith(".apple.com", StringComparison.OrdinalIgnoreCase) ||
            url.Host.EndsWith(".cdn-apple.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("URL de instalador Apple não verificada.");
        Directory.CreateDirectory(destinationDirectory);
        var filename = $"InstallAssistant-{version.Version}-{version.BuildNumber}.pkg";
        var destination = Path.Combine(destinationDirectory, filename);
        var partial = destination + ".part";
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        long read = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            int count;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while ((count = await source.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count), ct);
                read += count;
                progress?.Report(new DownloadProgressInfo("Baixando InstallAssistant.pkg da Apple",
                    total > 0 ? Math.Min(99, 100d * read / total) : 0, read, total,
                    read / 1048576d / Math.Max(sw.Elapsed.TotalSeconds, 0.1)));
            }
        }
        if (total > 0 && read != total) throw new IOException("Download incompleto do instalador Apple.");
        File.Move(partial, destination, true);
        progress?.Report(new DownloadProgressInfo("Instalador baixado", 100, read, total, 0));
        return destination;
    }

}
