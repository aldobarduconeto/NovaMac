using NovaMac.Models;

namespace NovaMac.Services;

public static class SmBiosGenerator
{
    private static readonly char[] Base34Chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();

    public static void PopulateSmBios(VirtualMachineConfig vm, string model = "MacPro7,1")
    {
        vm.SmbiosModel = model;
        vm.SystemUuid = Guid.NewGuid().ToString().ToUpperInvariant();

        // Generate valid Apple 12-char Serial Number (F5K... for MacPro7,1 or C02... for iMacPro1,1)
        string prefix = model == "MacPro7,1" ? "F5K" : "C02";
        string suffix = model == "MacPro7,1" ? "K7GD" : "HX87";
        string mid = GenerateRandomChars(5);
        vm.SerialNumber = $"{prefix}{mid}{suffix}";

        // Generate 17-char MLB (Board Serial Number)
        vm.BoardSerialNumber = $"{prefix}{GenerateRandomChars(9)}0000{GenerateRandomChars(1)}";

        // Generate locally administered or Apple OUI MAC address for ROM
        var rnd = Random.Shared;
        vm.RomMacAddress = $"00:16:CB:{rnd.Next(0, 256):X2}:{rnd.Next(0, 256):X2}:{rnd.Next(0, 256):X2}";
    }

    private static string GenerateRandomChars(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Base34Chars[Random.Shared.Next(Base34Chars.Length)];
        }
        return new string(chars);
    }
}
