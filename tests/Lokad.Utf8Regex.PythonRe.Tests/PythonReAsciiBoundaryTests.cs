using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAsciiBoundaryTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Fact]
    public void AsciiBoundaryLowersToTheOwnedPrimitiveWithoutLookaroundState()
    {
        foreach (var options in new[] { PythonReCompileOptions.Ascii, PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase })
        {
            var parse = new PythonReParser(@"\b").Parse(options);
            var program = PythonReScalarCompiler.Compile(parse);
            Assert.Equal(2, program.Instructions.Length);
            Assert.Equal(Utf8SliceInstructionKind.Boundary, program.Instructions[0].Kind);
            Assert.Equal((int)Utf8SliceWordKind.Ascii, program.Instructions[0].B);
            Assert.Empty(program.Assertions);
            Assert.Equal(0, program.RepeatCount);
            Assert.Equal(0, program.GroupCount);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PrimitiveAndExpandedReferenceAgreeAtEveryScalarOffset(bool nonBoundary)
    {
        var escape = nonBoundary ? @"\B" : @"\b";
        // User-authored lookarounds exercise the old scalar lowering independently
        // of the parser-owned escape marker. Python \Z is its strict end anchor.
        var expanded = nonBoundary ? PythonReParser.AsciiNonWordBoundary.Replace(@"\z", @"\Z") : PythonReParser.AsciiWordBoundary;
        string[] subjects = ["", "a", " ", "aa bb", "_0", "éA😀Bıİ", "e\u0301", "İıſK𝟙", "A\n", "\r\n", "\0x_", "😀 é x"];
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { PythonReCompileOptions.Ascii, PythonReCompileOptions.Ascii | PythonReCompileOptions.IgnoreCase })
        foreach (var scoped in new[] { false, true })
        {
            var pattern = scoped ? "(?a:" + escape + ")" : escape;
            var controlPattern = scoped ? "(?a:" + expanded + ")" : expanded;
            var actual = new Utf8PythonRegex(pattern, options, Regex.InfiniteMatchTimeout, Funded);
            var control = new Utf8PythonRegex(controlPattern, options, Regex.InfiniteMatchTimeout, Funded);
            foreach (var text in subjects)
            {
                var input = Encoding.UTF8.GetBytes(text);
                var offset = 0;
                while (true)
                {
                    var expected = control.FindIterDetailed(input, offset, token.Token);
                    var found = actual.FindIterDetailed(input, offset, token.Token);
                    Assert.Equal(expected.Select(x => x.Value), found.Select(x => x.Value));
                    Assert.Equal(expected.Length, actual.Count(input, offset, token.Token));
                    Assert.Equal(control.IsMatch(input, offset, token.Token), actual.IsMatch(input, offset, token.Token));
                    foreach (var limit in new[] { 0, 1, 2 })
                        Assert.Equal(control.SplitToStrings(input, limit, offset, token.Token), actual.SplitToStrings(input, limit, offset, token.Token));
                    Assert.Equal(control.SubnToString(input, "-", 0, offset, token.Token).ResultText,
                        actual.SubnToString(input, "-", 0, offset, token.Token).ResultText);
                    if (offset == input.Length) break;
                    Rune.DecodeFromUtf8(input.AsSpan(offset), out _, out var width);
                    offset += width;
                }
            }
        }
    }

    [Fact]
    public void StrictAsciiMembershipKeepsTurkishIAndUnicodeScopesDistinct()
    {
        var ascii = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii);
        Assert.Equal(0, ascii.Count("İıé😀"u8));
        Assert.Equal(2, ascii.Count("İAı"u8));
        Assert.Equal(new string?[] { "İ", "A", "ı" }, ascii.SplitToStrings("İAı"u8));
        var unicode = new Utf8PythonRegex(@"\b");
        Assert.Equal(2, unicode.Count("İıé😀"u8));
        Assert.Equal(2, new Utf8PythonRegex(@"(?u:\b)", PythonReCompileOptions.Ascii).Count("İıé😀"u8));
        Assert.Equal(0, new Utf8PythonRegex(@"(?a:\b)").Count("İıé😀"u8));
        Assert.Equal(0, new Utf8PythonRegex(@"\B", PythonReCompileOptions.Ascii).Count([]));
        Assert.Equal(new string?[] { "" }, new Utf8PythonRegex(@"\B", PythonReCompileOptions.Ascii).SplitToStrings([]));
    }

    [Fact]
    public void CapturesEmptyAlternativesAndCursorAliasesRemainOwned()
    {
        var regex = new Utf8PythonRegex(@"(?P<b>\b)|(?P<a>a)", PythonReCompileOptions.Ascii);
        var control = new Utf8PythonRegex("(?P<b>" + PythonReParser.AsciiWordBoundary + ")|(?P<a>a)", PythonReCompileOptions.Ascii);
        var input = "a éa😀a"u8.ToArray();
        Assert.Equal(control.FindIterDetailed(input).Select(x => x.Groups), regex.FindIterDetailed(input).Select(x => x.Groups));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext());
        var alias = cursor;
        var retained = cursor.Current;
        var groups = retained.Groups;
        Assert.True(alias.MoveNext());
        Assert.Equal(alias.Current, cursor.Current);
        while (cursor.MoveNext()) { }
        Assert.Equal(groups, retained.Groups);
        Assert.False(alias.MoveNext());
        cursor.Dispose();
        Assert.Equal(groups, retained.Groups);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionAppliesAfterWholeSubjectValidation(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string(' ', 320000) + "a");
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8Validation.Validate(input, ref validation);
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii, timeout, Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
            entered = true;
            if (expire) ticks += 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.SplitToStrings(input, 0, 0, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.SplitToStrings(input, 0, 0, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.Equal(new string?[] { "", "a", "" }, regex.SplitToStrings("a"u8));
    }

    [Fact]
    public void OutputAdmissionAndWholeInputValidityStillApply()
    {
        foreach (var policy in new[] { Funded with { MaxResultBytes = 1 }, Funded with { MaxWorkingBytes = 1 }, Funded with { MaxWorkSteps = 1 } })
        {
            var regex = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii, Regex.InfiniteMatchTimeout, policy);
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.SplitToStrings("alpha beta"u8));
        }
        var normal = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii);
        byte[] malformed = [.. "alpha beta"u8, 0xff];
        Assert.Throws<ArgumentException>(() => normal.SplitToStrings(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => normal.SplitToStrings(malformed, 0, 0, token.Token)).CancellationToken);
    }
}
