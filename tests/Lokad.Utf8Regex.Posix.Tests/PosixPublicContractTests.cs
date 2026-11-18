using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Tests.Common;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixPublicContractTests
{
    [Fact]
    public void NullableConstructorDefaultInheritsTimeoutButTypedZeroIsRejected()
    {
        var inherited = new Utf8PosixRegex("a", options: PosixCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = default
        });
        Assert.Equal(Timeout.InfiniteTimeSpan, inherited.MatchTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a", options: PosixCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = default(TimeSpan)
        }));
    }

    [Fact]
    public void PublicApiMatchesReviewedSnapshot()
        => PublicApiSnapshot.AssertMatches(typeof(Utf8PosixRegex).Assembly, "Lokad.Utf8Regex.Posix.Tests");
    [Fact]
    public void AdapterReferencesOnlyCoreAndBclWithoutAnExternalMatcher()
        => AssemblyDependencyInventory.AssertNoExternalMatchers(typeof(Utf8PosixRegex).Assembly.Location);

    public static IEnumerable<object[]> SelectionCases() => FixtureRows("cases");
    public static IEnumerable<object[]> IterationCases() => FixtureRows("iterationCases");
    public static IEnumerable<object[]> RejectedPatterns()
    {
        using var fixtures = Fixtures();
        foreach (var pattern in fixtures.RootElement.GetProperty("rejectedPatterns").EnumerateArray())
            yield return [pattern.GetString()!];
    }

    private static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Corpus", "selection.json")));

    private static IEnumerable<object[]> FixtureRows(string family)
    {
        using var fixtures = Fixtures();
        foreach (var row in fixtures.RootElement.GetProperty(family).EnumerateArray())
            yield return [row.GetProperty("id").GetString()!, row.GetRawText()];
    }
    [Theory]
    [MemberData(nameof(RejectedPatterns))]
    public void UnsupportedPatternsFailAtPublicAdmission(string pattern)
        => Assert.Throws<ArgumentException>(() => new Utf8PosixRegex(pattern));
    [Theory]
    [MemberData(nameof(SelectionCases))]
    public void PublicMatchPreservesWinnerForEveryCaptureSpanSize(string id, string fixture)
    {
        using var document = JsonDocument.Parse(fixture);
        var row = document.RootElement;
        Assert.Equal(id, row.GetProperty("id").GetString());
        using var token = new CancellationTokenSource();
        var account = new Account();
        var pattern = row.GetProperty("pattern").GetString()!;
        var options = row.TryGetProperty("options", out var flag) ? Enum.Parse<PosixCompileOptions>(flag.GetString()!) : default;
        var regex = new Utf8PosixRegex(pattern, options: options, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var start = row.TryGetProperty("startOffsetInBytes", out var floor) ? floor.GetInt32() : 0;
        var expected = row.GetProperty("groups");
        // IsMatch has no search floor; the fixture's selected Match does.
        if (start == 0) Assert.Equal(expected.ValueKind != JsonValueKind.Null, regex.IsMatch(input, token.Token, account));
        var value = regex.Match(input, startOffsetInBytes: start, cancellationToken: token.Token, workingMemoryAllowance: account);
        Check(value, expected.ValueKind == JsonValueKind.Null ? default : expected[0]);
        for (var size = 0; size <= regex.GroupCount + 2; size++)
        {
            var sentinel = new Utf8CaptureRange(99, 99 + 2);
            var output = Enumerable.Repeat(sentinel, size).ToArray();
            var actual = regex.Match(input, captures: output, startOffsetInBytes: start, cancellationToken: token.Token, workingMemoryAllowance: account);
            Assert.Equal(value, actual);
            for (var slot = 0; slot < output.Length; slot++)
            {
                if (slot > regex.GroupCount) Assert.Equal(sentinel, output[slot]);
                else Check(output[slot], expected.ValueKind == JsonValueKind.Null ? default : expected[slot]);
            }
            Assert.Equal(0UL, account.Used);
        }
    }
    [Theory]
    [MemberData(nameof(IterationCases))]
    public void PublicCountAndCursorAgreeOnScalarEmptyProgression(string id, string fixture)
    {
        using var document = JsonDocument.Parse(fixture);
        var row = document.RootElement;
        Assert.Equal(id, row.GetProperty("id").GetString());
        var options = row.TryGetProperty("options", out var flag) ? Enum.Parse<PosixCompileOptions>(flag.GetString()!) : default;
        var regex = new Utf8PosixRegex(row.GetProperty("pattern").GetString()!, options: options);
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var start = row.TryGetProperty("startOffsetInBytes", out var floor) ? floor.GetInt32() : 0;
        var expected = row.GetProperty("matches"); var count = 0;
        var account = new Account();
        Assert.Equal(expected.GetArrayLength(), regex.Count(input, start, workingMemoryAllowance: account));
        using var cursor = regex.CreateMatchCursor(input, start, workingMemoryAllowance: account);
        var output = new Utf8CaptureRange[regex.GroupCount + 1];
        while (cursor.MoveNext(captures: output, match: out var match))
        {
            Assert.True(count < expected.GetArrayLength(), "Cursor produced an extra match.");
            Check(match, expected[count++]);
            Assert.Equal(match, output[0]);
        }
        Assert.Equal(expected.GetArrayLength(), count);
        Assert.All(output, value => Assert.False(value.Success));
        Assert.Equal(0UL, account.Used);
    }
    [Theory]
    [InlineData(PosixCompileOptions.None, "aaa")]
    [InlineData(PosixCompileOptions.IgnoreCase, "AAA")]
    public void CursorAliasesAndRetainedValuesUseExplicitLifetime(PosixCompileOptions options, string text)
    {
        var regex = new Utf8PosixRegex("(a|aa)", options: options); var account = new Account();
        var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text), workingMemoryAllowance: account);
        var alias = cursor; var groups = new Utf8CaptureRange[2];
        Assert.True(cursor.MoveNext(captures: groups, match: out var first));
        var retainedGroup = groups[1]; Assert.Equal(2, (first.EndOffsetInBytes - first.StartOffsetInBytes));
        Assert.True(alias.MoveNext(match: out var second)); Assert.Equal(2, second.StartOffsetInBytes);
        Assert.True(account.Used > 0);
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal(first, retainedGroup); Assert.Equal(first, groups[0]);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(match: out _));
        cursor.Dispose(); Assert.Equal(0UL, account.Used);
    }
    [Fact]
    public void CursorAliasesCanAlternateWholeAndCaptureWindows()
    {
        var regex = new Utf8PosixRegex("(a|aa)(a*)b"); var account = new Account();
        using var cursor = regex.CreateMatchCursor("aaabaaabaaab"u8.ToArray(), workingMemoryAllowance: account);
        var alias = cursor;
        Assert.True(cursor.MoveNext(match: out var first)); Assert.Equal(4, (first.EndOffsetInBytes - first.StartOffsetInBytes));
        var groups = new Utf8CaptureRange[3];
        Assert.True(alias.MoveNext(captures: groups, match: out var second));
        Assert.Equal(new Utf8CaptureRange(4, 4 + 2), groups[1]);
        Assert.Equal(new Utf8CaptureRange(6, 6 + 1), groups[2]);
        var retained = groups[1]; var whole = new Utf8CaptureRange[1];
        Assert.True(cursor.MoveNext(captures: whole, match: out var third));
        Assert.Equal(8, third.StartOffsetInBytes); Assert.Equal(third, whole[0]);
        Assert.False(alias.MoveNext(captures: groups, match: out _));
        Assert.All(groups, value => Assert.False(value.Success));
        Assert.Equal(2, (retained.EndOffsetInBytes - retained.StartOffsetInBytes)); Assert.Equal(4, second.StartOffsetInBytes);
        Assert.Equal(0UL, account.Used);
    }
    [Fact]
    public void FoldedGreedyCursorRetainsOriginalCoordinatesAcrossAliases()
    {
        var account = new Account();
        var regex = new Utf8PosixRegex("(k+)([0-9]+)", options: PosixCompileOptions.IgnoreCase, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(5),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 10000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        var cursor = regex.CreateMatchCursor("éKK007 k2"u8.ToArray(), 2, workingMemoryAllowance: account);
        var alias = cursor; var groups = new Utf8CaptureRange[3];
        Assert.True(cursor.MoveNext(captures: groups, match: out var first));
        var retained = groups[1];
        Assert.Equal(new Utf8CaptureRange(2, 2 + 7), first);
        Assert.Equal(new Utf8CaptureRange(2, 2 + 4), retained);
        Assert.True(alias.MoveNext(captures: groups, match: out var second));
        Assert.Equal(new Utf8CaptureRange(10, 10 + 2), second);
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal(new Utf8CaptureRange(2, 2 + 4), retained);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(match: out _));
    }
    [Fact]
    public void LineEndRewindCursorKeepsScalarCoordinatesAndRetainedAliases()
    {
        var account = new Account();
        var regex = new Utf8PosixRegex("^([k]+)([[:space:]]+)$", options: PosixCompileOptions.Newline | PosixCompileOptions.IgnoreCase, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(5),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 10000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        var cursor = regex.CreateMatchCursor("é\nKK \nb\nk \nx"u8.ToArray(), 3, workingMemoryAllowance: account);
        var alias = cursor; var groups = new Utf8CaptureRange[3];
        Assert.True(cursor.MoveNext(captures: groups, match: out var first));
        Assert.Equal(new Utf8CaptureRange(3, 3 + 5), first);
        var retained = groups[1];
        Assert.Equal(new Utf8CaptureRange(3, 3 + 4), retained);
        Assert.True(alias.MoveNext(captures: groups, match: out var second));
        Assert.Equal(new Utf8CaptureRange(11, 11 + 2), second);
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal(new Utf8CaptureRange(3, 3 + 4), retained);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(match: out _));
    }
    [Fact]
    public void ProvenCaptureCursorReusesHistoriesAcrossAliasesAndOutputSizes()
    {
        var regex = new Utf8PosixRegex("(a?){2,4}"); var account = new Account();
        using var cursor = regex.CreateMatchCursor("aa!a"u8.ToArray(), workingMemoryAllowance: account);
        var alias = cursor; var groups = new Utf8CaptureRange[2];
        Assert.True(cursor.MoveNext(captures: groups, match: out var first));
        Assert.Equal(new Utf8CaptureRange(0, 0 + 2), first);
        var retained = groups[1];
        Assert.Equal(new Utf8CaptureRange(1, 1 + 1), retained);
        var whole = new Utf8CaptureRange[1];
        Assert.True(alias.MoveNext(captures: whole, match: out var empty));
        Assert.Equal(new Utf8CaptureRange(2, 2 + 0), empty);
        Assert.Equal(empty, whole[0]);
        Assert.True(cursor.MoveNext(captures: groups, match: out var third));
        Assert.Equal(new Utf8CaptureRange(3, 3 + 1), third);
        Assert.Equal(new Utf8CaptureRange(4, 4 + 0), groups[1]);
        Assert.True(alias.MoveNext(captures: groups, match: out var last));
        Assert.Equal(new Utf8CaptureRange(4, 4 + 0), last);
        Assert.Equal(last, groups[1]);
        Assert.False(cursor.MoveNext(captures: groups, match: out _));
        Assert.All(groups, value => Assert.False(value.Success));
        Assert.Equal(new Utf8CaptureRange(1, 1 + 1), retained);
        Assert.Equal(4, regex.Count("aa!a"u8));
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(match: out _));
    }
    [Fact]
    public void FlatWholeOperationsDoNotRequestAbsentTagBuffers()
    {
        var regex = new Utf8PosixRegex("(a)", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        var account = new Account { Maximum = 0 };
        Assert.True(regex.IsMatch("aa"u8, workingMemoryAllowance: account));
        Assert.True(regex.Match("aa"u8, workingMemoryAllowance: account).Success);
        Assert.Equal(2, regex.Count("aa"u8, workingMemoryAllowance: account));
        // Requesting captures needs the tag buffers and still admits before
        // acquiring them. Fixed workspace owner metadata is outside this policy.
        var groups = new Utf8CaptureRange[2];
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match("aa"u8, captures: groups, workingMemoryAllowance: account)).ResourceKind);
        Assert.Equal(0, account.Attempts); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void ByteRangesKeepScalarsAcrossLongInputs()
    {
        const int prefix = 4093, repeats = 2048;
        var input = Encoding.UTF8.GetBytes(new string('x', prefix) + "😀" + new string('é', repeats));
        var regex = new Utf8PosixRegex("(😀)(é+)"); var groups = new Utf8CaptureRange[3];
        var whole = regex.Match(input, captures: groups, startOffsetInBytes: prefix);
        Assert.Equal(new Utf8CaptureRange(prefix, (prefix) + (4 + 2 * repeats)), whole);
        Assert.Equal(whole, groups[0]);
        Assert.Equal(new Utf8CaptureRange(prefix, (prefix) + 4), groups[1]);
        Assert.Equal(new Utf8CaptureRange(prefix + 4, (prefix + 4) + (2 * repeats)), groups[2]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedCapturePublicationClearsPartialOutputAndRefunds(bool deadline)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var token = new CancellationTokenSource(); var account = new Account();
        const int depth = 128;
        var regex = new Utf8PosixRegex(new string('(', depth) + "a" + new string(')', depth), options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            ResourcePolicy = default
        });
        var output = new Utf8CaptureRange[depth + 1]; var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered || !output[0].Success) return;
            triggered = true;
            if (deadline) now = 101; else token.Cancel();
        });
        try
        {
            if (deadline)
            {
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("a"u8, captures: output, startOffsetInBytes: 0, cancellationToken: token.Token, workingMemoryAllowance: account));
                Assert.Equal(regex.Pattern, error.Pattern); Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("a"u8, captures: output, startOffsetInBytes: 0, cancellationToken: token.Token, workingMemoryAllowance: account)).CancellationToken);
            Assert.True(triggered);
            Assert.All(output, match => Assert.False(match.Success)); Assert.Equal(0UL, account.Used);
        }
        finally { diagnostics.Complete(); }
    }
    [Theory]
    [InlineData(false, false, "a")]
    [InlineData(true, false, "a")]
    [InlineData(false, true, "a")]
    [InlineData(true, true, "a")]
    [InlineData(false, false, "😀")]
    [InlineData(true, false, "😀")]
    [InlineData(false, true, "😀")]
    [InlineData(true, true, "😀")]
    [InlineData(false, false, "k")]
    [InlineData(true, false, "k")]
    [InlineData(false, true, "k")]
    [InlineData(true, true, "k")]
    public void InterruptionInsideClosedCaptureSelectionClearsOutputAndRefunds(bool deadline, bool projection, string scalar)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        {
            now = 0;
            using var token = new CancellationTokenSource();
            var account = new Account(); Assert.True(account.TryReserve(17));
            var body = $"({scalar}|{scalar}{scalar})*";
            var folded = scalar == "k";
            var regex = new Utf8PosixRegex($"x({body}b|{body}b)", options: folded ? PosixCompileOptions.IgnoreCase : default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1_000_000,
                    MaxWorkingBytes = 1 << 20
                }
            });
            var retained = new Utf8CaptureRange(0, 0 + 1);
            var output = Enumerable.Repeat(retained, regex.GroupCount + 1).ToArray();
            var input = Encoding.UTF8.GetBytes("x" + string.Concat(Enumerable.Repeat(folded ? "K" : scalar, 128)) + "b#");
            var triggered = false;
            var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                var frames = new StackTrace().GetFrames();
                var dependency = frames.Any(frame => frame.GetMethod() is { Name: "ChartMember", DeclaringType: { } type } && type == typeof(PosixWorkspace));
                var selection = frames.Any(frame => frame.GetMethod() is { Name: "SelectChart", DeclaringType: { } type } && type == typeof(PosixWorkspace));
                // A SelectChart caller also exists during membership. Require
                // its absence from dependencies to observe actual projection.
                if (triggered || (projection ? !selection || dependency : !dependency)) return;
                triggered = true;
                if (deadline) now = 101; else token.Cancel();
            });
            try
            {
                if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, captures: output, startOffsetInBytes: 0, cancellationToken: token.Token, workingMemoryAllowance: account));
                else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                    regex.Match(input, captures: output, startOffsetInBytes: 0, cancellationToken: token.Token, workingMemoryAllowance: account)).CancellationToken);
                Assert.True(triggered, "The injection must reach the actual closed dependency/projection loop.");
                Assert.True(account.Peak > 17);
                Assert.All(output, match => Assert.False(match.Success));
                Assert.Equal(17UL, account.Used); account.Release(17);
                Assert.True(retained.Success);
            }
            finally { diagnostics.Complete(); }
        }
    }
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public void InterruptionInsideBoundaryProofClearsOutputAndRefunds(bool deadline, bool parent, bool large)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        {
            var regex = new Utf8PosixRegex(large ? "a|" + new string('b', 48) :
                parent ? "((a|a.)*)!*" : "((a|a[^!#])*)!*", options: default, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
                    ResourcePolicy = new()
                    {
                        MaxWorkSteps = 1_000_000,
                        MaxWorkingBytes = 1 << 20
                    }
                });
            var reached = false;
            // The normal diagnostic hook polls every64 charged steps. Vary a
            // bounded set of real run lengths to observe a poll inside this
            // short proof, rather than adding a production phase callback.
            for (var count = 16; count < 80 && !reached; count++)
            {
                now = 0;
                using var token = new CancellationTokenSource();
                var account = new Account(); Assert.True(account.TryReserve(17));
                var retained = new Utf8CaptureRange(0, 0 + 1);
                var output = Enumerable.Repeat(retained, regex.GroupCount + 1).ToArray();
                var input = Encoding.ASCII.GetBytes(large ? new string('x', count) + "a#" : new string('a', count) + "!#");
                var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
                {
                    var frames = new StackTrace().GetFrames();
                    if (reached || !frames.Any(frame =>
                        frame.GetMethod() is { DeclaringType: { } type } method &&
                        method.Name == (parent ? "FindReachableBoundaryCeiling" : "FindBoundaryCeiling") &&
                        type == typeof(PosixWorkspace))) return;
                    reached = true;
                    if (deadline) now = 101; else token.Cancel();
                });
                Exception? error;
                try { error = Record.Exception(() => regex.Match(input, captures: output, startOffsetInBytes: 0, cancellationToken: token.Token, workingMemoryAllowance: account)); }
                finally { diagnostics.Complete(); }
                if (reached)
                {
                    if (deadline) Assert.IsType<RegexMatchTimeoutException>(error);
                    else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
                    Assert.All(output, match => Assert.False(match.Success));
                }
                else Assert.Null(error);
                Assert.Equal(17UL, account.Used); account.Release(17);
                Assert.True(retained.Success);
            }
            Assert.True(reached, "The injection must reach the actual global/reachable whole boundary proof.");
        }
    }
    [Theory]
    [InlineData(false, "(a)")]
    [InlineData(true, "(a)")]
    [InlineData(false, "(a?){2,4}")]
    [InlineData(true, "(a?){2,4}")]
    public void CursorKeepsOriginalInterruptionAcrossAdvancesAndClosesOnFailure(bool deadline, string pattern)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var token = new CancellationTokenSource(); var account = new Account();
        var regex = new Utf8PosixRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            ResourcePolicy = default
        });
        using var cursor = regex.CreateMatchCursor("aa"u8.ToArray(), cancellationToken: token.Token, workingMemoryAllowance: account);
        Assert.True(cursor.MoveNext(match: out var retained));
        if (deadline) now = 101; else token.Cancel();
        var output = new[] { retained, retained };
        if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(captures: output, match: out _));
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(captures: output, match: out _)).CancellationToken);
        Assert.All(output, match => Assert.False(match.Success)); Assert.Equal(0UL, account.Used);
        Assert.True(retained.Success); Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(match: out _));
    }
    [Theory]
    [InlineData(PosixCompileOptions.None)]
    [InlineData(PosixCompileOptions.IgnoreCase)]
    public void PublicPoliciesRefuseBeforeCoveredAcquisitionAndDoNotRetry(PosixCompileOptions options)
    {
        var regex = new Utf8PosixRegex("(a|aa)*", options: options, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        var account = new Account();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch("a"u8, workingMemoryAllowance: account)).ResourceKind);
        Assert.Equal(0, account.Attempts);
        var normal = new Utf8PosixRegex("a|aa", options: options); var refused = new Account { Maximum = 0 };
        Assert.Throws<Utf8MemoryAdmissionException>(() => normal.Count("a"u8, workingMemoryAllowance: refused));
        Assert.Equal(1, refused.Attempts); Assert.Equal(0UL, refused.Used);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => normal.IsMatch("a"u8, canceled.Token, account)).CancellationToken);
        Assert.Equal(0, account.Attempts);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxResultBytes = 0
            }
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.Zero,
            ResourcePolicy = default
        }));
    }
    [Fact]
    public void CompilationUsesOriginalCancellationDeadlineAndInstructionAdmission()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => Utf8PosixRegex.Compile("a", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: token.Token)).CancellationToken);
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8PosixRegex.Compile("(a|aa)*", options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = default,
                ConstructionPolicy = new()
                {
                    MaxInstructionCapacity = 1
                },
                ConstructionTimeout = Timeout.InfiniteTimeSpan
            }, cancellationToken: default)).ResourceKind);
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now++ == 0 ? 0 : 101);
        Assert.Throws<RegexMatchTimeoutException>(() => Utf8PosixRegex.Compile("a", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = TimeSpan.FromMilliseconds(100)
        }, cancellationToken: default));
    }
    [Theory]
    [InlineData("(a|aa)*", "aaaaa", 1, 4, 1)]
    [InlineData("(a|aa)*", "aaaa", 1, 2, 2)]
    [InlineData("((a)|(aa))*", "aaa", 1, 2, 1)]
    [InlineData("((a)|(aa))*", "aaa", 2, 2, 1)]
    [InlineData("((a)|(aa))*", "aaa", 3, -1, 0)]
    [InlineData("(a|())*", "a", 2, -1, 0)]
    [InlineData("((a?)*)*", "", 2, 0, 0)]
    [InlineData("(a?){2,4}", "a", 1, 1, 0)]
    [InlineData("(a?){0,2}", "a", 1, 0, 1)]
    [InlineData("((a|aa)*)", "aaa", 2, 2, 1)]
    public void RepeatedAndNullableCaptureHistoriesPreserveEarlierPriority(string pattern, string input, int slot, int start, int length)
    {
        var regex = new Utf8PosixRegex(pattern); var groups = new Utf8CaptureRange[regex.GroupCount + 1];
        Assert.True(regex.Match(Encoding.UTF8.GetBytes(input), captures: groups).Success);
        Assert.Equal(start >= 0, groups[slot].Success);
        if (start >= 0) { Assert.Equal(start, groups[slot].StartOffsetInBytes); Assert.Equal(length, (groups[slot].EndOffsetInBytes - groups[slot].StartOffsetInBytes)); }
    }
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public void LongRepeatedHistoriesKeepTheLastScalarAndCursorAlias(int count)
    {
        var input = Encoding.UTF8.GetBytes("é" + string.Concat(Enumerable.Repeat("😀", count)));
        var regex = new Utf8PosixRegex("(😀)*");
        var account = new Account();
        using var cursor = regex.CreateMatchCursor(input, 2, workingMemoryAllowance: account);
        var alias = cursor;
        var captures = new Utf8CaptureRange[2];
        Assert.True(cursor.MoveNext(captures: captures, match: out var retained));
        Assert.Equal(4 * count, (retained.EndOffsetInBytes - retained.StartOffsetInBytes));
        Assert.Equal(2 + 4 * (count - 1), captures[1].StartOffsetInBytes);
        Assert.Equal(4, (captures[1].EndOffsetInBytes - captures[1].StartOffsetInBytes));
        Assert.True(alias.MoveNext(captures: captures, match: out var empty));
        Assert.Equal(2 + 4 * count, empty.StartOffsetInBytes);
        Assert.Equal(0, (empty.EndOffsetInBytes - empty.StartOffsetInBytes));
        Assert.False(alias.MoveNext(captures: captures, match: out _));
        alias.Dispose();
        Assert.Equal(0UL, account.Used);
        Assert.Equal(4 * count, (retained.EndOffsetInBytes - retained.StartOffsetInBytes));
    }
    [Theory]
    [InlineData(0, "a", "")]
    [InlineData(1, "a", "")]
    [InlineData(2, "a", "")]
    [InlineData(3, "a", "")]
    [InlineData(4, "a", "")]
    [InlineData(5, "a", "")]
    [InlineData(6, "a", "")]
    [InlineData(5, "😀", "éz")]
    public void RepeatedNestedAlternativesFollowTheDerivedLongestFirstPartition(int count, string atom, string prefix)
    {
        // Among equal whole matches, the first outer occurrence takes two
        // atoms when possible, then the next does. The final occurrence is
        // one atom iff count is odd. Its other child is absent, including
        // when that child participated in an earlier outer occurrence.
        var regex = new Utf8PosixRegex($"(({atom})|({atom}{atom}))*");
        var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(atom, count)));
        var floor = Encoding.UTF8.GetByteCount(prefix); var width = Encoding.UTF8.GetByteCount(atom);
        var output = new Utf8CaptureRange[4];
        var whole = regex.Match(input, captures: output, startOffsetInBytes: floor);
        Assert.Equal(new Utf8CaptureRange(floor, (floor) + (count * width)), whole);
        Assert.Equal(whole, output[0]);
        if (count == 0) { Assert.All(output[1..], capture => Assert.False(capture.Success)); return; }
        var last = (count & 1) == 0 ? 2 : 1;
        var expected = new Utf8CaptureRange(floor + (count - last) * width, (floor + (count - last) * width) + (last * width));
        Assert.Equal(expected, output[1]);
        Assert.Equal(expected, output[last == 1 ? 2 : 3]);
        Assert.False(output[last == 1 ? 3 : 2].Success);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RequiredNullableIterationsKeepOnlyNecessaryEmptyTails(int count)
    {
        // Earlier occurrences consume the available atoms. The two required
        // iterations force a final empty capture only when fewer than two
        // atoms exist; optional empty iterations lose to a shorter history.
        var regex = new Utf8PosixRegex("(a?){2,4}"); var groups = new Utf8CaptureRange[2];
        Assert.Equal(count, regex.Match(Encoding.ASCII.GetBytes(new string('a', count)), captures: groups).EndOffsetInBytes);
        var length = count >= 2 ? 1 : 0; var start = count - length;
        Assert.Equal(new Utf8CaptureRange(start, (start) + (length)), groups[1]);
    }
    [Fact]
    public void SmallAsciiLanguagesHaveIndependentLeftmostLongestWholeMatches()
    {
        // BCL is only an acceptance oracle for each fixed ASCII substring.
        // Enumerating substring extents derives whole-match longest selection;
        // its ordered Match/capture winner is never used as a POSIX oracle.
        string[] patterns = ["", "a|aa", "a*b?", "(a|b)*", "(a|aa)*", "(a?){2,4}",
            "(ab|a)b*", "[ab]{1,3}", "[^b]+", "(a*)*", "(a|())*", "a?(ab)?b?"];
        foreach (var pattern in patterns)
        {
            var posix = new Utf8PosixRegex(pattern, options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1_000_000,
                    MaxWorkingBytes = 1 << 20
                }
            });
            var acceptance = new Regex("\\A(?:" + pattern + ")\\z");
            for (var length = 0; length <= 4; length++)
                for (var bits = 0; bits < 1 << length; bits++)
                {
                    var text = new string(Enumerable.Range(0, length).Select(i => (bits & 1 << i) == 0 ? 'a' : 'b').ToArray());
                    var expectedStart = -1; var expectedEnd = -1;
                    for (var from = 0; from <= length && expectedStart < 0; from++)
                        for (var to = length; to >= from; to--)
                        {
                            if (!acceptance.IsMatch(text.AsSpan(from, to - from))) continue;
                            expectedStart = from; expectedEnd = to; break;
                        }
                    var input = Encoding.ASCII.GetBytes(text); var actual = posix.Match(input);
                    Assert.Equal(expectedStart >= 0, actual.Success);
                    Assert.Equal(actual.Success, posix.IsMatch(input));
                    if (!actual.Success) continue;
                    Assert.Equal(expectedStart, actual.StartOffsetInBytes); Assert.Equal(expectedEnd - expectedStart, (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
                }
        }
    }
    [Theory]
    [InlineData("(a|aa)*")]
    [InlineData("(a|aa)*b")]
    [InlineData("(a?){2,4}")]
    public void RefusalAtEveryWorkingAcquisitionRefundsAndClearsOutput(string pattern)
    {
        var regex = new Utf8PosixRegex(pattern); var granted = new Account();
        var output = new Utf8CaptureRange[2];
        var input = Encoding.ASCII.GetBytes("aaaaaa" + (pattern.EndsWith('b') ? "b" : ""));
        Assert.True(regex.Match(input, captures: output, workingMemoryAllowance: granted).Success);
        Assert.True(granted.Attempts > 2); Assert.Equal(0UL, granted.Used);
        for (var admitted = 0; admitted < granted.Attempts; admitted++)
        {
            var account = new Account { AllowedAttempts = admitted };
            Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Match(input, captures: output, workingMemoryAllowance: account));
            Assert.Equal(admitted + 1, account.Attempts); Assert.Equal(0UL, account.Used);
            Assert.All(output, match => Assert.False(match.Success));
        }
    }
    [Fact]
    public void CursorWorkIsCumulativeAcrossAdvances()
    {
        var account = new Account();
        var regex = new Utf8PosixRegex("a", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 256
            }
        });
        using var cursor = regex.CreateMatchCursor(Encoding.ASCII.GetBytes(new string('a', 128)), workingMemoryAllowance: account);
        var hits = 0;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        { while (cursor.MoveNext(match: out _)) hits++; });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.InRange(hits, 1, 127); Assert.Equal(0UL, account.Used);
        Assert.True(regex.IsMatch("a"u8));
    }
    [Fact]
    public void NumericWorkingAdmissionIncludesGrowthOverlap()
    {
        var broad = new Utf8PosixRegex("(a|aa)*"); var measured = new Account();
        Assert.True(broad.Match("aaaaaa"u8, workingMemoryAllowance: measured).Success);
        var capped = new Utf8PosixRegex("(a|aa)*", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = measured.Peak - 1
            }
        });
        var loan = new Account();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => capped.Match("aaaaaa"u8, workingMemoryAllowance: loan)).ResourceKind);
        Assert.Equal(0UL, loan.Used);
    }
    [Theory]
    [InlineData("IsMatch", false, PosixCompileOptions.None)]
    [InlineData("IsMatch", true, PosixCompileOptions.None)]
    [InlineData("Match", false, PosixCompileOptions.None)]
    [InlineData("Match", true, PosixCompileOptions.None)]
    [InlineData("Count", false, PosixCompileOptions.None)]
    [InlineData("Count", true, PosixCompileOptions.None)]
    [InlineData("Cursor", false, PosixCompileOptions.None)]
    [InlineData("Cursor", true, PosixCompileOptions.None)]
    [InlineData("Match", false, PosixCompileOptions.IgnoreCase)]
    [InlineData("Match", true, PosixCompileOptions.IgnoreCase)]
    [InlineData("Cursor", false, PosixCompileOptions.IgnoreCase)]
    [InlineData("Cursor", true, PosixCompileOptions.IgnoreCase)]
    [InlineData("FoldedGreedyCapture", false, PosixCompileOptions.IgnoreCase)]
    [InlineData("FoldedGreedyCapture", true, PosixCompileOptions.IgnoreCase)]
    [InlineData("NewlineRewindCapture", false, PosixCompileOptions.Newline)]
    [InlineData("NewlineRewindCapture", true, PosixCompileOptions.Newline)]
    [InlineData("SparseGreedyCapture", false, PosixCompileOptions.IgnoreCase)]
    [InlineData("SparseGreedyCapture", true, PosixCompileOptions.IgnoreCase)]
    [InlineData("SingleFlatCapture", false, PosixCompileOptions.None)]
    [InlineData("SingleFlatCapture", true, PosixCompileOptions.None)]
    [InlineData("FixedFoldedCapture", false, PosixCompileOptions.IgnoreCase)]
    [InlineData("FixedFoldedCapture", true, PosixCompileOptions.IgnoreCase)]
    [InlineData("TerminalUnicodeCapture", false, PosixCompileOptions.None)]
    [InlineData("TerminalUnicodeCapture", true, PosixCompileOptions.None)]
    [InlineData("RequiredUnicodeCapture", false, PosixCompileOptions.None)]
    [InlineData("RequiredUnicodeCapture", true, PosixCompileOptions.None)]
    public void MatchingObservesOriginalInterruptionAndRefundsWorkingLoans(string operation, bool deadline, PosixCompileOptions options)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var token = new CancellationTokenSource(); var account = new Account();
        var sparseGreedy = operation == "SparseGreedyCapture";
        var foldedGreedy = operation == "FoldedGreedyCapture" || sparseGreedy;
        var rewind = operation == "NewlineRewindCapture";
        var flat = operation == "SingleFlatCapture";
        var fixedFolded = operation == "FixedFoldedCapture";
        var terminalUnicode = operation == "TerminalUnicodeCapture";
        var requiredUnicode = operation == "RequiredUnicodeCapture";
        var regex = new Utf8PosixRegex(requiredUnicode ? "Z(é+)é()" : terminalUnicode ? "Z(é+)()" : flat ? "Z(a+)()" : fixedFolded ? "Z(k+)()" : foldedGreedy ? "(k+)([0-9]+)" : rewind ? "^([a-z]+)([[:space:]]+)$" : "a", options: options, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000
            }
        });
        var input = terminalUnicode || requiredUnicode ? Encoding.UTF8.GetBytes("Z" + new string('é', 8192)) :
            flat ? Encoding.ASCII.GetBytes("Z" + new string('a', 8192)) :
            fixedFolded ? Encoding.UTF8.GetBytes("Z" + string.Concat(Enumerable.Repeat("K", 8192))) :
            sparseGreedy ? Encoding.UTF8.GetBytes(new string('!', 8192) + "K007") :
            foldedGreedy ? Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("K", 8192)) + "007") :
            rewind ? Encoding.UTF8.GetBytes(new string('a', 8192) + " \nb") :
            Encoding.ASCII.GetBytes(new string('x', 1_000_000) + "a");
        var triggered = false;
        // Construction is complete and these small workspaces reach the first
        // scheduled checkpoint while matching. Stack membership depends on JIT
        // inlining and cannot determine when the public contract requests stop.
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered) return;
            triggered = true; if (deadline) now = 101; else token.Cancel();
        });
        try
        {
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(Call);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(Call).CancellationToken);
            Assert.True(triggered); Assert.Equal(0UL, account.Used);
        }
        finally { diagnostics.Complete(); }
        void Call()
        {
            switch (operation)
            {
                case "IsMatch": regex.IsMatch(input, token.Token, account); break;
                case "Match": regex.Match(input, cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "Count": regex.Count(input, cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "FoldedGreedyCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "SingleFlatCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "FixedFoldedCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "TerminalUnicodeCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "RequiredUnicodeCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "SparseGreedyCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "NewlineRewindCapture": regex.Match(input, captures: new Utf8CaptureRange[3], cancellationToken: token.Token, workingMemoryAllowance: account); break;
                default: using (var cursor = regex.CreateMatchCursor(input, cancellationToken: token.Token, workingMemoryAllowance: account)) cursor.MoveNext(match: out _); break;
            }
        }
    }

    private static void Check(Utf8CaptureRange actual, JsonElement expected)
    {
        var success = expected.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.Equal(success, actual.Success);
        if (!success) return;
        Assert.Equal(expected[0].GetInt32(), actual.StartOffsetInBytes); Assert.Equal(expected[1].GetInt32(), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used, Peak, Maximum = 1 << 20;
        internal int Attempts;
        internal int AllowedAttempts = int.MaxValue;
        public bool TryReserve(ulong bytes)
        { Attempts++; if (Attempts > AllowedAttempts || bytes > Maximum - Used) return false; Used += bytes; Peak = Math.Max(Peak, Used); return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
