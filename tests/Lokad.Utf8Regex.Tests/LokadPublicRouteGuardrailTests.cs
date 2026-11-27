using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class LokadPublicRouteGuardrailTests
{
    [Fact]
    public void SherlockIngPreparesTheVariableOffsetSuffix()
    {
        var regex = new Utf8Regex(@"\s[a-zA-Z]{0,12}ing\s", options: RegexOptions.None);
        // The prefix has variable UTF-8 width; the suffix is a candidate, not a fixed offset.
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void SherlockWordHolmesUsesOwnedScalarExecutionWithoutAFixedOffset(RegexOptions options)
    {
        var regex = new Utf8Regex(@"\w+\s+Holmes", options: options);
        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        // Historic compiled-plan labels do not describe the scalar VM selected
        // before native/fallback dispatch. No offset through these loops is fixed.
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var input = Encoding.UTF8.GetBytes("!éé Holmes!𐐀 Holmes!");
        Assert.True(regex.IsMatch(input));
        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal((options & RegexOptions.RightToLeft) != 0 ? 13 : 1, match.StartOffsetInBytes);
        Assert.Equal(11, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(match.StartOffsetInBytes, detailed.Value.StartOffsetInBytes);
        Assert.Equal((match.EndOffsetInBytes - match.StartOffsetInBytes), (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes));
        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.DebugIsOwnedSlice);
        var count = 0;
        while (enumerator.MoveNext()) { Assert.Equal(11, (enumerator.Current.EndOffsetInBytes - enumerator.Current.StartOffsetInBytes)); count++; }
        Assert.Equal(2, count);
        Assert.Equal("!X!X!", Encoding.UTF8.GetString(regex.Replace(input, "X"u8)));
        var splits = new List<string>();
        foreach (var split in regex.EnumerateSplits(input)) splits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        Assert.Equal(new[] { "!", "!", "!" }, splits);
        Assert.False(regex.IsMatch("Holmes"u8));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Fact]
    public void LeipzigNameFamilyStaysOnLiteralFamilyEngine()
    {
        var regex = new Utf8Regex("Tom|Sawyer|Huckleberry|Finn", options: RegexOptions.None);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonIpRecognitionMatchesTheOracle(RegexOptions options)
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        string[] inputs =
        [
            "012.200.033.199",
            "012.200.033.19A",
            "01.02.03.04",
            "1.2.3.4",
            "25.26.27.28",
            "255.249.199.000",
            "256.200.033.199",
            "999.999.999.999",
            "prefix 012.200.033.199 suffix",
            "é 012.200.033.199",
            "012.200.033.199é",
            "٠١٢.٢٠٠.٠٣٣.١٩٩",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        for (var value = 0; value < 300; value++)
        {
            var firstOctet = $"{value:D3}.200.033.199";
            Assert.Equal(oracle.IsMatch(firstOctet), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(firstOctet)));

            var finalOctet = $"012.200.033.{value:D3}";
            Assert.Equal(oracle.IsMatch(finalOctet), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(finalOctet)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostDatePreservesUnicodeDigitsAndAnchors(RegexOptions options)
    {
        const string pattern = @"^\d{1,2}/\d{1,2}/\d{4}$";
        var regex = new Utf8Regex(pattern, options: options);
        Assert.True(regex.IsMatch("12/12/2001"u8));

        string[] inputs =
        [
            "1/2/2001",
            "1/12/2001",
            "12/2/2001",
            "12/12/2001",
            "12/12/2001\n",
            "12/123/2001",
            "12-12-2001",
            "١٢/١٢/٢٠٠١",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonUriRecognitionKeepsGreedyTailsAndUnicodeStarts(RegexOptions options)
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatch = regex.IsMatch("https://example.com/path?q=1"u8);
        Assert.True(isMatch);

        string[] inputs =
        [
            "scheme://ab",
            "https://atlas.example.org/reports/export?id=42",
            "https://example.com/path#fragment",
            "https://example.com/path?q=1#fragment",
            "xx https://host/path trailing",
            "https://a https://valid.example/path",
            "prefix:noise https://valid.example/path",
            "prefix:/noise:still-no-uri",
            "https://a\rhttps://b",
            "https://ab\ttrailing",
            "https://ab\ntrailing",
            "https://ab\0rest",
            $"https://{new string('a', 33)}",
            $"https://{new string('a', 34)}",
            $"https://{new string('a', 35)}",
            "é://host.example/path",
            "é https://atlas.example/path",
            "https://hôte.example/path https://atlas.example/path",
            "not a uri é",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }
    }

    [Fact]
    public void NegatedRunsAroundLiteralKeepUnicodeMatches()
    {
        var regex = new Utf8Regex(@"[^a]+\.[^z]+", options: RegexOptions.Compiled);
        var asciiMatch = regex.IsMatch("bbb.ccc"u8);
        Assert.True(asciiMatch);
        var asciiMiss = regex.IsMatch("no literal"u8);
        Assert.False(asciiMiss);
        Assert.True(regex.IsMatch("é.ccc"u8));
    }

    [Fact]
    public void LeadingDotRunSearchKeepsLiteralAndLineContext()
    {
        var regex = new Utf8Regex(@".*(ss)", options: RegexOptions.None);
        var isMatch = regex.IsMatch("Essential services are provided by regular exprs."u8);
        Assert.True(isMatch);
        Assert.True(regex.IsMatch("ss at the start"u8));
        Assert.True(regex.IsMatch("at the end ss"u8));
        Assert.True(regex.IsMatch("before a newline\nss"u8));
        Assert.True(regex.IsMatch("café ss"u8));
        Assert.False(regex.IsMatch("s\ns"u8));

        var noncapturing = new Utf8Regex(@".*(?:needle)", options: RegexOptions.Compiled);
        Assert.True(noncapturing.IsMatch("a needle in text"u8));
        Assert.False(noncapturing.IsMatch("no match"u8));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonDateMatchKeepsScalarResults(RegexOptions options)
    {
        const string pattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
        var regex = new Utf8Regex(pattern, options: options);

        string[] inputs =
        [
            "1/2/20",
            "Today is 11/18/2019.",
            "Today is 11/18/20199.",
            "x11/18/2019",
            "é11/18/2019",
            "😀1١/2/2024",
            "é1١/2/2024",
            "١١/١٨/٢٠١٩",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CommonDateMatcherMatchesDotNetAcrossFieldWidthsAndBoundaries(RegexOptions options)
    {
        const string pattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        string[] boundaries = [string.Empty, " ", "x"];

        for (var firstWidth = 0; firstWidth <= 3; firstWidth++)
        {
            for (var secondWidth = 0; secondWidth <= 3; secondWidth++)
            {
                for (var thirdWidth = 0; thirdWidth <= 5; thirdWidth++)
                {
                    foreach (var prefix in boundaries)
                    {
                        foreach (var suffix in boundaries)
                        {
                            var input = prefix +
                                new string('1', firstWidth) + "/" +
                                new string('2', secondWidth) + "/" +
                                new string('3', thirdWidth) +
                                suffix;

                            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                            var expectedCount = oracle.Count(input);
                            Assert.Equal(expectedCount != 0, regex.IsMatch(bytes));
                            Assert.Equal(expectedCount, regex.Count(bytes));
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void CommonDateDelimiterWindowsKeepWiderStartsAndCaptureOffsets()
    {
        const string pattern = @"\b(\d{1,2})/\d{1,2}/\d{2,4}\b";
        using var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled,
            policy: new() { ResourcePolicy = new() { MaxWorkingBytes = 1 << 20 } });
        using var source = new CancellationTokenSource();
        ReadOnlySpan<byte> input = "x11/18/201A | é1/2/2026 | 𝟙/٢/٢٠٢٦ | 11/20/2026"u8;
        var wideDate = "𝟙/٢/٢٠٢٦"u8;
        var wideStart = input.IndexOf(wideDate);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[2];

        Assert.False(regex.IsMatch(input[..wideStart], source.Token));
        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(new Utf8CaptureRange(wideStart, wideStart + wideDate.Length),
            regex.Match(input, captures: captures, cancellationToken: source.Token));
        Assert.Equal(new Utf8CaptureRange(wideStart, wideStart + "𝟙"u8.Length), captures[1]);

        var asciiStart = input.IndexOf("11/20/2026"u8);
        Assert.Equal(new Utf8CaptureRange(asciiStart, asciiStart + "11/20/2026"u8.Length),
            regex.Match(input, wideStart + wideDate.Length, captures, source.Token));
        Assert.Equal(new Utf8CaptureRange(asciiStart, asciiStart + 2), captures[1]);
    }

    [Fact]
    public void WholeDatePreservesItsPublicByteRange()
    { var regex = new Utf8Regex(@"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$", options: RegexOptions.Compiled); Assert.Equal(new Utf8CaptureRange(0, 10), regex.Match("12/12/2001"u8)); }
    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsPostcodeMatchPreservesOptionalShapeSemantics(RegexOptions options)
    {
        const string pattern = "^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$";
        using var regex = new Utf8Regex(pattern, options: options);
        using var cancellation = new CancellationTokenSource();
        using var limited = new Utf8Regex(pattern, options: options, policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 }
        });
        Assert.True(limited.IsMatch("A01ZZ"u8, cancellation.Token));
        Assert.Equal(new Utf8CaptureRange(0, 5), limited.Match("A01ZZ"u8, cancellationToken: cancellation.Token));
        var proofPolicy = new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkingBytes = 1 } };
        using var proof = new Utf8Regex(pattern, options: options, policy: proofPolicy);
        using var strict = new Utf8Regex(pattern[..^1] + @"\z", options: options, policy: proofPolicy);
        Assert.True(strict.IsMatch("SW1 1ZZ"u8, cancellation.Token));
        Assert.False(strict.IsMatch("SW1 1ZZ\n"u8, cancellation.Token));

        string[] inputs =
        [
            "A01ZZ",
            "A0 1ZZ",
            "A0Z1ZZ",
            "A0Z 1ZZ",
            "AB01ZZ",
            "AB0 1ZZ",
            "AB0Z1ZZ",
            "AB0Z 1ZZ",
            "AB0Z 1ZZ\n",
            "AB0Z 1ZZ\r\n",
            "ABC0Z 1ZZ",
            "AB0Z 1Z9",
            "éB0Z 1ZZ",
        ];
        foreach (var input in inputs)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var expected = Regex.IsMatch(input, pattern);
            Assert.Equal(expected, regex.IsMatch(bytes));
            Assert.Equal(expected, proof.IsMatch(bytes, cancellation.Token));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsFloatMatchKeepsSignedDecimalAndEndAnchorSemantics(RegexOptions options)
    {
        const string pattern = @"^[-+]?\d*\.?\d*$";
        var regex = new Utf8Regex(pattern, options: options);

        var match = regex.Match("-3.14159"u8);
        Assert.True(match.Success);

        string[] inputs =
        [
            "",
            "+",
            "-3.14159",
            ".5",
            "5.",
            "5\n",
            "+.\n",
            "1.2.3",
            "--1",
            "١.٥",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CreditCardAlternativesKeepScalarResults(RegexOptions options)
    {
        const string pattern = @"([0-9]{4}[- ]){3}[0-9]{3,4}";
        var regex = new Utf8Regex(pattern, options: options);

        string[] inputs =
        [
            "1234-5678-1234-456",
            "1234 5678 1234 4567",
            "1234-5678 1234-456",
            "x1234-5678-1234-456y",
            "1234-5678-1234-456x",
            "1234-5678-1234-45",
            "1234-5678-1234",
            "1234-5678-1234-45x",
            "1234-5678_1234-456",
            "1234-5678-123x-456",
            "١٢٣٤-٥٦٧٨-١٢٣٤-٤٥٦",
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(Regex.IsMatch(input, pattern), regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoostdocsFtpLineMatchKeepsAlternativeOrderAndCaptures(RegexOptions options)
    {
        const string pattern = @"^([0-9]+)(\-| |$)(.*)$";
        var oracle = new Regex(pattern, options);
        using var regex = new Utf8Regex(pattern, options: options);
        using var limited = new Utf8Regex(pattern, options: options, policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 }
        });
        using var cancellation = new CancellationTokenSource();

        string[] inputs =
        [
            "100- message",
            "100 message",
            "100",
            "100\n",
            "100-",
            "100 \n",
            "100_x",
            "100-\nmore",
            "é100-message",
            "100-é",
            "100 😀",
        ];
        foreach (var input in inputs)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var expected = oracle.Match(input);
            Assert.Equal(expected.Success, limited.IsMatch(bytes, cancellation.Token));
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(bytes));
            Utf8MatchSequence.AssertCaptures(expected, actual, bytes, input);
        }
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void OrderedLiteralWindowGapMinimumAgreesWithOracle(RegexOptions options)
    {
        const string pattern = @"river.{10,25}Tom";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        foreach (var gap in new[] { 8, 9, 10, 11, 25, 26 })
        {
            var input = "river" + new string('x', gap) + "Tom";
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

            Assert.Equal(oracle.Count(input), regex.Count(inputBytes));
            Assert.Equal(oracle.Count(input) > 0, regex.IsMatch(inputBytes));

            var enumerated = 0;
            foreach (var _ in regex.EnumerateMatches(inputBytes))
            {
                enumerated++;
            }

            Assert.Equal(oracle.Count(input), enumerated);
        }

        const string phantom = "down the river, me and Tom";
        var phantomBytes = System.Text.Encoding.UTF8.GetBytes(phantom);
        Assert.Equal(0, oracle.Count(phantom));
        Assert.Equal(0, regex.Count(phantomBytes));
        Assert.False(regex.IsMatch(phantomBytes));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void OrderedLiteralWindowNegatedCategoryGapAgreesWithOracle(RegexOptions options)
    {
        // An aligned literal pair can still fail when the bounded gap
        // contains a scalar excluded by its class.
        var rows = new (string Pattern, string Input)[]
        {
            (@"a\D{2,5}b", "xa12b"),
            (@"a\D{2,5}b", "xayzb"),
            (@"a\D{2}b", "xa12b"),
            (@"a[\D]{2,5}b", "xa12b"),
            (@"a[^\s]{2,5}b", "xa  b"),
            (@"a[^\s]{2,5}b", "xayzb"),
            (@"a\P{Nd}{2,5}b", "xa12b"),
            (@"a\P{Nd}{2,5}b", "xayzb"),
            (@"a\W{2,5}b", "xa12b"),
            (@"a\W{2,5}b", "xa.,b"),
            (@"a[^\D]{2,5}b", "xa12b"),
            (@"a[^\D]{2,5}b", "xayzb"),
            (@"a[^\W]{2,5}b", "xayzb"),
            (@"a[^\W]{2,5}b", "xa b"),
            (@"a[^α-ω]{2,5}b", "xaαβb"),
            (@"a[^α-ω]{2,5}b", "xayzb"),
            ("a[^\U0001F600]{2,5}b", "xa\U0001F600\U0001F600b"),
            ("a[^\U0001F600]{2,5}b", "xayzb"),
        };

        foreach (var row in rows)
        {
            var regex = new Utf8Regex(row.Pattern, options: options);
            var oracle = new Regex(row.Pattern, options, Regex.InfiniteMatchTimeout);
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(row.Input);

            Assert.Equal(oracle.IsMatch(row.Input), regex.IsMatch(inputBytes));
            Assert.Equal(oracle.Matches(row.Input).Count, regex.Count(inputBytes));

            var want = oracle.Match(row.Input);
            var got = regex.Match(inputBytes);
            Assert.Equal(want.Success, got.Success);
            if (want.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(row.Input.AsSpan(0, want.Index)), got.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(row.Input.AsSpan(want.Index, want.Length)), (got.EndOffsetInBytes - got.StartOffsetInBytes));
            }
        }

        // Literal pairs also admit negated-newline, any-text and whitespace gaps.
        foreach (var control in new (string Pattern, string Input)[]
        {
            (@"a[^\n]{2,5}b", "xa  b"),
            (@"a[\s\S]{2,5}b", "xa12b"),
            (@"a\s{2,}?b", "xa  b"),
            (@"a\s{2,}b", "xa  b"),
        })
        {
            var regex = new Utf8Regex(control.Pattern, options: options);
            var oracle = new Regex(control.Pattern, options, Regex.InfiniteMatchTimeout);
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(control.Input);
            Assert.Equal(oracle.IsMatch(control.Input), regex.IsMatch(inputBytes));
            Assert.Equal(oracle.Matches(control.Input).Count, regex.Count(inputBytes));
        }
    }
}
