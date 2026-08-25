using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;
using PCRE;

namespace Lokad.Utf8Regex.Benchmarks;

/// <summary>
/// Benchmark-only access to the native PCRE2 10.47 standard matcher bundled by PCRE.NET 1.5.0.
/// This type must not be referenced by any shipped project.
/// </summary>
internal sealed class PcreNetNativeBenchmarkBaseline : IDisposable
{
    internal const string PackageId = "PCRE.NET";
    internal const string PackageVersion = "1.5.0";
    internal const string PackageSha512 = "Zu3NJGiU1S7tHHaW4UdEK1WZ9LFYqPI+6Y0eiL6YPHVOHSoWjbq0x5j3uN9895DoIgO5XI/50S6dj2ZmRHirNA==";
    internal const string SourceRevision = "e5e5deaa30d50dd3a7ac68ec7fd9e9556551a84f";

    private readonly byte[] _input;
    private readonly PcreRegexUtf8 _regex;
    private readonly PcreMatchBuffer8Bit _matchBuffer;

    internal PcreNetNativeBenchmarkBaseline(Utf8Pcre2BenchmarkCase benchmarkCase)
    {
        _input = System.Text.Encoding.UTF8.GetBytes(benchmarkCase.Input);
        var settings = new PcreRegexSettings
        {
            Options = ToPcreNetOptions(benchmarkCase),
            NewLine = ToPcreNetNewLine(benchmarkCase.CompileSettings.Newline),
            BackslashR = ToPcreNetBackslashR(benchmarkCase.CompileSettings.Bsr),
            ExtraCompileOptions = benchmarkCase.CompileSettings.AllowLookaroundBackslashK
                ? PcreExtraCompileOptions.AllowLookaroundBsK
                : PcreExtraCompileOptions.None,
        };
        _regex = new PcreRegexUtf8(benchmarkCase.Pattern, settings);
        _matchBuffer = _regex.CreateMatchBuffer();
    }

    internal static string NativePcre2Version => PcreBuildInfo.Version;

    internal static PcreNetNativeBuildFingerprint CaptureBuildFingerprint()
    {
        var fields = BuildFingerprintFields(
            PcreBuildInfo.Version,
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.OSArchitecture,
            PcreBuildInfo.Jit,
            PcreBuildInfo.JitTarget,
            PcreBuildInfo.Unicode,
            PcreBuildInfo.UnicodeVersion,
            PcreBuildInfo.CompiledWidths,
            PcreBuildInfo.LinkSize,
            PcreBuildInfo.EffectiveLinkSize,
            PcreBuildInfo.NewLine,
            PcreBuildInfo.BackslashR,
            PcreBuildInfo.HeapLimit,
            PcreBuildInfo.MatchLimit,
            PcreBuildInfo.DepthLimit,
            PcreBuildInfo.ParensLimit,
            PcreBuildInfo.TablesLength,
            PcreBuildInfo.NeverBackslashC);
        return new PcreNetNativeBuildFingerprint
        {
            Sha256 = ComputeFingerprintSha256(fields),
            EngineVersion = PcreBuildInfo.Version,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystemArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            JitSupported = PcreBuildInfo.Jit,
            JitTarget = PcreBuildInfo.JitTarget,
            UnicodeSupported = PcreBuildInfo.Unicode,
            UnicodeVersion = PcreBuildInfo.UnicodeVersion,
            CompiledWidths = PcreBuildInfo.CompiledWidths,
            LinkSizeBytes = PcreBuildInfo.LinkSize,
            EffectiveLinkSizeBytes = PcreBuildInfo.EffectiveLinkSize,
            DefaultNewline = PcreBuildInfo.NewLine.ToString(),
            DefaultBackslashR = PcreBuildInfo.BackslashR.ToString(),
            DefaultHeapLimitKibibytes = PcreBuildInfo.HeapLimit,
            DefaultMatchLimit = PcreBuildInfo.MatchLimit,
            DefaultDepthLimit = PcreBuildInfo.DepthLimit,
            ParenthesesLimit = PcreBuildInfo.ParensLimit,
            CharacterTablesLengthBytes = PcreBuildInfo.TablesLength,
            BackslashCPermanentlyDisabled = PcreBuildInfo.NeverBackslashC,
        };
    }

