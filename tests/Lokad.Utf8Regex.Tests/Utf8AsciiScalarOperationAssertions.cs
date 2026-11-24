using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Qualified for the ASCII structure fixture's exported alphabet and predicates.
// Four supplementary scalars become distinct, unused BMP characters. Their
// L/Lu/Nd/Cs, word/space/digit, Greek/BasicLatin/HighSurrogates membership agrees.
// Patterns contain ASCII atoms plus complete emoji literals, and no BMP-wide
// numeric ranges. Keep the direct witnesses current when extending this domain.
internal static class Utf8AsciiScalarOperationAssertions
{
    internal sealed class Subject
    {
        internal readonly string Text;
        internal readonly string Projected;
        internal readonly byte[] Input;
        internal readonly int[] Utf16Boundaries;
        internal readonly int[] ByteBoundaries;

        internal Subject(string text)
        {
            Text = text;
            Input = Encoding.UTF8.GetBytes(text);
            var projected = new StringBuilder();
            var units = new List<int> { 0 };
            var bytes = new List<int> { 0 };
            foreach (var scalar in text.EnumerateRunes())
            {
                Assert.False(scalar.Value is 0xe000 or 0x0531 or 0x0966 or 0x09e6);
                projected.Append(scalar.IsBmp ? (char)scalar.Value : scalar.Value switch
                {
                    0x1f600 => '\ue000', // non-word, non-letter, non-digit
                    0x10400 => '\u0531', // Armenian Lu, outside the Greek block
                    0x104a0 => '\u0966', // Devanagari Nd, outside ASCII digits
                    0x1d7ce => '\u09e6', // Bengali Nd; retain scalar identity
                    _ => throw new InvalidOperationException("Unqualified supplementary fixture scalar."),
                });
                units.Add(units[^1] + scalar.Utf16SequenceLength);
                bytes.Add(bytes[^1] + scalar.Utf8SequenceLength);
            }
            Projected = projected.ToString();
            Utf16Boundaries = units.ToArray();
            ByteBoundaries = bytes.ToArray();
        }

        internal (int Index, int Length) Bytes(int index, int length) =>
            (ByteBoundaries[index], ByteBoundaries[index + length] - ByteBoundaries[index]);

        internal (int Index, int Length) Units(int index, int length) =>
            (Utf16Boundaries[index], Utf16Boundaries[index + length] - Utf16Boundaries[index]);

        internal string Value(int index, int length)
        {
            var (start, count) = Units(index, length);
            return Text.Substring(start, count);
        }

        internal string Restore(string value) => value.Replace("\ue000", "\U0001f600", StringComparison.Ordinal)
            .Replace("\u0531", "\U00010400", StringComparison.Ordinal)
            .Replace("\u0966", "\U000104a0", StringComparison.Ordinal)
            .Replace("\u09e6", "\U0001d7ce", StringComparison.Ordinal);
    }

