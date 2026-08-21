using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
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
    public void Utf8Pcre2RegexOverloadUsesDefaultMatchTimeout()
    {
        var previous = Utf8Pcre2Regex.DefaultMatchTimeout;
        try
        {
            Utf8Pcre2Regex.DefaultMatchTimeout = TimeSpan.FromMilliseconds(1234);

            var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None);

            Assert.Equal(TimeSpan.FromMilliseconds(1234), regex.MatchTimeout);
        }
        finally
        {
            Utf8Pcre2Regex.DefaultMatchTimeout = previous;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(int.MaxValue)]
    public void Utf8Pcre2RegexRejectsInvalidExplicitMatchTimeout(int milliseconds)
    {
        var timeout = TimeSpan.FromMilliseconds(milliseconds);

        var stringException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default, default, timeout));
        var utf8Exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex("abc"u8, Pcre2CompileOptions.None, default, default, timeout));

        Assert.Equal("matchTimeout", stringException.ParamName);
        Assert.Equal("matchTimeout", utf8Exception.ParamName);
    }

    [Fact]
    public void Utf8Pcre2RegexAcceptsInfiniteMatchTimeout()
    {
        var regex = new Utf8Pcre2Regex(
            "abc",
            Pcre2CompileOptions.None,
            default,
            default,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);

        Assert.Equal(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, regex.MatchTimeout);
    }

    [Fact]
    public void Utf8Pcre2RegexDefaultMatchTimeoutRejectsInvalidValueWithoutMutation()
    {
        var previous = Utf8Pcre2Regex.DefaultMatchTimeout;
        try
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                Utf8Pcre2Regex.DefaultMatchTimeout = TimeSpan.FromMilliseconds(-2));

            Assert.Equal("value", exception.ParamName);
            Assert.Equal(previous, Utf8Pcre2Regex.DefaultMatchTimeout);
        }
        finally
        {
            Utf8Pcre2Regex.DefaultMatchTimeout = previous;
        }
    }

    [Fact]
    public void Utf8Pcre2RegexRejectsUnknownCompileOptionBits()
    {
        var unknown = (Pcre2CompileOptions)(1 << 29);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex("abc", unknown));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Utf8Pcre2RegexRejectsUnknownCompileSettingValues()
    {
        var newlineException = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex(
            "abc",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = (Pcre2NewlineConvention)999 },
            default,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout));
        var bsrException = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex(
            "abc",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Bsr = (Pcre2BsrConvention)999 },
            default,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout));
        var backslashCException = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex(
            "abc",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { BackslashC = (Pcre2BackslashCPolicy)999 },
            default,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout));

        Assert.Equal("compileSettings", newlineException.ParamName);
        Assert.Equal("compileSettings", bsrException.ParamName);
        Assert.Equal("compileSettings", backslashCException.ParamName);
    }

    [Fact]
    public void Utf8Pcre2RegexExposesDuplicateNameEntriesForSpecialPattern()
    {
        var regex = new Utf8Pcre2Regex(
            @"(?:(?<n>foo)|(?<n>bar))\k<n>",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

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
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

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
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);

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
    public void GenericBacktrackingProbeReturnsAvailableFullMatch()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        var probe = regex.Probe("foo"u8, Pcre2PartialMode.Soft);

        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal("foo", probe.GetMatch().GetValueString());
    }

    [Fact]
    public void FailVerbIsHandledByTheGenericOperations()
    {
        var regex = new Utf8Pcre2Regex("a(*FAIL)b");

        Assert.False(regex.Match("abc"u8).Success);
        Assert.False(regex.MatchDetailed("abc"u8).Success);
        var enumerator = regex.EnumerateMatches("abc"u8);
        Assert.False(enumerator.MoveNext());
    }
}
