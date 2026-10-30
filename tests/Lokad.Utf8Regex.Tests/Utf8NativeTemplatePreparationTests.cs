using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeTemplatePreparationTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.NonBacktracking, RegexOptions.ECMAScript })
        foreach (var row in new[] {
            new[] { "(a)(b)", "ab/ab", "${2}:$1:$&:$+$0" },
            new[] { "(?<name>é|😀)(a)?", "😀a/é", "${name}|$2|${missing}|$00099|$+" },
            new[] { "(?<12>é)(?<5>😀)?", "é😀/é", "$12-$5-$+-${missing}-${01}" },
            new[] { "(a)?", "ba😀", "$`<$1>$'" },
            new[] { @"\b\w+\b", "é 😀 a", "[$0]" },
            new[] { "(.)", "😀é", "[$1]" } })
            yield return [row[0], row[1], row[2], options];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NativeTemplatesKeepScalarCapturesOptionsAndAllOutputForms(string pattern, string input, string template, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
        var subject = Encoding.UTF8.GetBytes(input); var native = Encoding.UTF8.GetBytes(template);
        var expected = regex.Replace(subject, template);
        Assert.Equal(expected, regex.Replace(subject, native));
        Assert.Equal(Encoding.UTF8.GetString(expected), regex.ReplaceToString(subject, template));
        var destination = new byte[expected.Length + 4]; destination.AsSpan().Fill(0xcc);
        Assert.Equal(OperationStatus.Done, regex.TryReplace(subject, native, destination, out var written));
        Assert.Equal(expected.Length, written); Assert.Equal(expected, destination[..written]);
        Assert.All(destination.AsSpan(written).ToArray(), value => Assert.Equal(0xcc, value));
        if (expected.Length > 0)
        {
            var shortDestination = new byte[expected.Length - 1]; shortDestination.AsSpan().Fill(0xcc);
            Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryReplace(subject, native, shortDestination, out written));
            Assert.Equal(0, written); Assert.All(shortDestination, value => Assert.Equal(0xcc, value));
        }
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Replace(subject, native, token.Token));
    }

    [Fact]
    public void WarmNativeQueriesBorrowTheirKeysAndLeaveConstructionCapacityUntouched()
    {
        var regex = new Utf8Regex("(?<word>a+)"); var key = "é${word}😀"u8.ToArray();
        Assert.Equal("éaaa😀"u8.ToArray(), regex.Replace("aaa"u8, key));
        var entries = regex.Inspection.DebugReplacementCacheEntryCount;
        key[0] = 0xff;
        Assert.Throws<ArgumentException>(() => regex.Replace([], key));
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, 0, 1_000_000, ulong.MaxValue))
        {
            Assert.Equal("éaaa😀"u8.ToArray(), regex.Replace("aaa"u8, "é${word}😀"u8));
            Assert.Equal(0UL, owner.Budget.ReservedBytes);
            // Strict public validation still shares the parent work backstop.
            Assert.Equal((ulong)"é${word}😀"u8.Length, owner.Budget.WorkSteps);
        }
        Assert.Equal(entries, regex.Inspection.DebugReplacementCacheEntryCount);
        Assert.Equal("éaaa😀"u8.ToArray(), regex.Replace("aaa"u8, "é${word}😀"));
        Assert.Equal(entries + 1, regex.Inspection.DebugReplacementCacheEntryCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeTemplateAndSubjectAliasesAreSafeBeforeDestinationPublication(bool warm)
    {
        var regex = new Utf8Regex("(?<word>a)"); var template = "é${word}😀"u8.ToArray();
        if (warm) regex.Replace("a"u8, template);
        var storage = new byte[64]; storage.AsSpan().Fill(0xcc); "aa"u8.CopyTo(storage); template.CopyTo(storage, 2);
        var expected = "éa😀éa😀"u8.ToArray();
        Assert.Equal(OperationStatus.Done, regex.TryReplace(storage.AsSpan(0, 2), storage.AsSpan(2, template.Length),
            storage, out var written));
        Assert.Equal(expected.Length, written); Assert.Equal(expected, storage[..written]);
    }

    [Theory]
    [InlineData("(foo)(bar)", "foobar!", "[$2:$1]", "[bar:foo]!")]
    [InlineData("(?<12>foo)(?<5>bar)", "foobar!", "$12:$5:$+", "foo:bar:foo!")]
    [InlineData("(?<x>foo)|(?<y>bar)", "foo/bar", "${x}${y}", "foo/bar")]
    [InlineData("(é)(😀)", "é😀", "[$2:$1]", "[😀:é]")]
    [InlineData("(ab[0-9]d)", "ab1d/ab2d", "<$1>", "<ab1d>/<ab2d>")]
    [InlineData("ab[0-9]d", "ab1d/ab2d", "<$&:$+>", "<ab1d:ab1d>/<ab2d:ab2d>")]
    public void MappedCaptureOutputKeepsBytePiecesAndFinalTextProjection(string pattern, string subject, string template, string expected)
    {
        var regex = new Utf8Regex(pattern); var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(expected, regex.ReplaceToString(input, template));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, Encoding.UTF8.GetBytes(template)));
    }
}
