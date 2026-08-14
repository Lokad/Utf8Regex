using System.Buffers;
using System.Text;
using Lokad.Utf8Regex;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LiteralGlobalOperationTests
{
    [Fact]
    public void LiteralGlobalOperationsShareTheGenericProgram()
    {
        var regex = new Utf8Pcre2Regex("é");
        var input = Encoding.UTF8.GetBytes("xéé😀é");

        Assert.Equal(3, regex.Count(input));
        var bytes = new List<(int Start, int End)>();
        var utf16 = new List<(int Start, int End)>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            var match = enumerator.Current;
            bytes.Add((match.StartOffsetInBytes, match.EndOffsetInBytes));
            utf16.Add((match.StartOffsetInUtf16, match.EndOffsetInUtf16));
        }

        Assert.Equal([(1, 3), (3, 5), (9, 11)], bytes);
        Assert.Equal([(1, 2), (2, 3), (5, 6)], utf16);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
    }

    [Fact]
    public void EmptyLiteralProgressesAtScalarsWithoutDuplicates()
    {
        var regex = new Utf8Pcre2Regex(string.Empty);
        var input = "😀x"u8;

        Assert.Equal(3, regex.Count(input));
        Assert.Equal(0, regex.Count(input, 0, Pcre2MatchOptions.NotEmpty));

        var byteStarts = new List<int>();
        var utf16Starts = new List<int>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            byteStarts.Add(enumerator.Current.StartOffsetInBytes);
            utf16Starts.Add(enumerator.Current.StartOffsetInUtf16);
        }

        Assert.Equal([0, 4, 5], byteStarts);
        Assert.Equal([0, 2, 3], utf16Starts);
    }

    [Fact]
    public void EmptyLiteralTreatsConfiguredCrlfAsOneProgressionUnit()
    {
        var regex = new Utf8Pcre2Regex(
            string.Empty,
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf },
            default,
            default);

        var starts = new List<int>();
        var enumerator = regex.EnumerateMatches("\r\nx"u8);
        while (enumerator.MoveNext())
        {
            starts.Add(enumerator.Current.StartOffsetInBytes);
        }

        Assert.Equal([0, 2, 3], starts);
    }

    [Fact]
    public void MatchManyStopsAfterOneLookahead()
    {
        var regex = new Utf8Pcre2Regex("a");
        Span<Utf8Pcre2MatchData> one = stackalloc Utf8Pcre2MatchData[1];

        var written = regex.MatchMany("a a"u8, one, out var isMore);

        Assert.Equal(1, written);
        Assert.True(isMore);
        Assert.Equal(0, one[0].StartOffsetInBytes);

        Span<Utf8Pcre2MatchData> all = stackalloc Utf8Pcre2MatchData[2];
        written = regex.MatchMany("a a"u8, all, out isMore);
        Assert.Equal(2, written);
        Assert.False(isMore);
        Assert.Equal(2, all[1].StartOffsetInBytes);

        written = regex.MatchMany("a a"u8, Span<Utf8Pcre2MatchData>.Empty, out isMore);
        Assert.Equal(0, written);
        Assert.True(isMore);
    }

    [Fact]
    public void LiteralReplacementUsesByteSegmentsAndGlobalMatches()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Equal("x<abc><abc>", regex.ReplaceToString("xabcabc"u8, "<$0>"));
        Assert.Equal(
            "[abc][abc]",
            regex.ReplaceToString(
                "xabcabc"u8,
                "[$&]",
                substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly));
        Assert.Equal(
            "x$0$0",
            regex.ReplaceToString(
                "xabcabc"u8,
                "$0",
                substitutionOptions: Pcre2SubstitutionOptions.SubstituteLiteral));
    }

    [Fact]
    public void LiteralTryReplaceIsTransactionalAndReportsOverflowLength()
    {
        var regex = new Utf8Pcre2Regex("abc");
        Span<byte> destination = stackalloc byte[6];
        "stays!"u8.CopyTo(destination);

        var status = regex.TryReplace(
            "abcabc"u8,
            "long"u8,
            destination,
            out var bytesWritten,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteOverflowLength);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(8, bytesWritten);
        Assert.True(destination.SequenceEqual("stays!"u8));
    }

    [Fact]
    public void LiteralEvaluatorUsesTheProducedMatchWithoutRerunning()
    {
        var regex = new Utf8Pcre2Regex("é");
        var calls = 0;
        var result = regex.Replace(
            "xéé"u8,
            calls,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                state++;
                writer.AppendAsciiByte((byte)'[');
                writer.Append(match.Value.GetValueBytes());
                writer.AppendAsciiByte((byte)']');
            });

        Assert.True(result.AsSpan().SequenceEqual("x[é][é]"u8));
    }

    [Fact]
    public void LiteralProbeSupportsFullPartialAndNoMatchResults()
    {
        var regex = new Utf8Pcre2Regex("café");

        var full = regex.Probe("xxcafé"u8, Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, full.Kind);
        Assert.Equal(2, full.Value.StartOffsetInBytes);

        var partial = regex.Probe("xxca"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal(2, partial.Value.StartOffsetInBytes);
        Assert.Equal(4, partial.Value.EndOffsetInBytes);

        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xyz"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(
            Utf8Pcre2ProbeKind.PartialMatch,
            new Utf8Pcre2Regex(@"\Aabc").Probe("ab"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void WarmedLiteralCountAndEnumerationAllocateNoObjectsPerResult()
    {
        var regex = new Utf8Pcre2Regex("a");
        var input = Encoding.UTF8.GetBytes(new string('a', 128));
        _ = regex.Count(input);
        var warm = regex.EnumerateMatches(input);
        while (warm.MoveNext())
        {
            _ = warm.Current.StartOffsetInBytes;
        }

        var sum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            sum += regex.Count(input);
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                sum += enumerator.Current.StartOffsetInBytes;
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void GenericGlobalDriverHasNoCompletePatternDispatchOrNullableArrayAssertion()
    {
        var root = FindRepositoryDirectory();
        var architecture = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Lokad.Utf8Regex.Pcre2",
            "Pcre2ExecutionArchitecture.cs"));
        var cursorStart = architecture.IndexOf("internal ref struct Pcre2LiteralGlobalMatchCursor", StringComparison.Ordinal);
        var cursorSource = architecture[cursorStart..];
        Assert.DoesNotContain("Pattern switch", cursorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Pattern ==", cursorSource, StringComparison.Ordinal);

        var coreTypes = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Lokad.Utf8Regex.Pcre2",
            "Utf8Pcre2CoreTypes.cs"));
        Assert.DoesNotContain("_matches!", coreTypes, StringComparison.Ordinal);
    }

    private static string FindRepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex.Pcre2")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
