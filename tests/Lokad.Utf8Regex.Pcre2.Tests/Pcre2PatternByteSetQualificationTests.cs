using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PatternByteSetQualificationTests
{
    [Fact]
    public void Pcre2ClassByteSetDenialCannotPublishStateAndRepeatedPreparationBorrowsIt()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var definition = new Pcre2CharacterClass([], false, new AsciiCharClass(1ul, 1ul << 63));
        using (var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue))
        {
            owner.Budget.AllocateArray<byte>(1, false);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(definition.PrepareAsciiByteSet);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Null(definition.AsciiByteSet);
            Assert.Equal(48ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        using var success = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue);
        definition.PrepareAsciiByteSet();
        var set = definition.AsciiByteSet;
        Assert.NotNull(set);
        var bytes = success.Budget.ReservedBytes;
        var work = success.Budget.DiagnosticCheckpoints;
        definition.PrepareAsciiByteSet();
        Assert.Same(set, definition.AsciiByteSet);
        Assert.Equal(bytes, success.Budget.ReservedBytes);
        Assert.Equal(work, success.Budget.DiagnosticCheckpoints);
        Assert.Equal(1, set.IndexOf(new byte[] { 128, 127, 255 }));
        Assert.Equal(2, set.IndexOfAnyExcept(new byte[] { 0, 127, 128, 255 }));
    }

    [Fact]
    public void Pcre2NegatedAsciiMaskStopsAtEveryNonAsciiByte()
    {
        using (var owner = new Utf8ConstructionScope(0, 0, 0))
        {
            Pcre2CharacterClass.Empty.PrepareAsciiByteSet();
            Assert.Null(Pcre2CharacterClass.Empty.AsciiByteSet);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.PeakDepth);
        }
        var definition = new Pcre2CharacterClass([], true, new AsciiCharClass(1ul << 9, 0, true));
        definition.PrepareAsciiByteSet();
        var set = definition.AsciiByteSet;
        Assert.NotNull(set);
        for (var value = 0; value < 256; value++)
        {
            var accepted = value < 128 && value != 9;
            Assert.Equal(accepted, set.Contains((byte)value));
            Assert.Equal(accepted ? -1 : 0, set.IndexOfAnyExcept(new byte[] { (byte)value }));
        }
        Assert.Equal(3, set.IndexOfAnyExcept("abc😀"u8));
    }

    [Fact]
    public void Pcre2RepeatAndBoundedPublicRoutesPreserveScalarStops()
    {
        foreach (var pattern in new[] { "[^x]++x", "[a-z]+x", "(?:ab|cd)+!", "(?:ab|[cd]{2})!" })
        {
            using var regex = new Utf8Pcre2Regex(pattern);
            var inputs = new[] { "ab!😀cdcd!x"u8.ToArray(), "😀😀x"u8.ToArray(), "zzzzx"u8.ToArray() };
            for (var index = 0; index < inputs.Length; index++)
            {
                var input = inputs[index];
                var matches = regex.EnumerateMatches(input);
                var count = 0;
                while (matches.MoveNext())
                {
                    var match = matches.Current;
                    count++;
                    Assert.True(match.EndOffsetInBytes > match.StartOffsetInBytes);
                    Assert.True(match.StartOffsetInBytes == 0 || (input[match.StartOffsetInBytes] & 0xc0) != 0x80);
                    Assert.True(match.EndOffsetInBytes == input.Length || (input[match.EndOffsetInBytes] & 0xc0) != 0x80);
                }
                Assert.Equal(count > 0, regex.IsMatch(input));
                Assert.Equal(count, regex.Count(input));
                Assert.Equal(pattern switch { "[^x]++x" => 1, "[a-z]+x" => index == 2 ? 1 : 0,
                    _ => index == 0 ? 2 : 0 }, count);
            }
        }
    }
}
