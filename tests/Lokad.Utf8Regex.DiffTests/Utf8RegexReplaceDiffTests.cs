using System.Text;
using System.Buffers;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexReplaceDiffTests
{
    [Theory]
    [InlineData("", "   ", "123", RegexOptions.None)]
    [InlineData("icrosoft", "MiCrOsOfT", "icrosoft", RegexOptions.IgnoreCase)]
    [InlineData("D\\.(.+)", "D.Bau", "David $1", RegexOptions.None)]
    [InlineData("(?<cat>cat)\\s*(?<dog>dog)", "cat dog", "${cat}est ${dog}est", RegexOptions.None)]
    public void ReplaceMatchesRuntimeForMirroredDotNetBatch(string pattern, string input, string replacement, RegexOptions options)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Replace.Tests.cs
        RegexParityContext.Create(pattern, input, options).AssertReplaceParity(replacement);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "ZZ")]
    [InlineData("abc", "nomatch", "ZZ")]
    [InlineData("needle", "needle in haystack", "pin")]
    public void ReplaceMatchesRuntimeForNativeLiteralSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("café", "αcaféβcaféγ", "tea")]
    [InlineData("😀", "😀x😀y", "!")]
    public void ReplaceMatchesRuntimeForNativeUtf8LiteralSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("café|niño", "αcaféβniñoγ", "tea")]
    [InlineData("café|niño|résumé", "résumé café niño", "x")]
    public void ReplaceMatchesRuntimeForNativeUtf8LiteralAlternationSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\b(?:café|niño)\b", "café niño caféx xniño niño", "tea")]
    [InlineData(@"\b(?:café|niño|résumé)\b", "résumé café niño", "x")]
    public void ReplaceMatchesRuntimeForBoundaryWrappedUtf8LiteralAlternationSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("café(?= noir)", "café noir café gris café noir", "bistro")]
    [InlineData("niño(?= listo)", "niño listo niño triste", "child")]
    public void ReplaceMatchesRuntimeForUtf8LiteralLookaheadSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("(café|niño)", "αcaféβniñoγ", "$1-x")]
    [InlineData("(café|niño|résumé)", "résumé café niño", "$1!")]
    public void ReplaceMatchesRuntimeForCapturedUtf8LiteralAlternationSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\b(café|niño)\b", "café niño caféx xniño niño", "$1-x")]
    [InlineData(@"\b(café|niño|résumé)\b", "résumé café niño", "$1!")]
    public void ReplaceMatchesRuntimeForCapturedBoundaryWrappedUtf8LiteralAlternationSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("(café)", "αcaféβcaféγ", "$1-bistro")]
    [InlineData("(😀)", "😀x😀y", "$1!")]
    public void ReplaceMatchesRuntimeForCapturedUtf8LiteralSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("(?<left>ab)(?<digit>[0-9])d", "ab1d xx ab2d", "${digit}${left}")]
    [InlineData("([A-Z])([0-9])", "A1 B2", "$2$1")]
    public void ReplaceMatchesRuntimeForDeterministicCaptureTemplateSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "$$")]
    [InlineData("abc", "xxabcxxabc", "$&")]
    [InlineData("abc", "xxabcxxabc", "$`|$'")]
    public void ReplaceUsesRuntimeReplacementSemanticsForNativeLiteralSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "x$$y")]
    [InlineData("ab.cd", "abXcd zz abYcd", "x$$y")]
    [InlineData("abc", "xxabcxxabc", "$1")]
    [InlineData("abc", "xxabcxxabc", "$10")]
    [InlineData("abc", "xxabcxxabc", "${1}")]
    [InlineData("abc", "xxabcxxabc", "${name}")]
    [InlineData("abc", "xxabcxxabc", "x$1y")]
    public void ReplaceKeepsLiteralOnlyReplacementPatternsOnNativePaths(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("abc", "xxABCxxabc", "ZZ")]
    [InlineData("abc", "nomatch", "ZZ")]
    public void ReplaceMatchesRuntimeForInvariantIgnoreCaseLiteralSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab.cd", "abXcd zz abYcd", "Q")]
    [InlineData("..a", "12a xx 34a", "Z")]
    [InlineData("ab[0-9]d", "ab1d xx ab2d", "Q")]
    [InlineData("a[^x]d", "ayd axd azd", "Z")]
    [InlineData("^abc", "abc abc", "Q")]
    [InlineData("abc$", "abc abc", "Q")]
    [InlineData("abc$", "abc\n", "Q")]
    [InlineData("^ab.cd$", "abXcd", "Q")]
    [InlineData(@"\d\d", "12 34", "Q")]
    [InlineData(@"a\sb", "a b axb", "Q")]
    [InlineData(@"\.", "a.b.c", "Q")]
    [InlineData(@"a{3}", "aaa x aaa", "Q")]
    [InlineData(@"\d{2}", "12 345", "Q")]
    [InlineData(@"[ab]{2}", "ab ba cc", "Q")]
    [InlineData(@"[\d][A-Z]", "3Z 9X zz", "Q")]
    [InlineData(@"[\w-][\s]", "_  -\t", "Q")]
    [InlineData(@"\n", "a\nb\nc", "Q")]
    [InlineData(@"[\r\n]", "\r\n", "Q")]
    [InlineData(@"\x41", "A BA", "Q")]
    [InlineData(@"[\x30A]", "0 A B", "Q")]
    [InlineData("cat|dog", "cat dog fish", "Q")]
    [InlineData(@"ab.cd|xy\d\d", "abXcd xy12 zz", "Q")]
    [InlineData("cat|horse", "cat horse fish", "Q")]
    [InlineData(@"ab?c", "ac abc abbc", "Q")]
    [InlineData(@"\d{2,4}", "1 12 123 1234", "Q")]
    [InlineData("(?:cat|horse)", "cat horse fish", "Q")]
    [InlineData("(ab.cd)", "abXcd zz", "Q")]
    public void ReplaceMatchesRuntimeForAsciiSimplePatternSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}", "AlphaBravo  xx Result yy ZetaTheta", "Q")]
    [InlineData(@"(?:[A-Z][a-z]+\s*){10,100}", "John Paul George Ringo Alice Bob Carol Dave Erin Frank", "Q")]
    public void ReplaceMatchesRuntimeForStructuralLinearLiteralReplacement(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab.cd", "xxABxCDyy", "ZZ")]
    [InlineData("a[bc]d", "zzaBdyy", "Q")]
    [InlineData(@"a\wb", "A_B a-b", "Q")]
    public void ReplaceMatchesRuntimeForInvariantIgnoreCaseAsciiSimplePatternSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab.cd", "abXcd zz abYcd", "$&!")]
    [InlineData("ab[0-9]d", "ab1d xx ab2d", "$$")]
    [InlineData("abc", "xxabcxxabc", "$`|$'|$_")]
    [InlineData("ab.cd", "abXcd zz abYcd", "$0!")]
    [InlineData("(ab)(cd)", "abcd xx abcd", "$2-$1")]
    [InlineData("([A-Z])([0-9])", "A1 B2", "$2$1")]
    [InlineData("(a)(b)?", "xab ay", "$2-$1")]
    [InlineData("(?<left>ab)(?<right>cd)", "abcd xx abcd", "${right}-${left}")]
    public void ReplaceUsesRuntimeReplacementSemanticsForAsciiSimplePatternSubset(string pattern, string input, string replacement)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("^abc", "abc\nzzz\nabc", "Q", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData("a.b", "a\nb xx a\nb", "Q", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    public void ReplaceMatchesRuntimeForAdditionalNativeOptionFamilies(string pattern, string input, string replacement, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab+", "ab abb abbb", "Q", RegexOptions.CultureInvariant)]
    [InlineData("café", "café café", "tea", RegexOptions.CultureInvariant)]
    [InlineData("(a)(b)?", "xab ay", "$`|$'|$+|$_", RegexOptions.CultureInvariant)]
    public void ReplaceFallsBackAndStillMatchesRuntime(string pattern, string input, string replacement, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void StaticReplaceMatchesInstanceBehavior()
    {
        var actual = Utf8Regex.Replace("xxabcxx"u8, "abc", "ZZ", RegexOptions.CultureInvariant);

        Assert.Equal("xxZZxx"u8.ToArray(), actual);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "piñata", RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", "茶", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d", "ab1d xx ab2d", "ZZ", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$&!", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$`|$'|$_", RegexOptions.CultureInvariant)]
    [InlineData("(ab)(cd)", "abcd xx abcd", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "$&!", RegexOptions.CultureInvariant)]
    [InlineData(@"\bé\b", "é a éb é é", "x", RegexOptions.CultureInvariant)]
    public void ReplaceUtf8OverloadMatchesRuntime(string pattern, string input, string replacement, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(replacement));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "piñata", RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", "茶", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "茶", RegexOptions.CultureInvariant)]
    public void TryReplaceWritesReplacementWhenDestinationIsLargeEnough(string pattern, string input, string replacement, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);
        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var destination = new byte[expected.Length];

        var status = utf8Regex.TryReplace(
            Encoding.UTF8.GetBytes(input),
            Encoding.UTF8.GetBytes(replacement),
            destination,
            out var bytesWritten);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected.Length, bytesWritten);
        Assert.Equal(expected, destination);
    }

    [Theory]
    [InlineData("(ab)(cd)", "abcd xx abcd", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$`|$'|$_", RegexOptions.CultureInvariant)]
    [InlineData("(a)(b)?", "xab ay", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "$&!", RegexOptions.CultureInvariant)]
    public void TryReplaceMatchesRuntimeForNativeReplacementPlans(string pattern, string input, string replacement, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);
        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var destination = new byte[expected.Length];

        var status = utf8Regex.TryReplace(
            Encoding.UTF8.GetBytes(input),
            Encoding.UTF8.GetBytes(replacement),
            destination,
            out var bytesWritten);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected.Length, bytesWritten);
        Assert.Equal(expected, destination);
    }

    [Fact]
    public void TryReplaceReturnsDestinationTooSmallWhenNeeded()
    {
        var utf8Regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);
        Span<byte> destination = stackalloc byte[3];
        "xyz"u8.CopyTo(destination);

        var status = utf8Regex.TryReplace("xxabcxx"u8, "toolong"u8, destination, out var bytesWritten);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(0, bytesWritten);
        Assert.Equal("xyz"u8.ToArray(), destination.ToArray());
    }

    [Fact]
    public void TryReplaceReturnsDestinationTooSmallForNativeReplacementPlan()
    {
        var utf8Regex = new Utf8Regex("(ab)(cd)", RegexOptions.CultureInvariant);
        Span<byte> destination = stackalloc byte[5];
        "12345"u8.CopyTo(destination);

        var status = utf8Regex.TryReplace("abcd xx abcd"u8, "$2-$1"u8, destination, out var bytesWritten);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(0, bytesWritten);
        Assert.Equal("12345"u8.ToArray(), destination.ToArray());
    }

    [Theory]
    [InlineData("(ab)+", "xxababyy", "Q", RegexOptions.CultureInvariant)]
    [InlineData("café", "café café", "tea", RegexOptions.CultureInvariant)]
    [InlineData("(a)(b)?", "xab ay", "$`|$'|$+|$_", RegexOptions.CultureInvariant)]
    public void ReplaceToStringMatchesRuntime(string pattern, string input, string replacement, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.Replace(input, pattern, replacement, options);
        var actual = utf8Regex.ReplaceToString(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReplaceEvaluatorMatchesRuntime()
    {
        const string input = "foo bar";
        const string pattern = "(\\w+)";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var state = 0;

        var actual = utf8Regex.Replace(
            Encoding.UTF8.GetBytes(input),
            state,
            static (in Utf8MatchContext match, ref Utf8ReplacementWriter writer, ref int count) =>
            {
                count++;
                writer.Append(match.GetGroup(1).GetValueString().AsSpan());
                writer.AppendAsciiByte((byte)'!');
            });

        var expected = Encoding.UTF8.GetBytes(
            Regex.Replace(input, pattern, static match => match.Groups[1].Value + "!"));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReplaceToStringEvaluatorMatchesRuntime()
    {
        const string input = "aa bb aaa";
        const string pattern = "(a)+";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var state = 0;

        var actual = utf8Regex.ReplaceToString(
            Encoding.UTF8.GetBytes(input),
            state,
            static (in Utf8MatchContext match, ref int count) =>
            {
                count += match.GetGroup(1).CaptureCount;
                return match.GetGroup(1).CaptureCount.ToString();
            });

        var expected = Regex.Replace(
            input,
            pattern,
            static match => match.Groups[1].Captures.Count.ToString(),
            RegexOptions.CultureInvariant);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReplaceMatchesRuntimeForEmptyNativeLiteral()
    {
        const string input = "abc";
        const string replacement = "-";
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(string.Empty, options);

        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, string.Empty, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), replacement);

        Assert.Equal(expected, actual);
    }
}
