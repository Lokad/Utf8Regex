using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8AsciiCultureInvariantStrategy
{
    private readonly string _pattern;
    private readonly TimeSpan _matchTimeout;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8CompiledEngineRuntime _runtime;
    private readonly string[] _groupNames;

    public Utf8AsciiCultureInvariantStrategy(
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout,
        string[] groupNames)
    {
        _pattern = pattern;
        _matchTimeout = matchTimeout;
        var strategyOptions = options | RegexOptions.CultureInvariant;
        PreparedRegex = Utf8FrontEnd.Compile(
            pattern,
            Utf8RegexSyntax.NormalizeNonSemanticOptions(strategyOptions));
        CompiledEngine = Utf8CompiledEngineSelector.Select(
            PreparedRegex,
            (options & RegexOptions.Compiled) != 0);
        _verifierRuntime = Utf8VerifierRuntime.Create(
            PreparedRegex,
            pattern,
            strategyOptions,
            matchTimeout);
        _runtime = Utf8CompiledEngineRuntime.Create(
            CompiledEngine,
            PreparedRegex,
            _verifierRuntime,
            strategyOptions);
        _groupNames = groupNames;
    }

    public Utf8PreparedRegex PreparedRegex { get; }

    public Utf8CompiledEngine CompiledEngine { get; }

    public NativeExecutionKind ExecutionKind => PreparedRegex.ExecutionKind;

    public Utf8CompiledEngineKind CompiledEngineKind => CompiledEngine.Kind;

    public string? FallbackReason => PreparedRegex.FallbackReason;

    public bool DebugTryMatchWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match) =>
        TryMatchDirectWithoutValidation(input, out match);

    public bool TryMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match) =>
        _runtime.TryMatchWithoutValidation(input, budget: Utf8ExecutionDeadline.Infinite, out match);

    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return _runtime.IsMatch(input, default, CreateBudget());
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return _runtime.Count(input, default, CreateBudget());
    }

    public Utf8ValueMatch Match(ReadOnlySpan<byte> input)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return _runtime.Match(input, default, CreateBudget());
    }

    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var decoded = subject.GetDecodedString();
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        return new Utf8MatchContext(input, decoded, match, subject.BoundaryMap, _groupNames);
    }

    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(
            input,
            subject.GetDecodedString(),
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            subject.Utf16ProjectionMap);
    }

    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(
            input,
            subject.GetDecodedString(),
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            count,
            subject.BoundaryMap);
    }

    internal Utf8OperationMatchCursor CreateMatchCursor(ReadOnlySpan<byte> input)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            PreparedRegex,
            _verifierRuntime,
            input,
            default,
            CreateBudget());
    }

    internal Regex FallbackRegex => _verifierRuntime.FallbackCandidateVerifier.FallbackRegex;

    private Utf8ExecutionDeadline CreateBudget() => Utf8ExecutionDeadline.Start(_matchTimeout);
}
