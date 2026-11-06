using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using NovaMac.Models;

namespace NovaMac.Services;

public record HostDiagnostics(
    bool QemuInstalled,
    string QemuExecutablePath,
    string QemuImgPath,
    string QemuVersion,
    bool WhpxAvailable,
    string CpuBrand,
    bool IsAmdCpu,
    int LogicalProcessors);

public class QemuService
{
    private const string AppleOsk = "ourhardworkbythesewordsguardedpleasedontsteal(c)AppleComputerInc";
    private readonly Dictionary<string, Process> _runningProcesses = new();
    private readonly AppSettingsService _settingsService;

    public QemuService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public (string? QemuSystemPath, string? QemuImgPath) FindQemuBinaries()
    {
        var settings = _settingsService.Current;
        if (!string.IsNullOrWhiteSpace(settings.CustomQemuSystemPath) && File.Exists(settings.CustomQemuSystemPath))
        {
            string? dir = Path.GetDirectoryName(settings.CustomQemuSystemPath);
            string defaultImg = dir != null ? Path.Combine(dir, "qemu-img.exe") : "qemu-img.exe";
            string imgPath = !string.IsNullOrWhiteSpace(settings.CustomQemuImgPath) && File.Exists(settings.CustomQemuImgPath)
                ? settings.CustomQemuImgPath
                : defaultImg;
            return (settings.CustomQemuSystemPath, imgPath);
        }

        // 1. Verificar QEMU Embutido no aplicativo (EmbeddedQemu)
        string baseAppDir = AppContext.BaseDirectory;
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] embeddedDirs =
        [
            Path.Combine(baseAppDir, "EmbeddedQemu"),
            baseAppDir,
            Path.Combine(Directory.GetCurrentDirectory(), "EmbeddedQemu"),
            Path.Combine(baseAppDir, "..", "..", "..", "EmbeddedQemu"),
            Path.Combine(baseAppDir, "..", "..", "..", "..", "EmbeddedQemu"),
            Path.Combine(userProfile, "Documents", "NovaMac", "NovaMac", "EmbeddedQemu"),
            Path.Combine(userProfile, "Documents", "NovaMac-portable", "EmbeddedQemu")
        ];

        foreach (var embDir in embeddedDirs)
        {
            try
            {
                if (Directory.Exists(embDir))
                {
                    string embSys = Path.Combine(embDir, "qemu-system-x86_64.exe");
                    string embImg = Path.Combine(embDir, "qemu-img.exe");
                    if (File.Exists(embSys))
                    {
                        return (embSys, File.Exists(embImg) ? embImg : "qemu-img.exe");
                    }
                }
            }
            catch
            {
                // Ignorar erros de caminho
            }
        }

        // 2. Verificar instalacoes padrao no host
        string[] candidateDirs =
        [
            @"C:\Program Files\qemu",
            @"C:\Program Files (x86)\qemu",
            Path.Combine(userProfile, "scoop", "apps", "qemu", "current"),
            @"C:\ProgramData\chocolatey\lib\Qemu\tools",
            @"C:\ProgramData\chocolatey\bin"
        ];

