using System.Text;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SliceCursorProjectionTests
{
    private const string Pattern = "(?:ab|cd)+";

    private static Utf8Pcre2Regex Create(string pattern = Pattern, int policy = 0) =>
        new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = policy is 1 or 5 ? 1_000_000UL : null,
                MaxDepth = policy is 2 or 5 ? 1024U : null,
                MaxWorkingBytes = policy is 3 or 5 ? 1_048_576UL : null,
                MaxResultBytes = policy is 4 or 5 ? 1_048_576UL : null
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void CopiesAndRetainedValuesKeepTheirOwnScalarCoordinates(int policy)
    {
        var regex = Create(policy: policy);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        var storage = Encoding.UTF8.GetBytes("guardéabcd😀abβcd");
        var input = storage.AsSpan(5);
        var cursor = regex.EnumerateMatches(input, 2, Pcre2MatchOptions.None);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        AssertRange(retained,2,6);
        AssertRange(cursor.Current,2,6);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        AssertRange(cursor.Current,10,12);
        Assert.True(cursor.MoveNext());
        AssertRange(cursor.Current,14,16);
        Assert.False(cursor.MoveNext());
        AssertRange(retained,2,6);
        AssertRange(alias.Current,2,6);
        Assert.True(alias.MoveNext());
        AssertRange(alias.Current,10,12);


    }

    [Theory]
    [InlineData(0)] [InlineData(5)]
    public void SamePositionEmptyRetryKeepsForwardProjection(int policy)
    {
        var regex = Create("(?:ab|cd)*?", policy);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        var cursor = regex.EnumerateMatches("éabcd😀ab"u8);
        var expected = new (int Start, int End, int Utf16Start, int Utf16End)[]
        {
            (0, 0, 0, 0), (2, 2, 1, 1), (2, 4, 1, 3),
            (4, 4, 3, 3), (4, 6, 3, 5), (6, 6, 5, 5),
            (10, 10, 7, 7), (10, 12, 7, 9), (12, 12, 9, 9),
        };
        foreach (var range in expected)
        {
            Assert.True(cursor.MoveNext());
            AssertRange(cursor.Current,range.Start,range.End);
            AssertRange(cursor.Current,range.Start,range.End);
        }
        Assert.False(cursor.MoveNext());
    }

    [Fact]
    public void BytePublicationDoesNotSpendWorkOnTheUnicodePrefix()
    {
        var regex = Create(policy: 5);
        var program = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        var input = Utf8ValidatedInput.BorrowBytes(Encoding.UTF8.GetBytes(new string('é', 4096) + "ab cd"));
        var cursor = new Pcre2SliceGlobalMatchCursor(program.SliceProgram, input, new(0),
            regex.DebugCompiledProgram.Request, CancellationToken.None);
        Assert.True(cursor.MoveNext());
        var steps = cursor.WorkingInvocation.Budget.Steps;
        var first = cursor.ProjectCurrent();
        var firstProjectionWork = cursor.WorkingInvocation.Budget.Steps - steps;
        Assert.True(cursor.MoveNext());
        steps = cursor.WorkingInvocation.Budget.Steps;
        var second = cursor.ProjectCurrent();
        var nextProjectionWork = cursor.WorkingInvocation.Budget.Steps - steps;
        Assert.Equal(firstProjectionWork, nextProjectionWork);
        Assert.Equal((8192, 8194), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        Assert.Equal((8195, 8197), (second.StartOffsetInBytes, second.EndOffsetInBytes));
        Assert.InRange(nextProjectionWork, 1UL, 2UL);
    }

    [Fact]
    public void CancellationAfterPublicationKeepsOriginalTokenAndRetainedValue()
    {
        using var source = new CancellationTokenSource();
        var cursor = Create(policy: 5).EnumerateMatches("éab😀cd"u8, 0, Pcre2MatchOptions.None, source.Token);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        source.Cancel();
        Exception? failure = null;
        try { cursor.MoveNext(); }
        catch (Exception error) { failure = error; }
        Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        AssertRange(retained,2,4);
        AssertRange(cursor.Current,2,4);
    }

    [Fact]
    public void DeadlineDoesNotRestartAfterPublishingAValue()
    {
        long tick = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => tick);
        var regex = new Utf8Pcre2Regex(Pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var cursor = regex.EnumerateMatches("éab😀cd"u8);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        tick = 2;
        Exception? failure = null;
        try { cursor.MoveNext(); }
        catch (Exception error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        AssertRange(retained,2,4);
        AssertRange(cursor.Current,2,4);
    }

    private static void AssertRange(Utf8Pcre2ValueMatch match,int start,int end)
    {
        Assert.True(match.Success);
        Assert.True(match.IsUtf8SliceWellFormed);
        Assert.Equal((start,end),            (match.StartOffsetInBytes,match.EndOffsetInBytes));
    }
}
