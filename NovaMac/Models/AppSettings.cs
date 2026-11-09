namespace NovaMac.Models;

public class AppSettings
{
    // Appearance
    public string Theme { get; set; } = "Default"; // "Default", "Light", "Dark"
    public string BackdropType { get; set; } = "Acrylic"; // "Mica", "MicaAlt", "Acrylic"

    // Paths
    public string CustomVmsDirectory { get; set; } = string.Empty;
    public string CustomCacheDirectory { get; set; } = string.Empty;
    public string CustomQemuSystemPath { get; set; } = string.Empty;
    public string CustomQemuImgPath { get; set; } = string.Empty;

    // Defaults for New VMs
    public int DefaultCpuCores { get; set; } = 4;
    public int DefaultRamGb { get; set; } = 8;
    public int DefaultDiskGb { get; set; } = 80;
    public string DefaultResolution { get; set; } = "1920x1080";
    public bool DefaultEnableAudio { get; set; } = true;
    public bool DefaultFullscreen { get; set; } = false;
    public int BaseSshPort { get; set; } = 2222;
    public int BaseVncPort { get; set; } = 5900;
    public bool AutoAssignUniquePorts { get; set; } = true;
}
