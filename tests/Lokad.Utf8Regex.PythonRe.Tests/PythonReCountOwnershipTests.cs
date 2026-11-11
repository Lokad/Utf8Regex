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
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
        Assert.Equal(regex.FindAll(input, startOffsetInBytes).Length, regex.Count(input, startOffsetInBytes));
    }

    [Fact]
    public void SplitBackendCountAgreesWithFindAll()
    {
        // Count and FindAll share scalar discovery with Python empty-match
        // adjacency and agree with the pinned count.
        var regex = new Utf8PythonRegex("\\b|\\w+", PythonReCompileOptions.None);
        var input = Encoding.UTF8.GetBytes("\u00E9 skip y");
        Assert.Equal(3, regex.Count(input, 8));
        Assert.Equal(3, regex.FindAll(input, 8).Length);
    }

    [Theory]
    [InlineData("(?P<word>foo)-(?P=word)", PythonReCompileOptions.None, "xx foo-foo yy foo-foo", 3, 2)]
    public void NamedReferenceCountRunsOwned(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, regex.Count(input, startOffsetInBytes));
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void CapturedRepeatCountNeedsNoScratchAndPreservesActualCaptureValues(Utf8InputValidation validation)
    {
        var policy = new Utf8ResourcePolicy
            { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        const string pattern = "([A-Za-z]){2,3}";
        var count = new Utf8PythonRegex(pattern, resourcePolicy: policy, inputValidation: validation);
        var input = "éabcdefg 😀 ab"u8.ToArray();
        using var token = new CancellationTokenSource();
        Assert.Equal(3, count.Count(input));
        Assert.Equal(2, count.Count(input, 5, token.Token));
        var values = new Utf8PythonRegex(pattern, inputValidation: validation).FindAllToStrings(input);
        Assert.Equal(new[] { "c", "f", "b" }, values.ScalarValues);
        if (validation == Utf8InputValidation.Validate)
            Assert.Throws<ArgumentException>(() => count.Count([.. input, 0xff]));
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void IndependentCapturedCountsNeedNoCaptureScratch(Utf8InputValidation validation)
    {
        var policy = new Utf8ResourcePolicy
            { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        foreach (var (pattern, text, expected, afterFirst) in new[]
        {
            ("([a-z]+)-([0-9]+)", "é😀item-12 other-345", 2, 14),
            ("item-([0-9]+)", "é😀item-12 item-345", 2, 14),
            ("([a-z]+)([0-9]+)", "é😀item12 other345", 2, 13),
        })
        {
            using var regex = new Utf8PythonRegex(pattern, resourcePolicy: policy, inputValidation: validation);
            var input = Encoding.UTF8.GetBytes(text);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.Count(input));
                Assert.Equal(validation == Utf8InputValidation.Validate ? 1 : 0, session.SubjectValidations);
            }
            finally { session.Complete(); }
            using var token = new CancellationTokenSource();
            Assert.Equal(1, regex.Count(input, afterFirst, token.Token));
            Assert.Equal(0, regex.Count(input, input.Length));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 1));
            if (validation == Utf8InputValidation.Validate)
            {
                Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff]));
                Assert.Throws<ArgumentException>(() => regex.Count([0xff, .. input], 7));
            }
            using var detailed = new Utf8PythonRegex(pattern, inputValidation: validation);
            var retained = detailed.SearchDetailedData(input);
            Assert.Equal(expected, detailed.Count(input));
            Assert.Equal("12", retained.Groups[^1].ValueText);
        }
    }

    [Theory]
    [InlineData(@"([a-z]+)-\1", "ab-ab")]
    [InlineData("(a)?b(?(1)c|d)", "abc bd")]
    [InlineData("(?=(a+))a", "aaa")]
    [InlineData("(a*)", "aaa")]
    public void CaptureReadersAssertionsAndNullableProgressionRetainAdmission(string pattern, string text)
    {
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            using var regex = new Utf8PythonRegex(pattern, resourcePolicy: new() { MaxWorkingBytes = 1 },
                inputValidation: validation);
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void CountPreservesProgramFundingAndAdmitsRemainingVmStorage(Utf8InputValidation validation)
    {
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(16 << 20);
        Assert.True(account.TryReserve(17));
        using var simple = Utf8PythonRegex.Compile("([a-z]+)-([0-9]+)", default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, account, validation);
        using var compound = Utf8PythonRegex.Compile("((ab)+)x", default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, account, validation);
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
        simple.Dispose(); compound.Dispose();
        Assert.Equal(17UL, account.Used);
        account.Release(17);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void CaptureFreeCountRetainsEveryUsefulWorkCutoff(Utf8InputValidation validation)
    {
        const string pattern = "([a-z]+)-([0-9]+)";
        var input = "é😀item-12 other-345"u8.ToArray();
        var succeeded = false;
        for (ulong limit = 1; limit <= 256; limit++)
        {
            using var regex = new Utf8PythonRegex(pattern, resourcePolicy: new()
                { MaxWorkSteps = limit, MaxDepth = 256, MaxWorkingBytes = 1, MaxResultBytes = 1 },
                inputValidation: validation);
            int? published = null;
            var failure = Record.Exception(() => published = regex.Count(input));
            if (failure is null) { Assert.Equal(2, published); succeeded = true; break; }
            var denied = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denied.ResourceKind);
            Assert.Null(published);
        }
        Assert.True(succeeded, "The bounded witness never admitted its complete count.");
    }
}
