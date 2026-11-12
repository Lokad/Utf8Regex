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
        var regex = new Utf8PosixRegex(pattern, options, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var start = row.TryGetProperty("startOffsetInBytes", out var floor) ? floor.GetInt32() : 0;
        var expected = row.GetProperty("groups");
        // IsMatch has no search floor; the fixture's selected Match does.
        if (start == 0) Assert.Equal(expected.ValueKind != JsonValueKind.Null, regex.IsMatch(input, token.Token, account));
        var value = regex.Match(input, start, token.Token, account);
        Check(value, expected.ValueKind == JsonValueKind.Null ? default : expected[0]);
        for (var size = 0; size <= regex.GroupCount + 2; size++)
        {
            var sentinel = new Utf8ValueMatch(true, true, 99, 2, 99, 2);
            var output = Enumerable.Repeat(sentinel, size).ToArray();
            var actual = regex.Match(input, output, start, token.Token, account);
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
        var regex = new Utf8PosixRegex(row.GetProperty("pattern").GetString()!);
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var start = row.TryGetProperty("startOffsetInBytes", out var floor) ? floor.GetInt32() : 0;
        var expected = row.GetProperty("matches"); var count = 0;
        var account = new Account();
        Assert.Equal(expected.GetArrayLength(), regex.Count(input, start, workingMemoryAllowance: account));
        using var cursor = regex.CreateMatchCursor(input, start, workingMemoryAllowance: account);
        var output = new Utf8ValueMatch[regex.GroupCount + 1];
        while (cursor.MoveNext(output, out var match))
        {
            Assert.True(count < expected.GetArrayLength(), "Cursor produced an extra match.");
            Check(match, expected[count++]);
            Assert.Equal(match, output[0]);
        }
        Assert.Equal(expected.GetArrayLength(), count);
        Assert.All(output, value => Assert.False(value.Success));
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void CursorAliasesAndRetainedValuesUseExplicitLifetime()
    {
        var regex = new Utf8PosixRegex("(a|aa)"); var account = new Account();
        var cursor = regex.CreateMatchCursor("aaa"u8.ToArray(), workingMemoryAllowance: account);
        var alias = cursor; var groups = new Utf8ValueMatch[2];
        Assert.True(cursor.MoveNext(groups, out var first));
        var retainedGroup = groups[1]; Assert.Equal(2, first.LengthInBytes);
        Assert.True(alias.MoveNext(out var second)); Assert.Equal(2, second.IndexInBytes);
        Assert.True(account.Used > 0);
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal(first, retainedGroup); Assert.Equal(first, groups[0]);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        cursor.Dispose(); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void CursorAliasesCanAlternateWholeAndCaptureWindows()
    {
        var regex = new Utf8PosixRegex("(a|aa)(a*)b"); var account = new Account();
        using var cursor = regex.CreateMatchCursor("aaabaaabaaab"u8.ToArray(), workingMemoryAllowance: account);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out var first)); Assert.Equal(4, first.LengthInBytes);
        var groups = new Utf8ValueMatch[3];
        Assert.True(alias.MoveNext(groups, out var second));
        Assert.Equal(new Utf8ValueMatch(true, true, 4, 2, 4, 2), groups[1]);
        Assert.Equal(new Utf8ValueMatch(true, true, 6, 1, 6, 1), groups[2]);
        var retained = groups[1]; var whole = new Utf8ValueMatch[1];
        Assert.True(cursor.MoveNext(whole, out var third));
        Assert.Equal(8, third.IndexInBytes); Assert.Equal(third, whole[0]);
        Assert.False(alias.MoveNext(groups, out _));
        Assert.All(groups, value => Assert.False(value.Success));
        Assert.Equal(2, retained.LengthInBytes); Assert.Equal(4, second.IndexInBytes);
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void ProvenCaptureCursorReusesHistoriesAcrossAliasesAndOutputSizes()
    {
        var regex = new Utf8PosixRegex("(a?){2,4}"); var account = new Account();
        using var cursor = regex.CreateMatchCursor("aa!a"u8.ToArray(), workingMemoryAllowance: account);
        var alias = cursor; var groups = new Utf8ValueMatch[2];
        Assert.True(cursor.MoveNext(groups, out var first));
        Assert.Equal(new Utf8ValueMatch(true, true, 0, 2, 0, 2), first);
        var retained = groups[1];
        Assert.Equal(new Utf8ValueMatch(true, true, 1, 1, 1, 1), retained);
        var whole = new Utf8ValueMatch[1];
        Assert.True(alias.MoveNext(whole, out var empty));
        Assert.Equal(new Utf8ValueMatch(true, true, 2, 0, 2, 0), empty);
        Assert.Equal(empty, whole[0]);
        Assert.True(cursor.MoveNext(groups, out var third));
        Assert.Equal(new Utf8ValueMatch(true, true, 3, 1, 3, 1), third);
        Assert.Equal(new Utf8ValueMatch(true, true, 4, 0, 4, 0), groups[1]);
        Assert.True(alias.MoveNext(groups, out var last));
        Assert.Equal(new Utf8ValueMatch(true, true, 4, 0, 4, 0), last);
        Assert.Equal(last, groups[1]);
        Assert.False(cursor.MoveNext(groups, out _));
        Assert.All(groups, value => Assert.False(value.Success));
        Assert.Equal(new Utf8ValueMatch(true, true, 1, 1, 1, 1), retained);
        Assert.Equal(4, regex.Count("aa!a"u8));
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void FlatWholeOperationsDoNotRequestAbsentTagBuffers()
    {
        var regex = new Utf8PosixRegex("(a)", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var account = new Account { Maximum = 0 };
        Assert.True(regex.IsMatch("aa"u8, workingMemoryAllowance: account));
        Assert.True(regex.Match("aa"u8, workingMemoryAllowance: account).Success);
        Assert.Equal(2, regex.Count("aa"u8, workingMemoryAllowance: account));
        // Requesting captures needs the tag buffers and still admits before
        // acquiring them. Fixed workspace owner metadata is outside this policy.
        var groups = new Utf8ValueMatch[2];
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match("aa"u8, groups, workingMemoryAllowance: account)).ResourceKind);
        Assert.Equal(0, account.Attempts); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void MalformedWholeSubjectsCannotBeHiddenByEarlyHitsOrSearchFloors()
    {
        var regex = new Utf8PosixRegex("a"); byte[] malformed = [(byte)'a', 0xff];
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed, 1));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed, malformed.Length));
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(malformed, 1));
        var unicode = "😀a"u8.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(unicode, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(unicode, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CreateMatchCursor(unicode, unicode.Length + 1));
        Assert.False(new Utf8PosixRegex("^a").Match("ba"u8, 1).Success);
    }

    [Fact]
    public void ValidatedCoordinateProjectionKeepsScalarsAcrossCountingBlocks()
    {
        const int prefix = 4093, repeats = 2048;
        var input = Encoding.UTF8.GetBytes(new string('x', prefix) + "😀" + new string('é', repeats));
        var regex = new Utf8PosixRegex("(😀)(é+)"); var groups = new Utf8ValueMatch[3];
        var whole = regex.Match(input, groups, prefix);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix, repeats + 2, prefix, 4 + 2 * repeats), whole);
        Assert.Equal(whole, groups[0]);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix, 2, prefix, 4), groups[1]);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix + 2, repeats, prefix + 4, 2 * repeats), groups[2]);
    }

    [Fact]
    public void CoordinateProjectionWorkRefusalUsesThePublicFailureAndClearsOutput()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 8192) + "a");
        using var token = new CancellationTokenSource(); var account = new Account();
        var baseline = new Utf8PosixRegex("a");
        var invocation = baseline.StartInvocation(token.Token, account);
        ulong beforeProjection;
        Utf8PosixRegex.ValidateInput(input, 8192, ref invocation);
        var workspace = baseline.CreateWorkspace(ref invocation, captureHistory: false);
        try
        {
            Assert.True(workspace.Find(input, new int[workspace.StateLength], 8192, false, ref invocation, out _, out _, captureHistory: false));
            beforeProjection = invocation.Budget.Steps;
        }
        finally { workspace.Release(ref invocation); }
        // ClearOutput consumes one step. The next charge is the coordinate
        // scan, whose shared helper reports an internal breach to the adapter.
        var limited = new Utf8PosixRegex("a", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = beforeProjection + 1 });
        var output = new[] { new Utf8ValueMatch(true, true, 0, 1, 0, 1) };
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            limited.Match(input, output, 8192, token.Token, account));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(beforeProjection + 2, error.Requested);
        Assert.False(output[0].Success); Assert.Equal(0UL, account.Used);
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
        var regex = new Utf8PosixRegex(new string('(', depth) + "a" + new string(')', depth), default,
            deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan, default);
        var output = new Utf8ValueMatch[depth + 1]; var triggered = false;
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
                var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("a"u8, output, 0, token.Token, account));
                Assert.Equal(regex.Pattern, error.Pattern); Assert.Empty(error.Input);
            }
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("a"u8, output, 0, token.Token, account)).CancellationToken);
            Assert.True(triggered);
            Assert.All(output, match => Assert.False(match.Success)); Assert.Equal(0UL, account.Used);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, true, null)]
    [InlineData(false, true, "b")]
    [InlineData(true, true, "b")]
    [InlineData(false, true, "([bc])*")]
    [InlineData(true, true, "([bc])*")]
    public void InterruptionDuringCaptureHistoryCompetitionClearsOutputAndRefunds(bool deadline, bool pruning, string? suffix)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var token = new CancellationTokenSource();
        var account = new Account();
        // An enclosed alternative remains outside the bounded root-branch proof,
        // retaining actual general history competition.
        var regex = new Utf8PosixRegex(pruning ? "(a|aa)*" + suffix : "((a|aa)*b|((a|aa)*).)#?", default,
            deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 });
        var retained = new Utf8ValueMatch(true, true, 0, 1, 0, 1);
        var output = Enumerable.Repeat(retained, regex.GroupCount + 1).ToArray(); var triggered = false;
        // Equal-prefix pruning can finish between two 64-step hook polls.
        // Observe actual captured-history rollback on that optimized route;
        // the general route retains its full history-comparison witness.
        var phase = suffix == "([bc])*" ? "FindBoundaryBoundedParentTag" : pruning ? "Backtrack" : "BetterHistory";
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            // Test-only phase observation through the existing checkpoint hook:
            // do not cancel at an arbitrary step or add a production phase switch.
            if (triggered || !new StackTrace().GetFrames().Any(frame =>
                frame.GetMethod() is { DeclaringType: { } type } method && method.Name == phase && type == typeof(PosixWorkspace))) return;
            triggered = true;
            if (deadline) now = 101; else token.Cancel();
        });
        try
        {
            // A long captured suffix guarantees bounded hook polls while the
            // parent proof walks its accepted trace, before saving the winner.
            var input = Encoding.ASCII.GetBytes(pruning ? new string('a', 128) +
                (suffix == "([bc])*" ? new string('b', 4096) : suffix is null ? "" : "b") : new string('a', 12) + "b#");
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, output, 0, token.Token, account));
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Match(input, output, 0, token.Token, account)).CancellationToken);
            Assert.True(triggered, "The injection must reach execution over captured histories.");
            Assert.True(account.Peak > 0);
            Assert.All(output, match => Assert.False(match.Success)); Assert.Equal(0UL, account.Used);
            Assert.True(retained.Success);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptionInsideRootScopeProofClearsOutputAndRefunds(bool deadline)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex("(a|aa)*b|((a|aa)*).", default,
                deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 }, inputValidation: validation);
            var reached = false;
            for (var count = 16; count < 80 && !reached; count++)
            {
                now = 0;
                using var token = new CancellationTokenSource();
                var account = new Account(); Assert.True(account.TryReserve(17));
                var retained = new Utf8ValueMatch(true, true, 0, 1, 0, 1);
                var output = Enumerable.Repeat(retained, regex.GroupCount + 1).ToArray();
                var input = Encoding.ASCII.GetBytes(new string('a', count) + "b#");
                var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
                {
                    if (reached || !new StackTrace().GetFrames().Any(frame =>
                        frame.GetMethod() is { Name: "TryGetRootBranchScope", DeclaringType: { } type } && type == typeof(PosixWorkspace))) return;
                    reached = true;
                    if (deadline) now = 101; else token.Cancel();
                });
                Exception? error;
                try { error = Record.Exception(() => regex.Match(input, output, 0, token.Token, account)); }
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
            Assert.True(reached, "The injection must reach the actual root-branch scope proof.");
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void InterruptionInsideBoundaryProofClearsOutputAndRefunds(bool deadline, bool parent)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex(parent ? "((a|a.)*)!*" : "((a|a[^!#])*)!*", default,
                deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 }, inputValidation: validation);
            var reached = false;
            // The normal diagnostic hook polls every64 charged steps. Vary a
            // bounded set of real run lengths to observe a poll inside this
            // short proof, rather than adding a production phase callback.
            for (var count = 16; count < 80 && !reached; count++)
            {
                now = 0;
                using var token = new CancellationTokenSource();
                var account = new Account(); Assert.True(account.TryReserve(17));
                var retained = new Utf8ValueMatch(true, true, 0, 1, 0, 1);
                var output = Enumerable.Repeat(retained, regex.GroupCount + 1).ToArray();
                var input = Encoding.ASCII.GetBytes(new string('a', count) + "!#");
                var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
                {
                    var frames = new StackTrace().GetFrames();
                    if (reached || !frames.Any(frame =>
                        frame.GetMethod() is { DeclaringType: { } type } method &&
                        method.Name == (parent ? "FindReachableBoundaryCeiling" : "FindBoundaryCeiling") &&
                        type == typeof(PosixWorkspace)) || parent && !frames.Any(frame =>
                        frame.GetMethod() is { Name: "FindBoundaryBoundedParentTag" })) return;
                    reached = true;
                    if (deadline) now = 101; else token.Cancel();
                });
                Exception? error;
                try { error = Record.Exception(() => regex.Match(input, output, 0, token.Token, account)); }
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
            Assert.True(reached, "The injection must reach the actual whole/parent boundary proof.");
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
        var regex = new Utf8PosixRegex(pattern, default, deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan, default);
        using var cursor = regex.CreateMatchCursor("aa"u8.ToArray(), cancellationToken: token.Token, workingMemoryAllowance: account);
        Assert.True(cursor.MoveNext(out var retained));
        if (deadline) now = 101; else token.Cancel();
        var output = new[] { retained, retained };
        if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(output, out _));
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(output, out _)).CancellationToken);
        Assert.All(output, match => Assert.False(match.Success)); Assert.Equal(0UL, account.Used);
        Assert.True(retained.Success); Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void PublicPoliciesRefuseBeforeCoveredAcquisitionAndDoNotRetry()
    {
        var regex = new Utf8PosixRegex("(a|aa)*", default, Timeout.InfiniteTimeSpan, new() { MaxWorkingBytes = 1 });
        var account = new Account();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch("a"u8, workingMemoryAllowance: account)).ResourceKind);
        Assert.Equal(0, account.Attempts);
        var normal = new Utf8PosixRegex("a|aa"); var refused = new Account { Maximum = 0 };
        Assert.Throws<Utf8MemoryAdmissionException>(() => normal.Count("a"u8, workingMemoryAllowance: refused));
        Assert.Equal(1, refused.Attempts); Assert.Equal(0UL, refused.Used);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => normal.IsMatch("a"u8, canceled.Token, account)).CancellationToken);
        Assert.Equal(0, account.Attempts);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a", default, Timeout.InfiniteTimeSpan, new() { MaxResultBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("a", default, TimeSpan.Zero, default));
    }

    [Fact]
    public void CompilationUsesOriginalCancellationDeadlineAndInstructionAdmission()
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => Utf8PosixRegex.Compile("a", default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, token.Token)).CancellationToken);
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8PosixRegex.Compile("(a|aa)*", default,
                Timeout.InfiniteTimeSpan, default, new() { MaxInstructionCapacity = 1 }, Timeout.InfiniteTimeSpan, default)).ResourceKind);
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now++ == 0 ? 0 : 101);
        Assert.Throws<RegexMatchTimeoutException>(() => Utf8PosixRegex.Compile("a", default,
            Timeout.InfiniteTimeSpan, default, default, TimeSpan.FromMilliseconds(100), default));
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
        var regex = new Utf8PosixRegex(pattern); var groups = new Utf8ValueMatch[regex.GroupCount + 1];
        Assert.True(regex.Match(Encoding.UTF8.GetBytes(input), groups).Success);
        Assert.Equal(start >= 0, groups[slot].Success);
        if (start >= 0) { Assert.Equal(start, groups[slot].IndexInBytes); Assert.Equal(length, groups[slot].LengthInBytes); }
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
        var captures = new Utf8ValueMatch[2];
        Assert.True(cursor.MoveNext(captures, out var retained));
        Assert.Equal(4 * count, retained.LengthInBytes);
        Assert.Equal(2 + 4 * (count - 1), captures[1].IndexInBytes);
        Assert.Equal(4, captures[1].LengthInBytes);
        Assert.True(alias.MoveNext(captures, out var empty));
        Assert.Equal(2 + 4 * count, empty.IndexInBytes);
        Assert.Equal(0, empty.LengthInBytes);
        Assert.False(alias.MoveNext(captures, out _));
        alias.Dispose();
        Assert.Equal(0UL, account.Used);
        Assert.Equal(4 * count, retained.LengthInBytes);
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
        var output = new Utf8ValueMatch[4];
        var whole = regex.Match(input, output, floor);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, count * atom.Length, floor, count * width), whole);
        Assert.Equal(whole, output[0]);
        if (count == 0) { Assert.All(output[1..], capture => Assert.False(capture.Success)); return; }
        var last = (count & 1) == 0 ? 2 : 1;
        var expected = new Utf8ValueMatch(true, true, prefix.Length + (count - last) * atom.Length,
            last * atom.Length, floor + (count - last) * width, last * width);
        Assert.Equal(expected, output[1]);
        Assert.Equal(expected, output[last == 1 ? 2 : 3]);
        Assert.False(output[last == 1 ? 3 : 2].Success);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void RequiredNullableIterationsKeepOnlyNecessaryEmptyTails(int count)
    {
        // Earlier occurrences consume the available atoms. The two required
        // iterations force a final empty capture only when fewer than two
        // atoms exist; optional empty iterations lose to a shorter history.
        var regex = new Utf8PosixRegex("(a?){2,4}"); var groups = new Utf8ValueMatch[2];
        Assert.Equal(count, regex.Match(Encoding.ASCII.GetBytes(new string('a', count)), groups).LengthInBytes);
        var length = count >= 2 ? 1 : 0; var start = count - length;
        Assert.Equal(new Utf8ValueMatch(true, true, start, length, start, length), groups[1]);
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
            var posix = new Utf8PosixRegex(pattern, default, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 });
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
                Assert.Equal(expectedStart, actual.IndexInBytes); Assert.Equal(expectedEnd - expectedStart, actual.LengthInBytes);
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
        var output = new Utf8ValueMatch[2];
        var input = Encoding.ASCII.GetBytes("aaaaaa" + (pattern.EndsWith('b') ? "b" : ""));
        Assert.True(regex.Match(input, output, workingMemoryAllowance: granted).Success);
        Assert.True(granted.Attempts > 2); Assert.Equal(0UL, granted.Used);
        for (var admitted = 0; admitted < granted.Attempts; admitted++)
        {
            var account = new Account { AllowedAttempts = admitted };
            Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Match(input, output, workingMemoryAllowance: account));
            Assert.Equal(admitted + 1, account.Attempts); Assert.Equal(0UL, account.Used);
            Assert.All(output, match => Assert.False(match.Success));
        }
    }

    [Fact]
    public void CursorWorkIsCumulativeAcrossAdvances()
    {
        var account = new Account();
        var regex = new Utf8PosixRegex("a", default, Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 256 });
        using var cursor = regex.CreateMatchCursor(Encoding.ASCII.GetBytes(new string('a', 128)), workingMemoryAllowance: account);
        var hits = 0;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        { while (cursor.MoveNext(out _)) hits++; });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.InRange(hits, 1, 127); Assert.Equal(0UL, account.Used);
        Assert.True(regex.IsMatch("a"u8));
    }

    [Fact]
    public void NumericWorkingAdmissionIncludesGrowthOverlap()
    {
        var broad = new Utf8PosixRegex("(a|aa)*"); var measured = new Account();
        Assert.True(broad.Match("aaaaaa"u8, workingMemoryAllowance: measured).Success);
        var capped = new Utf8PosixRegex("(a|aa)*", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkingBytes = measured.Peak - 1 });
        var loan = new Account();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => capped.Match("aaaaaa"u8, workingMemoryAllowance: loan)).ResourceKind);
        Assert.Equal(0UL, loan.Used);
    }

    [Theory]
    [InlineData("IsMatch", false)] [InlineData("IsMatch", true)]
    [InlineData("Match", false)] [InlineData("Match", true)]
    [InlineData("Count", false)] [InlineData("Count", true)]
    [InlineData("Cursor", false)] [InlineData("Cursor", true)]
    public void FullValidationObservesOriginalInterruptionBeforeTheNextWorkRefusal(string operation, bool deadline)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var token = new CancellationTokenSource(); var account = new Account();
        var regex = new Utf8PosixRegex("a", default, deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 64 });
        var input = Encoding.ASCII.GetBytes("a" + new string('x', 1_000_000)); var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => { triggered = true; if (deadline) now = 101; else token.Cancel(); });
        try
        {
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(Call);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(Call).CancellationToken);
            Assert.True(triggered); Assert.Equal(0, account.Attempts); Assert.Equal(0UL, account.Used);
        }
        finally { diagnostics.Complete(); }
        void Call()
        {
            switch (operation)
            {
                case "IsMatch": regex.IsMatch(input, token.Token, account); break;
                case "Match": regex.Match(input, cancellationToken: token.Token, workingMemoryAllowance: account); break;
                case "Count": regex.Count(input, cancellationToken: token.Token, workingMemoryAllowance: account); break;
                default: using (regex.CreateMatchCursor(input, cancellationToken: token.Token, workingMemoryAllowance: account)) { } break;
            }
        }
    }

    private static void Check(Utf8ValueMatch actual, JsonElement expected)
    {
        var success = expected.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.Equal(success, actual.Success);
        if (!success) return;
        Assert.True(actual.IsByteAligned);
        Assert.Equal(expected[0].GetInt32(), actual.IndexInBytes); Assert.Equal(expected[1].GetInt32(), actual.LengthInBytes);
        Assert.Equal(expected[2].GetInt32(), actual.IndexInUtf16); Assert.Equal(expected[3].GetInt32(), actual.LengthInUtf16);
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
