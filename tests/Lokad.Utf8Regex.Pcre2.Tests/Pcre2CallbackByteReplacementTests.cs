using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CallbackByteReplacementTests
{
    [Theory]
    [InlineData("x", "xx", "��")]
    [InlineData("(x)", "xx", "��")]
    [InlineData("(?<=x)(|y)", "xy", "x��")]
    public void StringReturnsCannotJoinSurrogateHalvesAcrossMatches(string pattern, string input, string expected)
    {
        var actual = new Utf8Pcre2Regex(pattern).ReplaceToString(Encoding.UTF8.GetBytes(input), 0,
            static (in Utf8Pcre2MatchContext match, ref int state) => new string(state++ == 0 ? (char)0xd83d : (char)0xde00, 1));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CallbackExpansionHasOneNativeByteProducer()
    {
        var type = typeof(Utf8Pcre2Regex).Assembly.GetType("Lokad.Utf8Regex.Pcre2.Utf8Pcre2Regex+Pcre2CallbackReplacementOutput`2");
        Assert.NotNull(type);
        Assert.True(type.IsByRefLike);
    }

    [Theory]
    [InlineData("x", "αx😀xβ", "α[2:1]😀[7:1]β")]
    [InlineData("[x]", "αx😀xβ", "α[2:1]😀[7:1]β")]
    [InlineData("(?<w>😀)", "α😀β😀", "α[2:4]β[8:4]")]
    [InlineData("foo(?<Bar>BAR)?", "αfooBAR😀fooβ", "α[2:6]😀[12:3]β")]
    [InlineData("(?<=abc)(|def)", "abcdefabc😀", "abc[3:0][3:3]abc[9:0]😀")]
    public void BothCallbackFlavorsPreserveByteRangesAndEmptyRetry(string pattern, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        if (pattern == "foo(?<Bar>BAR)?") Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        if (pattern == "(?<=abc)(|def)") Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        var bytes = Encoding.UTF8.GetBytes(input);
        var actual = regex.Replace(bytes, 0, static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            state++;
            writer.Append($"[{match.Value.StartOffsetInBytes}:{match.Value.EndOffsetInBytes-match.Value.StartOffsetInBytes}]".AsSpan());
        });
        Assert.Equal(Encoding.UTF8.GetBytes(expected), actual);
        Assert.Equal(expected, regex.ReplaceToString(bytes, 0, static (in Utf8Pcre2MatchContext match, ref int state) =>
        {
            state++;
            return $"[{match.Value.StartOffsetInBytes}:{match.Value.EndOffsetInBytes-match.Value.StartOffsetInBytes}]";
        }));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("(x)")]
    [InlineData("foo(?<Bar>BAR)?")]
    public void WriterAliasesAndResetShareAccumulatedOutput(string pattern)
    {
        var input = pattern.StartsWith("foo") ? "αfoo😀fooβ" : "αx😀xβ";
        var result = new Utf8Pcre2Regex(pattern).Replace(Encoding.UTF8.GetBytes(input), 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                var alias = writer;
                if (state++ == 1)
                {
                    writer = default;
                    alias.Append("discarded"u8);
                    writer.Append("𐐨"u8);
                }
                else alias.Append("first"u8);
            });
        Assert.Equal("𐐨β"u8.ToArray(), result);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void ByteFragmentsAreValidatedAfterTheWholeOutputIsAssembled()
    {
        var actual = new Utf8Pcre2Regex("(x)").Replace("xx"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
                writer.Append(state++ == 0 ? new byte[] { 0xf0,0x9f } : new byte[] { 0x98,0x80 }));
        Assert.Equal("😀"u8.ToArray(), actual);
        Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => writer.Append(new byte[] {0xf0})));
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void ExplicitByteCapturesDoNotRequireUtf16Projection()
    {
        var regex = new Utf8Pcre2Regex(@"(\C)", Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { BackslashC=Pcre2BackslashCPolicy.Allow }, default, Timeout.InfiniteTimeSpan);
        var bytes = regex.Replace("😀"u8, 0, static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            writer.Append(match.GetGroup(1).GetValueBytes()));
        Assert.Equal("😀"u8.ToArray(), bytes);
        Assert.Equal("xxxx", regex.ReplaceToString("😀"u8, 0, static (in Utf8Pcre2MatchContext match, ref int state) => "x"));
    }

    [Fact]
    public void NestedReplacementRestoresOuterOutputAndReleasesAllBuffers()
    {
        var result = new Utf8Pcre2Regex("(x)").Replace("αxβ"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                var outer = Utf8SliceByteOutputScope.Current;
                Assert.NotNull(outer);
                var nested = new Utf8Pcre2Regex("y").ReplaceToString("y"u8, 0,
                    static (in Utf8Pcre2MatchContext match, ref int state) => "😀");
                Assert.Same(outer, Utf8SliceByteOutputScope.Current);
                writer.Append(nested.AsSpan());
            });
        Assert.Equal("α😀β"u8.ToArray(), result);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationInsideTheFinalCallbackAbortsWithoutAResult(bool text)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(x)");
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            if (text) regex.ReplaceToString("x"u8, cancellation,
                static (in Utf8Pcre2MatchContext match, ref CancellationTokenSource state) => {state.Cancel();return "😀";},0,Pcre2MatchOptions.None,cancellation.Token);
            else regex.Replace("x"u8,cancellation,
                static (in Utf8Pcre2MatchContext match,ref Utf8ReplacementWriter writer,ref CancellationTokenSource state) => {writer.Append("😀"u8);state.Cancel();},0,Pcre2MatchOptions.None,cancellation.Token);
        });
        Assert.Equal(cancellation.Token,error.CancellationToken);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void CallbackExceptionsArePreservedAndScopesAreReleased()
    {
        var failure = new InvalidOperationException("callback failure");
        Assert.Same(failure,Assert.Throws<InvalidOperationException>(() => new Utf8Pcre2Regex("(x)").Replace("x"u8,failure,
            static (in Utf8Pcre2MatchContext match,ref Utf8ReplacementWriter writer,ref InvalidOperationException state) => {writer.Append("😀"u8);throw state;})));
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void ResetBuffersKeepTheirLoansUntilAliasesHaveFinished()
    {
        var observation = new Pcre2CallbackOutputObservation();
        var result = new Utf8Pcre2Regex("(x)").Replace("x-x"u8, observation,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref Pcre2CallbackOutputObservation state) =>
            {
                state.Scope = Utf8SliceByteOutputScope.Current;
                Assert.NotNull(state.Scope);
                if (state.Calls++ == 0)
                {
                    Assert.Equal(512ul, state.Scope.Invocation.Ledger.ReservedBytes);
                    var alias = writer;
                    alias.Append("😀"u8);
                    Assert.Equal(528ul, state.Scope.Invocation.Ledger.ReservedBytes);
                    writer = default;
                    writer.Append("𐐨"u8);
                    Assert.Equal(800ul, state.Scope.Invocation.Ledger.ReservedBytes);
                    alias.Append("discarded"u8);
                }
                else
                {
                    Assert.Equal(528ul, state.Scope.Invocation.Ledger.ReservedBytes);
                    writer.Append("𐐨"u8);
                }
            });
        Assert.Equal("𐐨-𐐨"u8.ToArray(), result);
        Assert.Equal(2, observation.Calls);
        Assert.NotNull(observation.Scope);
        Assert.Equal(0ul, observation.Scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    private sealed class Pcre2CallbackOutputObservation
    {
        internal Utf8SliceByteOutputScope? Scope;
        internal int Calls;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OutputDeadlineIsCheckedAfterTheCallback(bool text)
    {
        long clock=10000;
        using var timer=Utf8ExecutionDeadline.UseClockForDiagnostics(()=>clock);
        var regex=new Utf8Pcre2Regex("(x)",Pcre2CompileOptions.None,default,default,TimeSpan.FromMilliseconds(1));
        Action tick=()=>clock+=2;
        var failure=Assert.Throws<Pcre2MatchException>(() =>
        {
            if(text) regex.ReplaceToString("x"u8,tick,static(in Utf8Pcre2MatchContext match,ref Action state)=>{state();return "";});
            else regex.Replace("x"u8,tick,static(in Utf8Pcre2MatchContext match,ref Utf8ReplacementWriter writer,ref Action state)=>state());
        });
        Assert.Equal(Pcre2ErrorKind.Timeout,failure.ErrorKind);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void StringFragmentsPreservePairsAcrossTheBoundedEncodingBlock()
    {
        var fragment=new string('a',2047)+"😀"+new string((char)0xd83d,1);
        Assert.Equal(new string('a',2047)+"😀�",new Utf8Pcre2Regex("(x)").ReplaceToString("x"u8,fragment,
            static(in Utf8Pcre2MatchContext match,ref string state)=>state));
    }

    [Fact]
    public void StartOffsetsAndNoMatchRetainOriginalBytes()
    {
        var regex=new Utf8Pcre2Regex("(?<w>😀)");
        Assert.Equal("😀x!β"u8.ToArray(),regex.Replace("😀x😀β"u8,0,
            static(in Utf8Pcre2MatchContext match,ref Utf8ReplacementWriter writer,ref int state)=>writer.AppendAsciiByte((byte)'!'),5,Pcre2MatchOptions.None));
        Assert.Equal("αβ",regex.ReplaceToString("αβ"u8,0,static(in Utf8Pcre2MatchContext match,ref int state)=>throw new InvalidOperationException("No callback expected.")));
    }

    [Fact]
    public void ExplicitMatchOptionsUseTheDetailedIterationWithByteCoordinates()
    {
        var regex = new Utf8Pcre2Regex("(?<w>😀)");
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        Assert.Equal("[😀][😀]", regex.ReplaceToString("😀😀"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref int state) => $"[{match.GetGroup(1).GetValueString()}]",
            0, Pcre2MatchOptions.Anchored));
        Assert.Equal("[😀][😀]"u8.ToArray(), regex.Replace("😀😀"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                writer.AppendAsciiByte((byte)'[');
                writer.Append(match.GetGroup(1).GetValueBytes());
                writer.AppendAsciiByte((byte)']');
            }, 0, Pcre2MatchOptions.Anchored));
    }
}