    internal static Pcre2BenchmarkWorkspaceContract CaptureWorkspaceContract() => new()
    {
        StateHolder = nameof(PcreMatchBuffer8Bit),
        Lifetime = "One buffer is constructed per qualified operation row, reused for checksum, warmup, calibration, paired samples, and allocation probing, then disposed.",
        ConcurrencyContract = "The buffer is not thread-safe and not reentrant; a caller must provide a distinct buffer for overlapping operations.",
        RetainedMemoryContract = "The reusable buffer retains native match data between operations.",
        RetainedNativeHeapHighWaterUnavailableReason =
            "PCRE.NET 1.5.0 does not expose retained match-data heap-frame high-water memory through its public API.",
    };

    internal PcreNetNativePlanFingerprint CapturePlanFingerprint()
    {
        var info = _regex.PatternInfo;
        var firstCodeType = info.FirstCodeType;
        var firstCodeUnit = firstCodeType == 1
            ? CaptureOptionalPatternInfo(() => info.FirstCodeUnit)
            : null;
        var lastCodeType = info.LastCodeType;
        var lastCodeUnit = lastCodeType == 1
            ? CaptureOptionalPatternInfo(() => info.LastCodeUnit)
            : null;
        var patternMatchLimit = CaptureOptionalPatternInfo(() => info.MatchLimit);
        var patternDepthLimit = CaptureOptionalPatternInfo(() => info.DepthLimit);
        var patternHeapLimit = CaptureOptionalPatternInfo(() => info.HeapLimit);
        var fields = BuildFingerprintFields(
            info.PatternSize,
            info.FrameSize,
            info.JitSize,
            info.IsCompiled,
            info.CaptureCount,
            info.NamedGroupsCount,
            info.MaxBackReference,
            info.CanMatchEmptyString,
            info.MaxLookBehind,
            info.MinSubjectLength,
            firstCodeType,
            firstCodeUnit?.ToString(CultureInfo.InvariantCulture) ?? "unset",
            lastCodeType,
            lastCodeUnit?.ToString(CultureInfo.InvariantCulture) ?? "unset",
            (uint)info.ArgOptions,
            (uint)info.AllOptions,
            (uint)info.ExtraOptions,
            patternMatchLimit?.ToString(CultureInfo.InvariantCulture) ?? "unset",
            patternDepthLimit?.ToString(CultureInfo.InvariantCulture) ?? "unset",
            patternHeapLimit?.ToString(CultureInfo.InvariantCulture) ?? "unset");
        return new PcreNetNativePlanFingerprint
        {
            Sha256 = ComputeFingerprintSha256(fields),
            PatternSizeBytes = info.PatternSize,
            FrameSizeBytes = info.FrameSize,
            JitSizeBytes = info.JitSize,
            IsJitCompiled = info.IsCompiled,
            CaptureCount = info.CaptureCount,
            NamedGroupsCount = info.NamedGroupsCount,
            MaximumBackReference = info.MaxBackReference,
            CanMatchEmptyString = info.CanMatchEmptyString,
            MaximumLookbehindCharacters = info.MaxLookBehind,
            MinimumSubjectCharacters = info.MinSubjectLength,
            FirstCodeType = firstCodeType,
            FirstCodeUnit = firstCodeUnit,
            LastCodeType = lastCodeType,
            LastCodeUnit = lastCodeUnit,
            ArgumentOptions = info.ArgOptions.ToString(),
            EffectiveOptions = info.AllOptions.ToString(),
            ExtraOptions = info.ExtraOptions.ToString(),
            PatternMatchLimit = patternMatchLimit,
            PatternDepthLimit = patternDepthLimit,
            PatternHeapLimitKibibytes = patternHeapLimit,
        };
    }

    internal int Execute(Utf8Pcre2BenchmarkOperation operation) => Execute(operation, _matchBuffer);

    internal int ExecuteWithFreshMatchBuffer(Utf8Pcre2BenchmarkOperation operation)
    {
        using var matchBuffer = _regex.CreateMatchBuffer();
        return Execute(operation, matchBuffer);
    }

    internal Pcre2BenchmarkResultChecksum ComputeChecksum(Utf8Pcre2BenchmarkOperation operation) =>
        ComputeChecksum(operation, _matchBuffer);

    internal Pcre2BenchmarkResultChecksum ComputeChecksumWithFreshMatchBuffer(
        Utf8Pcre2BenchmarkOperation operation)
    {
        using var matchBuffer = _regex.CreateMatchBuffer();
        return ComputeChecksum(operation, matchBuffer);
    }

