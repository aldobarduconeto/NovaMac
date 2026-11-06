using System.IO;
using System.Net.Http;
using System.Text;
using NovaMac.Models;

namespace NovaMac.Services;

public class OpenCoreService
{
    private readonly HttpClient _httpClient;

    private const string OpenCoreImageUrl = "https://raw.githubusercontent.com/kholia/OSX-KVM/master/OpenCore/OpenCore.qcow2";
    private const string OpenCoreImageMirrorUrl = "https://cdn.jsdelivr.net/gh/kholia/OSX-KVM@master/OpenCore/OpenCore.qcow2";

    private const string OvmfCodeUrl = "https://raw.githubusercontent.com/kholia/OSX-KVM/master/OVMF_CODE_4M.fd";
    private const string OvmfCodeMirrorUrl = "https://cdn.jsdelivr.net/gh/kholia/OSX-KVM@master/OVMF_CODE_4M.fd";

    private const string OvmfVarsUrl = "https://raw.githubusercontent.com/kholia/OSX-KVM/master/OVMF_VARS-1920x1080.fd";
    private const string OvmfVarsMirrorUrl = "https://cdn.jsdelivr.net/gh/kholia/OSX-KVM@master/OVMF_VARS-1920x1080.fd";

    public OpenCoreService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NovaMac-Windows/2.0");
    }

    public async Task<(string OpenCorePath, string OvmfCodePath, string OvmfVarsPath)> ProvisionFirmwareAndBootloaderAsync(
        VirtualMachineConfig vm,
        string cacheDirectory,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        Directory.CreateDirectory(vm.BundleDirectory);

                        
        string cachedOpenCore = Path.Combine(cacheDirectory, "OpenCore.qcow2");
        string cachedOvmfCode = Path.Combine(cacheDirectory, "OVMF_CODE.fd");
        string cachedOvmfVars = Path.Combine(cacheDirectory, "OVMF_VARS.fd");

        // CORREÇÃO CRÍTICA DE INSTALAÇÃO DO MACOS:
        // O arquivo padrão edk2-x86_64-code.fd do QEMU upstream possui exatamente 3.653.632 bytes e é incompatível
        // com as variáveis NVRAM do OSX-KVM (OVMF_VARS.fd). Isso causava corrupção ou reinicialização de NVRAM a cada boot,
        // impedindo a conclusão dos estágios pós-reboot da instalação do macOS.
        // Se o arquivo tiver esse tamanho ou for menor que 2MB, forçamos o download do OVMF_CODE_4M.fd oficial (4MB).
        if (File.Exists(cachedOvmfCode))
        {
            long codeLen = new FileInfo(cachedOvmfCode).Length;
            if (codeLen == 3_653_632 || codeLen < 2_000_000)
            {
                try { File.Delete(cachedOvmfCode); } catch { }
            }
        }

        if (!File.Exists(cachedOvmfCode) || new FileInfo(cachedOvmfCode).Length < 2_000_000)
        {
            progress?.Report(new DownloadProgressInfo("Baixando firmware UEFI compatível (OVMF_CODE_4M.fd)...", 15, 0, 0, 0));
            bool downloaded = await TryDownloadWithMirrorAsync(OvmfCodeUrl, OvmfCodeMirrorUrl, cachedOvmfCode, ct);
            if (!downloaded)
            {
                // Fallback emergencial offline somente se a rede estiver inalcançável
                string baseDir = AppContext.BaseDirectory;
                string localEdk2 = Path.Combine(baseDir, "EmbeddedQemu", "share", "edk2-x86_64-code.fd");
                if (File.Exists(localEdk2))
                {
                    File.Copy(localEdk2, cachedOvmfCode, true);
                }
            }
        }

        if (!File.Exists(cachedOvmfVars) || new FileInfo(cachedOvmfVars).Length < 10_000)
        {
            progress?.Report(new DownloadProgressInfo("Baixando variáveis NVRAM UEFI (OVMF_VARS.fd)...", 35, 0, 0, 0));
            await TryDownloadWithMirrorAsync(OvmfVarsUrl, OvmfVarsMirrorUrl, cachedOvmfVars, ct);
        }

        if (!File.Exists(cachedOpenCore) || new FileInfo(cachedOpenCore).Length < 1_000_000)
        {
            progress?.Report(new DownloadProgressInfo("Baixando bootloader OpenCore para QEMU...", 60, 0, 0, 0));
            await TryDownloadWithMirrorAsync(OpenCoreImageUrl, OpenCoreImageMirrorUrl, cachedOpenCore, ct);
        }

        string vmOpenCore = Path.Combine(vm.BundleDirectory, "OpenCore.qcow2");
        string vmOvmfCode = Path.Combine(vm.BundleDirectory, "OVMF_CODE.fd");
        string vmOvmfVars = Path.Combine(vm.BundleDirectory, "OVMF_VARS.fd");

        // Se o arquivo na VM for a versão antiga defeituosa (3.653.632 bytes), sobrescrever pela corrigida
        if (File.Exists(vmOvmfCode) && new FileInfo(vmOvmfCode).Length == 3_653_632 && File.Exists(cachedOvmfCode) && new FileInfo(cachedOvmfCode).Length > 2_000_000)
        {
            File.Copy(cachedOvmfCode, vmOvmfCode, true);
        }
        else if (File.Exists(cachedOvmfCode) && (!File.Exists(vmOvmfCode) || new FileInfo(vmOvmfCode).Length < 100_000))
        {
            File.Copy(cachedOvmfCode, vmOvmfCode, true);
        }

        if (File.Exists(cachedOvmfVars) && (!File.Exists(vmOvmfVars) || new FileInfo(vmOvmfVars).Length < 10_000))
        {
            File.Copy(cachedOvmfVars, vmOvmfVars, true);
        }

        if (File.Exists(cachedOpenCore) && (!File.Exists(vmOpenCore) || new FileInfo(vmOpenCore).Length < 1_000_000))
        {
            File.Copy(cachedOpenCore, vmOpenCore, true);
        }

        await WriteOpenCorePlistAsync(vm, ct);

        progress?.Report(new DownloadProgressInfo("OpenCore + OVMF configurados com sucesso!", 100, 0, 0, 0));

        return (vmOpenCore, vmOvmfCode, vmOvmfVars);
    }

    public static async Task WriteOpenCorePlistAsync(VirtualMachineConfig vm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vm.BundleDirectory))
            return;

        Directory.CreateDirectory(vm.BundleDirectory);
        string configPlistPath = Path.Combine(vm.BundleDirectory, "OpenCore-config.plist");
        await File.WriteAllTextAsync(configPlistPath, GenerateOpenCorePlist(vm), Encoding.UTF8, ct);
    }

    private async Task<bool> TryDownloadWithMirrorAsync(string primaryUrl, string mirrorUrl, string destinationPath, CancellationToken ct)
    {
        try
        {
            await DownloadFileAsync(primaryUrl, destinationPath, ct);
            return true;
        }
        catch
        {
            try
            {
                await DownloadFileAsync(mirrorUrl, destinationPath, ct);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken ct)
    {
        using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await resp.Content.CopyToAsync(fs, ct);
    }

    public static string GenerateOpenCorePlist(VirtualMachineConfig vm)
    {
        string bootArgs = !string.IsNullOrWhiteSpace(vm.OpenCoreBootArgs)
            ? vm.OpenCoreBootArgs.Trim()
            : "-v keepsyms=1 debug=0x100 agdpmod=pikera revpatch=sbvmm,cpuname -lilubetaall -cryptbeta";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <!-- Gerado automaticamente pelo NovaMac for Windows -->
            <plist version="1.0">
            <dict>
                <key>NVRAM</key>
                <dict>
                    <key>Add</key>
                    <dict>
                        <key>7C436110-AB2A-4BBB-A880-FE41995C9F82</key>
                        <dict>
                            <key>boot-args</key>
                            <string>{bootArgs}</string>
                            <key>csr-active-config</key>
                            <data>AwgAAA==</data>
                            <key>prev-lang:kbd</key>
                            <string>pt-BR:128</string>
                        </dict>
                    </dict>
                </dict>
                <key>PlatformInfo</key>
                <dict>
                    <key>Automatic</key>
                    <true/>
                    <key>Generic</key>
                    <dict>
                        <key>MLB</key>
                        <string>{vm.BoardSerialNumber}</string>
                        <key>SystemProductName</key>
                        <string>{vm.SmbiosModel}</string>
                        <key>SystemSerialNumber</key>
                        <string>{vm.SerialNumber}</string>
                        <key>SystemUUID</key>
                        <string>{vm.SystemUuid}</string>
                        <key>ROM</key>
                        <string>{vm.RomMacAddress}</string>
                    </dict>
                </dict>
                <key>UEFI</key>
                <dict>
                    <key>Output</key>
                    <dict>
                        <key>Resolution</key>
                        <string>{vm.Resolution}</string>
                    </dict>
                </dict>
            </dict>
            </plist>
            """;
    }
}
