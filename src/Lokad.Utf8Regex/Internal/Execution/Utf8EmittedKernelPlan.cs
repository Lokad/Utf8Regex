using Lokad.Utf8Regex.Internal.Diagnostics;
namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8EmittedKernelBlockKind : byte
{
    None = 0,
    FindAnchorSet = 1,
    DispatchPrefixesAtAnchor = 2,
    ConsumeAsciiWhitespace = 3,
    RequireAsciiUpper = 4,
    ConsumeAsciiWordTail = 5,
    AcceptAndAdvance = 6,
    FindCommonPrefix = 7,
    MatchSharedPrefixSuffix = 8,
}

internal readonly struct Utf8EmittedKernelBlock
{
    public Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind kind)
    {
        Kind = kind;
    }

    public Utf8EmittedKernelBlockKind Kind { get; }
}

internal enum Utf8EmittedKernelKind : byte
{
    None = 0,
    UpperWordIdentifierFamily = 1,
    SharedPrefixAsciiWhitespaceSuffix = 2,
}

internal readonly struct Utf8EmittedKernelPlan
{
    private Utf8EmittedKernelPlan(
        Utf8EmittedKernelKind kind,
        Utf8CompiledFindOptimization findOptimization,
        byte[][] prefixes,
        byte requiredSuffixByte,
        Utf8EmittedKernelBlock[] blocks)
    {
        Kind = kind;
        FindOptimization = findOptimization;
        Prefixes = prefixes;
        RequiredSuffixByte = requiredSuffixByte;
        Blocks = blocks;
    }

    public Utf8EmittedKernelKind Kind { get; }

    public Utf8CompiledFindOptimization FindOptimization { get; }

    public byte[][] Prefixes { get; }

    public byte RequiredSuffixByte { get; }

    public Utf8EmittedKernelBlock[] Blocks { get; }

    public bool HasValue => Kind != Utf8EmittedKernelKind.None;

    public static Utf8EmittedKernelPlan CreateUpperWordIdentifierFamily(
        Utf8CompiledFindOptimization findOptimization,
        byte[][] prefixes,
        Utf8EmittedKernelBlock[] blocks) =>
        new(Utf8EmittedKernelKind.UpperWordIdentifierFamily, findOptimization, prefixes, 0, blocks);

    public static Utf8EmittedKernelPlan CreateSharedPrefixAsciiWhitespaceSuffix(
        Utf8CompiledFindOptimization findOptimization,
        byte[][] prefixes,
        byte requiredSuffixByte,
        Utf8EmittedKernelBlock[] blocks) =>
        new(Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix, findOptimization, prefixes, requiredSuffixByte, blocks);

    public Utf8ExecutionRoute Route => Kind switch
    {
        Utf8EmittedKernelKind.UpperWordIdentifierFamily => Utf8ExecutionRoute.NativeStructuralFamilyEmitUpperWordIdentifier,
        Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix => Utf8ExecutionRoute.NativeStructuralFamilyEmitSharedPrefixSuffix,
        _ => Utf8ExecutionRoute.NativeStructuralFamilyEmit,
    };
}
