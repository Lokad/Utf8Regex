using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledFindOptimizationKind : byte
{
    None = 0,
    AnchorByteSetAtOffset = 1,
    SharedPrefix = 2,
}

internal readonly struct Utf8CompiledFindOptimization
{
    public Utf8CompiledFindOptimization(int anchorOffset, byte[] anchorBytes)
    {
        Kind = Utf8CompiledFindOptimizationKind.AnchorByteSetAtOffset;
        AnchorOffset = anchorOffset;
        AnchorBytes = anchorBytes;
        CommonPrefix = [];
        PrefixDiscriminator = default;
    }

    public Utf8CompiledFindOptimization(byte[] commonPrefix, AsciiExactLiteralPrefixDiscriminator prefixDiscriminator)
    {
        Kind = Utf8CompiledFindOptimizationKind.SharedPrefix;
        AnchorOffset = 0;
        AnchorBytes = [];
        CommonPrefix = commonPrefix;
        PrefixDiscriminator = prefixDiscriminator;
    }

    public Utf8CompiledFindOptimizationKind Kind { get; }

    public int AnchorOffset { get; }

    public byte[] AnchorBytes { get; }

    public byte[] CommonPrefix { get; }

    public AsciiExactLiteralPrefixDiscriminator PrefixDiscriminator { get; }
}
