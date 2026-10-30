using System.Text;
using Lokad.Utf8Regex.Internal;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFundedProgramTests
{
    private static Utf8PythonRegex Compile(string pattern, Allowance allowance, CancellationToken token = default,
        Utf8ConstructionPolicy construction = default) => Utf8PythonRegex.Compile(pattern, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, construction, Timeout.InfiniteTimeSpan, token, allowance);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProgramStaysFundedUntilExplicitClosure(bool utf8)
    {
        var allowance = new Allowance(16 << 20); Assert.True(allowance.TryReserve(13));
        using var token = new CancellationTokenSource();
        using var regex = utf8 ? Utf8PythonRegex.Compile("(?P<x>é+)"u8, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, token.Token, allowance)
            : Compile("(?P<x>é+)", allowance, token.Token);
        var held = allowance.Used; Assert.True(held > 13); token.Cancel();
        Assert.Equal(1, regex.Count("éé"u8)); Assert.Equal(held, allowance.Used);
        var retained = regex.SearchDetailedData("éé"u8);
        regex.Dispose(); regex.Dispose(); Assert.Equal(13UL, allowance.Used);
        Assert.Equal("éé", retained.Groups[1].ValueText);
        Assert.Throws<ObjectDisposedException>(() => regex.Count("é"u8));
        allowance.Release(13);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataCopyBelongsToCallerAndFundedCloseRejectsNewCopies(bool native)
    {
        const string pattern = "(?P<x>😀)";
        var encoded = Encoding.UTF8.GetBytes(pattern);
        var allowance = new Allowance(16 << 20);
        using var regex = native ? Utf8PythonRegex.Compile(encoded, PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, allowance)
            : Compile(pattern, allowance);
        var held = allowance.Used; var destination = new byte[encoded.Length];
        Assert.Equal(1, regex.CaptureGroupCount);
        Assert.True(regex.TryGetGroupNumber("x"u8, out var number)); Assert.Equal(1, number);
        Assert.Equal(System.Buffers.OperationStatus.Done, regex.TryCopyPatternUtf8(destination, out var required));
        Assert.Equal(encoded.Length, required); Assert.Equal(encoded, destination); Assert.Equal(held, allowance.Used);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
        Assert.Throws<ObjectDisposedException>(() => regex.TryCopyPatternUtf8(destination, out _));
        Assert.Throws<ObjectDisposedException>(() => regex.TryGetGroupNumber("x"u8, out _));
        Assert.Equal(encoded, destination);
    }

    [Fact]
    public void FailedAndCanceledCompilationRefundsOnlyItsOwnReservations()
    {
        var allowance = new Allowance(16 << 20); Assert.True(allowance.TryReserve(13));
        Assert.Throws<PythonRePatternException>(() => Compile("(?P<x>", allowance)); Assert.Equal(13UL, allowance.Used);
        using var token = new CancellationTokenSource();
        allowance.AfterGrant = token.Cancel;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => Compile("(a+)", allowance, token.Token)).CancellationToken);
        Assert.Equal(13UL, allowance.Used); allowance.AfterGrant = null;
        allowance.Maximum = allowance.Used;
        Assert.Throws<Utf8MemoryAdmissionException>(() => Compile("a", allowance)); Assert.Equal(13UL, allowance.Used);
        allowance.Release(13);
    }

    [Fact]
    public void SuppliedAllowanceDoesNotAcquireAnUnrelatedDefaultByteCap()
    {
        var allowance = new Allowance(16 << 20); ulong observed = 0;
        allowance.AfterGrant = () => observed = Utf8ConstructionScope.CurrentBudget.MaximumBytes;
        using (var regex = Compile("a", allowance)) Assert.Equal(ulong.MaxValue, observed);
        using (var regex = Compile("a", allowance, construction: new Utf8ConstructionPolicy { MaxBytes = 1 << 20 }))
            Assert.Equal(1UL << 20, observed);
        Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public async Task ExistingCursorKeepsProgramFundingAcrossClosureAndHostWaits()
    {
        var allowance = new Allowance(16 << 20); using var regex = Compile("(?P<x>.)", allowance);
        var held = allowance.Used;
        using var cursor = regex.CreateMatchCursor("é😀"u8.ToArray()); var alias = cursor;
        var withCursor = allowance.Used; Assert.True(withCursor >= held);
        regex.Dispose(); Assert.Equal(withCursor, allowance.Used);
        Assert.Throws<ObjectDisposedException>(() => regex.CreateMatchCursor("a"u8.ToArray()));
        await Task.Yield();
        Assert.True(alias.MoveNext(out var first)); Assert.Equal("é", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        Assert.True(cursor.MoveNext(out _)); Assert.True(allowance.Used >= withCursor);
        Assert.False(alias.MoveNext(out _)); Assert.Equal(0UL, allowance.Used);
        alias.Dispose(); cursor.Dispose(); Assert.Equal(0UL, allowance.Used);
        Assert.Equal("é", Encoding.UTF8.GetString(first.GetValueMemory().Span));
    }

    [Fact]
    public void CallbackClosureDefersRefundThroughRequiredOutputProcessing()
    {
        var allowance = new Allowance(16 << 20); using var regex = Compile("(a)", allowance);
        var held = allowance.Used;
        var output = regex.SubnToString("aa"u8, regex, (expression, match) =>
        {
            var active = allowance.Used; Assert.True(active >= held);
            expression.Dispose(); Assert.Equal(active, allowance.Used);
            return match.Value.ValueText.ToUpperInvariant();
        });
        Assert.Equal("AA", output.ResultText); Assert.Equal(2, output.ReplacementCount);
        Assert.Equal(0UL, allowance.Used);
    }

    [Theory]
    [InlineData("(?P<x>a)")] [InlineData("a")] [InlineData("[a-z]+")]
    public void ClosedFundedExpressionsRejectEveryPublicOperationFamily(string pattern)
    {
        var allowance = new Allowance(16 << 20); using var regex = Compile(pattern, allowance); regex.Dispose();
        Action[] operations = [
            () => regex.IsMatch("a"u8), () => { _ = regex.Search("a"u8); },
            () => { _ = regex.Match("a"u8); }, () => { _ = regex.FullMatch("a"u8); },
            () => regex.Count("a"u8), () => { _ = regex.SearchDetailed("a"u8); },
            () => { _ = regex.MatchDetailed("a"u8); }, () => { _ = regex.FullMatchDetailed("a"u8); },
            () => regex.SearchDetailedData("a"u8), () => regex.MatchDetailedData("a"u8), () => regex.FullMatchDetailedData("a"u8),
            () => regex.SearchToString("a"u8), () => regex.MatchToString("a"u8), () => regex.FullMatchToString("a"u8),
            () => regex.FindAll("a"u8), () => regex.FindAllToStrings("a"u8), () => regex.FindAllToUtf8("a"u8),
            () => regex.FindIterDetailed("a"u8), () => regex.SplitRanges("a"u8), () => regex.SplitToStrings("a"u8),
            () => regex.SplitDetailed("a"u8), () => regex.Subn("a"u8, "x"), () => regex.SubnToString("a"u8, "x"),
            () => regex.SubnToString("a"u8, 0, (_, _) => "x"), () => regex.Subn("a"u8, 0, (_, _) => "x"),
            () => regex.CreateMatchCursor("a"u8.ToArray()),
        ];
        foreach (var operation in operations) Assert.Throws<ObjectDisposedException>(operation);
        Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void DenialAtDifferentConstructionStagesRestoresTheSharedBalance()
    {
        var denied = 0;
        for (ulong capacity = 128; capacity <= 1 << 20; capacity *= 2)
        {
            var allowance = new Allowance(capacity); Assert.True(allowance.TryReserve(13));
            try { using var regex = Compile("(?P<word>[a-z]+)|(?P<number>[0-9]+)", allowance); }
            catch (Utf8MemoryAdmissionException) { denied++; }
            Assert.Equal(13UL, allowance.Used); allowance.Release(13);
        }
        Assert.True(denied > 1);
        var malformed = new Allowance(1 << 20);
        Assert.Throws<ArgumentException>(() => Utf8PythonRegex.Compile(new byte[] { 0xff }, default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, malformed));
        Assert.Equal(0UL, malformed.Used);
    }

    [Fact]
    public async Task ConcurrentProgramsReturnTheirOwnFunding()
    {
        var allowance = new Allowance(16 << 20);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 20; i++)
            {
                using var regex = Compile("(?P<x>a+)", allowance);
                Assert.Equal(1, regex.Count("aaa"u8));
            }
        })));
        Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void RepeatedPatternsDoNotAccumulateHistoricalFunding()
    {
        var allowance = new Allowance(16 << 20);
        for (var i = 0; i < 100; i++)
        {
            using (var regex = Compile("(?P<x>a{" + (i % 8 + 1) + "})", allowance)) Assert.True(allowance.Used > 0);
            Assert.Equal(0UL, allowance.Used);
        }
        using var ordinary = new Utf8PythonRegex("a"); ordinary.Dispose(); Assert.Equal(1, ordinary.Count("a"u8));
    }
}