    private int Execute(
        Utf8Pcre2BenchmarkOperation operation,
        PcreMatchBuffer8Bit matchBuffer) => operation switch
    {
        Utf8Pcre2BenchmarkOperation.IsMatch => matchBuffer.IsMatch(_input) ? 1 : 0,
        Utf8Pcre2BenchmarkOperation.Count => CountMatches(matchBuffer),
        Utf8Pcre2BenchmarkOperation.EnumerateMatches => ComputeRangeSink(matchBuffer, int.MaxValue),
        Utf8Pcre2BenchmarkOperation.MatchMany => ComputeRangeSink(matchBuffer, 8),
        _ => throw new NotSupportedException(
            $"The PCRE.NET UTF-8 span baseline does not expose equivalent work for {operation}."),
    };

    private Pcre2BenchmarkResultChecksum ComputeChecksum(
        Utf8Pcre2BenchmarkOperation operation,
        PcreMatchBuffer8Bit matchBuffer)
    {
        var checksum = new Pcre2BenchmarkChecksumBuilder(operation);
        switch (operation)
        {
            case Utf8Pcre2BenchmarkOperation.IsMatch:
                return checksum.Complete(matchBuffer.IsMatch(_input) ? 1 : 0, false);
            case Utf8Pcre2BenchmarkOperation.Count:
                return checksum.Complete(CountMatches(matchBuffer), false);
            case Utf8Pcre2BenchmarkOperation.EnumerateMatches:
                return ComputeRangeChecksum(matchBuffer, ref checksum, int.MaxValue);
            case Utf8Pcre2BenchmarkOperation.MatchMany:
                return ComputeRangeChecksum(matchBuffer, ref checksum, 8);
            default:
                throw new NotSupportedException(
                    $"The PCRE.NET UTF-8 span baseline does not expose equivalent work for {operation}.");
        }
    }

    internal Pcre2ProgressionAudit ComputeProgressionAudit()
    {
        var checksum = new Pcre2BenchmarkChecksumBuilder(Utf8Pcre2BenchmarkOperation.EnumerateMatches);
        var enumeratedCount = 0;
        foreach (var match in _matchBuffer.Matches(_input))
        {
            checksum.AddRange(match.Index, match.Length);
            enumeratedCount++;
        }

        return new Pcre2ProgressionAudit(
            CountMatches(_matchBuffer),
            enumeratedCount,
            checksum.Complete(enumeratedCount, false));
    }

    internal static bool Supports(Utf8Pcre2BenchmarkOperation operation) => operation is
        Utf8Pcre2BenchmarkOperation.IsMatch or
        Utf8Pcre2BenchmarkOperation.Count or
        Utf8Pcre2BenchmarkOperation.EnumerateMatches or
        Utf8Pcre2BenchmarkOperation.MatchMany;

    public void Dispose()
    {
        _matchBuffer.Dispose();
        GC.KeepAlive(_regex);
    }

    private int CountMatches(PcreMatchBuffer8Bit matchBuffer)
    {
        var count = 0;
        foreach (var _ in matchBuffer.Matches(_input))
        {
            count++;
        }

        return count;
    }

    private int ComputeRangeSink(PcreMatchBuffer8Bit matchBuffer, int limit)
    {
        var written = 0;
        var sink = 0;
        var isMore = false;
        foreach (var match in matchBuffer.Matches(_input))
        {
            if (written == limit)
            {
                isMore = true;
                break;
            }

            sink = Pcre2BenchmarkRangeSink.Add(sink, match.Index, match.Length);
            written++;
        }

        return Pcre2BenchmarkRangeSink.Complete(sink, written, isMore);
    }

    private Pcre2BenchmarkResultChecksum ComputeRangeChecksum(
        PcreMatchBuffer8Bit matchBuffer,
        ref Pcre2BenchmarkChecksumBuilder checksum,
        int limit)
    {
        var written = 0;
        var isMore = false;
        foreach (var match in matchBuffer.Matches(_input))
        {
            if (written == limit)
            {
                isMore = true;
                break;
            }

            checksum.AddRange(match.Index, match.Length);
            written++;
        }

        return checksum.Complete(written, isMore);
    }

    private static PcreOptions ToPcreNetOptions(Utf8Pcre2BenchmarkCase benchmarkCase)
    {
        var result = PcreOptions.None;
        if ((benchmarkCase.Options & RegexOptions.IgnoreCase) != 0)
        {
            result |= PcreOptions.IgnoreCase;
        }

        if ((benchmarkCase.Options & RegexOptions.Multiline) != 0)
        {
            result |= PcreOptions.MultiLine;
        }

        if ((benchmarkCase.Options & RegexOptions.Singleline) != 0)
        {
            result |= PcreOptions.Singleline;
        }

        if ((benchmarkCase.Options & RegexOptions.IgnorePatternWhitespace) != 0)
        {
            result |= PcreOptions.IgnorePatternWhitespace;
        }

        if (benchmarkCase.CompileSettings.AllowDuplicateNames)
        {
            result |= PcreOptions.DupNames;
        }

        return result;
    }

