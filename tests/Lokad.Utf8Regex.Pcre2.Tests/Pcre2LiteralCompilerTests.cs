using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralCompilerTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("café")]
    [InlineData("😀")]
    public void LiteralProgramsKeepCanonicalGroupZeroWithoutRetainingCore(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var program = regex.DebugCompiledProgram;
        Assert.Null(program.PrimaryUtf8);
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(pattern));
        Assert.True(match.Success);
        Assert.Equal(1, match.CaptureSlotCount);
        Assert.Equal(pattern, match.GetGroup(0).GetValueString());
        Assert.Equal(0, regex.NameEntryCount);
        Assert.Equal(0, match.NameEntryCount);
        Assert.All(new[] { program.Operations.IsMatch, program.Operations.Count, program.Operations.Match,
            program.Operations.Enumerate, program.Operations.Replace },
            operation => Assert.Same(program.Operations.Match, operation));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void PublicLiteralConstructionStillEnforcesMajorCaps(int kind, bool bytes)
    {
        var policy = kind switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 },
        };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = Utf8Pcre2Regex.Compile("a😀b"u8, Pcre2CompileOptions.None, default,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy,
                Timeout.InfiniteTimeSpan, CancellationToken.None);
            else _ = Utf8Pcre2Regex.Compile("a😀b", Pcre2CompileOptions.None, default,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, policy,
                Timeout.InfiniteTimeSpan, CancellationToken.None);
        });
        Assert.Equal(kind switch
        {
            0 => Utf8ResourceLimitKind.ConstructionWorkSteps,
            1 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes,
        }, failure.ResourceKind);
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.Equal(1, new Utf8Pcre2Regex("a😀b").Count("a😀b"u8));
    }

    [Fact]
    public void LiteralByteDenialPrecedesItsFirstMajorAllocation()
    {
        using var owner = new Utf8ConstructionScope(1000, 1, 100000, 16);
        var request = new Pcre2CompileRequest("abc", Pcre2CompileOptions.None, default,
            default, Timeout.InfiniteTimeSpan);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2LiteralCompiler.Compile(request));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.True(owner.Budget.WorkSteps > 0);
    }

    [Theory]
    [InlineData("abc", "xxabczz", 0, 2, 5, 2, 5)]
    [InlineData("café", "--café--", 0, 2, 7, 2, 6)]
    [InlineData("😀", "a😀b", 0, 1, 5, 1, 3)]
    [InlineData("", "a😀b", 1, 1, 1, 1, 1)]
    public void LiteralOneShotOperationsPreserveByteAndUtf16Coordinates(
        string pattern,
        string inputText,
        int startOffsetInBytes,
        int expectedByteStart,
        int expectedByteEnd,
        int expectedUtf16Start,
        int expectedUtf16End)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(inputText);

        Assert.True(regex.IsMatch(input, startOffsetInBytes));

        var match = regex.Match(input, startOffsetInBytes);
        Assert.True(match.Success);
        Assert.Equal(expectedByteStart, match.StartOffsetInBytes);
        Assert.Equal(expectedByteEnd, match.EndOffsetInBytes);
        Assert.Equal(expectedUtf16Start, match.StartOffsetInUtf16);
        Assert.Equal(expectedUtf16End, match.EndOffsetInUtf16);

        var detailed = regex.MatchDetailed(input, startOffsetInBytes);
        Assert.True(detailed.Success);
        Assert.Equal(1, detailed.CaptureSlotCount);
        Assert.Equal(expectedByteStart, detailed.Value.StartOffsetInBytes);
        Assert.Equal(expectedByteEnd, detailed.Value.EndOffsetInBytes);
        Assert.Equal(expectedUtf16Start, detailed.Value.StartOffsetInUtf16);
        Assert.Equal(expectedUtf16End, detailed.Value.EndOffsetInUtf16);
    }

    [Theory]
    [InlineData(@"a\.b", "--a.b--", "a.b")]
    [InlineData(@"\[x\]", "[x]", "[x]")]
    [InlineData(@"a\|b", "a|b", "a|b")]
    [InlineData(@"\\", "--\\--", "\\")]
    public void EscapedMetacharactersCompileAsLiteralAtoms(string pattern, string inputText, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void LiteralAnchorsDistinguishAbsoluteAndRequestedStartPositions()
    {
        var absolute = new Utf8Pcre2Regex(@"\Aabc");
        Assert.True(absolute.IsMatch("abc abc"u8));
        Assert.False(absolute.IsMatch("abc abc"u8, 1));
        Assert.False(absolute.IsMatch("xabc"u8));

        var requested = new Utf8Pcre2Regex(@"\Gabc");
        Assert.True(requested.IsMatch("xabc"u8, 1));
        Assert.False(requested.IsMatch("xxabc"u8, 1));

        var absoluteEnd = new Utf8Pcre2Regex(@"abc\z");
        var endMatch = absoluteEnd.Match("abc xx abc"u8, 1);
        Assert.True(endMatch.Success);
        Assert.Equal(7, endMatch.StartOffsetInBytes);
        Assert.False(absoluteEnd.IsMatch("abc xx abc\n"u8));

        var whole = new Utf8Pcre2Regex(@"\Aabc\z");
        Assert.True(whole.IsMatch("abc"u8));
        Assert.False(whole.IsMatch("xabc"u8));
        Assert.False(whole.IsMatch("abcx"u8));
    }

    [Fact]
    public void CompileAndMatchAnchoringOptionsAreAppliedByLiteralProgram()
    {
        var compileAnchored = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.Anchored | Pcre2CompileOptions.EndAnchored);
        Assert.True(compileAnchored.IsMatch("xabc"u8, 1));
        Assert.False(compileAnchored.IsMatch("xxabc"u8, 1));

        var regex = new Utf8Pcre2Regex("abc");
        Assert.True(regex.IsMatch("xabc"u8, 1, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored));
        Assert.False(regex.IsMatch("xxabc"u8, 1, Pcre2MatchOptions.Anchored));
    }

    [Fact]
    public void EmptyLiteralHonorsNotEmptyMatchOptionsAndScalarProgression()
    {
        var regex = new Utf8Pcre2Regex(string.Empty);
        var input = "😀x"u8;

        Assert.False(regex.IsMatch(input, 0, Pcre2MatchOptions.NotEmpty));
        var afterFirstScalar = regex.Match(input, 0, Pcre2MatchOptions.NotEmptyAtStart);
        Assert.True(afterFirstScalar.Success);
        Assert.Equal(4, afterFirstScalar.StartOffsetInBytes);
        Assert.Equal(2, afterFirstScalar.StartOffsetInUtf16);
        Assert.False(regex.IsMatch(input, input.Length, Pcre2MatchOptions.NotEmptyAtStart));
    }

    [Fact]
    public void Utf8PatternConstructorRejectsMalformedInputBeforeDecoding()
    {
        var malformed = new byte[] { (byte)'a', 0xC3, 0x28 };

        var exception = Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(malformed));
        Assert.Contains("byte offset 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AlternationUsesGenericBacktrackingSyntaxPath()
    {
        var regex = new Utf8Pcre2Regex("abc|def");

        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.True(regex.IsMatch("--def--"u8));
    }

    [Fact]
    public void LiteralSearchHonorsConfiguredMatchWorkLimit()
    {
        var regex = new Utf8Pcre2Regex(
            "needle",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 },
            Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(new string('x', 16_384));

        var exception = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    [Fact]
    public void LiteralIsMatchAndValueMatchAreAllocationFreeAfterWarmup()
    {
        var regex = new Utf8Pcre2Regex("needle");
        var input = Encoding.UTF8.GetBytes(new string('x', 256) + "needle");
        _ = regex.IsMatch(input);
        _ = regex.Match(input).Success;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            Assert.True(regex.IsMatch(input));
            Assert.True(regex.Match(input).Success);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
