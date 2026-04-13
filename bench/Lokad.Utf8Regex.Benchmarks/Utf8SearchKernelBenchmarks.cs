using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8SearchKernelBenchmarks
{
    private ReadOnlyMemory<byte> _literalInput;
    private ReadOnlyMemory<byte> _ignoreCaseInput;
    private ReadOnlyMemory<byte> _longIgnoreCaseInput;
    private ReadOnlyMemory<byte> _alternationInput;
    private ReadOnlyMemory<byte> _sharedPrefixAlternationInput;
    private ReadOnlyMemory<byte> _sharedFirstByteAlternationInput;
    private ReadOnlyMemory<byte> _mixedBucketAlternationInput;
    private ReadOnlyMemory<byte> _largeAlternationInput;
    private byte[] _literal = null!;
    private byte[] _ignoreCaseLiteral = null!;
    private byte[] _longIgnoreCaseLiteral = null!;
    private PreparedLiteralSetSearch _alternationSearch;
    private PreparedLiteralSetSearch _sharedPrefixAlternationSearch;
    private PreparedLiteralSetSearch _sharedFirstByteAlternationSearch;
    private PreparedLiteralSetSearch _mixedBucketAlternationSearch;
    private PreparedLiteralSetSearch _largeAlternationSearch;
    private PreparedMultiLiteralSearch _multiLiteralAlternationSearch;
    private PreparedMultiLiteralSearch _ignoreCaseMultiLiteralSearch;
    private PreparedMultiLiteralSearch _largeMultiLiteralAlternationSearch;
    private ReadOnlyMemory<byte> _denseEnumerateInput;
    private Utf8StructuralLinearProgram _denseEnumerateProgram;
    private ReadOnlyMemory<byte> _validationAsciiLarge;
    private ReadOnlyMemory<byte> _validationTwoByteLarge;
    private ReadOnlyMemory<byte> _validationThreeByteLarge;
    private ReadOnlyMemory<byte> _validationMixedLarge;

    [GlobalSetup]
    public void Setup()
    {
        _literal = Encoding.UTF8.GetBytes("needle");
        _ignoreCaseLiteral = Encoding.UTF8.GetBytes("Needle");
        _longIgnoreCaseLiteral = Encoding.UTF8.GetBytes("NeedleAlphaBetaGammaDeltaSigma");

        _literalInput = Encoding.UTF8.GetBytes(BuildInput("needle"));
        _ignoreCaseInput = Encoding.UTF8.GetBytes(BuildInput("nEeDlE"));
        _longIgnoreCaseInput = Encoding.UTF8.GetBytes(BuildInput("nEeDlEaLpHaBeTaGaMmAdElTaSiGmA"));
        _alternationInput = Encoding.UTF8.GetBytes(BuildAlternationInput());
        _sharedPrefixAlternationInput = Encoding.UTF8.GetBytes(BuildSharedPrefixAlternationInput());
        _sharedFirstByteAlternationInput = Encoding.UTF8.GetBytes(BuildSharedFirstByteAlternationInput());
        _mixedBucketAlternationInput = Encoding.UTF8.GetBytes(BuildMixedBucketAlternationInput());
        _largeAlternationInput = Encoding.UTF8.GetBytes(BuildLargeAlternationInput());
        _alternationSearch = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("alpha"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("omega"),
            Encoding.UTF8.GetBytes("zeta"),
        ]);
        _sharedPrefixAlternationSearch = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("count"),
            Encoding.UTF8.GetBytes("cover"),
            Encoding.UTF8.GetBytes("cobalt"),
            Encoding.UTF8.GetBytes("coding"),
        ]);
        _sharedFirstByteAlternationSearch = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("amber"),
            Encoding.UTF8.GetBytes("atlas"),
            Encoding.UTF8.GetBytes("axiom"),
            Encoding.UTF8.GetBytes("adore"),
        ]);
        _mixedBucketAlternationSearch = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("amber"),
            Encoding.UTF8.GetBytes("atlas"),
            Encoding.UTF8.GetBytes("axiom"),
            Encoding.UTF8.GetBytes("adore"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("omega"),
        ]);
        _largeAlternationSearch = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("abacus"),
            Encoding.UTF8.GetBytes("absorb"),
            Encoding.UTF8.GetBytes("accord"),
            Encoding.UTF8.GetBytes("acumen"),
            Encoding.UTF8.GetBytes("anchor"),
            Encoding.UTF8.GetBytes("anthem"),
            Encoding.UTF8.GetBytes("aspire"),
            Encoding.UTF8.GetBytes("aviate"),
            Encoding.UTF8.GetBytes("beacon"),
            Encoding.UTF8.GetBytes("binary"),
            Encoding.UTF8.GetBytes("bronze"),
            Encoding.UTF8.GetBytes("candid"),
            Encoding.UTF8.GetBytes("cobble"),
            Encoding.UTF8.GetBytes("cortex"),
            Encoding.UTF8.GetBytes("dynamo"),
            Encoding.UTF8.GetBytes("needle"),
        ]);
        _multiLiteralAlternationSearch = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("alpha"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("omega"),
            Encoding.UTF8.GetBytes("zeta"),
        ], ignoreCase: false);
        _ignoreCaseMultiLiteralSearch = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("Alpha"),
            Encoding.UTF8.GetBytes("Needle"),
            Encoding.UTF8.GetBytes("Omega"),
            Encoding.UTF8.GetBytes("Zeta"),
        ], ignoreCase: true);
        _largeMultiLiteralAlternationSearch = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("abacus"),
            Encoding.UTF8.GetBytes("absorb"),
            Encoding.UTF8.GetBytes("accord"),
            Encoding.UTF8.GetBytes("acumen"),
            Encoding.UTF8.GetBytes("anchor"),
            Encoding.UTF8.GetBytes("anthem"),
            Encoding.UTF8.GetBytes("aspire"),
            Encoding.UTF8.GetBytes("aviate"),
            Encoding.UTF8.GetBytes("beacon"),
            Encoding.UTF8.GetBytes("binary"),
            Encoding.UTF8.GetBytes("bronze"),
            Encoding.UTF8.GetBytes("candid"),
            Encoding.UTF8.GetBytes("cobble"),
            Encoding.UTF8.GetBytes("cortex"),
            Encoding.UTF8.GetBytes("dynamo"),
            Encoding.UTF8.GetBytes("needle"),
        ], ignoreCase: false);
        _denseEnumerateInput = Encoding.UTF8.GetBytes(BuildRepeated("ab1d-", 4096));
        _denseEnumerateProgram = Utf8FrontEnd.Analyze("ab[0-9]d", RegexOptions.CultureInvariant).RegexPlan.StructuralLinearProgram;
        _validationAsciiLarge = Utf8ValidationBenchmarkProfiles.Create("ascii-large");
        _validationTwoByteLarge = Utf8ValidationBenchmarkProfiles.Create("two-byte-large");
        _validationThreeByteLarge = Utf8ValidationBenchmarkProfiles.Create("three-byte-large");
        _validationMixedLarge = Utf8ValidationBenchmarkProfiles.Create("mixed-large");
    }

    [Benchmark(Baseline = true)]
    public int ExactLiteral()
    {
        return Utf8SearchKernel.IndexOfLiteral(_literalInput.Span, _literal, ignoreCase: false);
    }

    [Benchmark]
    public int IgnoreCaseLiteral()
    {
        return Utf8SearchKernel.IndexOfLiteral(_ignoreCaseInput.Span, _ignoreCaseLiteral, ignoreCase: true);
    }

    [Benchmark]
    public int IgnoreCaseLongLiteral()
    {
        return Utf8SearchKernel.IndexOfLiteral(_longIgnoreCaseInput.Span, _longIgnoreCaseLiteral, ignoreCase: true);
    }

    [Benchmark]
    public int ExactLiteralAlternation()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_alternationInput.Span, _alternationSearch);
    }

    [Benchmark]
    public int ExactLiteralAlternationSharedPrefix()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_sharedPrefixAlternationInput.Span, _sharedPrefixAlternationSearch);
    }

    [Benchmark]
    public int ExactLiteralAlternationSharedFirstByte()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_sharedFirstByteAlternationInput.Span, _sharedFirstByteAlternationSearch);
    }

    [Benchmark]
    public int ExactLiteralAlternationMixedBuckets()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_mixedBucketAlternationInput.Span, _mixedBucketAlternationSearch);
    }

    [Benchmark]
    public int ExactLiteralAlternationLargeSet()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_largeAlternationInput.Span, _largeAlternationSearch);
    }

    [Benchmark]
    public int PreparedMultiLiteralAlternation()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_alternationInput.Span, _multiLiteralAlternationSearch);
    }

    [Benchmark]
    public int PreparedMultiLiteralIgnoreCaseAlternation()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_ignoreCaseInput.Span, _ignoreCaseMultiLiteralSearch);
    }

    [Benchmark]
    public int PreparedMultiLiteralLargeSet()
    {
        return Utf8SearchKernel.IndexOfAnyLiteral(_largeAlternationInput.Span, _largeMultiLiteralAlternationSearch);
    }

    [Benchmark]
    public int PreparedMultiLiteralLargeSetCountRestarting()
    {
        var input = _largeAlternationInput.Span;
        var count = 0;
        var start = 0;
        while ((uint)start <= (uint)input.Length &&
               _largeMultiLiteralAlternationSearch.TryFindFirstMatch(input[start..], out var index, out var matchedLength, out _))
        {
            count++;
            start += index + matchedLength;
        }

        return count;
    }

    [Benchmark]
    public int PreparedMultiLiteralLargeSetCountStateful()
    {
        var input = _largeAlternationInput.Span;
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (_largeMultiLiteralAlternationSearch.TryFindNextNonOverlappingMatch(input, ref state, out _, out _, out _))
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int StructuralLinearDenseDeterministicScanOnly()
    {
        var input = _denseEnumerateInput.Span;
        var sum = 0;
        var state = new Utf8AsciiDeterministicScanState(0, _denseEnumerateProgram.DeterministicProgram.SearchLiteralOffset);
        while (Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
            _denseEnumerateProgram,
            input,
            ref state,
            budget: null,
            out var match))
        {
            sum += match.Index;
        }

        return sum;
    }

    [Benchmark]
    public int ThrowIfInvalidOnlyAsciiLarge()
    {
        Utf8Validation.ThrowIfInvalidOnly(_validationAsciiLarge.Span);
        return _validationAsciiLarge.Length;
    }

    [Benchmark]
    public int ThrowIfInvalidOnlyTwoByteLarge()
    {
        Utf8Validation.ThrowIfInvalidOnly(_validationTwoByteLarge.Span);
        return _validationTwoByteLarge.Length;
    }

    [Benchmark]
    public int ThrowIfInvalidOnlyThreeByteLarge()
    {
        Utf8Validation.ThrowIfInvalidOnly(_validationThreeByteLarge.Span);
        return _validationThreeByteLarge.Length;
    }

    [Benchmark]
    public int ThrowIfInvalidOnlyMixedLarge()
    {
        Utf8Validation.ThrowIfInvalidOnly(_validationMixedLarge.Span);
        return _validationMixedLarge.Length;
    }

    [Benchmark]
    public int ValidateOnlyAsciiLarge()
    {
        return Utf8Validation.Validate(_validationAsciiLarge.Span).Utf16Length;
    }

    [Benchmark]
    public int ValidateOnlyTwoByteLarge()
    {
        return Utf8Validation.Validate(_validationTwoByteLarge.Span).Utf16Length;
    }

    [Benchmark]
    public int ValidateOnlyThreeByteLarge()
    {
        return Utf8Validation.Validate(_validationThreeByteLarge.Span).Utf16Length;
    }

    [Benchmark]
    public int ValidateOnlyMixedLarge()
    {
        return Utf8Validation.Validate(_validationMixedLarge.Span).Utf16Length;
    }

    private static string BuildInput(string needle)
    {
        var sb = new StringBuilder(64 * 1024);
        for (var i = 0; i < 2048; i++)
        {
            sb.Append("prefix-");
            sb.Append(i % 17 == 0 ? needle : "haystack");
            sb.Append("-suffix|");
        }

        return sb.ToString();
    }

    private static string BuildAlternationInput()
    {
        var sb = new StringBuilder(64 * 1024);
        for (var i = 0; i < 2048; i++)
        {
            sb.Append("scan:");
            sb.Append(i % 31 == 0 ? "needle" : i % 23 == 0 ? "alpha" : "haystack");
            sb.Append(';');
        }

        return sb.ToString();
    }

    private static string BuildRepeated(string token, int count)
    {
        var sb = new StringBuilder(token.Length * count);
        for (var i = 0; i < count; i++)
        {
            sb.Append(token);
        }

        return sb.ToString();
    }

    private static string BuildSharedPrefixAlternationInput()
    {
        var sb = new StringBuilder(64 * 1024);
        for (var i = 0; i < 2048; i++)
        {
            sb.Append("scan:");
            sb.Append(i % 37 == 0 ? "coding" : i % 29 == 0 ? "count" : "cxxxxx");
            sb.Append(';');
        }

        return sb.ToString();
    }

    private static string BuildSharedFirstByteAlternationInput()
    {
        var sb = new StringBuilder(64 * 1024);
        for (var i = 0; i < 2048; i++)
        {
            sb.Append("scan:");
            sb.Append(i % 41 == 0 ? "axiom" : i % 31 == 0 ? "amber" : "aqqqq");
            sb.Append(';');
        }

        return sb.ToString();
    }

    private static string BuildMixedBucketAlternationInput()
    {
        var sb = new StringBuilder(64 * 1024);
        for (var i = 0; i < 2048; i++)
        {
            sb.Append("scan:");
            sb.Append(i % 43 == 0 ? "needle" : i % 37 == 0 ? "amber" : "aqqqq");
            sb.Append(';');
        }

        return sb.ToString();
    }

    private static string BuildLargeAlternationInput()
    {
        var sb = new StringBuilder(64 * 1024);
        for (var i = 0; i < 2048; i++)
        {
            sb.Append("scan:");
            sb.Append(i % 47 == 0 ? "needle" : i % 31 == 0 ? "dynamo" : i % 19 == 0 ? "beacon" : "haystack");
            sb.Append(';');
        }

        return sb.ToString();
    }
}
