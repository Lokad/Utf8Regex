using Lokad.Utf8Regex.Internal.Input;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class ExactUtf8LiteralFamilyBoundaryTests
{
    public static TheoryData<string, string> BoundaryCases => new()
    {
        {
            @"\b(?:café|niño|résumé)\b",
            "café xcafé caféx niño résumé 😀café😀"
        },
        {
            @"\b(?:café|niño|résumé)",
            "café xcafé caféx niño résumé"
        },
        {
            @"(?:café|niño|résumé)\b",
            "café xcafé caféx niño résumé"
        },
        {
            @"\B(?:café|niño|résumé)\B",
            "xcaféx xniñox xrésuméx"
        },
        {
            @"\b(?:café|foo|𐐀)\b",
            "café foo 𐐀 xcaféx xfoox x𐐀x"
        },
        {
            @"\b(?:caféteria|café|niño)\b",
            "caféteria café cafétéria niño"
        },
        {
            @"\b(?:café|caféteria|niño)\b",
            "caféteria café cafétéria niño"
        },
        {
            @"(?:café|café noir|niño)(?=!)",
            "café noir! café! niño! café gris!"
        },
        {
            @"\b(?:café|caféteria|caféterias|niño)\b",
            "caféterias cafetería café niño cafeteriasx"
        },
        {
            @"\b(?:a-b|b|niño)\b",
            "xa-b a-b b niño"
        },
        {
            @"\b(?:Task|ValueTask|IAsyncEnumerable)\b",
            "Task xTask Taskx ValueTask IAsyncEnumerable éTask Taské 😀Task😀"
        },
        {
            @"\b(?:-a|a-|𐐀|café)\b",
            "-a a- x-a a-x 𐐀 x𐐀x café xcafé caféx"
        },
        {
            @"\B(?:-a|a-|𐐀|café)\B",
            "-a a- x-a a-x 𐐀 x𐐀x café xcafé caféx"
        },
    };

    [Theory]
    [InlineData(@"\b(?:foo|foobar|baz)\b", true)]
    [InlineData(@"\b(?:foobar|foo|baz)\b", true)]
    [InlineData(@"\b(?:foo|bar|baz)\b", false)]
    [InlineData(@"(?:foo|foobar|baz)", false)]
    public void PrefixOverlapIsPreparedOnce(string pattern, bool expected)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(expected, regex.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap);
    }

    [Theory]
    [InlineData(@"\b(?:a-b|b|niño)\b", true)]
    [InlineData(@"\b(?:abcd|bcde|niño)\b", true)]
    [InlineData(@"\b(?:ababa|niño|résumé)\b", true)]
    [InlineData(@"\b(?:café|niño|résumé)\b", false)]
    public void ProperStartOverlapIsPreparedOnce(string pattern, bool expected)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(expected, regex.Inspection.SearchPlan.HasAlternateLiteralProperStartOverlap);
    }

    [Fact]
    public void CompiledAsciiBoundaryFamiliesUseTheNativeLiteralRuntime()
    {
        var prefix = new Utf8Regex(            @"\b(?:foo|foobar|baz)\b", options:             RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var noPrefix = new Utf8Regex(            @"\b(?:foo|quux|baz)\b", options:             RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(prefix.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, prefix.Inspection.CompiledEngineKind);
        Assert.False(noPrefix.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, noPrefix.Inspection.CompiledEngineKind);
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void BoundaryLiteralFamilyGlobalOperationsMatchScalarContract(string pattern, string input)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            AssertGlobalParity(pattern, input, options, Regex.InfiniteMatchTimeout);
            AssertGlobalParity(pattern, input, options, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public void BorrowedBoundaryFamilyCursorsKeepScalarRangesAndSplitLimits()
    {
        using var regex = new Utf8Regex(@"\b(?<word>cat|dog)\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "écat|😀cat😀|dog\u200c|dog"u8;
        var matches = regex.EnumerateMatches(input);
        Assert.False(matches.DebugIsOwnedSlice);
        var copy = matches;
        Assert.True(matches.MoveNext());
        Assert.Equal(new Utf8CaptureRange(10, 13), matches.Current);
        Assert.True(matches.MoveNext());
        Assert.Equal(new Utf8CaptureRange(25, 28), matches.Current);
        Assert.False(matches.MoveNext());
        Assert.True(copy.MoveNext());
        Assert.Equal(new Utf8CaptureRange(10, 13), copy.Current);

        var views = new List<(int, int)>();
        foreach (var split in regex.EnumerateSplits(input, count: 0))
            views.Add((split.IndexInBytes, split.LengthInBytes));
        Assert.Equal(new[] { (0, 10), (13, 12), (28, 0) }, views);
        views.Clear();
        foreach (var split in regex.EnumerateSplits(input, count: 2))
            views.Add((split.IndexInBytes, split.LengthInBytes));
        Assert.Equal(new[] { (0, 10), (13, 15) }, views);

        var whole = regex.EnumerateSplits(input, count: 1);
        Assert.True(whole.MoveNext());
        Assert.True(input.SequenceEqual(whole.Current.GetValueBytes()));
        Assert.False(whole.MoveNext());
    }

    [Theory]
    [InlineData("😀that😀", true)]
    [InlineData("there éthat", false)]
    [InlineData("𐐀the that\u200d", false)]
    [InlineData("the\u0301 xand !have!", true)]
    [InlineData("xfor the", true)]
    public void ControlledWordFamilyBooleanNeedsNoWorkingStorage(string text, bool expected)
    {
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            using var regex = new Utf8Regex(@"\b(?:the|and|that|have|for)\b", options, new()
            {
                ResourcePolicy = new() { MaxWorkSteps = 4096, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 }
            });
            Assert.Equal(expected, regex.IsMatch(input, token.Token));
            Assert.Equal(expected, regex.IsMatch(input, token.Token, new NoWorkingStorage()));
        }
    }

    [Fact]
    public void BoundaryFamilyCaptureSlotsKeepThePrefixAndClippedEndWithoutScratch()
    {
        using var regex = new Utf8Regex(@"\b(?:the|the-re|and|that|for)\b", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4096, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 }
        });
        using var cancellation = new CancellationTokenSource();
        var loan = new NoWorkingStorage();
        var input = "𐐀the 😀that\u200d !and! foré"u8;
        var andStart = "𐐀the 😀that\u200d !"u8.Length;
        var forStart = andStart + "and! "u8.Length;
        var sentinel = new Utf8CaptureRange(100, 101);
        Span<Utf8CaptureRange> slots = [sentinel, sentinel];

        var match = regex.Match(input, "𐐀"u8.Length, slots, cancellation.Token, loan);
        Assert.Equal(new Utf8CaptureRange(andStart, andStart + 3), match);
        Assert.Equal(match, slots[0]);
        Assert.False(regex.Match(input, forStart, slots, cancellation.Token, loan).Success);
        Assert.False(slots[0].Success);
        match = regex.Match(input, forStart, slots, cancellation.Token, loan, endOffsetInBytes: forStart + 3);
        Assert.Equal(new Utf8CaptureRange(forStart, forStart + 3), match);
        Assert.Equal(match, slots[0]);
        Assert.Equal(new Utf8CaptureRange(0, 3), regex.Match("the-re"u8, 0, slots, cancellation.Token, loan));
        Assert.Equal(new Utf8CaptureRange(0, 3), slots[0]);
        Assert.Equal(sentinel, slots[1]);
    }

    [Theory]
    [InlineData(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "𐐀Task Taské 😀Task😀 ValueTask", RegexOptions.None, 2)]
    [InlineData(@"\b(?:a-b|a|b)\b", "a-b", RegexOptions.None, 1)]
    [InlineData(@"\b(?:a|b|a-b)\b", "a-b", RegexOptions.None, 2)]
    [InlineData(@"\B(?:cat|dog)\b", "cat écat caté xdog", RegexOptions.None, 2)]
    [InlineData(@"\b(?:cat|dog)\b", "écat caté xdog", RegexOptions.ECMAScript, 2)]
    public void ControlledLiteralCountPreservesBoundariesAndBranchPriority(
        string pattern, string text, RegexOptions mode, int expected)
    {
        var options = mode | RegexOptions.CultureInvariant | RegexOptions.Compiled;
        var input = Encoding.UTF8.GetBytes(text);
        using var ordinary = new Utf8Regex(pattern, options);
        using var finite = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4096, MaxWorkingBytes = 1, MaxResultBytes = 1 }
        });
        using var token = new CancellationTokenSource();
        var allowance = new NoWorkingStorage();
        Assert.Equal(expected, finite.Count(input, token.Token));
        Assert.Equal(expected, ordinary.Count(input, token.Token, allowance));
        Assert.Equal(expected, finite.Count(input, token.Token, allowance));
    }

    [Fact]
    public void BorrowedCountAtAnOffsetKeepsThePrecedingScalar()
    {
        using var regex = new Utf8Regex(@"\b(?:Task|ValueTask)\b", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4096 }
        });
        var invocation = new Utf8SliceInvocation(new(4096, 1, 1024), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(1, regex.CountForResult("éTask Task"u8, 2, ref invocation));
    }

    [Fact]
    public void SharedLeadingBoundaryRejectsInteriorHeadsBeforeBranchWork()
    {
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("éthe ", 32)) + " and!");
        using var regex = new Utf8Regex(@"\b(?:the|and|that|have|for)\b", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 200 }
        });

        Assert.True(regex.IsMatch(bytes));
        using var cursor = regex.CreateMatchCursor(bytes);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal("and"u8.ToArray(), match.GetValueMemory().ToArray());
        Assert.False(cursor.MoveNext(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlledWordFamilySearchChecksItsBudgetAndOriginalToken(bool writeSlots)
    {
        const string pattern = @"\b(?:the|and|that|have|for)\b";
        using var denied = new Utf8Regex(pattern, policy: new() { ResourcePolicy = new() { MaxWorkSteps = 1 } });
        var sentinel = new Utf8CaptureRange(100, 101);
        Utf8CaptureRange[] slots = [sentinel, sentinel];

        void Search(Utf8Regex regex, ReadOnlySpan<byte> input, CancellationToken token = default,
            IUtf8MemoryAllowance? loan = null)
        {
            if (writeSlots) regex.Match(input, 0, slots, token, loan);
            else if (loan is null) regex.IsMatch(input, token);
            else regex.IsMatch(input, token, loan);
        }

        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Search(denied, "the"u8)).ResourceKind);
        if (writeSlots) Assert.False(slots[0].Success);
        slots[0] = sentinel;

        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4096, MaxWorkingBytes = 1 }
        });
        var input = new byte[160 * 1024];
        Array.Fill(input, (byte)'z');
        var session = Utf8SearchDiagnosticsSession.Start(_ => token.Cancel());
        try
        {
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                Search(regex, input, token.Token, new NoWorkingStorage())).CancellationToken);
            if (writeSlots) Assert.False(slots[0].Success);
            Assert.Equal(sentinel, slots[1]);
        }
        finally { session.Complete(); }
    }

    private sealed class NoWorkingStorage : IUtf8MemoryAllowance
    {
        public bool TryReserve(ulong bytes) => false;
        public void Release(ulong bytes) => throw new InvalidOperationException("No working storage was reserved.");
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, int.MaxValue)]
    public void BoundaryLiteralFamilyNativeSplitHonorsResultCount(bool compiled, int count)
    {
        const string pattern = @"\b(?:café|niño|résumé)\b";
        const string input = "café xcafé caféx niño résumé 😀café😀";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);
        var bytes = Encoding.UTF8.GetBytes(input);
        var actualValues = new List<string>();

        foreach (var split in actual.EnumerateSplits(bytes, count))
        {
            actualValues.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Assert.True(actual.CanUseNativeSplit());
        Assert.Equal(expected.Split(input, count), actualValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryLiteralFamilySplitSelectorExcludesUnsupportedShapes(bool compiled)
    {
        const string input = "café niño résumé café noir";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(input);
        (string Pattern, RegexOptions ExtraOptions)[] cases =
        [
            (@"\b(?:café|niño|résumé)\b", RegexOptions.RightToLeft),
        ];

        foreach (var testCase in cases)
        {
            var caseOptions = options | testCase.ExtraOptions;
            var expected = new Regex(testCase.Pattern, caseOptions);
            var actual = new Utf8Regex(testCase.Pattern, options: caseOptions);
            var actualValues = new List<string>();

            Assert.False(actual.CanUseNativeSplit());
            foreach (var split in actual.EnumerateSplits(bytes))
            {
                actualValues.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
            }

            var expectedValues = new List<string>();
            foreach (var split in expected.EnumerateSplits(input))
            {
                var (index, length) = split.GetOffsetAndLength(input.Length);
                expectedValues.Add(input.Substring(index, length));
            }

            Assert.Equal(expectedValues, actualValues);
        }
    }

    [Theory]
    [InlineData(@"\b(?:foo|foobar|baz)\b", "foobar foo foobarx baz xfoox")]
    [InlineData(@"(?:foo|foobar|baz)(?=!)", "foobar! foo! baz! foobar?")]
    public void AsciiPrefixLiteralFamilyRequirementsMatchDotNet(string pattern, string input)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            AssertGlobalParity(
                pattern,
                input,
                options,
                Regex.InfiniteMatchTimeout,
                expectedExecutionKind: null);
            AssertGlobalParity(
                pattern,
                input,
                options,
                TimeSpan.FromSeconds(1),
                expectedExecutionKind: null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf8PrefixLiteralFamilyRequirementsMatchDotNetRightToLeft(bool compiled)
    {
        const string pattern = @"\b(?:café|caféteria|niño)\b";
        const string input = "caféteria café cafétéria niño cafetería";
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected.IsMatch(input), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(input), actual.Count(bytes));
        var expectedMatch = expected.Match(input);
        var actualMatch = actual.Match(bytes);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
    }

    [Theory]
    [InlineData(@"\b(?:café|foo|𐐀)\b", 0, false)]
    [InlineData(@"\b(?:café|foo|𐐀)\b", 0, true)]
    [InlineData(@"\b(?:-a|a-|𐐀|café)\b", 0, false)]
    [InlineData(@"\b(?:-a|a-|𐐀|café)\b", 0, true)]
    [InlineData(@"\B(?:-a|a-|𐐀|café)\B", 6, false)]
    [InlineData(@"\B(?:-a|a-|𐐀|café)\B", 6, true)]
    public void DeseretLiteralBoundariesUseWordScalar(string pattern, int byteIndex, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        foreach (var timeout in new[] { Regex.InfiniteMatchTimeout, TimeSpan.FromSeconds(1) })
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = timeout
            });
            Assert.Equal(1, regex.Count("𐐀 x𐐀x"u8));
            var match = regex.Match("𐐀 x𐐀x"u8);
            Assert.True(match.Success);
            Assert.Equal((byteIndex, 4), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal(byteIndex == 0 ? "<> x𐐀x" : "𐐀 x<>x", Encoding.UTF8.GetString(regex.Replace("𐐀 x𐐀x"u8, "<>"u8)));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    private static void AssertGlobalParity(
        string pattern,
        string input,
        RegexOptions options,
        TimeSpan timeout,
        NativeExecutionKind? expectedExecutionKind = NativeExecutionKind.ExactUtf8Literals)
    {
        // This frozen literal alphabet uses emoji and Deseret Lu. Distinct unused
        // proxies preserve literal identity and word membership; ranges map back
        // to the original bytes. No classes, numeric ranges or other predicates
        // enter this comparator. Direct Deseret boundary witnesses are below.
        var subject = new Utf8AsciiScalarOperationAssertions.Subject(input);
        foreach (var scalar in pattern.EnumerateRunes())
            Assert.True(scalar.IsBmp || scalar.Value is 0x10400 or 0x1f600);
        Assert.False(pattern.Contains('\ue000') || pattern.Contains('\u0531'));
        var projectedPattern = pattern.Replace("\U00010400", "\u0531", StringComparison.Ordinal)
            .Replace("\U0001f600", "\ue000", StringComparison.Ordinal);
        var expected = new Regex(projectedPattern, options & ~RegexOptions.Compiled, timeout);
        var actual = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        Assert.Equal(new Regex(pattern, options & ~RegexOptions.Compiled).GetGroupNumbers(), expected.GetGroupNumbers());
        var bytes = subject.Input;

        if (expectedExecutionKind is { } executionKind)
        {
            Assert.Equal(executionKind, actual.Inspection.ExecutionKind);
        }
        Assert.Equal(expected.IsMatch(subject.Projected), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(subject.Projected), actual.Count(bytes));

        var expectedFirst = expected.Match(subject.Projected);
        var actualFirst = actual.Match(bytes);
        Assert.Equal(expectedFirst.Success, actualFirst.Success);
        if (expectedFirst.Success)
        {
            Assert.Equal(subject.Bytes(expectedFirst.Index, expectedFirst.Length), (actualFirst.StartOffsetInBytes, (actualFirst.EndOffsetInBytes - actualFirst.StartOffsetInBytes)));
        }

        var expectedMatches = new List<(int Index, int Length)>();
        foreach (var match in expected.EnumerateMatches(subject.Projected))
        {
            expectedMatches.Add(subject.Bytes(match.Index, match.Length));
        }

        var actualMatches = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(bytes))
        {
            actualMatches.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }

        Assert.Equal(expectedMatches, actualMatches);

        actualMatches.Clear();
        using (var cursor = actual.CreateMatchCursor(bytes))
            while (cursor.MoveNext(out var match))
                actualMatches.Add((match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        Assert.Equal(expectedMatches, actualMatches);

        var expectedSplits = new List<(int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in expected.EnumerateSplits(subject.Projected))
        {
            var (index, length) = split.GetOffsetAndLength(subject.Projected.Length);
            var (byteIndex, byteLength) = subject.Bytes(index, length);
            expectedSplits.Add((byteIndex, byteLength, subject.Value(index, length)));
        }

        var actualSplits = new List<(int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in actual.EnumerateSplits(bytes))
        {
            actualSplits.Add((
                split.IndexInBytes,
                split.LengthInBytes,
                Encoding.UTF8.GetString(split.GetValueBytes())));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.Equal(
            subject.Restore(expected.Replace(subject.Projected, "<$&>")),
            Encoding.UTF8.GetString(actual.Replace(bytes, "<$&>"u8)));
    }
}
