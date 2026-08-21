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

    internal int Execute(Utf8Pcre2BenchmarkOperation operation) => operation switch
    {
        Utf8Pcre2BenchmarkOperation.IsMatch => _matchBuffer.IsMatch(_input) ? 1 : 0,
        Utf8Pcre2BenchmarkOperation.Count => CountMatches(),
        Utf8Pcre2BenchmarkOperation.EnumerateMatches => SumMatchIndexes(int.MaxValue),
        Utf8Pcre2BenchmarkOperation.MatchMany => SumMatchIndexes(8),
        _ => throw new NotSupportedException(
            $"The PCRE.NET UTF-8 span baseline does not expose equivalent work for {operation}."),
    };

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

    private int CountMatches()
    {
        var count = 0;
        foreach (var _ in _matchBuffer.Matches(_input))
        {
            count++;
        }

        return count;
    }

    private int SumMatchIndexes(int limit)
    {
        var count = 0;
        var sum = 0;
        foreach (var match in _matchBuffer.Matches(_input))
        {
            if (count++ == limit)
            {
                break;
            }

            sum += match.Index;
        }

        return sum;
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
}