        foreach (var dir in candidateDirs)
        {
            string sys = Path.Combine(dir, "qemu-system-x86_64.exe");
            string img = Path.Combine(dir, "qemu-img.exe");
            if (File.Exists(sys))
            {
                return (sys, File.Exists(img) ? img : "qemu-img.exe");
            }
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string sys = Path.Combine(p.Trim(), "qemu-system-x86_64.exe");
                    string img = Path.Combine(p.Trim(), "qemu-img.exe");
                    if (File.Exists(sys))
                    {
                        return (sys, File.Exists(img) ? img : "qemu-img.exe");
                    }
                }
                catch
                {
                    // Ignore invalid path entries
                }
            }
        }

        return (null, null);
    }

    public async Task<HostDiagnostics> RunDiagnosticsAsync()
    {
        var (qemuSys, qemuImg) = FindQemuBinaries();
        bool qemuInstalled = !string.IsNullOrEmpty(qemuSys) && File.Exists(qemuSys);
        string version = "Não detectado";

        if (qemuInstalled && qemuSys != null)
        {
            try
            {
                var psi = new ProcessStartInfo(qemuSys, "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    version = output.Split('\n').FirstOrDefault()?.Trim() ?? "Instalado";
                }
            }
            catch
            {
                version = "Instalado";
            }
        }

        string cpuId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "x86_64 CPU";
        bool isAmd = cpuId.Contains("AMD", StringComparison.OrdinalIgnoreCase);

        return new HostDiagnostics(
            qemuInstalled,
            qemuSys ?? @"C:\Program Files\qemu\qemu-system-x86_64.exe",
            qemuImg ?? @"C:\Program Files\qemu\qemu-img.exe",
            version,
            IsWhpxAvailable(),
            cpuId,
            isAmd,
            Environment.ProcessorCount);
    }

    public async Task CreateOrConvertDisksAsync(
        VirtualMachineConfig vm,
        string sourceRecoveryDmgPath,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(vm.BundleDirectory);
        var (_, qemuImg) = FindQemuBinaries();

        string systemDisk = Path.Combine(vm.BundleDirectory, "SystemDisk.qcow2");
        string recoveryImg = Path.Combine(vm.BundleDirectory, "BaseSystem.img");

        vm.SystemDiskPath = systemDisk;
        vm.RecoveryImagePath = recoveryImg;

        if (!string.IsNullOrEmpty(qemuImg) && File.Exists(qemuImg))
        {
            if (!File.Exists(systemDisk))
            {
                progress?.Report(new DownloadProgressInfo($"Criando disco virtual macOS ({vm.DiskSizeGb} GB)...", 80, 0, 0, 0));
                await RunProcessAsync(qemuImg, $"create -f qcow2 -o cluster_size=1M,lazy_refcounts=on \"{systemDisk}\" {vm.DiskSizeGb}G", ct);
            }

            if (File.Exists(sourceRecoveryDmgPath) && !File.Exists(recoveryImg))
            {
                progress?.Report(new DownloadProgressInfo("Convertendo BaseSystem.dmg para imagem RAW de alta velocidade...", 90, 0, 0, 0));
                try
                {
                    await RunProcessAsync(qemuImg, $"convert -f dmg -O raw \"{sourceRecoveryDmgPath}\" \"{recoveryImg}\"", ct);
                }
                catch
                {
                    File.Copy(sourceRecoveryDmgPath, recoveryImg, true);
                }
            }
        }
        else
        {
            if (File.Exists(sourceRecoveryDmgPath) && !File.Exists(recoveryImg))
            {
                File.Copy(sourceRecoveryDmgPath, recoveryImg, true);
            }
        }

        await WriteLaunchScriptsAsync(vm);
    }

    [DllImport("WinHvPlatform.dll", ExactSpelling = true)]
    private static extern int WHvGetCapability(
        uint capabilityCode, out int capability, uint capabilitySize, out uint writtenSize);

    public static bool IsWhpxAvailable()
    {
        const uint hypervisorPresent = 0;
        try
        {
            return WHvGetCapability(hypervisorPresent, out int present, sizeof(int), out uint written) >= 0
                && written == sizeof(int) && present != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool IsPortAvailable(int port)
    {
        try
        {
            var ipProps = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipProps.GetActiveTcpListeners();
            return !tcpListeners.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return true;
        }
    }

    public static int FindNextAvailablePort(int startingPort)
    {
        int port = startingPort;
        while (!IsPortAvailable(port) && port < 65535)
        {
            port++;
        }
        return port;
    }

    public void EnsureAvailablePorts(VirtualMachineConfig vm)
    {
        if (!IsPortAvailable(vm.SshPortForward))
        {
            vm.SshPortForward = FindNextAvailablePort(vm.SshPortForward + 1);
        }
        if (!IsPortAvailable(vm.VncPortForward))
        {
            vm.VncPortForward = FindNextAvailablePort(vm.VncPortForward + 1);
        }
    }

    public string BuildQemuArguments(VirtualMachineConfig vm)
    {
        string safeName = vm.Name.Replace(",", "").Replace("\"", "").Trim();
        var args = new List<string>();

        var (qemuSys, _) = FindQemuBinaries();
        if (!string.IsNullOrEmpty(qemuSys))
        {
            string? qemuDir = Path.GetDirectoryName(qemuSys);
            if (!string.IsNullOrEmpty(qemuDir))
            {
                string shareDir = Path.Combine(qemuDir, "share");
                if (Directory.Exists(shareDir))
                {
                    args.Add($"-L \"{shareDir}\"");
                }
            }
        }

        args.Add($"-name \"{safeName},process=NovaMac-{vm.Id}\"");
        args.Add($"-m {vm.RamGb * 1024}M");
        args.Add($"-smp {vm.CpuCores},sockets=1,cores={vm.CpuCores},threads=1");

        args.Add("-accel whpx,kernel-irqchip=off");

        string cpuFlags;
        if (vm.CpuProfile.Equals("Penryn", StringComparison.OrdinalIgnoreCase))
        {
            cpuFlags = "+ssse3,+sse4.2,+popcnt,+aes,+xsave,+xsaveopt,+invtsc,vmware-cpuid-freq=on,+pcid,+x2apic";
        }
        else
        {
            cpuFlags = "+ssse3,+sse4.2,+popcnt,+avx,+avx2,+aes,+xsave,+xsaveopt,+bmi1,+bmi2,+smep,+smap,+fma,+movbe,+invtsc,vmware-cpuid-freq=on,+pcid,+x2apic,+pdpe1gb";
        }

        string smbiosProduct = vm.SmbiosModel.Replace(",", ",,");
        args.Add($"-cpu {vm.CpuProfile},vendor=GenuineIntel,{cpuFlags}");
        args.Add("-machine q35,smm=off,vmport=off,hpet=off,i8042=off");

        // CORREÇÃO CRÍTICA DE INSTALAÇÃO DO MACOS:
        // O macOS exige relógio em UTC no hardware RTC. Se base=localtime for usado no Windows,
        // o instalador do macOS sofre desvio de fuso horário, invalidando os certificados SSL e tíquetes de instalação
        // da Apple ("An error occurred while preparing the installation").
        args.Add("-rtc base=utc,clock=host");

        args.Add($"-device isa-applesmc,osk={AppleOsk}");
        args.Add($"-smbios type=2,manufacturer=\"Apple Inc.\",product=\"{smbiosProduct}\",serial=\"{vm.BoardSerialNumber}\"");
        args.Add($"-uuid {vm.SystemUuid}");

        if (!string.IsNullOrEmpty(vm.OvmfCodePath))
        {
            args.Add($"-drive if=pflash,format=raw,readonly=on,file=\"{vm.OvmfCodePath}\"");
        }
        if (!string.IsNullOrEmpty(vm.OvmfVarsPath))
        {
            args.Add($"-drive if=pflash,format=raw,file=\"{vm.OvmfVarsPath}\"");
        }

        args.Add("-global ICH9-LPC.acpi-pci-hotplug-with-bridge-support=off");
        args.Add("-device ich9-ahci,id=sata");
        args.Add("-device usb-ehci,id=ehci");
        args.Add("-device usb-kbd,bus=ehci.0");
        args.Add("-device usb-tablet,bus=ehci.0");

        // 1. BOOTLOADER OPENCORE (bootindex=1)
        if (!string.IsNullOrEmpty(vm.OpenCoreDiskPath))
        {
            string ocFormat = vm.OpenCoreDiskPath.EndsWith(".qcow2", StringComparison.OrdinalIgnoreCase) ? "qcow2" : "raw";
            args.Add($"-drive id=OpenCoreBoot,if=none,snapshot=on,cache=unsafe,format={ocFormat},file=\"{vm.OpenCoreDiskPath}\"");
            args.Add("-device ide-hd,bus=sata.2,drive=OpenCoreBoot,bootindex=1");
        }

        // 2. DISCO DE SISTEMA PRINCIPAL (bootindex=2)
        // CORREÇÃO CRÍTICA DE INSTALAÇÃO DO MACOS:
        // O OpenCore do OSX-KVM mapeia nativamente o disco de instalação no barramento AHCI SATA (sata.4).
        // Usar sata.4 como padrão garante reconhecimento imediato no Utilitário de Disco como APFS
        // e permite que a Fase 2 pós-reboot encontre o instalador gravado no disco sem ficar preso em loop.
        // O bootindex=2 assegura prioridade superior à mídia de recuperação (bootindex=3).
        if (!string.IsNullOrEmpty(vm.SystemDiskPath))
        {
            args.Add($"-drive id=MacHDD,if=none,format=qcow2,cache=writeback,discard=unmap,detect-zeroes=unmap,aio=threads,file=\"{vm.SystemDiskPath}\"");
            args.Add(string.Equals(vm.DiskBus, "nvme", StringComparison.OrdinalIgnoreCase)
                ? "-device nvme,drive=MacHDD,serial=NovaMacNVMe01,bootindex=2"
                : "-device ide-hd,bus=sata.4,drive=MacHDD,bootindex=2");
        }

        // 3. MÍDIA DE RECUPERAÇÃO / INSTALADOR (bootindex=3)
        // Definir bootindex=3 assegura que, após o primeiro estágio da instalação gravar o sistema no disco,
        // o reinício do computador inicialize a partir do disco principal instalado (bootindex=2) e não do instalador.
        if (vm.MountRecoveryDrive && !string.IsNullOrEmpty(vm.RecoveryImagePath))
        {
            args.Add($"-drive id=InstallMedia,if=none,format=raw,cache=unsafe,file=\"{vm.RecoveryImagePath}\"");
            args.Add("-device ide-hd,bus=sata.3,drive=InstallMedia,bootindex=3");
        }

        string vga = string.IsNullOrWhiteSpace(vm.VgaAdapter) ? "std" : vm.VgaAdapter;
        args.Add($"-vga {vga}");
        args.Add("-global VGA.vgamem_mb=128");
        args.Add("-display sdl,gl=off");

        if (vm.Fullscreen)
        {
            args.Add("-full-screen");
        }

        // Use the DNS proxy inside the guest NAT subnet.
        args.Add($"-netdev user,id=net0,hostfwd=tcp::{vm.SshPortForward}-:22,hostfwd=tcp::{vm.VncPortForward}-:5900");
        args.Add($"-device {vm.NetworkCard},netdev=net0,id=net0,mac={vm.RomMacAddress}");

        // CORREÇÃO DE ÁUDIO:
        // hda-output evita travamentos caso o host não possua dispositivo de gravação/microfone configurado no DirectSound
        if (vm.EnableAudio)
        {
            args.Add("-audiodev dsound,id=snd0,out.frequency=44100");
            args.Add("-device ich9-intel-hda");
            args.Add("-device hda-output,audiodev=snd0");
        }

        if (!string.IsNullOrWhiteSpace(vm.SharedFolderPath) && Directory.Exists(vm.SharedFolderPath))
        {
            args.Add($"-drive file=fat:rw:\"{vm.SharedFolderPath}\",format=raw,media=disk");
        }

        if (!string.IsNullOrWhiteSpace(vm.CustomQemuArgs))
        {
            args.Add(vm.CustomQemuArgs.Trim());
        }

        return string.Join(" ", args);
    }

    public async Task WriteLaunchScriptsAsync(VirtualMachineConfig vm)
    {
        await OpenCoreService.WriteOpenCorePlistAsync(vm);
    }

    public async Task<Process> StartVmAsync(VirtualMachineConfig vm)
    {
        if (!IsWhpxAvailable())
            throw new InvalidOperationException("O WHPX é obrigatório. Habilite a Plataforma do Hipervisor do Windows e confira a virtualização no firmware do computador.");

        if (System.Text.RegularExpressions.Regex.IsMatch(vm.CustomQemuArgs?.Replace("\"", "") ?? string.Empty,
            @"(?:^|\s)--?(?:accel|machine|M|readconfig)(?=\s|=|$)"))
            throw new InvalidOperationException("Remova dos argumentos personalizados as opções de aceleração, máquina ou arquivo de configuração. O NovaMac usa WHPX.");

        var (qemuSys, qemuImg) = FindQemuBinaries();
        if (string.IsNullOrEmpty(qemuSys) || !File.Exists(qemuSys))
        {
            throw new FileNotFoundException(
                "O executável do QEMU (qemu-system-x86_64.exe) não foi encontrado. " +
                "Configure o caminho nas Configurações do Aplicativo ou instale via Diagnóstico WHPX & QEMU.");
        }

        if (!File.Exists(vm.SystemDiskPath) && !string.IsNullOrEmpty(qemuImg) && File.Exists(qemuImg))
        {
            await RunProcessAsync(qemuImg, $"create -f qcow2 -o cluster_size=1M,lazy_refcounts=on \"{vm.SystemDiskPath}\" {vm.DiskSizeGb}G", CancellationToken.None);
        }

        EnsureAvailablePorts(vm);
        await WriteLaunchScriptsAsync(vm);

        string args = BuildQemuArguments(vm);
        string logPath = Path.Combine(vm.BundleDirectory, "qemu.log");
        var (proc, failure) = await LaunchQemuAsync(qemuSys, args, vm.BundleDirectory);

        if (failure is not null)
        {
            try
            {
                await File.WriteAllTextAsync(logPath, failure);
            }
            catch (IOException)
            {
                // Preserve the launch result if the diagnostic file cannot be written.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (proc is null)
        {
            throw new InvalidOperationException($"{failure}\n\nDiagnóstico: {logPath}");
        }

        string batPath = Path.Combine(vm.BundleDirectory, "start-vm.bat");
        try
        {
            await File.WriteAllTextAsync(batPath, $"@echo off\r\ncd /d \"{vm.BundleDirectory}\"\r\n\"{qemuSys}\" {args}\r\nif errorlevel 1 pause\r\n");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        _runningProcesses[vm.Id] = proc;
        vm.IsRunning = true;
        vm.ProcessId = proc.Id;
        vm.LastRunAt = DateTime.Now;
        proc.Exited += (_, _) => _runningProcesses.Remove(vm.Id);
        if (proc.HasExited)
            _runningProcesses.Remove(vm.Id);

        return proc;
    }

    private static async Task<(Process? Process, string? Failure)> LaunchQemuAsync(
        string executable, string args, string workingDirectory)
    {
        var errors = new StringBuilder();
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                errors.AppendLine(e.Data);
        };

        if (!proc.Start())
        {
            proc.Dispose();
            throw new InvalidOperationException("Falha ao iniciar o processo do QEMU.");
        }

        proc.BeginErrorReadLine();
        var exited = proc.WaitForExitAsync();
        await Task.WhenAny(exited, Task.Delay(3000));
        if (!proc.HasExited)
            return (proc, null);

        // Wait for stderr to finish draining before classifying the failure.
        await exited;
        int exitCode = proc.ExitCode;
        proc.Dispose();
        return (null, $"O QEMU encerrou durante a inicialização (código {exitCode}).\n{errors.ToString().Trim()}");
    }

    public void StopVm(VirtualMachineConfig vm)
    {
        if (_runningProcesses.TryGetValue(vm.Id, out var proc))
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(true);
                }
            }
            catch
            {
                // Ignore if already exited
            }
            finally
            {
                _runningProcesses.Remove(vm.Id);
                vm.IsRunning = false;
                vm.ProcessId = null;
            }
        }
    }

    public bool IsVmRunning(string vmId)
    {
        if (_runningProcesses.TryGetValue(vmId, out var proc))
        {
            try
            {
                if (!proc.HasExited)
                    return true;
            }
            catch
            {
            }
            _runningProcesses.Remove(vmId);
        }
        return false;
    }

    public int? GetVmProcessId(string vmId)
    {
        if (_runningProcesses.TryGetValue(vmId, out var proc))
        {
            try
            {
                if (!proc.HasExited)
                    return proc.Id;
            }
            catch
            {
            }
        }
        return null;
    }

    private static async Task RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Não foi possível iniciar {fileName}");
        await proc.WaitForExitAsync(ct);
    }
}
