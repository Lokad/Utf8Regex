using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiClassAndBoundaryTests
{
    [Fact]
    public void MembershipAndNegationAreExactForEveryAsciiByte()
    {
        var word = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsWord);
        var notWord = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsWord, negated: true);

        for (var value = 0; value < 128; value++)
        {
            var expected = char.IsAsciiLetterOrDigit((char)value) || value == '_';
            Assert.Equal(expected, word.Contains((byte)value));
            Assert.Equal(!expected, notWord.Contains((byte)value));
        }

        Assert.False(word.Contains(0x80));
        Assert.False(notWord.Contains(0x80));
        Assert.Equal(63, word.Count);
        Assert.Equal(65, notWord.Count);
        Assert.Equal(AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore, word.KnownPredicateKind);
        Assert.Equal(AsciiCharClassPredicateKind.None, notWord.KnownPredicateKind);
    }

    [Fact]
    public void EqualityRangesAndKnownPredicatesUseTheSameCarrier()
    {
        var range = AsciiCharClass.FromRange(Utf8InclusiveByteRange.Create((byte)'0', (byte)'9'));
        var predicate = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsDigit);

        Assert.Equal(range, predicate);
        Assert.True(range.HasSameDefinition(predicate));
        Assert.Equal(AsciiCharClassPredicateKind.Digit, range.KnownPredicateKind);
        Assert.Equal(Enumerable.Range('0', 10).Select(static value => (byte)value), range.GetPositiveMatchBytes());
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8InclusiveByteRange.Create(0x7F, 0x80));
    }

    [Fact]
    public void DotNetProjectionOwnsRegexCharClassDecoding()
    {
        Assert.True(DotNetAsciiCharClassProjector.TryProjectAsciiIntersection(RuntimeFrontEnd.RegexCharClass.DigitClass, out var digits));
        Assert.True(DotNetAsciiCharClassProjector.TryProjectAsciiIntersection(RuntimeFrontEnd.RegexCharClass.SpaceClass, out var spaces));
        Assert.True(DotNetAsciiCharClassProjector.TryProjectWholeClass(RuntimeFrontEnd.RegexCharClass.SpaceClass, out _));
        Assert.True(DotNetAsciiCharClassProjector.TryProjectWholeClass(RuntimeFrontEnd.RegexCharClass.ECMASpaceClass, out var ecmaSpaces));
        Assert.False(DotNetAsciiCharClassProjector.TryProjectWholeClass(RuntimeFrontEnd.RegexCharClass.DigitClass, out _));
        Assert.True(DotNetAsciiCharClassProjector.RequiresAsciiInput(RuntimeFrontEnd.RegexCharClass.SpaceClass));
        Assert.True(DotNetAsciiCharClassProjector.RequiresAsciiInput(RuntimeFrontEnd.RegexCharClass.DigitClass));
        Assert.False(DotNetAsciiCharClassProjector.RequiresAsciiInput(RuntimeFrontEnd.RegexCharClass.ECMASpaceClass));

        for (var value = 0; value < 128; value++)
        {
            Assert.Equal(value is >= '0' and <= '9', digits.Contains((byte)value));
            Assert.Equal(Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value), spaces.Contains((byte)value));
            Assert.Equal(spaces.Contains((byte)value), ecmaSpaces.Contains((byte)value));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("é")]
    [InlineData("aé_b")]
    [InlineData("a😀b")]
    [InlineData("a\u0301b")]
    [InlineData("Ж cat")]
    public void CanonicalBoundaryMatchesDotNetUtf16BoundaryClassification(string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        var byteOffset = 0;
        var utf16Offset = 0;
        AssertBoundary(text, utf8, byteOffset, utf16Offset);

        foreach (var rune in text.EnumerateRunes())
        {
            byteOffset += rune.Utf8SequenceLength;
            utf16Offset += rune.Utf16SequenceLength;
            AssertBoundary(text, utf8, byteOffset, utf16Offset);
        }
    }

    [Fact]
    public void ScalarNeighborsCoverAllWidthsAndRejectNonBoundaries()
    {
        var utf8 = Encoding.UTF8.GetBytes("Aé€😀Z");
        var offsets = new[] { 0, 1, 3, 6, 10, 11 };
        var scalars = new[] { 'A', 'é', '€', 0x1F600, 'Z' };

        for (var index = 0; index < scalars.Length; index++)
        {
            Assert.True(Utf8ScalarNeighbors.TryGetNext(utf8, offsets[index], out var next));
            Assert.Equal(scalars[index], next.Value);
            Assert.True(Utf8ScalarNeighbors.TryGetPrevious(utf8, offsets[index + 1], out var previous));
            Assert.Equal(scalars[index], previous.Value);
        }

        Assert.False(Utf8ScalarNeighbors.TryGetPrevious(utf8, 0, out _));
        Assert.False(Utf8ScalarNeighbors.TryGetNext(utf8, utf8.Length, out _));
        Assert.Throws<InvalidOperationException>(() => Utf8ScalarNeighbors.TryGetNext(utf8, 2, out _));
        Assert.Throws<InvalidOperationException>(() => Utf8ScalarNeighbors.TryGetPrevious(utf8, 2, out _));
    }

    [Fact]
    public void AsciiBoundaryFastPathDeclinesUnicodeNeighbors()
    {
        var utf8 = Encoding.UTF8.GetBytes("aé");

        Assert.True(DotNetUtf8WordBoundary.TryGetAsciiBoundary(utf8, 0, out var atStart));
        Assert.True(atStart);
        Assert.False(DotNetUtf8WordBoundary.TryGetAsciiBoundary(utf8, 1, out _));
        Assert.False(DotNetUtf8WordBoundary.TryGetAsciiBoundary(utf8, utf8.Length, out _));
    }

    [Fact]
    public void ProjectionAndFixedDistanceMembershipHaveSingleOwners()
    {
        var root = FindRepositoryRoot();
        var projector = File.ReadAllText(Path.Combine(root, "src", "Lokad.Utf8Regex", "Internal", "FrontEnd", "DotNetAsciiCharClassProjector.cs"));
        Assert.Contains("RegexCharClass", projector, StringComparison.Ordinal);

        var formerProjectors = new[]
        {
            "Utf8AsciiSimplePatternLowerer.CharClasses.cs",
            "Utf8NativeExecutionAnalyzer.cs",
        };
        foreach (var file in formerProjectors)
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "Lokad.Utf8Regex", "Internal", "FrontEnd", file));
            Assert.DoesNotContain("new bool[128]", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CharInClassBase((char)i, runtimeSet)", source, StringComparison.Ordinal);
        }

        var coreFiles = Directory.GetFiles(Path.Combine(root, "src", "Lokad.Utf8Regex"), "*.cs", SearchOption.AllDirectories);
        Assert.DoesNotContain(coreFiles, static file => File.ReadAllText(file).Contains("struct Utf8AsciiByteSet", StringComparison.Ordinal));
        Assert.DoesNotContain(coreFiles, static file => File.ReadAllText(file).Contains("MatchesSet(byte value, Utf8FixedDistanceSet", StringComparison.Ordinal));
    }

    private static void AssertBoundary(string text, byte[] utf8, int byteOffset, int utf16Offset)
    {
        var previousIsWord = utf16Offset > 0 && RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(text[utf16Offset - 1]);
        var nextIsWord = utf16Offset < text.Length && RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(text[utf16Offset]);
        Assert.Equal(previousIsWord != nextIsWord, DotNetUtf8WordBoundary.IsBoundary(utf8, byteOffset));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Lokad.Utf8Regex.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
