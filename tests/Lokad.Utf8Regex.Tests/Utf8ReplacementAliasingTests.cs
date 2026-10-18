using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementAliasingTests
{
    [Theory]
    [InlineData("a", "a-b-a-c", "LONG", "LONG-b-LONG-c", 0, 0)]
    [InlineData("a", "a-b-a-c", "LONG", "LONG-b-LONG-c", 4, 0)]
    [InlineData("a", "a-b-a-c", "LONG", "LONG-b-LONG-c", 0, 4)]
    [InlineData("aa", "aa-b-aa-c", "", "-b--c", 0, 2)]
    [InlineData("a", "a-é-a-😀", "😀", "😀-é-😀-😀", 1, 0)]
    [InlineData("(a)", "a-b-a-c", "<$1>", "<a>-b-<a>-c", 0, 0)]
    [InlineData("z", "a-é-😀", "LONG", "a-é-😀", 0, 2)]
    public void ReplacementPreservesOverlappingSubject(string pattern, string text, string template,
        string expected, int inputStart, int destinationStart)
    {
        foreach (var utf8Template in new[] { false, true })
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            var storage = new byte[128];
            var input = Encoding.UTF8.GetBytes(text);
            input.CopyTo(storage, inputStart);
            var regex = new Utf8Regex(pattern, options);
            var status = utf8Template
                ? regex.TryReplace(storage.AsSpan(inputStart, input.Length), Encoding.UTF8.GetBytes(template),
                    storage.AsSpan(destinationStart), out var written)
                : regex.TryReplace(storage.AsSpan(inputStart, input.Length), template,
                    storage.AsSpan(destinationStart), out written);

            Assert.Equal(OperationStatus.Done, status);
            Assert.Equal(expected, Encoding.UTF8.GetString(storage.AsSpan(destinationStart, written)));
        }
    }

    [Fact]
    public void Utf8TemplatePreservesOverlappingDestination()
    {
        var storage = new byte[64];
        "XY"u8.CopyTo(storage.AsSpan(1));
        var regex = new Utf8Regex("a", RegexOptions.CultureInvariant);

        var status = regex.TryReplace("a-b-a"u8, storage.AsSpan(1, 2), storage, out var written);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal("XY-b-XY", Encoding.UTF8.GetString(storage.AsSpan(0, written)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsufficientOverlappingDestinationRemainsUnchanged(bool utf8Template)
    {
        var storage = Encoding.UTF8.GetBytes("a-b-a-c");
        var original = storage.ToArray();
        var regex = new Utf8Regex("a", RegexOptions.CultureInvariant);

        var status = utf8Template
            ? regex.TryReplace(storage, "LONG"u8, storage, out var written)
            : regex.TryReplace(storage, "LONG", storage, out written);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(0, written);
        Assert.Equal(original, storage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteResultDenialPreservesOverlappingSubject(bool utf8Template)
    {
        var storage = new byte[64];
        "a-b-a-c"u8.CopyTo(storage);
        var original = storage.ToArray();
        var regex = new Utf8Regex("a", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxResultBytes = 1 });

        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (utf8Template) regex.TryReplace(storage.AsSpan(0, 7), "LONG"u8, storage, out _);
            else regex.TryReplace(storage.AsSpan(0, 7), "LONG", storage, out _);
        });

        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        Assert.Equal(original, storage);
    }
}
