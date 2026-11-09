using System.IO;
using System.Text.Json;
using NovaMac.Models;

namespace NovaMac.Services;

public class VmStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppSettingsService _settingsService;

    public string BaseAppDataDirectory { get; }

    public string VmsRootDirectory => EnsureDirectory(_settingsService.Current.CustomVmsDirectory, "VMs");

    public string CacheDirectory => EnsureDirectory(_settingsService.Current.CustomCacheDirectory, "Cache");

    private string EnsureDirectory(string customPath, string defaultName)
    {
        string path = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(BaseAppDataDirectory, defaultName)
            : customPath;
        Directory.CreateDirectory(path);
        return path;
    }

    public VmStorageService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        BaseAppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NovaMac");
        Directory.CreateDirectory(BaseAppDataDirectory);
        _ = VmsRootDirectory;
        _ = CacheDirectory;
    }

    public string CreateBundlePath(string vmName, string vmId)
    {
        InstallationStorage.RequireSsd();

        string safeName = string.Join("_", vmName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "macOS_VM";
        }
        return Path.Combine(VmsRootDirectory, $"{safeName}_{vmId}.novavm");
    }

    public (int SshPort, int VncPort) GetNextAvailablePorts()
    {
        var settings = _settingsService.Current;
        int baseSsh = settings.BaseSshPort > 0 ? settings.BaseSshPort : 2222;
        int baseVnc = settings.BaseVncPort > 0 ? settings.BaseVncPort : 5900;

        if (!settings.AutoAssignUniquePorts)
        {
            return (baseSsh, baseVnc);
        }

        var existingVms = LoadAllVms();
        var usedSsh = existingVms.Select(v => v.SshPortForward).ToHashSet();
        var usedVnc = existingVms.Select(v => v.VncPortForward).ToHashSet();

        while (usedSsh.Contains(baseSsh) || !QemuService.IsPortAvailable(baseSsh))
            baseSsh++;

        while (usedVnc.Contains(baseVnc) || !QemuService.IsPortAvailable(baseVnc))
            baseVnc++;

        return (baseSsh, baseVnc);
    }

    public async Task SaveVmAsync(VirtualMachineConfig vm)
    {
        if (string.IsNullOrWhiteSpace(vm.BundleDirectory))
        {
            vm.BundleDirectory = CreateBundlePath(vm.Name, vm.Id);
        }

        Directory.CreateDirectory(vm.BundleDirectory);
        string configPath = Path.Combine(vm.BundleDirectory, "config.json");
        string json = JsonSerializer.Serialize(vm, JsonOptions);
        await File.WriteAllTextAsync(configPath, json);
    }

    public async Task<VirtualMachineConfig> CloneVmAsync(VirtualMachineConfig source)
    {
        var (sshPort, vncPort) = GetNextAvailablePorts();
        string newId = Guid.NewGuid().ToString("N")[..10];
        string newName = $"{source.Name} (Cópia)";
        string newBundle = CreateBundlePath(newName, newId);
        Directory.CreateDirectory(newBundle);

        var clone = new VirtualMachineConfig
        {
            Id = newId,
            Name = newName,
            ReleaseId = source.ReleaseId,
            MacOSName = source.MacOSName,
            MacOSVersion = source.MacOSVersion,
            AccentHex = source.AccentHex,
            CpuCores = source.CpuCores,
            RamGb = source.RamGb,
            DiskSizeGb = source.DiskSizeGb,
            Resolution = source.Resolution,
            VgaAdapter = source.VgaAdapter,
            CpuProfile = source.CpuProfile,
            DiskBus = source.DiskBus,
            EnableAudio = source.EnableAudio,
            Fullscreen = source.Fullscreen,
            MountRecoveryDrive = source.MountRecoveryDrive,
            OpenCoreBootArgs = source.OpenCoreBootArgs,
            SshPortForward = sshPort,
            VncPortForward = vncPort,
            NetworkCard = source.NetworkCard,
            SharedFolderPath = source.SharedFolderPath,
            SmbiosModel = source.SmbiosModel,
            BundleDirectory = newBundle,
            SystemDiskPath = Path.Combine(newBundle, "SystemDisk.qcow2"),
            OpenCoreDiskPath = Path.Combine(newBundle, "OpenCore.qcow2"),
            RecoveryImagePath = source.RecoveryImagePath,
            OvmfCodePath = Path.Combine(newBundle, "OVMF_CODE.fd"),
            OvmfVarsPath = Path.Combine(newBundle, "OVMF_VARS.fd"),
            CustomQemuArgs = source.CustomQemuArgs,
            CreatedAt = DateTime.Now
        };

        SmBiosGenerator.PopulateSmBios(clone, clone.SmbiosModel);

        if (File.Exists(source.OpenCoreDiskPath))
            File.Copy(source.OpenCoreDiskPath, clone.OpenCoreDiskPath, true);
        if (File.Exists(source.OvmfCodePath))
            File.Copy(source.OvmfCodePath, clone.OvmfCodePath, true);
        if (File.Exists(source.OvmfVarsPath))
            File.Copy(source.OvmfVarsPath, clone.OvmfVarsPath, true);

        await SaveVmAsync(clone);
        return clone;
    }

    public List<VirtualMachineConfig> LoadAllVms()
    {
        var result = new List<VirtualMachineConfig>();
        foreach (string dir in Directory.GetDirectories(VmsRootDirectory, "*.novavm"))
        {
            string configPath = Path.Combine(dir, "config.json");
            if (!File.Exists(configPath))
                continue;

            try
            {
                string json = File.ReadAllText(configPath);
                var vm = JsonSerializer.Deserialize<VirtualMachineConfig>(json, JsonOptions);
                if (vm != null)
                {
                    vm.BundleDirectory = dir;
                    result.Add(vm);
                }
            }
            catch
            {
                // Ignora bundle corrompido
            }
        }

        return result.OrderByDescending(v => v.CreatedAt).ToList();
    }

    public void DeleteVm(VirtualMachineConfig vm)
    {
        if (!string.IsNullOrEmpty(vm.BundleDirectory) && Directory.Exists(vm.BundleDirectory))
        {
            Directory.Delete(vm.BundleDirectory, true);
        }
    }

    public (int FileCount, double TotalMegabytes) GetCacheUsageInfo()
    {
        string cacheDirectory = CacheDirectory;
        if (!Directory.Exists(cacheDirectory))
            return (0, 0);

        var files = Directory.GetFiles(cacheDirectory, "*.*", SearchOption.AllDirectories);
        long totalBytes = files.Sum(f => new FileInfo(f).Length);
        return (files.Length, totalBytes / (1024.0 * 1024.0));
    }

    public void ClearCache()
    {
        string cacheDirectory = CacheDirectory;
        if (!Directory.Exists(cacheDirectory))
            return;

        foreach (var file in Directory.GetFiles(cacheDirectory))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignora arquivos bloqueados
            }
        }
    }
}
