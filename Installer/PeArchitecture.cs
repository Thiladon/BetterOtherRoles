using System.IO;

namespace BorInstaller;

/// <summary>
///     Whether a Windows executable or DLL is 32 or 64 bits (machine of its PE header): the Steam game can be either,
///     and BepInEx (winhttp.dll) must be the same.
/// </summary>
internal static class PeArchitecture
{
    private const ushort MachineX64 = 0x8664;

    /// <summary>
    ///     Gets a value indicating whether the file is 64 bits, or null when it cannot be read.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>Whether it is 64 bits.</returns>
    public static bool? Is64Bit(string path)
    {
        if (!File.Exists(path)) return null;

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 64) return null;

        stream.Position = 60;
        var header = reader.ReadInt32();
        if (header <= 0 || header + 6 > stream.Length) return null;

        stream.Position = header + 4;
        return reader.ReadUInt16() == MachineX64;
    }
}