    private static PcreNewLine ToPcreNetNewLine(Pcre2NewlineConvention value) => value switch
    {
        Pcre2NewlineConvention.Default => PcreNewLine.Default,
        Pcre2NewlineConvention.Cr => PcreNewLine.Cr,
        Pcre2NewlineConvention.Lf => PcreNewLine.Lf,
        Pcre2NewlineConvention.Crlf => PcreNewLine.CrLf,
        Pcre2NewlineConvention.Any => PcreNewLine.Any,
        Pcre2NewlineConvention.AnyCrlf => PcreNewLine.AnyCrLf,
        Pcre2NewlineConvention.Nul => PcreNewLine.Nul,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static PcreBackslashR ToPcreNetBackslashR(Pcre2BsrConvention value) => value switch
    {
        Pcre2BsrConvention.Default => PcreBackslashR.Default,
        Pcre2BsrConvention.AnyCrlf => PcreBackslashR.AnyCrLf,
        Pcre2BsrConvention.Unicode => PcreBackslashR.Unicode,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ComputeFingerprintSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string BuildFingerprintFields(params object?[] values) =>
        string.Join('\n', values.Select(static value => Convert.ToString(value, CultureInfo.InvariantCulture)));

    private static uint? CaptureOptionalPatternInfo(Func<uint> capture)
    {
        try
        {
            return capture();
        }
        catch (PcreException exception) when (
            exception.Message.Contains("requested value is not set", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }
}

internal sealed class PcreNetNativeBuildFingerprint
{
    public required string Sha256 { get; init; }

    public required string EngineVersion { get; init; }

    public required string ProcessArchitecture { get; init; }

    public required string OperatingSystemArchitecture { get; init; }

    public bool JitSupported { get; init; }

    public required string JitTarget { get; init; }

    public bool UnicodeSupported { get; init; }

    public required string UnicodeVersion { get; init; }

    public uint CompiledWidths { get; init; }

    public uint LinkSizeBytes { get; init; }

    public uint EffectiveLinkSizeBytes { get; init; }

    public required string DefaultNewline { get; init; }

    public required string DefaultBackslashR { get; init; }

    public uint DefaultHeapLimitKibibytes { get; init; }

    public uint DefaultMatchLimit { get; init; }

    public uint DefaultDepthLimit { get; init; }

    public uint ParenthesesLimit { get; init; }

    public uint CharacterTablesLengthBytes { get; init; }

    public bool BackslashCPermanentlyDisabled { get; init; }
}

internal sealed class PcreNetNativePlanFingerprint
{
    public required string Sha256 { get; init; }

    public ulong PatternSizeBytes { get; init; }

    public ulong FrameSizeBytes { get; init; }

    public ulong JitSizeBytes { get; init; }

    public bool IsJitCompiled { get; init; }

    public int CaptureCount { get; init; }

    public uint NamedGroupsCount { get; init; }

    public uint MaximumBackReference { get; init; }

    public bool CanMatchEmptyString { get; init; }

    public uint MaximumLookbehindCharacters { get; init; }

    public uint MinimumSubjectCharacters { get; init; }

    public uint FirstCodeType { get; init; }

    public uint? FirstCodeUnit { get; init; }

    public uint LastCodeType { get; init; }

    public uint? LastCodeUnit { get; init; }

    public required string ArgumentOptions { get; init; }

    public required string EffectiveOptions { get; init; }

    public required string ExtraOptions { get; init; }

    public uint? PatternMatchLimit { get; init; }

    public uint? PatternDepthLimit { get; init; }

    public uint? PatternHeapLimitKibibytes { get; init; }
}

internal sealed class Pcre2BenchmarkWorkspaceContract
{
    public required string StateHolder { get; init; }

    public required string Lifetime { get; init; }

    public required string ConcurrencyContract { get; init; }

    public required string RetainedMemoryContract { get; init; }

    public long? RetainedNativeHeapHighWaterBytes { get; init; }

    public string? RetainedNativeHeapHighWaterUnavailableReason { get; init; }
}

internal readonly record struct Pcre2ProgressionAudit(
    int CountResult,
    int EnumeratedCount,
    Pcre2BenchmarkResultChecksum EnumerationChecksum);