    internal static Regex Comparator(string pattern, RegexOptions options)
    {
        foreach (var scalar in pattern.EnumerateRunes())
            Assert.True(scalar.Value < 128 || scalar.Value == 0x1f600, "Unqualified non-ASCII pattern atom.");
        Assert.DoesNotMatch(@"\\[uUx]", pattern);
        foreach (Match property in Regex.Matches(pattern, @"\\[pP]\{([^}]+)\}"))
            Assert.Contains(property.Groups[1].Value,
                new[] { "Cs", "IsBasicLatin", "IsGreek", "IsHighSurrogates", "L", "Lu", "Nd" });
        // Ordinary and compiled owned lanes have one scalar contract. Use the
        // interpreted BCL comparator to avoid the diagnosed .NET 10.0.12
        // compiled capture defects; its code-unit subjects here are all BMP.
        return new Regex(pattern.Replace("\U0001f600", "\ue000", StringComparison.Ordinal),
            options & ~RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
    }

    internal static void Operations(string pattern, RegexOptions options, string text, string template = "Q", bool requireSlice = false)
    {
        var subject = new Subject(text);
        var oracle = Comparator(pattern, options);
        var metadata = new Regex(pattern, options & ~RegexOptions.Compiled);
        var expected = oracle.Matches(subject.Projected).Cast<Match>().ToArray();
        var startOffset = (options & RegexOptions.RightToLeft) != 0 ? subject.Input.Length : 0;
        var metadataRegex = new Utf8Regex(pattern, options: options);
        if (requireSlice) Assert.True(metadataRegex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(metadata.GetGroupNames(), oracle.GetGroupNames());
        Assert.Equal(metadata.GetGroupNumbers(), oracle.GetGroupNumbers());
        Assert.Equal(metadata.GetGroupNames(), Utf8GroupNames.Read(metadataRegex));
        Assert.Equal(metadata.GetGroupNumbers(), Enumerable.Range(0, metadataRegex.CaptureGroupCount + 1).Select(metadataRegex.GroupNumberFromCaptureSlot).ToArray());
        Owned(metadataRegex);

        // Keep independent cold instances for every public operation, as in the
        // old batteries; one operation cannot prepare the route for another.
        var isMatch = new Utf8Regex(pattern, options: options);
        Assert.Equal(expected.Length != 0, isMatch.IsMatch(subject.Input));
        var count = new Utf8Regex(pattern, options: options);
        Assert.Equal(expected.Length, count.Count(subject.Input));
        var match = new Utf8Regex(pattern, options: options);
        Value(subject, oracle.Match(subject.Projected), match.Match(subject.Input, startOffset, []));
        var captures = new Utf8Regex(pattern, options: options);
        Utf8MatchSequence.AssertCaptures(oracle.Match(subject.Projected),
            Utf8MatchSequence.First(captures.CreateMatchCursor(subject.Input)),
            subject.Input, index => subject.ByteBoundaries[index]);
        var enumerate = new Utf8Regex(pattern, options: options);
        Sequence(subject, expected, enumerate.EnumerateMatches(subject.Input));
        foreach (var maximum in new[] { 0, 1, 2, int.MaxValue })
        {
            var split = new Utf8Regex(pattern, options: options);
            if (maximum == 1)
            {
                // .NET 10.0.12 ValueSplitEnumerator throws for count=1 even on
                // empty input. The scalar contract returns the entire subject.
                var cursor = split.EnumerateSplits(subject.Input, maximum);
                Assert.True(cursor.MoveNext());
                Assert.Equal((0, subject.Input.Length), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
                Assert.Equal(text, Encoding.UTF8.GetString(cursor.Current.GetValueBytes()));
                Assert.False(cursor.MoveNext());
            }
            else
                Splits(subject, oracle.EnumerateSplits(subject.Projected, maximum), split.EnumerateSplits(subject.Input, maximum));
            Owned(split);
        }
        var utf8Template = Encoding.UTF8.GetBytes(template);
        var replacement = subject.Restore(oracle.Replace(subject.Projected, Encoding.UTF8.GetString(utf8Template)));
        var bytes = Encoding.UTF8.GetBytes(replacement);
        using var utf8Replace = new Utf8Regex(pattern, options: options);
        Assert.Equal(bytes, utf8Replace.Replace(subject.Input, utf8Template));
        using var destinationRegex = new Utf8Regex(pattern, options: options);
        var destination = new byte[bytes.Length + 1];
        var status = destinationRegex.TryReplace(subject.Input, utf8Template, destination, out var written);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
        Owned(destinationRegex);

        using var source = new CancellationTokenSource();
        var live = new Utf8Regex(pattern, options: options);
        Assert.Equal(expected.Length != 0, live.IsMatch(subject.Input, source.Token));
        Assert.Equal(expected.Length, live.Count(subject.Input, source.Token));
        Value(subject, oracle.Match(subject.Projected), live.Match(subject.Input, startOffset, [], source.Token));
        Utf8MatchSequence.AssertCaptures(oracle.Match(subject.Projected),
            Utf8MatchSequence.First(live.CreateMatchCursor(subject.Input, cancellationToken: source.Token)),
            subject.Input, index => subject.ByteBoundaries[index]);
        Sequence(subject, expected, live.EnumerateMatches(subject.Input, cancellationToken: source.Token));
        Splits(subject, oracle.EnumerateSplits(subject.Projected), live.EnumerateSplits(subject.Input, cancellationToken: source.Token));
        Assert.Equal(bytes, live.Replace(subject.Input, utf8Template, source.Token));
    }

    internal static void Offset(string pattern, RegexOptions options, string text, int offset)
    {
        var subject = new Subject(text);
        var floor = Array.BinarySearch(subject.Utf16Boundaries, offset);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var regex = new Utf8Regex(pattern, options: options);
            if (floor < 0)
            {
                var byteOffset = offset < 0 ? -1 : offset > text.Length ? subject.Input.Length + 1 :
                    subject.ByteBoundaries[~floor - 1] + 1;
                Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
                    regex.EnumerateMatches(subject.Input, byteOffset, token)).ParamName);
                Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
                { using var cursor = regex.CreateMatchCursor(subject.Input, byteOffset, token); }).ParamName);
                Owned(regex);
                continue;
            }
            var oracle = Comparator(pattern, options);
            var expected = oracle.Matches(subject.Projected, floor).Cast<Match>().ToArray();
            Value(subject, oracle.Match(subject.Projected, floor), regex.Match(subject.Input, subject.ByteBoundaries[floor], [], token));
            Owned(regex);
            regex = new Utf8Regex(pattern, options: options);
            Utf8MatchSequence.AssertCaptures(oracle.Match(subject.Projected, floor),
                Utf8MatchSequence.First(regex.CreateMatchCursor(subject.Input, subject.ByteBoundaries[floor], token)),
                subject.Input, index => subject.ByteBoundaries[index]);
            Owned(regex);
            regex = new Utf8Regex(pattern, options: options);
            Sequence(subject, expected, regex.EnumerateMatches(subject.Input, subject.ByteBoundaries[floor], token));
            Owned(regex);
        }
    }

    internal static void Owned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    private static void Value(Subject subject, Match expected, Utf8CaptureRange actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        var (start, length) = subject.Bytes(expected.Index, expected.Length);
        Assert.Equal((start, start + length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }

    private static void Sequence(Subject subject, Match[] expected, Utf8ValueMatchEnumerator cursor)
    {
        foreach (var match in expected)
        {
            Assert.True(cursor.MoveNext());
            Value(subject, match, cursor.Current);
        }
        Assert.False(cursor.MoveNext());
        Assert.False(cursor.MoveNext());
    }

    private static void Splits(Subject subject, Regex.ValueSplitEnumerator expected, Utf8ValueSplitEnumerator actual)
    {
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            var (index, length) = expected.Current.GetOffsetAndLength(subject.Projected.Length);
            Assert.Equal(subject.Bytes(index, length), (actual.Current.IndexInBytes, actual.Current.LengthInBytes));
            Assert.Equal(subject.Value(index, length), Encoding.UTF8.GetString(actual.Current.GetValueBytes()));
        }
        Assert.False(actual.MoveNext());
    }
}
