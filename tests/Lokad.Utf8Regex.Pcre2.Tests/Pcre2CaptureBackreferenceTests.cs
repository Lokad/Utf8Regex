using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CaptureBackreferenceTests
{
    [Fact]
    public void PrimaryMatchWritesNestedCapturesFromByteCoordinates()
    {
        var regex = new Utf8Pcre2Regex("(é)(😀)");
        var input = Encoding.UTF8.GetBytes("xé😀y");
        ReadOnlySpan<byte> match1Input = input;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var match = regex.Match(match1Input, captures: match1Captures);

        Assert.True(match.Success);
        Assert.Equal(3, (match.Success ? match1Captures.Length : 0));
        AssertGroup(match1Captures[0], match1Input, "é😀", 1, 7);
        AssertGroup(match1Captures[1], match1Input, "é", 1, 3);
        AssertGroup(match1Captures[2], match1Input, "😀", 3, 7);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void PrimaryMatchWritesLargeCaptureSets()
    {
        const int explicitCaptureCount = 40;
        var regex = new Utf8Pcre2Regex(string.Concat(Enumerable.Repeat("(a)", explicitCaptureCount)));
        var text = new string('a', explicitCaptureCount);
        ReadOnlySpan<byte> match2Input = Encoding.UTF8.GetBytes(text);
        var match2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match2Input, captures: match2Captures);

        Assert.True(match.Success);
        Assert.Equal(explicitCaptureCount + 1, (match.Success ? match2Captures.Length : 0));
        AssertGroup(match2Captures[0], match2Input, text, 0, explicitCaptureCount);
        for (var slot = 1; slot <= explicitCaptureCount; slot++)
        {
            AssertGroup(match2Captures[slot], match2Input, "a", slot - 1, slot);
        }
    }

    [Fact]
    public void PrimaryCaptureMatchingSupportsConcurrentReuse()
    {
        const int explicitCaptureCount = 40;
        var regex = new Utf8Pcre2Regex(string.Concat(Enumerable.Repeat("(a)", explicitCaptureCount)));
        var input = Encoding.UTF8.GetBytes(new string('a', explicitCaptureCount));
        var failures = 0;

        Parallel.For(0, 256, _ =>
        {
            ReadOnlySpan<byte> match3Input = input;
            var match3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var match = regex.Match(match3Input, captures: match3Captures);
            var last = match3Captures[explicitCaptureCount];
            if (!match.Success ||
                (match.Success ? match3Captures.Length : 0) != explicitCaptureCount + 1 ||
                !last.Success ||
                last.StartOffsetInBytes != explicitCaptureCount - 1 ||
                last.EndOffsetInBytes != explicitCaptureCount)
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.Equal(0, failures);
    }

    // R03 backreference slice: "(a|ab)\1" first-match now rides the shared
    // slice runner (see Pcre2SliceBackrefTests); the remaining rows stay on
    // the generic backtracking VM.
    [Theory]
    [InlineData("(a)\\g{1}", "aa", "aa")]
    [InlineData("(a)\\g{-1}", "aa", "aa")]
    [InlineData("(?<x>a)\\k<x>", "aa", "aa")]
    [InlineData("(?'x'a)\\k'x'", "aa", "aa")]
    [InlineData("(?P<x>a)(?P=x)", "aa", "aa")]
    public void NumericRelativeAndNamedBackreferencesUseOwnedPrograms(
        string pattern,
        string input,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        if (pattern.StartsWith("(?<", StringComparison.Ordinal) || pattern.StartsWith("(?'", StringComparison.Ordinal))
            Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        else
            Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("(a|b\\1)+", "aba")]
    [InlineData("(\\2a|(b))+", "bba")]
    [InlineData("(\\g{+1}a|(b))+", "bba")]
    [InlineData("(?<outer>\\k<later>a|(?<later>b))+", "bba")]
    public void RepeatedGroupsExposeTheirLastCompletedValueToPermittedForwardReferences(
        string pattern,
        string input)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(input, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Theory]
    [InlineData("(a)\\11", "a\t")]
    [InlineData("(a)\\40", "a ")]
    [InlineData("(a)\\1134", "aK4")]
    public void AmbiguousLongDigitEscapesFollowPcre2BackreferenceThenOctalRules(
        string pattern,
        string input)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void CaptureJournalRestoresUnsetAndFinalRepeatedCaptures()
    {
        var rollback4Regex = new Utf8Pcre2Regex("(?:(a)c|(b))\\2");
        ReadOnlySpan<byte> rollback4Input = "bb"u8;
        var rollback4Captures = new Utf8Pcre2CaptureRange[rollback4Regex.CaptureGroupCount + 1];
        var rollback = rollback4Regex.Match(rollback4Input, captures: rollback4Captures);
        var repeated5Regex = new Utf8Pcre2Regex("(?<x>a|b)+");
        ReadOnlySpan<byte> repeated5Input = "ab"u8;
        var repeated5Captures = new Utf8Pcre2CaptureRange[repeated5Regex.CaptureGroupCount + 1];
        var repeated = repeated5Regex.Match(repeated5Input, captures: repeated5Captures);

        Assert.True(rollback.Success);
        Assert.False(rollback4Captures[1].Success);
        AssertGroup(rollback4Captures[2], rollback4Input, "b", 0, 1);
        Assert.True(repeated.Success);
        AssertGroup(repeated5Captures[1], repeated5Input, "b", 1, 2);
    }

    [Fact]
    public void UnsetBackreferenceFailsAndCaselessUnicodeBackreferenceMatches()
    {
        Assert.False(new Utf8Pcre2Regex("(a)?b\\1").IsMatch("b"u8));

        var regex = new Utf8Pcre2Regex("(é)\\1", options: Pcre2CompileOptions.Caseless);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("Éé")));
    }

    [Fact]
    public void NamedCaptureMetadataComesFromTheGenericCompiler()
    {
        var regex = new Utf8Pcre2Regex("(?P<word>é+)-(?P=word)");
        ReadOnlyMemory<byte> match6Input = Encoding.UTF8.GetBytes("éé-éé");
        using var match6Cursor = regex.CreateMatchCursor(match6Input);
        match6Cursor.MoveNext(out var match6Result);
        match6Cursor.Dispose();
        var match = match6Result;
        var nameBytes = new byte[4];
        Span<int> numbers = stackalloc int[1];

        Assert.True(match.Success);
        Assert.Equal(1, regex.NameEntryCount);
        Assert.Equal(System.Buffers.OperationStatus.Done,
            regex.TryCopyNameEntryUtf8(0, nameBytes, out var bytesRequired, out var groupNumber));
        Assert.Equal("word"u8.ToArray(), nameBytes);
        Assert.Equal(4, bytesRequired);
        Assert.Equal(1, groupNumber);
        Assert.Equal(1, regex.CopyNumbersForUtf8Name("word"u8, numbers, out var hasMoreNumbers));
        Assert.False(hasMoreNumbers);
        Assert.Equal(1, numbers[0]);
        Assert.True(match.TryGetGroup(1, out var word)); Assert.True(word.Success);
        Assert.Equal("éé", System.Text.Encoding.UTF8.GetString((word.Success ? match6Input.Span.Slice(word.StartOffsetInBytes, word.EndOffsetInBytes - word.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("名")]
    [InlineData("𐐀2")]
    public void UtfModeCaptureNamesAcceptUnicodeLettersAndDecimalDigits(string name)
    {
        var regex = new Utf8Pcre2Regex($"(?<{name}>é)\\k<{name}>");
        ReadOnlyMemory<byte> match7Input = Encoding.UTF8.GetBytes("éé");
        using var match7Cursor = regex.CreateMatchCursor(match7Input);
        match7Cursor.MoveNext(out var match7Result);
        match7Cursor.Dispose();
        var match = match7Result;

        Assert.True(match.Success);
        Span<int> numbersByName = stackalloc int[1];
        Assert.Equal(1, regex.CopyNumbersForUtf8Name(Encoding.UTF8.GetBytes(name), numbersByName, out var moreByName));
        Assert.False(moreByName);
        Assert.Equal(1, numbersByName[0]);
        Assert.True(match.TryGetGroup(numbersByName[0], out var capture)); Assert.True(capture.Success);
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString((capture.Success ? match7Input.Span.Slice(capture.StartOffsetInBytes, capture.EndOffsetInBytes - capture.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void CaptureReplacementSupportsNumericAndNamedTemplates()
    {
        var regex = new Utf8Pcre2Regex("(?<left>a)(?<right>b)");
        var input = "ab ab"u8;

        Assert.Equal("b-a b-a", Encoding.UTF8.GetString(regex.Replace(input, "${right}-$1")));

        var numericResult = regex.Replace(input, "$2/$1");
        Assert.Equal("b/a b/a", Encoding.UTF8.GetString(numericResult));
    }

    [Fact]
    public void GlobalCapturePatternUsesGroupZeroCursorWithoutMaterializingCaptures()
    {
        var regex = new Utf8Pcre2Regex("(a|ab)\\1");
        var input = "aa abab aa"u8;

        Assert.Equal(3, regex.Count(input));
        Assert.Equal("<aa> <abab> <aa>", Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));
    }

    [Fact]
    public void WarmedBackreferenceIsMatchReusesCaptureState()
    {
        // R03 backreference slice: "(ab|a)+\1z" IsMatch now rides the shared
        // slice runner (which rents ledger-charged slots per call), so the
        // warmed zero-allocation pin moves to a backreference shape that
        // stays on the generic backtracking VM (octal-disambiguated "\11").
        var regex = new Utf8Pcre2Regex("(a)\\11");
        var input = "a\x09"u8;
        for (var index = 0; index < 32; index++)
        {
            _ = regex.IsMatch(input);
        }

        for (var index = 0; index < 256; index++)
        {
            _ = regex.IsMatch(input);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
        {
            _ = regex.IsMatch(input);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertGroup(
        Utf8Pcre2CaptureRange group, ReadOnlySpan<byte> input, string value, int startInBytes, int endInBytes)
    {
        Assert.True(group.Success);
        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(input[group.StartOffsetInBytes..group.EndOffsetInBytes]));
        Assert.Equal(startInBytes, group.StartOffsetInBytes);
        Assert.Equal(endInBytes, group.EndOffsetInBytes);
    }
}
