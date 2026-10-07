using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTextAdmissionTests
{
    [Theory]
    [InlineData("(?P<é>a)(?P=é)")]
    [InlineData("(?i:[a-z😀-🙏])")]
    [InlineData("\\U00010400{2}(a)(?(1)b|c)")]
    [InlineData("(?s:a.*b)")]
    public void TranslationGrowthDenialsPreserveParentBytesDepthAndRoots(string pattern)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        ulong peak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = PythonReTranslator.Translate(parse);
            peak = measured.Budget.PeakBytes;
        }
        foreach (var allowance in new[] { 0ul, 112ul, peak / 2, peak - 1, peak })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, allowance + 48, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(1, false);
            var failure = Record.Exception(() => PythonReTranslator.Translate(parse));
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.True(owner.Budget.PeakBytes <= allowance + 48);
            if (failure is not null)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
                Assert.Null(owner.Budget.ObjectCheckpoint);
            }
            if (allowance == peak) Assert.Null(failure);
            owner.Budget.ReleaseArray(parent, false);
        }
    }

    [Fact]
    public void TranslationRetainsRenderedTextNamesAndNumericProjectionTables()
    {
        var parse = new PythonReParser("(?P<é>a)(b)(?P=é)").Parse(PythonReCompileOptions.None);
        using var owner = Utf8ConstructionScope.Enter();
        var result = PythonReTranslator.Translate(parse);
        Assert.Equal("g1", result.EmittedGroupNames["é"]);
        Assert.Equal(new[] { 0, 2, 1 }, result.ManagedNumbersByPython);
        Assert.Equal("(?<g1>a)(b)\\k<g1>", result.NativePattern);
        var retainedStrings = new List<object>();
        for (var allocation = owner.Budget.ObjectCheckpoint; allocation is not null; allocation = allocation.Previous)
            if (allocation.Value is string) retainedStrings.Add(allocation.Value);
        Assert.Contains(retainedStrings, value => ReferenceEquals(value, result.Pattern));
        Assert.Contains(retainedStrings, value => ReferenceEquals(value, result.NativePattern));
        Assert.Contains(retainedStrings, value => ReferenceEquals(value, result.EmittedGroupNames["é"]));
        Assert.True(owner.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<int>(3)
            + Utf8ConstructionBudget.StringBytes(result.Pattern.Length) + Utf8ConstructionBudget.StringBytes(result.NativePattern.Length));
    }

    [Theory]
    [InlineData("\\x61", "a")]
    [InlineData("\\u00e9", "é")]
    [InlineData("\\U0001f600", "😀")]
    [InlineData("\\141", "a")]
    [InlineData("\\0", "\0")]
    [InlineData("\\U0010FFFF", "\U0010FFFF")]
    public void NumericEscapesKeepScalarMatchingWithoutTemporaryDigitStrings(string pattern, string subject)
    {
        var regex = new Utf8PythonRegex(pattern);
        var bytes = System.Text.Encoding.UTF8.GetBytes(subject);
        Assert.True(regex.IsMatch(bytes));
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }
}
