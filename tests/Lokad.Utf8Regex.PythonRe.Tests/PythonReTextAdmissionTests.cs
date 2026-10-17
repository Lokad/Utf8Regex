using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTextAdmissionTests
{
    [Fact]
    public void OrdinarySpaceLiteralDoesNotExhaustDefaultConstructionWork()
    {
        var subject = new string(' ', 8192);
        var regex = new Utf8PythonRegex(subject);
        var bytes = System.Text.Encoding.UTF8.GetBytes(subject);
        var match = regex.FullMatch(bytes);
        Assert.True(match.Success);
        Assert.Equal((0, bytes.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.False(regex.FullMatch(bytes.AsSpan(1)).Success);
    }

    [Theory]
    [InlineData(1024)] [InlineData(4096)]
    public void DenseSpaceTranslationFitsLinearConstructionWork(int count)
    {
        var parse = new PythonReParser(new string(' ', count)).Parse(PythonReCompileOptions.None);
        var allowance = 128ul * (ulong)count;
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, allowance, ulong.MaxValue);
        var result = PythonReTranslator.Translate(parse);
        Assert.Equal(new string(' ', count), result.Pattern);
        Assert.Equal(result.Pattern, result.NativePattern);
        Assert.InRange(owner.Budget.WorkSteps, (ulong)count, allowance);
    }

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

            }
            if (allowance == peak) Assert.Null(failure);
            owner.Budget.ReleaseArray(parent, false);
        }
    }

    [Fact]
    public void TranslationRetainsRenderedTextAndNames()
    {
        var parse = new PythonReParser("(?P<é>a)(b)(?P=é)").Parse(PythonReCompileOptions.None);
        using var owner = Utf8ConstructionScope.Enter();
        var result = PythonReTranslator.Translate(parse);
        Assert.Equal("g1", result.EmittedGroupNames["é"]);
        Assert.Equal("(?<g1>a)(b)\\k<g1>", result.NativePattern);
        Assert.True(owner.Budget.ReservedBytes >= Utf8ConstructionBudget.StringBytes(result.Pattern.Length) + Utf8ConstructionBudget.StringBytes(result.NativePattern.Length));
    }

    [Theory]
    [InlineData("\\x61", "a")]
    [InlineData("\\u00e9", "é")]
    [InlineData("\\U0001f600", "😀")]
    [InlineData("\\141", "a")]
    [InlineData("\\0", "\0")]
    [InlineData("\\U0010FFFF", "\U0010FFFF")]
    [InlineData("^😀\\141\\.\\0[\\101]$", "😀a.\0A")]
    [InlineData("^(?:😀|X)\\!\\=\\:$", "😀!=:")]
    [InlineData("^(?:x|😀)\\377[\\7]$", "😀ÿ\u0007")]
    public void NumericEscapesKeepScalarMatchingWithoutTemporaryDigitStrings(string pattern, string subject)
    {
        var regex = new Utf8PythonRegex(pattern);
        var bytes = System.Text.Encoding.UTF8.GetBytes(subject);
        Assert.True(regex.IsMatch(bytes));
        Assert.True(regex.DebugUsesOwnedSlice);
    }
}
