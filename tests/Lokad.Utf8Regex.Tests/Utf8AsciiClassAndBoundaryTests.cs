using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

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
    public void SharedKnownClassPredicateMatchesItsClassCarrierForEveryByte()
    {
        (AsciiCharClassPredicateKind Kind, Func<byte, bool> Predicate)[] predicates =
        [
            (AsciiCharClassPredicateKind.Digit, Utf8AsciiBytePredicates.IsDigit),
            (AsciiCharClassPredicateKind.AsciiLetter, Utf8AsciiBytePredicates.IsLetter),
            (AsciiCharClassPredicateKind.AsciiLetterOrDigit, Utf8AsciiBytePredicates.IsLetterOrDigit),
            (AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore, Utf8AsciiBytePredicates.IsWord),
            (AsciiCharClassPredicateKind.AsciiHexDigit, Utf8AsciiBytePredicates.IsHexDigit),
        ];

        foreach (var (kind, predicate) in predicates)
        {
            var charClass = AsciiCharClass.FromPredicate(predicate);
            Assert.Equal(kind, charClass.KnownPredicateKind);
            for (var value = 0; value <= byte.MaxValue; value++)
            {
                Assert.Equal(charClass.Contains((byte)value), Utf8AsciiBytePredicates.MatchesKnownClass((byte)value, kind));
            }
        }

        Assert.False(Utf8AsciiBytePredicates.MatchesKnownClass((byte)'0', AsciiCharClassPredicateKind.None));
    }

    [Fact]
    public void ScalarProjectionReadsCanonicalClassData()
    {
        var digits = ProjectToken(@"\d", RegexOptions.None);
        var spaces = ProjectToken(@"\s", RegexOptions.None);
        var ecmaSpaces = ProjectToken(@"\s", RegexOptions.ECMAScript);
        Assert.True(spaces.RequiresAsciiInput);
        Assert.True(digits.RequiresAsciiInput);
        Assert.False(ecmaSpaces.RequiresAsciiInput);
        Assert.Equal(Utf8SimplePatternScalarClassKind.UnicodeWhitespace, spaces.ScalarClassKind);
        Assert.Equal(Utf8SimplePatternScalarClassKind.None, ecmaSpaces.ScalarClassKind);

        for (var value = 0; value < 128; value++)
        {
            Assert.Equal(value is >= '0' and <= '9', digits.CharClass.Contains((byte)value));
            Assert.Equal(Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value), spaces.CharClass.Contains((byte)value));
            Assert.Equal(spaces.CharClass.Contains((byte)value), ecmaSpaces.CharClass.Contains((byte)value));
        }
    }

    [Fact]
    public void ScalarProjectionPreservesMixedShorthandExclusions()
    {
        var digitsWithoutZero = ProjectToken(@"[^\D0]", RegexOptions.None);
        var wordsWithoutUnderscore = ProjectToken(@"[^\W_]", RegexOptions.None);
        var spacesWithoutSpace = ProjectToken(@"[^\S ]", RegexOptions.None);
        var notNewline = ProjectToken(@"[^\n]", RegexOptions.Singleline);
        var dot = ProjectToken(".", RegexOptions.Singleline);
        Assert.Equal(AsciiSimplePatternTokenKind.Dot, dot.Kind);
        Assert.Equal(AsciiSimplePatternTokenKind.CharClass, notNewline.Kind);
        for (var value = 0; value < 128; value++)
        {
            Assert.Equal(value is >= '1' and <= '9', digitsWithoutZero.CharClass.Contains((byte)value));
            Assert.Equal(char.IsAsciiLetterOrDigit((char)value), wordsWithoutUnderscore.CharClass.Contains((byte)value));
            Assert.Equal(value is >= '\t' and <= '\r', spacesWithoutSpace.CharClass.Contains((byte)value));
            Assert.Equal(value != '\n', notNewline.CharClass.Contains((byte)value));
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void ProjectedClassesKeepExplicitScalarOutcomes(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.Singleline |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var input = "0a1\u0660\U000104a0\U0001f600\n_"u8.ToArray();
        (string Pattern, (int, int)[] Ranges, string Replaced)[] cases =
        [
            (@"[^\D0]", [(2, 1), (3, 2), (5, 4)], "0aQQQ\U0001f600\n_"),
            (@"[^\W_]", [(0, 1), (1, 1), (2, 1), (3, 2), (5, 4)], "QQQQQ\U0001f600\n_"),
            (@"[^\S ]", [(13, 1)], "0a1\u0660\U000104a0\U0001f600Q_"),
            (@"[^\n]", [(0, 1), (1, 1), (2, 1), (3, 2), (5, 4), (9, 4), (14, 1)], "QQQQQQ\nQ"),
        ];
        foreach (var value in cases)
        {
            var regex = new Utf8Regex(value.Pattern, options);
            var expected = value.Ranges.ToArray();
            if (rtl) Array.Reverse(expected);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(expected.Length, regex.Count(input));
            var actual = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input)) actual.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected, actual);
            var first = regex.Match(input);
            Assert.Equal(expected[0], (first.IndexInBytes, first.LengthInBytes));
            var detailed = regex.MatchDetailed(input);
            Assert.Equal(expected[0], (detailed.IndexInBytes, detailed.LengthInBytes));
            Assert.Equal(value.Replaced, regex.ReplaceToString(input, "Q"));
            Assert.Equal(Encoding.UTF8.GetBytes(value.Replaced), regex.Replace(input, "Q"u8));
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
    public void CanonicalBoundaryMatchesBclWordBoundaryClassification(string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        var boundaries = new HashSet<int>(new Regex(@"\b", RegexOptions.CultureInvariant).Matches(text).Select(static match => match.Index));
        var byteOffset = 0;
        var utf16Offset = 0;
        AssertBoundary(utf8, byteOffset, utf16Offset, boundaries);

        foreach (var rune in text.EnumerateRunes())
        {
            byteOffset += rune.Utf8SequenceLength;
            utf16Offset += rune.Utf16SequenceLength;
            AssertBoundary(utf8, byteOffset, utf16Offset, boundaries);
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

        Assert.True(Utf8ScalarWordBoundary.TryGetAsciiBoundary(utf8, 0, out var atStart));
        Assert.True(atStart);
        Assert.False(Utf8ScalarWordBoundary.TryGetAsciiBoundary(utf8, 1, out _));
        Assert.False(Utf8ScalarWordBoundary.TryGetAsciiBoundary(utf8, utf8.Length, out _));
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void ProjectionAndFixedDistanceMembershipHaveSingleOwners()
    {
        var root = FindRepositoryRoot();
        var projector = File.ReadAllText(Path.Combine(root, "src", "Lokad.Utf8Regex", "Internal", "FrontEnd", "Utf8ScalarAsciiProjector.cs"));
        Assert.Contains("scalarClass.Data.AsSpan()", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("Utf8OwnedCharClass", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeProjection", projector, StringComparison.Ordinal);

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

    private static void AssertBoundary(byte[] utf8, int byteOffset, int utf16Offset, HashSet<int> boundaries)
    {
        Assert.Equal(boundaries.Contains(utf16Offset), Utf8ScalarWordBoundary.IsBoundary(utf8, byteOffset));
    }

    private static AsciiSimplePatternToken ProjectToken(string pattern, RegexOptions options)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        var scalarClass = tree.Root.Child(0).ScalarClass;
        Assert.NotNull(scalarClass);
        Assert.True(Utf8ScalarAsciiProjector.TryCreateToken(scalarClass, out var token));
        return token;
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
