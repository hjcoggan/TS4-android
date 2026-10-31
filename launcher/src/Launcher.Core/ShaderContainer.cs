using System.Buffers.Binary;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// One Xenos shader container (header and microcode), named <c>ps_</c>/<c>vs_</c> plus the
/// FNV-1a-64 of its bytes: the name <c>--ts4_shader_dump_dir</c> and the shader top-up folder give it
/// (src/d3d/host.cpp, src/native/shader_topup.cpp).
/// </summary>
public sealed record ShaderContainer(string Name, byte[] Bytes)
{
    /// <summary>The game ignores a larger one too (<c>ValidContainerHeader</c> in src/d3d/host.cpp).</summary>
    public const int MaxSize = 1 << 20;

    private const int FixedHeader = 12;

    public static ShaderContainer Of(byte[] bytes) =>
        new($"{(bytes[3] == 1 ? "vs" : "ps")}_{Fnv1a64(bytes):x16}", bytes);

    public static ulong Fnv1a64(ReadOnlySpan<byte> bytes)
    {
        var hash = 14695981039346656037ul;
        foreach (var b in bytes) hash = (hash ^ b) * 1099511628211ul;
        return hash;
    }

    /// <summary>The first four bytes of a container: big-endian <c>0x102A1100</c> (pixel) or <c>0x102A1101</c> (vertex).</summary>
    public static bool HasMagic(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x10 && bytes[1] == 0x2A && bytes[2] == 0x11 && bytes[3] <= 1;

    /// <summary>
    /// The container rule: the magic, then the header size and the microcode size (big-endian words at 4 and 8)
    /// adding up to the bytes' length. Keep it equal to the game's Module scan (src/module_shaders.cpp).
    /// </summary>
    public static bool IsContainer(ReadOnlySpan<byte> bytes)
    {
        if (!HasMagic(bytes) || bytes.Length < FixedHeader || bytes.Length > MaxSize) return false;
        var header = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]);
        var microcode = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        return header >= FixedHeader && microcode > 0 && (long)header + microcode == bytes.Length;
    }
}
