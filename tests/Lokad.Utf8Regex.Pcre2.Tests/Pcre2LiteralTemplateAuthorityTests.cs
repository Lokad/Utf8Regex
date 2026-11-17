using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LiteralTemplateAuthorityTests
{
    [Theory]
    [InlineData(0, 48UL)] [InlineData(1, 208UL)] [InlineData(17, 2032UL)]
    public void BytePublicationAdmitsOutputAndRanges(int count, ulong bytePeak)
    {
        var input = count == 0 ? "z"u8.ToArray() : Enumerable.Repeat((byte)'a', count).ToArray();
        var replacement = new string('b', count == 17 ? 100 : 1);
        var expected = count == 0 ? "z" : new string('b', count * replacement.Length);
        var admitted = Create(new() { HeapLimitInBytes = bytePeak });
        Pcre2PreparationMemoryPolicyTests.Warm(admitted, replacement);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), admitted.Replace(input, replacement));
        var denied = Create(new() { HeapLimitInBytes = bytePeak - 1 });
        Pcre2PreparationMemoryPolicyTests.Warm(denied, replacement);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(
            () => denied.Replace(input, replacement)).ErrorKind);
    }

    [Theory]
    [InlineData("a", "aé😀a", "é😀", Pcre2SubstitutionOptions.None, "é😀é😀é😀")]
    [InlineData("a", "é😀", "b", Pcre2SubstitutionOptions.None, "é😀")]
    [InlineData("a", "é😀", "b", Pcre2SubstitutionOptions.SubstituteReplacementOnly, "")]
    [InlineData("", "é😀", "_", Pcre2SubstitutionOptions.None, "_é_😀_")]
    [InlineData("a", "aé😀a", "<$0>", Pcre2SubstitutionOptions.None, "<a>é😀<a>")]
    [InlineData("a", "aé😀a", "$0", Pcre2SubstitutionOptions.SubstituteLiteral, "$0é😀$0")]
    public void FiniteBytesPreserveScalarAndSubstitutionResults(string pattern, string subject,
        string replacement, Pcre2SubstitutionOptions options, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 10000, HeapLimitInBytes = 1UL << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, replacement, 0, options));
    }

    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits)
        => new("a", Pcre2CompileOptions.None, default, limits, Timeout.InfiniteTimeSpan);
}
