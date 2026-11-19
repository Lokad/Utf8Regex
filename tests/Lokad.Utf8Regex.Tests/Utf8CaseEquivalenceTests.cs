using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaseEquivalenceTests
{
    [Fact]
    public void ClosedAsciiCaseFibersUseOrdinaryMasksAndKeepCaptureRanges()
    {
        using var regex = Utf8Regex.Compile("^#?(?<digits>[a-f0-9]{6}|[a-f0-9]{3})$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var instructions = regex.Inspection.AvailableOwnedSliceProgram.Instructions;
        Assert.Contains(instructions, instruction => instruction.Kind == Utf8SliceInstructionKind.Set);
        Assert.DoesNotContain(instructions, instruction => instruction is { Kind: Utf8SliceInstructionKind.Set, C: not 0 });

        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];
        Assert.Equal(new Utf8CaptureRange(0, 4), regex.Match("#Af1\n"u8, captures: captures));
        Assert.Equal(new Utf8CaptureRange(1, 4), captures[1]);
        Assert.False(regex.Match("#AK1"u8, captures: captures).Success);
        Assert.False(captures[1].Success);
    }

    [Theory]
    [InlineData("^([a-f0-9]{3}|[a-f0-9]{6})$")]
    [InlineData("^([a-f0-9]{6}|[a-f0-9]{3})$")]
    public void RepeatWidthRulesOutEitherArmBeforeAcquiringAFrame(string pattern)
    {
        var policy = new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkingBytes = 1 } };
        using var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase, policy);

        Assert.Equal(new Utf8CaptureRange(0, 6), regex.Match("AF01BC"u8));
        Assert.Equal(new Utf8CaptureRange(0, 6), regex.Match("AF01BC\n"u8));
        Assert.Equal(new Utf8CaptureRange(0, 3), regex.Match("AF1"u8));
        Assert.False(regex.Match("AF01BC\n\n"u8).Success);
    }

    [Fact]
    public void CaseInertOptionalPrefixDoesNotRequireARepeatFrame()
    {
        var policy = new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkingBytes = 1 } };
        using var regex = new Utf8Regex("^#?([a-f0-9]{6}|[a-f0-9]{3})$", RegexOptions.IgnoreCase, policy);

        Assert.Equal(new Utf8CaptureRange(0, 7), regex.Match("#AF01BC"u8));
        Assert.Equal(new Utf8CaptureRange(0, 7), regex.Match("#AF01BC\n"u8));
        Assert.Equal(new Utf8CaptureRange(0, 6), regex.Match("AF01BC"u8));
        Assert.Equal(new Utf8CaptureRange(0, 4), regex.Match("#AF1\n"u8));
    }

    [Fact]
    public void OptionalPrefixCanBacktrackWhenItsSuffixAcceptsTheSameByte()
    {
        using var regex = new Utf8Regex("^#?(?<value>[#a-f]{3})$", RegexOptions.IgnoreCase);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];

        Assert.Equal(new Utf8CaptureRange(0, 3), regex.Match("#Af"u8, captures: captures));
        Assert.Equal(captures[0], captures[1]);
        Assert.Equal(new Utf8CaptureRange(0, 4), regex.Match("##Af"u8, captures: captures));
        Assert.Equal(new Utf8CaptureRange(1, 4), captures[1]);
    }

    [Fact]
    public void FoldedAsciiFamilyKeepsItsBytePlanAndVerifiesKelvinSubjects()
    {
        const string pattern = "kat|abc";
        var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        using var regex = Utf8Regex.Compile(pattern, options);
        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(2, regex.Count("KAT abc"u8));
        Assert.Equal(2, regex.Count("Kat ABC"u8));
        Assert.Equal(5, regex.Match("Kat"u8).EndOffsetInBytes);
    }

    [Theory]
    [InlineData(@"[k]at")]
    [InlineData(@"\x6Bat")]
    [InlineData(@"\153at")]
    public void FoldedBracketAndEscapedLiteralsIncludeKelvin(string pattern)
    {
        foreach (var engine in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            using var regex = Utf8Regex.Compile(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | engine);
            var program = regex.Inspection.AvailableOwnedSliceProgram;
            Assert.Empty(program.CategorySetMembers);
            Assert.Contains(program.Instructions, instruction => instruction is { Kind: Utf8SliceInstructionKind.Set, C: 1 });
            Assert.Equal(2, regex.Count("Kat KAT"u8));
            Assert.Equal(5, regex.Match("Kat"u8).EndOffsetInBytes);
        }
    }

    [Theory]
    [InlineData("[^k]at")]
    [InlineData("[ak-[k]]at")]
    public void FoldedMaskPreservesNegationAndSubtraction(string pattern)
    {
        using var regex = Utf8Regex.Compile(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.False(regex.IsMatch("Kat KAT"u8));
        var match = regex.Match("Kat Aat"u8);
        Assert.True(match.Success);
        Assert.Equal(6, match.StartOffsetInBytes);
        Assert.Equal(9, match.EndOffsetInBytes);
    }

    [Theory]
    [InlineData("en-US", 3)]
    [InlineData("tr-TR", 2)]
    public void FoldedMaskUsesTheProgramsCaseBehavior(string culture, int count)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var regex = Utf8Regex.Compile("[I]", RegexOptions.IgnoreCase);
            Assert.Empty(regex.Inspection.AvailableOwnedSliceProgram.CategorySetMembers);
            Assert.Equal(count, regex.Count("I ı i İ"u8));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("\u1C89", "\u1C8A")]
    [InlineData("\u0264", "\uA7CB")]
    [InlineData("\uA7CC", "\uA7CD")]
    [InlineData("\u019B", "\uA7DC")]
    [InlineData("\uA7DA", "\uA7DB")]
    [InlineData("\u00DF", "\u1E9E")]
    [InlineData("\u01C5", "\u01C6")]
    [InlineData("\u00C5", "\u212B")]
    public void Unicode16MatchingUsesCompleteScalarPairs(string left, string right)
    {
        foreach (var (pattern, subject) in new[] { (left, right), (right, left) })
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
                (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var input = Encoding.UTF8.GetBytes(subject);
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.Equal(input.Length, match.EndOffsetInBytes - match.StartOffsetInBytes);
            Assert.Equal(1, regex.Count(input));
        }
    }

    [Theory]
    [InlineData("\uA7CE", "\uA7CF")]
    [InlineData("\uA7D2", "\uA7D3")]
    [InlineData("\uA7D4", "\uA7D5")]
    public void NewerHostCasingDoesNotChangeOwnedUnicode16Matching(string left, string right)
    {
        var flags = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        Assert.False(new Utf8Regex(left, options: flags).IsMatch(Encoding.UTF8.GetBytes(right)));
        Assert.False(new Utf8Regex(right, options: flags).IsMatch(Encoding.UTF8.GetBytes(left)));
    }
}
