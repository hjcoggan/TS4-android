using System.Buffers.Binary;

namespace TopSpin4.Launcher.Core;

/// <summary>An XEX module version: major.minor.build.qfe in 4, 4, 16 and 8 bits.</summary>
public readonly record struct ModuleVersion(uint Value)
{
    public override string ToString() => $"v{Value >> 28}.{(Value >> 24) & 0xF}.{(Value >> 8) & 0xFFFF}.{Value & 0xFF}";
}

/// <summary>A Module's execution info: which title, disc edition and version it is.</summary>
internal readonly record struct XexExecutionInfo(uint MediaId, ModuleVersion Version, ModuleVersion BaseVersion, uint TitleId);

/// <summary>A module patch's delta descriptor: the version it raises a Module from and to.</summary>
internal readonly record struct XexDeltaPatch(ModuleVersion Target, ModuleVersion Source);

/// <summary>
/// The optional headers of an XEX2 image (a Module) or XEXP (a module patch) that identify it.
/// Layout after Xenia's <c>xex2_info.h</c>: "XEX2", flags, the PE data offset (the header's
/// end), a reserved word, the security info offset, then a count of (key, value) pairs.
/// </summary>
internal sealed record XexHeader(XexExecutionInfo? Execution, XexDeltaPatch? DeltaPatch)
{
    private const uint ExecutionInfoKey = 0x00040006;
    private const uint DeltaPatchDescriptorKey = 0x000005FF;

    /// <summary>How much of a file to read: real headers are a few KiB; the PE data offset bounds them.</summary>
    public const int MaxHeaderBytes = 1 << 20;

    /// <summary>The header in <paramref name="data"/> (the file's first bytes), or null when it is not XEX2.</summary>
    public static XexHeader? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[..4].SequenceEqual("XEX2"u8)) return null;
        var end = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(data[8..]), (uint)data.Length);
        var count = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        if (count > (end - 24) / 8) return null;
        XexExecutionInfo? execution = null;
        XexDeltaPatch? delta = null;
        for (var i = 0; i < count; i++)
        {
            var key = BinaryPrimitives.ReadUInt32BigEndian(data[(24 + i * 8)..]);
            var value = BinaryPrimitives.ReadUInt32BigEndian(data[(28 + i * 8)..]);
            if (key == ExecutionInfoKey && value <= end - 16)
            {
                var info = data[(int)value..];
                execution = new XexExecutionInfo(BinaryPrimitives.ReadUInt32BigEndian(info),
                    new ModuleVersion(BinaryPrimitives.ReadUInt32BigEndian(info[4..])),
                    new ModuleVersion(BinaryPrimitives.ReadUInt32BigEndian(info[8..])),
                    BinaryPrimitives.ReadUInt32BigEndian(info[12..]));
            }
            else if (key == DeltaPatchDescriptorKey && value <= end - 12)
            {
                var descriptor = data[(int)value..];
                delta = new XexDeltaPatch(new ModuleVersion(BinaryPrimitives.ReadUInt32BigEndian(descriptor[4..])),
                    new ModuleVersion(BinaryPrimitives.ReadUInt32BigEndian(descriptor[8..])));
            }
        }
        return new XexHeader(execution, delta);
    }
}
