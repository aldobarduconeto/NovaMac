using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NovaMac.Models;

public class VirtualMachineConfig : INotifyPropertyChanged
{
    private bool _isRunning;
    private int? _processId;

    public event PropertyChangedEventHandler? PropertyChanged;
    public static Microsoft.UI.Dispatching.DispatcherQueue? UiDispatcher { get; set; }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (UiDispatcher != null && !UiDispatcher.HasThreadAccess)
        {
            UiDispatcher.TryEnqueue(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Name { get; set; } = "macOS Virtual Machine";
    public string ReleaseId { get; set; } = "macos-15-sequoia";
    public string MacOSName { get; set; } = "macOS Sequoia";
    public string MacOSVersion { get; set; } = "15.4";
    public string AccentHex { get; set; } = "#30D158";

    // Hardware Configuration
    public int CpuCores { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
    public int RamGb { get; set; } = 8;
    public int DiskSizeGb { get; set; } = 80;
    public string Resolution { get; set; } = "1920x1080";
    public string VgaAdapter { get; set; } = "std"; // std, vmware, virtio, cirrus
    public string CpuProfile { get; set; } = "Cascadelake-Server";
    public string DiskBus { get; set; } = "sata"; // sata (default/compatível com instalador macOS) ou nvme
    public bool Fullscreen { get; set; } = false; // Iniciar QEMU em Tela Cheia (-full-screen)
    public bool EnableAudio { get; set; } = true;
    public bool MountRecoveryDrive { get; set; } = true;
    public int SshPortForward { get; set; } = 2222;
    public int VncPortForward { get; set; } = 5900;
    public string NetworkCard { get; set; } = "vmxnet3"; // vmxnet3 or e1000-82545em
    public string SharedFolderPath { get; set; } = string.Empty;

    // Bootloader & NVRAM
    public string OpenCoreBootArgs { get; set; } = "-v keepsyms=1 debug=0x100 agdpmod=pikera revpatch=sbvmm,cpuname -lilubetaall -cryptbeta";

    // SMBIOS / Apple Identity
    public string SmbiosModel { get; set; } = "MacPro7,1";
    public string SerialNumber { get; set; } = string.Empty;
    public string BoardSerialNumber { get; set; } = string.Empty;
    public string SystemUuid { get; set; } = Guid.NewGuid().ToString().ToUpperInvariant();
    public string RomMacAddress { get; set; } = "00:16:CB:AB:CD:EF";

    // Storage Paths inside the .novavm bundle directory
    public string BundleDirectory { get; set; } = string.Empty;
    public string SystemDiskPath { get; set; } = string.Empty;
    public string OpenCoreDiskPath { get; set; } = string.Empty;
    public string RecoveryImagePath { get; set; } = string.Empty;
    public string OvmfCodePath { get; set; } = string.Empty;
    public string OvmfVarsPath { get; set; } = string.Empty;
    public string CustomQemuArgs { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastRunAt { get; set; }

    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColorHex));
                OnPropertyChanged(nameof(StatusBorderBrushHex));
                OnPropertyChanged(nameof(IsStartEnabled));
                OnPropertyChanged(nameof(IsStopEnabled));
                OnPropertyChanged(nameof(IsRunningVisibility));
                OnPropertyChanged(nameof(IsStoppedVisibility));
            }
        }
    }

    [JsonIgnore]
    public int? ProcessId
    {
        get => _processId;
        set
        {
            if (_processId != value)
            {
                _processId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    [JsonIgnore]
    public string StatusText => IsRunning
        ? $"Em Execução (PID {ProcessId})"
        : "Parada";

    [JsonIgnore]
    public string StatusColorHex => IsRunning ? "#30D158" : "#8E8E93";

    [JsonIgnore]
    public string StatusBorderBrushHex => IsRunning ? "#30D158" : "#3A3A3C";

    [JsonIgnore]
    public bool IsStartEnabled => !IsRunning;

    [JsonIgnore]
    public bool IsStopEnabled => IsRunning;

    [JsonIgnore]
    public Visibility IsRunningVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    [JsonIgnore]
    public Visibility IsStoppedVisibility => !IsRunning ? Visibility.Visible : Visibility.Collapsed;

    [JsonIgnore]
    public string HardwareSummary => $"{CpuCores} vCPUs • {RamGb} GB RAM • {DiskSizeGb} GB • {Resolution}";

    [JsonIgnore]
    public string PortsDisplay => $"SSH :{SshPortForward}  •  VNC :{VncPortForward}";

    [JsonIgnore]
    public string ShortSmbiosSummary => $"{SmbiosModel} • {SerialNumber}";

    [JsonIgnore]
    public string SpecsSummary => $"{CpuCores} vCPUs ({CpuProfile}) • {RamGb} GB RAM • {DiskSizeGb} GB ({(DiskBus == "nvme" ? "NVMe" : "SATA APFS")}) • {Resolution}{(Fullscreen ? " • 🖥️ Tela Cheia" : "")} • WHPX";
}
