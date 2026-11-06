using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NovaMac.Services;

internal static class InstallationStorage
{
    internal static void RequireSsd()
    {
        var mountPoint = new StringBuilder(32768);
        var volumeName = new StringBuilder(64);
        if (GetVolumePathNameW(AppContext.BaseDirectory, mountPoint, mountPoint.Capacity) &&
            GetVolumeNameForVolumeMountPointW(mountPoint.ToString(), volumeName, volumeName.Capacity))
        {
            const uint shareReadWrite = 3;
            const uint openExisting = 3;
            using var volume = CreateFileW(volumeName.ToString().TrimEnd('\\'), 0, shareReadWrite,
                IntPtr.Zero, openExisting, 0, IntPtr.Zero);

            const uint storageQueryProperty = 0x002D1400;
            const int seekPenaltyProperty = 7;
            const int seekPenaltyOffset = 8;
            int[] query = [seekPenaltyProperty, 0, 0];
            byte[] descriptor = new byte[12];
            if (!volume.IsInvalid &&
                DeviceIoControl(volume, storageQueryProperty, query, query.Length * sizeof(int),
                    descriptor, descriptor.Length, out int bytesReturned, IntPtr.Zero) &&
                bytesReturned > seekPenaltyOffset)
            {
                if (descriptor[seekPenaltyOffset] == 0)
                    return;

                throw new InvalidOperationException("Para criar uma VM, execute o NovaMac a partir de um SSD. O disco atual é rotacional.");
            }
        }

        throw new InvalidOperationException("O Windows não conseguiu confirmar que o NovaMac está em um SSD. A criação da VM exige um SSD reconhecido pelo Windows.");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool GetVolumePathNameW(string fileName, StringBuilder volumePath, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool GetVolumeNameForVolumeMountPointW(string mountPoint, StringBuilder volumeName, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint access, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        int[] input, int inputSize, [Out] byte[] output, int outputSize, out int bytesReturned, IntPtr overlapped);
}
