using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledFindOptimizationKind : byte
{
    None = 0,
    AnchorByteSetAtOffset = 1,
    SharedPrefix = 2,
}

internal readonly struct Utf8CompiledFindOptimization
{
    public Utf8CompiledFindOptimization(int anchorOffset, byte[] anchorBytes, byte[][] anchorPrefixesByByte)
    {
        Kind = Utf8CompiledFindOptimizationKind.AnchorByteSetAtOffset;
        AnchorOffset = anchorOffset;
        AnchorBytes = anchorBytes;
        AnchorPrefixesByByte = anchorPrefixesByByte;
        CommonPrefix = [];
        PrefixDiscriminator = default;
    }

    public Utf8CompiledFindOptimization(byte[] commonPrefix, AsciiExactLiteralPrefixDiscriminator prefixDiscriminator)
    {
        Kind = Utf8CompiledFindOptimizationKind.SharedPrefix;
        AnchorOffset = 0;
        AnchorBytes = [];
        AnchorPrefixesByByte = null;
        CommonPrefix = commonPrefix;
        PrefixDiscriminator = prefixDiscriminator;
    }

    public Utf8CompiledFindOptimizationKind Kind { get; }

    public int AnchorOffset { get; }

    public byte[] AnchorBytes { get; }

    public byte[][]? AnchorPrefixesByByte { get; }

    public byte[] CommonPrefix { get; }

    public AsciiExactLiteralPrefixDiscriminator PrefixDiscriminator { get; }

    public bool HasValue => Kind != Utf8CompiledFindOptimizationKind.None;
}
