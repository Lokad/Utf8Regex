using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexConstructionTests
{
    [Fact]
    public void Utf8Pcre2RegexStoresConstructionProperties()
    {
        var settings = new Utf8Pcre2CompileSettings
        {
            AllowDuplicateNames = true,
            BackslashC = Pcre2BackslashCPolicy.Allow,
            AllowLookaroundBackslashK = true,
            Newline = Pcre2NewlineConvention.Any,
            Bsr = Pcre2BsrConvention.Unicode,
        };

        var limits = new Utf8Pcre2ExecutionLimits
        {
            MatchLimit = 123,
            DepthLimit = 45,
            HeapLimitInBytes = 678,
        };

        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.Caseless | Pcre2CompileOptions.Multiline, settings, limits, TimeSpan.FromSeconds(2));

        Assert.Equal("abc", regex.Pattern);
        Assert.Equal(Pcre2CompileOptions.Caseless | Pcre2CompileOptions.Multiline, regex.Options);
        Assert.Equal(settings, regex.CompileSettings);
        Assert.Equal(limits, regex.DefaultExecutionLimits);
        Assert.Equal(TimeSpan.FromSeconds(2), regex.MatchTimeout);
    }

    [Fact]
    public void Utf8Pcre2RegexUtf8PatternConstructorDecodesPattern()
    {
        var regex = new Utf8Pcre2Regex("caf\u00E9"u8, Pcre2CompileOptions.Caseless);

        Assert.Equal("caf\u00E9", regex.Pattern);
        Assert.Equal(Pcre2CompileOptions.Caseless, regex.Options);
    }

    [Fact]
    public void Utf8Pcre2RegexUsesDefaultMatchTimeoutWhenDefaultIsPassed()
    {
        var previous = Utf8Pcre2Regex.DefaultMatchTimeout;
        try
        {
            Utf8Pcre2Regex.DefaultMatchTimeout = TimeSpan.FromMilliseconds(1234);

            var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default, default, default);

            Assert.Equal(TimeSpan.FromMilliseconds(1234), regex.MatchTimeout);
        }
        finally
        {
            Utf8Pcre2Regex.DefaultMatchTimeout = previous;
        }
    }

    [Fact]
    public void Utf8Pcre2RegexRejectsNullPattern()
    {
        Assert.Throws<ArgumentNullException>(() => new Utf8Pcre2Regex((string)null!));
    }

    [Fact]
    public void Utf8Pcre2RegexReplaceRejectsNullReplacement()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Throws<ArgumentNullException>(() => regex.Replace("abc"u8, (string)null!));
    }

    [Fact]
    public void Utf8Pcre2RegexReplaceEvaluatorRejectsNullDelegate()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Throws<ArgumentNullException>(() => regex.Replace<int>("abc"u8, 0, null!));
    }

    [Fact]
    public void Utf8Pcre2RegexReplaceToStringEvaluatorRejectsNullDelegate()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Throws<ArgumentNullException>(() => regex.ReplaceToString<int>("abc"u8, 0, null!));
    }

    [Fact]
    public void Utf8Pcre2RegexCopyNumbersForNameRejectsNullName()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Throws<ArgumentNullException>(() => regex.CopyNumbersForName(null!, [], out _));
    }

    [Fact]
    public void Utf8Pcre2RegexTryGetFirstSetGroupRejectsNullName()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Throws<ArgumentNullException>(() => regex.TryGetFirstSetGroup("abc"u8, null!, out _));
    }

    [Fact]
    public void Utf8Pcre2RegexExposesDuplicateNameEntriesForSpecialPattern()
    {
        var regex = new Utf8Pcre2Regex(
            @"(?:(?<n>foo)|(?<n>bar))\k<n>",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true });

        Assert.Equal(2, regex.NameEntryCount);

        var entries = new Pcre2NameEntry[2];
        var written = regex.CopyNameEntries(entries, out var isMore);
        Assert.False(isMore);
        Assert.Equal(2, written);
        Assert.Equal("n", entries[0].Name);
        Assert.Equal(1, entries[0].Number);
        Assert.Equal("n", entries[1].Name);
        Assert.Equal(2, entries[1].Number);

        Span<int> numbers = stackalloc int[2];
        written = regex.CopyNumbersForName("n", numbers, out isMore);
        Assert.False(isMore);
        Assert.Equal(2, written);
        Assert.Equal(1, numbers[0]);
        Assert.Equal(2, numbers[1]);
    }

    [Fact]
    public void Utf8Pcre2RegexExposesManagedNamedGroupEntries()
    {
        var regex = new Utf8Pcre2Regex("(?<word>abc)");

        Assert.Equal(1, regex.NameEntryCount);

        var entries = new Pcre2NameEntry[1];
        var written = regex.CopyNameEntries(entries, out var isMore);
        Assert.False(isMore);
        Assert.Equal(1, written);
        Assert.Equal("word", entries[0].Name);
        Assert.Equal(1, entries[0].Number);
    }

    [Fact]
    public void Utf8Pcre2RegexAnalyzeReportsSpecialPatternFlags()
    {
        var regex = new Utf8Pcre2Regex(
            @"(?:(?<n>foo)|(?<n>bar))\k<n>",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true });

        var analysis = regex.Analyze();

        Assert.True(analysis.IsFullyNative);
        Assert.True(analysis.HasDuplicateNames);
        Assert.Equal(6, analysis.MinRequiredLengthInBytes);
        Assert.False(analysis.UsesBranchReset);
        Assert.False(analysis.UsesBacktrackingControlVerbs);
        Assert.False(analysis.UsesRecursion);
        Assert.False(analysis.MayProduceNonUtf8Slices);
        Assert.False(analysis.MayReportNonMonotoneMatchOffsets);
        Assert.False(analysis.RejectsNonMonotoneIterativeMatches);
        Assert.False(analysis.MayFailIterativeExecutionAtRuntime);
    }

    [Fact]
    public void Utf8Pcre2RegexAnalyzeReportsNonMonotoneLookaroundFlags()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=ab\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true });

        var analysis = regex.Analyze();

        Assert.True(analysis.IsFullyNative);
        Assert.False(analysis.UsesRecursion);
        Assert.True(analysis.MayReportNonMonotoneMatchOffsets);
        Assert.True(analysis.RejectsNonMonotoneIterativeMatches);
        Assert.False(analysis.MayFailIterativeExecutionAtRuntime);
    }

    [Fact]
    public void Utf8Pcre2RegexAnalyzeReportsSneakyIterativeRuntimeFailure()
    {
        var regex = new Utf8Pcre2Regex("a|(?(DEFINE)(?<sneaky>\\Ka))(?<=(?&sneaky))b");

        var analysis = regex.Analyze();

        Assert.True(analysis.IsFullyNative);
        Assert.True(analysis.UsesRecursion);
        Assert.False(analysis.MayReportNonMonotoneMatchOffsets);
        Assert.False(analysis.RejectsNonMonotoneIterativeMatches);
        Assert.True(analysis.MayFailIterativeExecutionAtRuntime);
    }

    [Fact]
    public void Utf8Pcre2RegexAnalyzeReportsRecursiveExecutionKinds()
    {
        var regex = new Utf8Pcre2Regex("a\\K.(?0)*");

        var analysis = regex.Analyze();

        Assert.True(analysis.UsesRecursion);
        Assert.False(analysis.MayFailIterativeExecutionAtRuntime);
    }

    [Fact]
    public void Utf8Pcre2RegexProbeOutsideSupportedProfileIsExplicitlyRejected()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        var exception = Assert.Throws<NotSupportedException>(() => regex.Probe("foo"u8, Pcre2PartialMode.Soft));

        Assert.Contains("does not support Probe", exception.Message);
    }

    [Fact]
    public void UnsupportedPatternThrowsNotSupportedInsteadOfNotImplemented()
    {
        var regex = new Utf8Pcre2Regex("a(*FAIL)b");

        var match = Assert.Throws<NotSupportedException>(() => regex.Match("abc"u8));
        Assert.Contains("SPEC-PCRE2", match.Message, StringComparison.Ordinal);

        var detailed = Assert.Throws<NotSupportedException>(() => regex.MatchDetailed("abc"u8));
        Assert.Contains("SPEC-PCRE2", detailed.Message, StringComparison.Ordinal);

        var enumerate = Assert.Throws<NotSupportedException>(() =>
        {
            var enumerator = regex.EnumerateMatches("abc"u8);
            enumerator.MoveNext();
        });
        Assert.Contains("SPEC-PCRE2", enumerate.Message, StringComparison.Ordinal);
    }
}
