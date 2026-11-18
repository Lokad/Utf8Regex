using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// The oracle checks the overlapping grammar's rejection verdict. Product
// admission owns its diagnostics and keeps them stable across constructors.
public sealed class Utf8CompileFailureDiffTests
{
    [Theory]
    [InlineData("(unterminated")]
    [InlineData("[z-a]")]
    [InlineData("*a")]
    [InlineData("a{2,1}")]
    [InlineData("\\")]
    [InlineData("[a")]
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("(?n:(?<-x>a))")]
    [InlineData("(?<=(?<1a>b))c")]
    [InlineData("[\\A]+")]
    [InlineData("[\\z]+")]
    [InlineData("[\\B]+")]
    public void InvalidPatternsHaveOwnedFailuresOnEveryRoute(string pattern)
    {
        var options = RegexOptions.CultureInvariant;
        var expected = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, options));
        var expectedCompiled = Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, options | RegexOptions.Compiled));
        var actual = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options: options));
        var actualCompiled = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options: options | RegexOptions.Compiled));

        Assert.Equal("pattern", actual.ParamName);
        Assert.Equal("pattern", actualCompiled.ParamName);
        Assert.Contains("UTF-16 pattern offset", actual.Message);
        Assert.Equal(actual.Message, actualCompiled.Message);
    }

    [Theory]
    [InlineData("(?n:(?<-x>a))", RegexParseError.UndefinedNamedReference, 9)]
    [InlineData("(?<=(?<1a>b))c", RegexParseError.CaptureGroupNameInvalid, 8)]
    public void InvalidNamesAndPopsHaveOwnedFailures(string pattern, RegexParseError error, int offset)
    {
        // Preserve rejection of these malformed names/pops on every constructor.
        // Oracle error attributes remain historical grammar evidence.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options: options));
            var actualTimeout = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout
            }));
            var actualDefault = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern));

            Assert.Equal(error, expected.Error);
            Assert.Equal(offset, expected.Offset);
            Assert.Equal("pattern", actual.ParamName);
            Assert.Equal("pattern", actualTimeout.ParamName);
            Assert.Equal("pattern", actualDefault.ParamName);
            Assert.Contains("UTF-16 pattern offset", actual.Message);
            Assert.Equal(actual.Message, actualTimeout.Message);
            Assert.Equal(actual.Message, actualDefault.Message);
        }

    }

    [Theory]
    [InlineData("(?<x>a)")]
    [InlineData("(?<1>a)")]
    [InlineData("(?<a>x)(?<-a>y)")]
    [InlineData("(?n:(?<a>x)(?<-a>y))")]
    public void FlipWideningControlsStillConstruct(string pattern)
    {
        // Valid named/numeric groups and balancing pops keep constructing on every
        // route; the flip rejects only the undefined pop and the invalid name.
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options);
            var product = new Utf8Regex(pattern, options: options);
            var productTimeout = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout
            });
            var copiedNames26 = new string[product.CaptureGroupCount + 1];
            for (var captureSlot26 = 0; captureSlot26 < copiedNames26.Length; captureSlot26++)
            {
                Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                    product.TryCopyGroupNameUtf8(captureSlot26, [], out var nameLength26));
                var nameBytes26 = new byte[nameLength26];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    product.TryCopyGroupNameUtf8(captureSlot26, nameBytes26, out var written26));
                Assert.Equal(nameLength26, written26);
                copiedNames26[captureSlot26] = System.Text.Encoding.UTF8.GetString(nameBytes26);
            }

            Assert.Equal(oracle.GetGroupNames(), copiedNames26);
            Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, product.CaptureGroupCount + 1).Select(product.GroupNumberFromCaptureSlot).ToArray());
            var copiedNames27 = new string[productTimeout.CaptureGroupCount + 1];
            for (var captureSlot27 = 0; captureSlot27 < copiedNames27.Length; captureSlot27++)
            {
                Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                    productTimeout.TryCopyGroupNameUtf8(captureSlot27, [], out var nameLength27));
                var nameBytes27 = new byte[nameLength27];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    productTimeout.TryCopyGroupNameUtf8(captureSlot27, nameBytes27, out var written27));
                Assert.Equal(nameLength27, written27);
                copiedNames27[captureSlot27] = System.Text.Encoding.UTF8.GetString(nameBytes27);
            }
            Assert.Equal(oracle.GetGroupNames(), copiedNames27);
        }
    }
}
