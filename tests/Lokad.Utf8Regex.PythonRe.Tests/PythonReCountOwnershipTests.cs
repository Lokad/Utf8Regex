using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReCountOwnershipTests
{
    public static IEnumerable<object?[]> OwnedCountCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse cat", 0, 3 };
        yield return new object?[] { "cat|horse", none, "cat horse cat", 4, 2 };
        yield return new object?[] { "cat|horse", none, "cat horse cat", 10, 1 };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse cat", 4, 2 };
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, 2 };
        yield return new object?[] { "(cat)", none, "cat horse cat", 4, 1 };
        yield return new object?[] { "([A-Za-z]{2,3})", none, "éabcdefg 😀 ab", 0, 3 };
        yield return new object?[] { "([A-Za-z]){2,3}", none, "éabcdefg 😀 ab", 5, 2 };
        yield return new object?[] { "(([A-Za-z]{2,3}))", none, "éabcdefg 😀 ab", 0, 3 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, 2 };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 4, 1 };
        yield return new object?[] { "(?:CAT|DOG)", ignoreCase, "cat dog", 0, 2 };
        yield return new object?[] { "(?:CAT|DOG)", ignoreCase, "cat dog", 4, 1 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, 2 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 9, 1 };
        yield return new object?[] { "needle|needle-long", none, "needle-long needle", 0, 2 };
        yield return new object?[] { "needle|needle-long", none, "needle-long needle", 6, 1 };
        yield return new object?[] { "cat|horse", none, "no match", 0, 0 };
        yield return new object?[] { "cat|horse", none, "", 0, 0 };
        yield return new object?[] { "(needle)", none, "é😀needle 中needle", 6, 2 };
        yield return new object?[] { "café|😀", none, "é😀café😀café", 6, 3 };
        yield return new object?[] { "([a-z]+)-([0-9]+)", none, "é😀item-12 other-345", 6, 2 };
        yield return new object?[] { "([a-z]+)([0-9]+)", none, "é😀item12 other345", 12, 1 };
        yield return new object?[] { "(?i:([a-z]+))-([0-9]+)", none, "İ-12 ı-3 ſ-4 K-5", 0, 4 };
        yield return new object?[] { @"([a-z]+)-\1", none, "ab-ab ab-ac ab-ab", 0, 2 };
        yield return new object?[] { @"(?i:(k+))\1", none, "Kk KK", 0, 1 };
        yield return new object?[] { "(a)?b(?(1)c|d)", none, "abc bd abx", 0, 2 };
        yield return new object?[] { "(?=(a+))a", none, "aa ba", 0, 3 };
        yield return new object?[] { "(a*)", none, "aa b", 0, 4 };
    }
    [Theory]
    [MemberData(nameof(OwnedCountCases))]
    public void OwnedCountMatchesFindAll(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, startOffsetInBytes, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(snapshots1.Count, regex.Count(input, startOffsetInBytes));
    }
    [Fact]
    public void SplitBackendCountAgreesWithFindAll()
    {
        // Count and FindAll share scalar discovery with Python empty-match
        // adjacency and agree with the pinned count.
        var regex = new Utf8PythonRegex("\\b|\\w+", options: PythonReCompileOptions.None);
        var input = Encoding.UTF8.GetBytes("\u00E9 skip y");
        Assert.Equal(3, regex.Count(input, 8));
        ReadOnlyMemory<byte> snapshots2Input = input;
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 8, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Equal(3, snapshots2.Count);
    }
    [Theory]
    [InlineData("(?P<word>foo)-(?P=word)", PythonReCompileOptions.None, "xx foo-foo yy foo-foo", 3, 2)]
    public void NamedReferenceCountRunsOwned(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
    }
    [Fact]

    public void CapturedRepeatCountNeedsNoScratchAndPreservesActualCaptureValues()
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000,
            MaxDepth = 1,
            MaxWorkingBytes = 1,
            MaxResultBytes = 1
        };
        const string pattern = "([A-Za-z]){2,3}";
        var count = new Utf8PythonRegex(pattern, policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = policy
        });
        var input = "éabcdefg 😀 ab"u8.ToArray();
        using var token = new CancellationTokenSource();
        Assert.Equal(3, count.Count(input));
        Assert.Equal(2, count.Count(input, 5, token.Token));
        var snapshots3Regex = new Utf8PythonRegex(pattern);
        ReadOnlyMemory<byte> snapshots3Input = input;
        var snapshots3 = new List<Utf8PythonRangeMatch>();
        using (var snapshots3Cursor = snapshots3Regex.CreateMatchCursor(snapshots3Input, 0, default))
        {
            while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                snapshots3.Add(snapshots3Match);
        }
        var values = snapshots3;
        Assert.Equal(new[] { "c", "f", "b" }, values.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }
    [Fact]

    public void IndependentCapturedCountsNeedNoCaptureScratch()
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 100000,
            MaxDepth = 256,
            MaxWorkingBytes = 1,
            MaxResultBytes = 1
        };
        foreach (var (pattern, text, expected, afterFirst) in new[]
        {
            ("([a-z]+)-([0-9]+)", "é😀item-12 other-345", 2, 14),
            ("item-([0-9]+)", "é😀item-12 item-345", 2, 14),
            ("([a-z]+)([0-9]+)", "é😀item12 other345", 2, 13),
        })
        {
            using var regex = new Utf8PythonRegex(pattern, policy: new Utf8RegexPolicy()
            {
                ResourcePolicy = policy
            });
            var input = Encoding.UTF8.GetBytes(text);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.Count(input));
            }
            finally
            {
                session.Complete();
            }
            using var token = new CancellationTokenSource();
            Assert.Equal(1, regex.Count(input, afterFirst, token.Token));
            Assert.Equal(0, regex.Count(input, input.Length));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 1));
            using var detailed = new Utf8PythonRegex(pattern);
            Utf8PythonRangeMatch retained = default;
            using (var captureCursor1 = detailed.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out retained);
            }
            Assert.Equal(expected, detailed.Count(input));
            Assert.Equal("12", System.Text.Encoding.UTF8.GetString((retained.Groups[retained.Groups.Length - 1].Success ? retained.GetGroupMemory(retained.Groups[retained.Groups.Length - 1].Number).Span : ReadOnlySpan<byte>.Empty)));
        }
    }
    [Theory]
    [InlineData(@"([a-z]+)-\1", "ab-ab")]
    [InlineData("(a)?b(?(1)c|d)", "abc bd")]
    [InlineData("(?=(a+))a", "aaa")]
    [InlineData("(a*)", "aaa")]
    public void CaptureReadersAssertionsAndNullableProgressionRetainAdmission(string pattern, string text)
    {
        {
            using var regex = new Utf8PythonRegex(pattern, policy: new Utf8RegexPolicy()
            {
                ResourcePolicy = new()
                {
                    MaxWorkingBytes = 1
                }
            });
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input)).ResourceKind);
        }
    }
    [Fact]

    public void CountPreservesProgramFundingAndAdmitsRemainingVmStorage()
    {
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(16 << 20);
        Assert.True(account.TryReserve(17));
        using var simple = Utf8PythonRegex.Compile("([a-z]+)-([0-9]+)", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = account
        }, cancellationToken: default);
        using var compound = Utf8PythonRegex.Compile("((ab)+)x", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = account
        }, cancellationToken: default);
        var held = account.Used;
        account.Maximum = held;
        Assert.Equal(2, simple.Count("é😀item-12 other-345"u8));
        Assert.Equal(held, account.Used);
        Assert.Throws<Utf8MemoryAdmissionException>(() => compound.Count("ababx abx"u8));
        Assert.Equal(held, account.Used);
        account.Maximum = 16 << 20;
        Assert.Equal(2, compound.Count("ababx abx"u8));
        Assert.Equal(held, account.Used);
        using var token = new CancellationTokenSource();
        account.AfterGrant = token.Cancel;
        Assert.Equal(token.Token,
            Assert.Throws<OperationCanceledException>(() => compound.Count("ababx abx"u8, 0, token.Token)).CancellationToken);
        account.AfterGrant = null;
        Assert.Equal(held, account.Used);
        simple.Dispose();
        compound.Dispose();
        Assert.Equal(17UL, account.Used);
        account.Release(17);
    }
    [Fact]

    public void CaptureFreeCountRetainsEveryUsefulWorkCutoff()
    {
        const string pattern = "([a-z]+)-([0-9]+)";
        var input = "é😀item-12 other-345"u8.ToArray();
        var succeeded = false;
        for (ulong limit = 1; limit <= 256; limit++)
        {
            using var regex = new Utf8PythonRegex(pattern, policy: new Utf8RegexPolicy()
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = limit,
                    MaxDepth = 256,
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                }
            });
            int? published = null;
            var failure = Record.Exception(() => published = regex.Count(input));
            if (failure is null)
            {
                Assert.Equal(2, published);
                succeeded = true;
                break;
            }
            var denied = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denied.ResourceKind);
            Assert.Null(published);
        }
        Assert.True(succeeded, "The bounded witness never admitted its complete count.");
    }
}
