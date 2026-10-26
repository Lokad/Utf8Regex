using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementPreparationIdentityTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var route in new[] { "Bytes", "Text", "Utf8Template", "Destination" })
        foreach (var finite in new[] { false, true }) yield return [route, finite];
    }

    private static Utf8Pcre2Regex Create(bool finite, string pattern = "(a)?b") => finite
        ? Utf8Pcre2Regex.Create(pattern, default, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 128,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 }, Timeout.InfiniteTimeSpan)
        : new Utf8Pcre2Regex(pattern);

    [Theory] [MemberData(nameof(Routes))]
    public void ExecutionFlagsPreserveScalarTemplateFragments(string route, bool finite)
    {
        var regex = Create(finite, "a");
        var cases = new (string Template, string Expected)[] {
            ("$0", "😀aé"), ("$$", "😀$é"), ("x$0y", "😀xayé"),
            (@"\n$0", "😀\\naé"), ("\ud800$0\udc00", "😀�a�é"),
            ("\ud800$$\udc00", "😀�$�é"), ("\ud800\udc00", "😀𐀀é"), ("é😀", "😀é😀é") };
        foreach (var item in cases)
        foreach (var options in new[] { Pcre2SubstitutionOptions.None, Pcre2SubstitutionOptions.UnsetEmpty,
            Pcre2SubstitutionOptions.UnknownUnset, Pcre2SubstitutionOptions.UnsetEmpty | Pcre2SubstitutionOptions.UnknownUnset })
            Assert.Equal(item.Expected, Run(regex, route, "😀aé", item.Template, options));
        Assert.Equal(cases.Length, regex.DebugReplacementCacheEntryCount);
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ExecutionFlagsSharePreparationAcrossPublicRoutes(string route, bool finite)
    {
        var regex = Create(finite);
        foreach (var options in new[] { Pcre2SubstitutionOptions.None,
            Pcre2SubstitutionOptions.UnsetEmpty, Pcre2SubstitutionOptions.UnknownUnset,
            Pcre2SubstitutionOptions.UnsetEmpty | Pcre2SubstitutionOptions.UnknownUnset,
            Pcre2SubstitutionOptions.SubstituteMatched, Pcre2SubstitutionOptions.SubstituteOverflowLength,
            Pcre2SubstitutionOptions.SubstituteReplacementOnly })
        {
            var expected = options == Pcre2SubstitutionOptions.SubstituteReplacementOnly ? "a" : "😀aé";
            Assert.Equal(expected, Run(regex, route, "😀abé", "$1", options));
            Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
        }

        Assert.Equal("\na", Run(regex, route, "ab", @"\n$1", Pcre2SubstitutionOptions.Extended));
        Assert.Equal(@"\na", Run(regex, route, "ab", @"\n$1", Pcre2SubstitutionOptions.None));
        Assert.Equal(3, regex.DebugReplacementCacheEntryCount);
    }

    [Theory] [MemberData(nameof(Routes))]
    public void SharedPreparationDoesNotCacheReferenceResolution(string route, bool finite)
    {
        var regex = Create(finite);
        Assert.Equal(Pcre2ErrorKind.UnsetReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(
            () => Run(regex, route, "b", "$1", default)).ErrorKind);
        Assert.Equal("", Run(regex, route, "b", "$1", Pcre2SubstitutionOptions.UnsetEmpty));
        Assert.Equal(Pcre2ErrorKind.UnsetReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(
            () => Run(regex, route, "b", "$1", default)).ErrorKind);
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);

        Assert.Equal(Pcre2ErrorKind.UnknownReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(
            () => Run(regex, route, "ab", "${missing}", default)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.UnsetReplacementGroup, Assert.Throws<Pcre2SubstitutionException>(
            () => Run(regex, route, "ab", "${missing}", Pcre2SubstitutionOptions.UnknownUnset)).ErrorKind);
        Assert.Equal("", Run(regex, route, "ab", "${missing}",
            Pcre2SubstitutionOptions.UnknownUnset | Pcre2SubstitutionOptions.UnsetEmpty));
        Assert.Equal(2, regex.DebugReplacementCacheEntryCount);
    }

    private static string Run(Utf8Pcre2Regex regex, string route, string text, string template,
        Pcre2SubstitutionOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        if (route == "Text") return regex.ReplaceToString(input, template, options);
        if (route == "Bytes") return Encoding.UTF8.GetString(regex.Replace(input, template, 0, options));
        var utf8Template = Encoding.UTF8.GetBytes(template);
        if (route == "Utf8Template") return Encoding.UTF8.GetString(regex.Replace(input, utf8Template, 0, options));
        var destination = new byte[64];
        destination.AsSpan().Fill(0xcc);
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, utf8Template, destination,
            out var written, 0, options, default));
        Assert.All(destination[written..], value => Assert.Equal(0xcc, value));
        return Encoding.UTF8.GetString(destination.AsSpan(0, written));
    }
}
