using System.Text;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDiagnosticTests
{
    [Theory]
    [InlineData("😀[", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.Syntax, 2)]
    [InlineData("é😀\\q", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.Syntax, 3)]
    [InlineData("😀\\N{FACE}", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.UnsupportedFeature, 2)]
    [InlineData("[\\N{FACE}]", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.UnsupportedFeature, 1)]
    [InlineData("x", PythonReCompileOptions.Locale, PythonReErrorSource.CompileOptions, PythonReErrorKind.UnsupportedFeature, -1)]
    [InlineData("[", PythonReCompileOptions.Ascii | PythonReCompileOptions.Unicode, PythonReErrorSource.CompileOptions, PythonReErrorKind.InvalidOptions, -1)]
    [InlineData("(?a)(?u)x", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.InvalidOptions, -1)]
    [InlineData("(?au:x)", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.InvalidOptions, 4)]
    [InlineData("(?L)x", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.InvalidOptions, 3)]
    [InlineData("(?-a:x)", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.InvalidOptions, 4)]
    [InlineData("(?<=a+)", default(PythonReCompileOptions), PythonReErrorSource.Pattern, PythonReErrorKind.Syntax, -1)]
    public void ConstructionRootsReportClassificationAndSourceUnits(string pattern, PythonReCompileOptions options,
        PythonReErrorSource source, PythonReErrorKind kind, int position)
    {
        for (var root = 0; root < 4; root++)
        {
            var failure = Assert.Throws<PythonRePatternException>(() =>
            {
                using var regex = root switch
                {
                    0 => new Utf8PythonRegex(pattern, options),
                    1 => new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options),
                    2 => Utf8PythonRegex.Compile(pattern, options, Timeout.InfiniteTimeSpan, default,
                        default, Timeout.InfiniteTimeSpan, default),
                    _ => Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), options, Timeout.InfiniteTimeSpan,
                        default, default, Timeout.InfiniteTimeSpan, default),
                };
            });
            Assert.Equal(source, failure.ErrorSource); Assert.Equal(kind, failure.ErrorKind);
            Assert.Equal(position, failure.Position);
        }
    }

    [Theory]
    [InlineData("\\", 0)]
    [InlineData(@"\q", 0)]
    [InlineData(@"\g", 2)]
    [InlineData(@"\gX", 2)]
    [InlineData(@"\g<", 3)]
    [InlineData(@"\g<>", 3)]
    [InlineData(@"\g<2>", 3)]
    [InlineData(@"\2", 1)]
    [InlineData(@"\13", 1)]
    [InlineData(@"\777", 0)]
    [InlineData(@"\g<-1>", 3)]
    [InlineData(@"\g<99999999999999999999>", 3)]
    [InlineData(@"\g<0002147483648>", 3)]
    public void TemplatesHaveAuthoritativeUtf16PositionsEvenWithNoMatch(string suffix, int suffixPosition)
    {
        // Expected positions use CPython's template token locations, projected
        // to this API's documented UTF-16 units, rather than scalar indices.
        using var regex = new Utf8PythonRegex("(a)");
        foreach (var prefix in new[] { "", "é😀" })
        foreach (var input in new[] { Array.Empty<byte>(), "zzz"u8.ToArray() })
        {
            var template = prefix + suffix;
            var bytes = Assert.Throws<PythonRePatternException>(() => regex.Subn(input, template));
            var text = Assert.Throws<PythonRePatternException>(() => regex.SubnToString(input, template));
            foreach (var error in new[] { bytes, text })
            {
                Assert.Equal(PythonReErrorSource.Replacement, error.ErrorSource);
                Assert.Equal(PythonReErrorKind.Syntax, error.ErrorKind);
                Assert.Equal(prefix.Length + suffixPosition, error.Position);
            }
        }
    }

    [Fact]
    public void FundedFailuresRefundDiscardedStorageAndPreserveCachedPlans()
    {
        var account = new Allowance(4 << 20); Assert.True(account.TryReserve(17));
        var failure = Assert.Throws<PythonRePatternException>(() => Utf8PythonRegex.Compile("😀[", default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, account));
        Assert.Equal(PythonReErrorSource.Pattern, failure.ErrorSource); Assert.Equal(2, failure.Position);
        Assert.Equal(17UL, account.Used);
        using var regex = Utf8PythonRegex.Compile("(?P<x>a)", default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account);
        Assert.Equal("a!", regex.SubnToString("a"u8, @"\g<x>!").ResultText); var held = account.Used;
        var replacement = new string('x', 131072) + "é😀" + @"\q";
        failure = Assert.Throws<PythonRePatternException>(() => regex.SubnRetained(""u8, replacement));
        Assert.Equal(PythonReErrorSource.Replacement, failure.ErrorSource);
        Assert.Equal(PythonReErrorKind.Syntax, failure.ErrorKind); Assert.Equal(131075, failure.Position);
        Assert.Equal(held, account.Used);
        Assert.Equal("a!", regex.SubnToString("a"u8, @"\g<x>!").ResultText);
        Assert.Equal(held, account.Used);
        using var token = new CancellationTokenSource(); account.AfterGrant = token.Cancel;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Subn(""u8, @"new\g<x>", 0, 0, token.Token)).CancellationToken);
        account.AfterGrant = null; Assert.Equal(held, account.Used);
        regex.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
    }

    [Fact]
    public void SyntaxStillPrecedesCombinedInlineOptionValidation()
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("(?a)(?u)["));
        Assert.Equal(PythonReErrorSource.Pattern, error.ErrorSource);
        Assert.Equal(PythonReErrorKind.Syntax, error.ErrorKind); Assert.Equal(8, error.Position);
    }

    [Fact]
    public void NamedReplacementLookupKeepsItsExistingExceptionDistinction()
    {
        using var regex = new Utf8PythonRegex("(a)");
        Assert.Throws<IndexOutOfRangeException>(() => regex.Subn(""u8, @"é😀\g<absent>"));
    }

    [Fact]
    public void ExistingPublicExceptionConstructorsDefaultToPatternSyntax()
    {
        var unknown = new PythonRePatternException("custom"); var positioned = new PythonRePatternException("custom", 5);
        Assert.Equal(-1, unknown.Position); Assert.Equal(5, positioned.Position);
        foreach (var error in new[] { unknown, positioned })
        {
            Assert.Equal(PythonReErrorSource.Pattern, error.ErrorSource);
            Assert.Equal(PythonReErrorKind.Syntax, error.ErrorKind); Assert.Equal("custom", error.Message);
        }
    }
}
