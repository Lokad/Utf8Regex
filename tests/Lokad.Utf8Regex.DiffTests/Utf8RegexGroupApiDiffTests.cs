using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

/// <summary>
/// R02 extraction 4: owned group metadata answers every group query identically to the
/// BCL oracle across plain, numbered, named, unicode-named, non-capturing, nested,
/// backreference, inline-option, balancing, conditional, ECMAScript, right-to-left,
/// ExplicitCapture, and empty patterns, including undefined names, out-of-range
/// numbers, and null-name rejection.
/// </summary>
public sealed class Utf8RegexGroupApiDiffTests
{
    public static IEnumerable<object?[]> GroupPatterns()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant };
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<word>\\w+)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<caf\u00e9>x)(y)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?:a)(b)", RegexOptions.CultureInvariant };
        yield return new object?[] { "((a)(b(c)))", RegexOptions.CultureInvariant };
        yield return new object?[] { "(a)\\1", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?i:abc)(d)", RegexOptions.CultureInvariant };
        yield return new object?[] { "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))", RegexOptions.CultureInvariant };
        yield return new object?[] { "(a)?(?(1)ab|cd)", RegexOptions.CultureInvariant };
        yield return new object?[] { "([a-z])+", RegexOptions.ECMAScript };
        yield return new object?[] { "(a)(b)", RegexOptions.RightToLeft | RegexOptions.CultureInvariant };
        yield return new object?[] { "(a)(b)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant };
        yield return new object?[] { string.Empty, RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<1>a)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<b>a)(c)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<a>x)(b)(?<c>y)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<a>(b))+", RegexOptions.CultureInvariant };
    }

    [Theory]
    [MemberData(nameof(GroupPatterns))]
    public void GroupApisMatchRuntime(string pattern, RegexOptions options)
    {
        var utf8 = new Utf8Regex(pattern, options: options);
        var runtime = new Regex(pattern, options);
        var copiedNames28 = new string[utf8.CaptureGroupCount + 1];
        for (var captureSlot28 = 0; captureSlot28 < copiedNames28.Length; captureSlot28++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                utf8.TryCopyGroupNameUtf8(captureSlot28, [], out var nameLength28));
            var nameBytes28 = new byte[nameLength28];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                utf8.TryCopyGroupNameUtf8(captureSlot28, nameBytes28, out var written28));
            Assert.Equal(nameLength28, written28);
            copiedNames28[captureSlot28] = System.Text.Encoding.UTF8.GetString(nameBytes28);
        }

        Assert.Equal(runtime.GetGroupNames(), copiedNames28);
        Assert.Equal(runtime.GetGroupNumbers(), Enumerable.Range(0, utf8.CaptureGroupCount + 1).Select(utf8.GroupNumberFromCaptureSlot).ToArray());

        var probes = runtime.GetGroupNames().Concat(new[] { "missing", "0", string.Empty, "999999" });
        foreach (var name in probes)
        {
            Assert.Equal(runtime.GroupNumberFromName(name), utf8.GroupNumberFromName(name));
        }

        for (var number = -1; number <= runtime.GetGroupNumbers().Length + 1; number++)
        {
            Assert.Equal(runtime.GroupNameFromNumber(number), utf8.GroupNameFromNumber(number));
        }
    }

    [Fact]
    public void GroupNumberFromNameNullThrowsLikeRuntime()
    {
        var runtime = Assert.Throws<ArgumentNullException>(() => new Regex("abc").GroupNumberFromName(null!));
        var utf8 = Assert.Throws<ArgumentNullException>(() => new Utf8Regex("abc").GroupNumberFromName(null!));
        Assert.Equal(runtime.ParamName, utf8.ParamName);
    }
}
