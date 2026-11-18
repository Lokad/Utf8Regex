using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarReplacementOutputTests
{
    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void LargeLiteralUsesCallerStorageInScalarMatchOrder(RegexOptions engine)
    {
        const string pattern = "(?<run>a+)|";
        var options = engine | RegexOptions.CultureInvariant;
        var template = Encoding.UTF8.GetBytes(new string('中', 513));
        var baseline = new Utf8Regex(pattern, options);
        using var live = new CancellationTokenSource();
        var expected = baseline.Replace("éaa😀a"u8, template, live.Token);
        var regex = new Utf8Regex(pattern, options, new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy { MaxResultBytes = 4096 }
        });

        // The destination also contains the original template. Prepared
        // literal bytes remain valid throughout publication.
        var destination = new byte[expected.Length + 1];
        template.CopyTo(destination.AsSpan(1));
        Assert.Equal(OperationStatus.Done,
            regex.TryReplace("éaa😀a"u8, destination.AsSpan(1, template.Length), destination, out var written, live.Token));
        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, destination.AsSpan(0, written).ToArray());
        Assert.Equal(0, destination[^1]);

        var shortDestination = Enumerable.Repeat((byte)0xa5, expected.Length - 1).ToArray();
        Assert.Equal(OperationStatus.DestinationTooSmall,
            regex.TryReplace("éaa😀a"u8, template, shortDestination, out written, live.Token));
        Assert.Equal(0, written);
        Assert.All(shortDestination, value => Assert.Equal((byte)0xa5, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeLiteralStorageDenialLeavesCallerBytesUntouched(bool overlappingSubject)
    {
        var limit = overlappingSubject ? 4096UL : 1UL;
        var regex = new Utf8Regex("a", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy { MaxResultBytes = limit }
        });
        var destination = new byte[6000];
        "a"u8.CopyTo(destination);
        var template = Encoding.UTF8.GetBytes(new string('中', 1600));
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            var subject = overlappingSubject ? destination.AsSpan(0, 1) : "a"u8;
            regex.TryReplace(subject, template, destination, out _);
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        Assert.Equal((byte)'a', destination[0]);
        Assert.All(destination.Skip(1), value => Assert.Equal((byte)0, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedByteTemplatesFailBeforeOutputAndDoNotPreventReuse(bool finite)
    {
        var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 128,
            MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 } : default;
        using var source = new CancellationTokenSource();
        byte[][] templates = [[0x61, 0xff, 0xf0, 0x9f, 0x98, 0x80], [0xf0, 0x9f], [0xed, 0xa0, 0x80]];
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex("a", options: Options(compiled, false), policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            foreach (var text in new[] { "a😀a", "miss" })
            foreach (var template in templates)
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Throws<ArgumentException>(() => regex.Replace(input, template, source.Token));
                var destination = new byte[32];
                Array.Fill(destination, (byte)0xa5);
                Assert.Throws<ArgumentException>(() => regex.TryReplace(input, template, destination, out _, source.Token));
                Assert.All(destination, value => Assert.Equal((byte)0xa5, value));
            }
            Assert.Equal("q😀q", Encoding.UTF8.GetString(regex.Replace("a😀a"u8, "q"u8, source.Token)));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TemplatesKeepCompleteScalarCapturesAcrossOutputForms(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("(?<outer>(?<x>.))", options: Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        foreach (var text in new[] { "\U0001f600", "a\U0001f600b", "\u00e9\U0001f600" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var template = "\ufffd${x}\ufffd|$&"u8;
            var expected = string.Concat(text.EnumerateRunes().Select(scalar => $"\ufffd{scalar}\ufffd|{scalar}"));
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
            var destination = new byte[expectedBytes.Length];
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
            Assert.Equal(expectedBytes.Length, written);
            Assert.Equal(expectedBytes, destination);
            destination = Enumerable.Repeat((byte)0xa5, expectedBytes.Length - 1).ToArray();
            Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryReplace(input, template, destination, out written, source.Token));
            Assert.Equal(0, written);
            Assert.All(destination, value => Assert.Equal((byte)0xa5, value));
        }
        AssertOwned(regex);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EmptySubstitutionsPreserveAdjacentUnicodeLiterals(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("()", options: Options(compiled, rtl));
        var template = "\ufffd$1\ufffd"u8;
        const string expected = "\ufffd\ufffd\U0001f600\ufffd\ufffd";
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace("\U0001f600"u8, template));
        AssertOwned(regex);
    }

    [Theory]
    [InlineData(false, false, "bytes")]
    [InlineData(false, true, "bytes")]
    [InlineData(true, false, "bytes")]
    [InlineData(true, true, "bytes")]
    [InlineData(false, false, "destination")]
    [InlineData(false, true, "destination")]
    [InlineData(true, false, "destination")]
    [InlineData(true, true, "destination")]
    public void TemplateMissesAvoidSubjectMapsAndDecodedStaging(bool compiled, bool rtl, string operation)
    {
        var regex = new Utf8Regex("(?<x>z)", options: Options(compiled, rtl));
        var text = new string('\u00e9', 50000);
        var input = Encoding.UTF8.GetBytes(text);
        var destination = new byte[input.Length];
        // Warm cached template analysis, VM and pools before observing allocations.
        for (var index = 0; index < 5; index++) Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var requiredPayloads = operation == "destination" ? input.Length : input.Length * 2;
        Assert.True(allocated <= requiredPayloads + 32768, $"{operation} allocated {allocated} bytes for {input.Length} input bytes.");
        AssertOwned(regex);

        void Run()
        {
            switch (operation)
            {
                case "bytes": Assert.Equal(input, regex.Replace(input, "$1"u8)); break;
                case "destination":
                    Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "$1"u8, destination, out var written));
                    Assert.Equal(input.Length, written);
                    Assert.Equal(input, destination);
                    break;
            }
        }
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
