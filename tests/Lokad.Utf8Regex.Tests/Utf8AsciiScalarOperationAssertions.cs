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
        var metadataRegex = new Utf8Regex(pattern, options);
        if (requireSlice) Assert.True(metadataRegex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(metadata.GetGroupNames(), oracle.GetGroupNames());
        Assert.Equal(metadata.GetGroupNumbers(), oracle.GetGroupNumbers());
        Assert.Equal(metadata.GetGroupNames(), metadataRegex.GetGroupNames());
        Assert.Equal(metadata.GetGroupNumbers(), metadataRegex.GetGroupNumbers());
        Owned(metadataRegex);

        // Keep independent cold instances for every public operation, as in the
        // old batteries; one operation cannot prepare the route for another.
        var isMatch = new Utf8Regex(pattern, options);
        Assert.Equal(expected.Length != 0, isMatch.IsMatch(subject.Input));
        Owned(isMatch);
        var count = new Utf8Regex(pattern, options);
        Assert.Equal(expected.Length, count.Count(subject.Input));
        Owned(count);
        var match = new Utf8Regex(pattern, options);
        Value(subject, oracle.Match(subject.Projected), match.Match(subject.Input));
        Owned(match);
        var detailed = new Utf8Regex(pattern, options);
        Detailed(subject, oracle.Match(subject.Projected), detailed.MatchDetailed(subject.Input));
        Owned(detailed);
        var enumerate = new Utf8Regex(pattern, options);
        Sequence(subject, expected, enumerate.EnumerateMatches(subject.Input));
        Owned(enumerate);
        foreach (var maximum in new[] { 0, 1, 2, int.MaxValue })
        {
            var split = new Utf8Regex(pattern, options);
            if (maximum == 1)
            {
                // .NET 10.0.12 ValueSplitEnumerator throws for count=1 even on
                // empty input. The scalar contract returns the entire subject.
                var cursor = split.EnumerateSplits(subject.Input, maximum);
                Assert.True(cursor.MoveNext());
                Assert.True(cursor.Current.IsByteAligned);
                Assert.Equal((0, subject.Input.Length), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
                Assert.Equal((0, text.Length), (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
                Assert.Equal(text, cursor.Current.GetValueString());
                Assert.False(cursor.MoveNext());
            }
            else
                Splits(subject, oracle.EnumerateSplits(subject.Projected, maximum), split.EnumerateSplits(subject.Input, maximum));
            Owned(split);
        }
        var replacement = subject.Restore(oracle.Replace(subject.Projected,
            Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template))));
        var bytes = Encoding.UTF8.GetBytes(replacement);
        var stringReplace = new Utf8Regex(pattern, options);
        Assert.Equal(replacement, stringReplace.ReplaceToString(subject.Input, template));
        Owned(stringReplace);
        var byteReplace = new Utf8Regex(pattern, options);
        Assert.Equal(bytes, byteReplace.Replace(subject.Input, template));
        Owned(byteReplace);
        var utf8Replace = new Utf8Regex(pattern, options);
        var utf8Template = Encoding.UTF8.GetBytes(template);
        Assert.Equal(bytes, utf8Replace.Replace(subject.Input, utf8Template));
        Owned(utf8Replace);
        foreach (var utf8 in new[] { false, true })
        {
            var destinationRegex = new Utf8Regex(pattern, options);
            var destination = new byte[bytes.Length + 1];
            var status = utf8 ? destinationRegex.TryReplace(subject.Input, utf8Template, destination, out var written)
                : destinationRegex.TryReplace(subject.Input, template, destination, out written);
            Assert.Equal(OperationStatus.Done, status);
            Assert.Equal(bytes, destination.AsSpan(0, written).ToArray());
            Owned(destinationRegex);
        }
        Evaluators(pattern, options, subject, oracle, expected);

        using var source = new CancellationTokenSource();
        var live = new Utf8Regex(pattern, options);
        Assert.Equal(expected.Length != 0, live.IsMatch(subject.Input, source.Token));
        Assert.Equal(expected.Length, live.Count(subject.Input, source.Token));
        Value(subject, oracle.Match(subject.Projected), live.Match(subject.Input, source.Token));
        Detailed(subject, oracle.Match(subject.Projected), live.MatchDetailed(subject.Input, source.Token));
        Sequence(subject, expected, live.EnumerateMatches(subject.Input, source.Token));
        Splits(subject, oracle.EnumerateSplits(subject.Projected), live.EnumerateSplits(subject.Input, source.Token));
        Assert.Equal(bytes, live.Replace(subject.Input, template, source.Token));
        Assert.Equal(replacement, live.ReplaceToString(subject.Input, template, source.Token));
        Owned(live);
    }

    internal static void Offset(string pattern, RegexOptions options, string text, int offset)
    {
        var subject = new Subject(text);
        var floor = Array.BinarySearch(subject.Utf16Boundaries, offset);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var regex = new Utf8Regex(pattern, options);
            if (floor < 0)
            {
                Action[] operations =
                [
                    () => { _ = regex.IsMatchFromUtf16Offset(subject.Input, offset, token); },
                    () => { _ = regex.CountFromUtf16Offset(subject.Input, offset, token); },
                    () => { _ = regex.MatchFromUtf16Offset(subject.Input, offset, token); },
                    () => { _ = regex.MatchDetailedFromUtf16Offset(subject.Input, offset, token); },
                    () => { _ = regex.EnumerateMatchesFromUtf16Offset(subject.Input, offset, token); },
                ];
                foreach (var operation in operations)
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(operation).ParamName);
                Owned(regex);
                continue;
            }
            var oracle = Comparator(pattern, options);
            var expected = oracle.Matches(subject.Projected, floor).Cast<Match>().ToArray();
            Assert.Equal(expected.Length != 0, regex.IsMatchFromUtf16Offset(subject.Input, offset, token));
            Owned(regex);
            regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected.Length, regex.CountFromUtf16Offset(subject.Input, offset, token));
            Owned(regex);
            regex = new Utf8Regex(pattern, options);
            Value(subject, oracle.Match(subject.Projected, floor), regex.MatchFromUtf16Offset(subject.Input, offset, token));
            Owned(regex);
            regex = new Utf8Regex(pattern, options);
            Detailed(subject, oracle.Match(subject.Projected, floor), regex.MatchDetailedFromUtf16Offset(subject.Input, offset, token));
            Owned(regex);
            regex = new Utf8Regex(pattern, options);
            Sequence(subject, expected, regex.EnumerateMatchesFromUtf16Offset(subject.Input, offset, token));
            Owned(regex);
        }
    }

    private sealed class CallbackState(Subject subject, Match[] matches)
    {
        internal readonly Subject Subject = subject;
        internal readonly Match[] Matches = matches;
        internal int Seen;
    }

    private static void Evaluators(string pattern, RegexOptions options, Subject subject, Regex oracle, Match[] matches)
    {
        var expected = subject.Restore(oracle.Replace(subject.Projected, static match => "[" + match.Value + "]"));
        var byteRegex = new Utf8Regex(pattern, options);
        var byteState = new CallbackState(subject, matches);
        var bytes = byteRegex.Replace(subject.Input, byteState,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref CallbackState state) =>
            {
                Assert.True(state.Seen < state.Matches.Length);
                Detailed(state.Subject, state.Matches[state.Seen++], context);
                writer.Append("["u8);
                Assert.True(context.TryGetValueBytes(out var value));
                writer.Append(value);
                writer.Append("]"u8);
            });
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes);
        Assert.Equal(matches.Length, byteState.Seen);
        Owned(byteRegex);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringState = new CallbackState(subject, matches);
        var result = stringRegex.ReplaceToString(subject.Input, stringState,
            static (in Utf8MatchContext context, ref CallbackState state) =>
            {
                Assert.True(state.Seen < state.Matches.Length);
                Detailed(state.Subject, state.Matches[state.Seen++], context);
                return "[" + context.GetValueString() + "]";
            });
        Assert.Equal(expected, result);
        Assert.Equal(matches.Length, stringState.Seen);
        Owned(stringRegex);
    }

    internal static void Owned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    private static void Value(Subject subject, Match expected, Utf8ValueMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(subject.Units(expected.Index, expected.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
        if (!expected.Success) return;
        Assert.True(actual.TryGetByteRange(out var index, out var length));
        Assert.Equal(subject.Bytes(expected.Index, expected.Length), (index, length));
    }

    internal static void Detailed(Subject subject, Match expected, Utf8MatchContext actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(subject.Units(expected.Index, expected.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
        Assert.Equal(subject.Value(expected.Index, expected.Length), actual.GetValueString());
        if (expected.Success)
        {
            Assert.True(actual.IsByteAligned);
            Assert.Equal(subject.Bytes(expected.Index, expected.Length), (actual.IndexInBytes, actual.LengthInBytes));
        }
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        for (var number = 0; number < expected.Groups.Count; number++)
        {
            var wanted = expected.Groups[number];
            var group = actual.GetGroup(number);
            Assert.Equal(wanted.Success, group.Success);
            Assert.Equal(subject.Units(wanted.Index, wanted.Length), (group.IndexInUtf16, group.LengthInUtf16));
            Assert.Equal(subject.Value(wanted.Index, wanted.Length), group.GetValueString());
            if (wanted.Success)
            {
                Assert.True(group.IsByteAligned);
                Assert.Equal(subject.Bytes(wanted.Index, wanted.Length), (group.IndexInBytes, group.LengthInBytes));
            }
            Assert.Equal(wanted.Captures.Count, group.CaptureCount);
            for (var index = 0; index < wanted.Captures.Count; index++)
            {
                var expectedCapture = wanted.Captures[index];
                var capture = group.GetCapture(index);
                Assert.True(capture.IsByteAligned);
                Assert.Equal(subject.Units(expectedCapture.Index, expectedCapture.Length), (capture.IndexInUtf16, capture.LengthInUtf16));
                Assert.Equal(subject.Bytes(expectedCapture.Index, expectedCapture.Length), (capture.IndexInBytes, capture.LengthInBytes));
                Assert.Equal(subject.Value(expectedCapture.Index, expectedCapture.Length), capture.GetValueString());
            }
        }
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
            Assert.True(actual.Current.IsByteAligned);
            Assert.Equal(subject.Bytes(index, length), (actual.Current.IndexInBytes, actual.Current.LengthInBytes));
            Assert.Equal(subject.Units(index, length), (actual.Current.IndexInUtf16, actual.Current.LengthInUtf16));
            Assert.Equal(subject.Value(index, length), actual.Current.GetValueString());
        }
        Assert.False(actual.MoveNext());
    }
}
