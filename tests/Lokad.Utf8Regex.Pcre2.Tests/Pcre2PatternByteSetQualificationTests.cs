using System.Reflection;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PatternByteSetQualificationTests
{
    private static void Prepare(Pcre2CharacterClass definition)
    {
        var method = typeof(Pcre2CharacterClass).GetMethod("PrepareAsciiByteSet",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            typeof(Pcre2CharacterClass).GetMethod("PrepareAsciiSearchValues", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try { method.Invoke(definition, null); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw(); }
    }
    private static PreparedByteSet Set(object program, string property)
    {
        var value = program.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(program);
        return Assert.IsType<PreparedByteSet>(value);
    }

    [Fact]
    public void Pcre2ClassByteSetDenialCannotPublishStateAndRepeatedPreparationBorrowsIt()
    {
        var definition = new Pcre2CharacterClass([], false, new AsciiCharClass(1ul, 1ul << 63));
        using (var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue, ulong.MaxValue))
        {
            var parent = owner.Budget.AllocateArray<byte>(1, false);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(definition));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);

        }
        using var success = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Prepare(definition);
        var set = Set(definition, "AsciiByteSet");
        var bytes = success.Budget.ReservedBytes; var work = success.Budget.WorkSteps;
        Prepare(definition);
        Assert.Same(set, Set(definition, "AsciiByteSet"));
        Assert.Equal(bytes, success.Budget.ReservedBytes); Assert.Equal(work, success.Budget.WorkSteps);
        Assert.Equal(1, set.IndexOf(new byte[] { 128, 127, 255 }));
        Assert.Equal(2, set.IndexOfAnyExcept(new byte[] { 0, 127, 128, 255 }));
    }

    [Fact]
    public void Pcre2NegatedAsciiMaskStopsAtEveryNonAsciiByte()
    {
        using (var owner = new Utf8ConstructionScope(0, 0, 0, 0))
        {
            Prepare(Pcre2CharacterClass.Empty);
            Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.WorkSteps);
            Assert.Equal(0ul, owner.Budget.PeakDepth);
        }
        var definition = new Pcre2CharacterClass([], true, new AsciiCharClass(1ul << 9, 0, true));
        Prepare(definition); var set = Set(definition, "AsciiByteSet");
        for (var value = 0; value < 256; value++)
        {
            var accepted = value < 128 && value != 9;
            Assert.Equal(accepted, set.Contains((byte)value));
            Assert.Equal(accepted ? -1 : 0, set.IndexOfAnyExcept(new byte[] { (byte)value }));
        }
        Assert.Equal(3, set.IndexOfAnyExcept("abc😀"u8));
    }

    [Fact]
    public void Pcre2DirectLeadingSetsShareAdmissionWithoutTemporaryClassArrays()
    {
        Func<object>[] create =
        [
            () => new Pcre2AsciiLiteralAlternationRepeatDirectProgram(null!,
                ["ab"u8.ToArray(), "ac"u8.ToArray(), "xy"u8.ToArray()], "!"u8.ToArray()),
            () => new Pcre2AsciiBoundedIsMatchDirectProgram(
                [[new AsciiCharClass(1ul << 33, 0)], [new AsciiCharClass(1ul << 35, 0)]], false, false, null!),
        ];
        foreach (var factory in create)
        {
            for (var denial = 0; denial < 3; denial++)
            {
                using var owner = new Utf8ConstructionScope(0, denial == 0 ? 0ul : ulong.MaxValue,
                    denial == 1 ? 0ul : ulong.MaxValue, denial == 2 ? 0ul : ulong.MaxValue);
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => factory());
                Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
                    1 => Utf8ResourceLimitKind.ConstructionWorkSteps, _ => Utf8ResourceLimitKind.ConstructionDepth }, failure.ResourceKind);
                Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);

            }
        }
        var repeat = Set(create[0](), "FirstBytes");
        Assert.Equal(1, repeat.IndexOf("!xyabc"u8)); Assert.Equal(-1, repeat.IndexOf("!😀"u8));
        var bounded = Set(create[1](), "LeadingBytes");
        Assert.Equal(2, bounded.IndexOf("??#"u8)); Assert.Equal(-1, bounded.IndexOf("a😀"u8));
    }

    [Fact]
    public void Pcre2RepeatAndBoundedPublicRoutesPreserveScalarStops()
    {
        foreach (var pattern in new[] { "[^x]++x", "[a-z]+x", "(?:ab|cd)+!", "(?:ab|[cd]{2})!" })
        {
            var regex = new Utf8Pcre2Regex(pattern);
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
                    _ = System.Text.Encoding.UTF8.GetString(input.AsSpan(match.StartOffsetInBytes,
                        match.EndOffsetInBytes - match.StartOffsetInBytes));
                }
                Assert.Equal(count > 0, regex.IsMatch(input));
                Assert.Equal(count, regex.Count(input));
                Assert.Equal(pattern switch { "[^x]++x" => 1, "[a-z]+x" => index == 2 ? 1 : 0,
                    _ => index == 0 ? 2 : 0 }, count);
            }
        }
    }
}
