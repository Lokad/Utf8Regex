using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Lokad.Utf8Regex.Tests;

/// <summary>R02a output accounting: owned fixed-template replacement matches
/// the BCL oracle on literals, group and special substitutions, unmatched
/// groups, empty advancement, and astral input, with ledger-charged growth and
/// no BCL fallback construction on owned paths.</summary>
public sealed class Utf8SliceReplaceTests
{
    private static void AssertReplaceParity(string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
            Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(replacement))));
            using var source = new CancellationTokenSource();
            Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement, source.Token)));
        }
    }

    [Theory]
    [InlineData("(a+)(b+)", "xxaaabbyy", "Z")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "[$1|$2]")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "$&!$&")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "$$")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$`>")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$'>")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$+>")]
    [InlineData("(a+)(b+)", "xxaaabbyy", "<$3>")]
    [InlineData("(a+)|(b+)", "xxaaayy", "<$1|$2>")]
    [InlineData("(ab|cd)+", "xxabcdcdyy", "[$1]")]
    [InlineData("a*?b", "xxaaabyy", "Z")]
    [InlineData("a*", "bb", "[Z]")]
    [InlineData("(a+)(b+)", "éaaabbé", "[$1|$2]")]
    [InlineData("(a+)(b+)", "xxaaabbyy zz", "Z $&")]
    [InlineData("\\t+", "a\tb", "-")]
    [InlineData("\\n+", "a\nb", "-")]
    [InlineData("\\cA+", "a\u0001b", "-")]
    public void OwnedReplaceMatchesOracle(string pattern, string inputText, string replacement)
    {
        AssertReplaceParity(pattern, inputText, replacement);
    }

    [Theory]
    [InlineData("(a.*b)", "xxaa1bbyy", "<$1>")]
    [InlineData("a(.*)b", "xa1bxa22by", "<$1>")]
    [InlineData("(a.c)+", "xxabcayc", "[$1]")]
    [InlineData(".*", "ab", "<$&>")]
    public void OwnedDotReplaceMatchesOracle(string pattern, string inputText, string replacement)
    {
        AssertReplaceParity(pattern, inputText, replacement);
    }

    [Fact]
    public void OwnedReplaceNeverConstructsBclFallback()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        var result = regex.Replace(Encoding.UTF8.GetBytes("xxaaabbyy"), "[$1]");
        Assert.Equal("xx[aaa]yy", Encoding.UTF8.GetString(result));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Fact]
    public void PreCanceledTokenAbortsOwnedReplace()
    {
        var regex = new Utf8Regex("(a+)(b+)", RegexOptions.CultureInvariant);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(
            () => regex.Replace(Encoding.UTF8.GetBytes("xxaaabbyy"), "Z", canceled.Token));
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralSliceReplaceOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a+)(b+)", "xxaaa\U0001F600bbyy", "[$1|$2]"),
            ("(a+)(b+)", "xxaaa\U0001F600bbyy", "Z"),
            ("(a+)(b+)", "xxaaa\U0001F600bbyy", "$&"),
            ("(a+)|(b+)", "xxaaa\U0001F600yy", "<$1|$2>"),
            ("(ab|cd)+", "xxab\U0001F600cdyy", "[$1]"),
            ("a*?b", "xxaaa\U0001F600byy", "Z"),
            ("(\\d+)-(\\d+)", "12-34\U0001F600", "[$1|$2]"),
            ("(\\d+)", "a12\U0001F600", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralSliceReplaceOwnedCases))]
    public void AstralSliceReplaceOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout).Replace(text, template);
        var bytesRegex = new Utf8Regex(pattern, options);
        Assert.True(bytesRegex.Inspection.DebugUsesOwnedSliceProgram);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(oracle, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(bytesRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, bytesRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(oracle, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(stringRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, stringRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(oracle, Encoding.UTF8.GetString(destination[..bytesWritten]));
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(destinationRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, destinationRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralSliceReplaceDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a.c)+", "xxabc\U0001F600ayc", "[$1]"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralSliceReplaceDeclineCases))]
    public void AstralSliceReplacementKeepsOracleAndOwnsShaping(string pattern, RegexOptions options, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout).Replace(text, template);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.True(stringRegex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(oracle, stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralSliceReplaceHalvesClosedOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a.*b)", "xxaa\U0001F600bbyy", "<$1>"),
            (".*", "a\U0001F600c", "<$&>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralSliceReplaceHalvesClosedOwnedCases))]
    public void AstralSliceReplaceHalvesClosedOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // Halves-closed star loops (bare dot-star and headed dot-star over
        // ASCII-indifferent runs) tile astral input unit-for-unit with the
        // oracle, so bytes, string, destination, and split lanes never
        // construct the fallback engine here. Divergent multi-atom branches
        // keep the decline battery above.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout).Replace(text, template);
        var bytesRegex = new Utf8Regex(pattern, options);
        Assert.True(bytesRegex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(oracle, Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template))));
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(bytesRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, bytesRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle, stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(stringRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, stringRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(oracle, Encoding.UTF8.GetString(destination[..bytesWritten]));
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(destinationRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, destinationRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var splitRegex = new Utf8Regex(pattern, options);
        var oracleSplits = new System.Collections.Generic.List<string>();
        foreach (var range in new Regex(pattern, options, Regex.InfiniteMatchTimeout).EnumerateSplits(text, int.MaxValue))
        {
            oracleSplits.Add(text[range.Start.Value..range.End.Value]);
        }
        var actualSplits = new System.Collections.Generic.List<string>();
        foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add(split.GetValueString());
        }
        Assert.Equal(oracleSplits, actualSplits);
        Assert.True(splitRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(splitRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, splitRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
