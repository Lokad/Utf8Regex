using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReUnicodeBoundaryTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Fact]
    public void UnicodeBoundaryKeepsThePinnedPredicateWithoutLookaroundPrograms()
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser(@"\b").Parse(PythonReCompileOptions.None));
        Assert.Equal(2, program.Instructions.Length);
        Assert.Equal(Utf8SliceInstructionKind.Boundary, program.Instructions[0].Kind);
        Assert.True(program.Instructions[0].C > 0);
        Assert.True(program.HasUnicodePatternAtoms);
        Assert.True(program.RequiresUnicodeWordBoundary);
        Assert.Empty(program.Assertions);
        var table = Assert.Single(Assert.Single(program.CategorySetMembers)).Table;
        Assert.Equal(((Func<int, bool>)PythonReScalarClassData.IsWord).Method, table.ScalarPredicate!.Method);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PrimitiveAndExpandedReferenceAgreeAtEveryScalarOffset(bool nonBoundary)
    {
        var escape = nonBoundary ? @"\B" : @"\b";
        // These user-authored assertions keep the old pinned \w lowering;
        // no parser-owned boundary marker occurs in the independent reference.
        var expanded = nonBoundary ? @"(?!\A\Z)(?:(?<=\w)(?=\w)|(?<!\w)(?!\w))"
            : @"(?:(?<!\w)(?=\w)|(?<=\w)(?!\w))";
        string[] subjects = ["", "a", " ", "aa bb", "_0", "éA😀Bıİ", "e\u0301", "²Ⅻ𝟙", "𐐀😀β",
            "\u200c\u200da", "\u203fa", "\u1c89a", "A\n", "\r\n", "\0x_", "😀 é x"];
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { PythonReCompileOptions.None, PythonReCompileOptions.IgnoreCase, PythonReCompileOptions.Ascii })
        {
            var actual = new Utf8PythonRegex("(?u:" + escape + ")", options, Regex.InfiniteMatchTimeout, Funded);
            var control = new Utf8PythonRegex("(?u:" + expanded + ")", options, Regex.InfiniteMatchTimeout, Funded);
            foreach (var text in subjects)
            {
                var input = Encoding.UTF8.GetBytes(text);
                for (var offset = 0;;)
                {
                    Assert.Equal(control.FindIterDetailed(input, offset, token.Token).Select(x => x.Value),
                        actual.FindIterDetailed(input, offset, token.Token).Select(x => x.Value));
                    Assert.Equal(control.Count(input, offset, token.Token), actual.Count(input, offset, token.Token));
                    Assert.Equal(control.IsMatch(input, offset, token.Token), actual.IsMatch(input, offset, token.Token));
                    AssertRange(control.Search(input, offset), actual.Search(input, offset));
                    AssertRange(control.Match(input, offset), actual.Match(input, offset));
                    AssertRange(control.FullMatch(input, offset), actual.FullMatch(input, offset));
                    foreach (var limit in new[] { 0, 1, 2 })
                        Assert.Equal(control.SplitToStrings(input, limit, offset, token.Token), actual.SplitToStrings(input, limit, offset, token.Token));
                    Assert.Equal(control.SubnToString(input, "-", 0, offset, token.Token).ResultText,
                        actual.SubnToString(input, "-", 0, offset, token.Token).ResultText);
                    if (offset == input.Length) break;
                    Rune.DecodeFromUtf8(input.AsSpan(offset), out _, out var width); offset += width;
                }
            }
        }
    }

    private static void AssertRange(Utf8PythonValueMatch expected, Utf8PythonValueMatch actual)
        => Assert.Equal((expected.Success, expected.StartOffsetInBytes, expected.EndOffsetInBytes,
                expected.StartOffsetInUtf16, expected.EndOffsetInUtf16, expected.GetValueString()),
            (actual.Success, actual.StartOffsetInBytes, actual.EndOffsetInBytes,
                actual.StartOffsetInUtf16, actual.EndOffsetInUtf16, actual.GetValueString()));

    [Fact]
    public void PinnedMembershipRetainsNumericMarksJoinersAndNewerUnicodeDistinctions()
    {
        var unicode = new Utf8PythonRegex(@"\b");
        foreach (var scalar in new[] { "²", "Ⅻ", "𝟙", "é", "İ", "𐐀" }) Assert.Equal(2, unicode.Count(Encoding.UTF8.GetBytes(scalar)));
        foreach (var scalar in new[] { "\u0301", "\u200c", "\u200d", "\u203f", "\u1c89", "😀" }) Assert.Equal(0, unicode.Count(Encoding.UTF8.GetBytes(scalar)));
        Assert.False(PythonReScalarClassData.IsWord(0x1c89)); // Assigned after the pinned Unicode version.
        Assert.Equal(2, unicode.Count("\u1c89a"u8));
        Assert.Equal(0, new Utf8PythonRegex(@"(?a:\b)").Count("²Ⅻé"u8));
        Assert.Equal(0, new Utf8PythonRegex(@"\B").Count([]));
    }

    [Fact]
    public void CapturesEmptyAlternativesAndCursorAliasesStayOwned()
    {
        var regex = new Utf8PythonRegex(@"(?P<b>\b)|(?P<a>a)");
        var control = new Utf8PythonRegex(@"(?P<b>(?:(?<!\w)(?=\w)|(?<=\w)(?!\w)))|(?P<a>a)");
        var input = "a éa😀a"u8.ToArray();
        Assert.Equal(control.FindIterDetailed(input).Select(x => x.Groups), regex.FindIterDetailed(input).Select(x => x.Groups));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext());
        var alias = cursor; var retained = cursor.Current; var groups = retained.Groups;
        Assert.True(alias.MoveNext()); Assert.Equal(alias.Current, cursor.Current);
        while (cursor.MoveNext()) { }
        cursor.Dispose(); Assert.Equal(groups, retained.Groups);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionAppliesAfterValidation(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string('a', 4096) + "é");
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8Validation.Validate(input, ref validation);
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8PythonRegex(@"\b", PythonReCompileOptions.None, timeout, Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
            entered = true; if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.SplitToStrings(input, 0, 0, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.SplitToStrings(input, 0, 0, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void OutputAdmissionAndStrictUtf8StillApply()
    {
        foreach (var policy in new[] { Funded with { MaxResultBytes = 1 }, Funded with { MaxWorkingBytes = 1 }, Funded with { MaxWorkSteps = 1 } })
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8PythonRegex(@"\b", PythonReCompileOptions.None,
                Regex.InfiniteMatchTimeout, policy).SplitToStrings("é alpha"u8));
        var regex = new Utf8PythonRegex(@"\b"); byte[] malformed = [.. "é alpha"u8, 0xff];
        Assert.Throws<ArgumentException>(() => regex.SplitToStrings(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.SplitToStrings(malformed, 0, 0, token.Token)).CancellationToken);
    }
}
