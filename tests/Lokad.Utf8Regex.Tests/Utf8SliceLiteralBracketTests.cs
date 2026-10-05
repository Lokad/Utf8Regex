using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceLiteralBracketTests
{
    public static IEnumerable<object[]> Cases()
    {
        (string Pattern, string Witness)[] rows = [
            (@"]", "]"), (@"a]+", "a]]"), (@"(]){2}", "]]"),
            (@"[[:alpha:]]", "a]"), (@"[[:digit:]]+", "i]]"),
            (@"[[.a.]]", ".]"), (@"[[=a=]]", "=]"),
            (@"[^[:alpha:]]", "b]"), (@"[q[:alpha:]]", "q]"),
            (@"[[:alpha:]", ":"), (@"[[.a.]", "."), (@"[[=a=]", "="),
            (@"[[:unknown:]]", "u]"), (@"[][:alpha:]]", "]]"),
            (@"[[:]]", "[]"), (@"([[:alpha:]])+", "a]p]"),
            (@"(?<x>[[=a=]])\k<x>", "=]=]"),
            (@"(?<12>[[.a.]])\k<12>", ".].]"),
            (@"(?=[[:alpha:]])([[:alpha:]])", "a]"),
            (@"(?<=[[:alpha:]])b", "a]b"),
            (@"(?<![[.a.]])b", ".]b"),
            (@"(?:(?<x>[[:alpha:]])|b)(?(x)]|c)", "a]]"),
            (@"[a-z-[:alpha:]]", "bc"),
            (@"[a-z-[[.a.]]]", "bc]"),
            (@"[[:alpha:]-[a]]", "a-a]"),
            (@"\A[^[:alpha:]]+?\z", "b]]"),
            (@"[\[:alpha:]]", "a]"), (@"[[]", "["),
            (@"[[-a]", "[_a"), (@"[[]:", "[:"),
            (@"[a-[[.a.]]x]", "ax]"), (@"[a-[[=a=]]", "a"),
        ];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var fold in new[] { false, true })
            yield return [row.Pattern, row.Witness, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                (fold ? RegexOptions.IgnoreCase : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void LiteralBracketsRetainCompleteOwnedOperations(string pattern, string witness, RegexOptions options)
    {
        string[] texts = ["", witness, "q" + witness + "z", witness + witness,
            "a]p]:].]=]q]b] A]", "1ab_3Cd!", "\u00e9]a\U0001f600]", "\U0001f600]\U0001f600b]"];
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, texts, options);
    }

    [Theory]
    [InlineData(@"[[:alpha:]]", true)]
    [InlineData(@"[[.a.]]", true)]
    [InlineData(@"[[=a=]]", true)]
    [InlineData(@"[q[:alpha:]]", true)]
    [InlineData(@"(?=[[:alpha:]])a", true)]
    [InlineData(@"(?<=[[:alpha:]])a", true)]
    [InlineData(@"[a-z-[[=a=]]]", true)]
    [InlineData(@"[\[:alpha:]]", false)]
    [InlineData(@"\[\[:alpha:\]\]", false)]
    [InlineData(@"[[]:", false)]
    public void OnlyParsedUnescapedClassMembersCarryTheProfileFact(string pattern, bool expected)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Equal(expected, program.UsesLiteralClassBracketSyntax);
    }

    [Theory]
    [InlineData(@"[x-[]")]
    [InlineData(@"[[:alpha:")]
    [InlineData(@"[a-[[.a.]")]
    [InlineData(@"[a-[[=a=]")]
    public void InvalidClassSyntaxKeepsExactConstructionErrors(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, ["a]"], options);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeReverseStringCallbacksObserveVmCancellationAndRecover(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("]", options, Regex.InfiniteMatchTimeout);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram);
        var input = Encoding.UTF8.GetBytes(new string(']', 4096));
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var failure = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString(input, 0,
                static (in Utf8MatchContext match, ref int calls) => { calls++; return "."; }, source.Token));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        finally { session.Complete(); }
        Assert.Equal(new string('.', 4096), regex.ReplaceToString(input, 0,
            static (in Utf8MatchContext match, ref int calls) => { calls++; return "."; }, CancellationToken.None));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
