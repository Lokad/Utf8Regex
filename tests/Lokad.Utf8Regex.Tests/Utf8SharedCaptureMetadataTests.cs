using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SharedCaptureMetadataTests
{
    public static IEnumerable<object[]> Cases()
    {
        (string Pattern, string Subject, string Replacement)[] cases =
        [
            ("abc", "abc", "$&"),
            ("(?<x>😀)(a)", "😀a", "${x}-$1-$2"),
            ("(?<12>😀)(?<x>a)", "😀a", "${12}-${x}"),
            ("(?<x>a)(?<x>b)", "ab", "${x}"),
            ("(?<2147483647>😀)", "😀", "${2147483647}"),
            ("(?=(?<x>😀))😀", "😀", "${x}"),
            ("(?<=(?<x>😀))a", "😀a", "${x}"),
            ("(?n)(a)(?<word>😀)", "a😀", "${word}-$1"),
        ];
        foreach (var value in cases)
        foreach (var compiled in new[] { false, true })
            yield return [value.Pattern, value.Subject, value.Replacement, compiled];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OneCatalogServesPreparationPublicCopiesCapturesAndReplacement(
        string pattern, string subject, string replacement, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8RegexProgram.Compile(pattern, options, Regex.InfiniteMatchTimeout);
        var catalog = program.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog!;
        Assert.NotNull(catalog); Assert.Same(catalog, program.CaptureCatalog);
        Assert.False(program.GroupNames.IsValueCreated); Assert.False(program.GroupNumbers.IsValueCreated);
        Assert.Same(catalog.GroupNames, program.GroupNames.Value);
        Assert.Same(catalog.GroupNumbers, program.GroupNumbers.Value);
        Assert.False(program.VerifierRuntime.FallbackCandidateVerifier.IsFallbackRegexCreated);

        // These expressions capture whole scalars, so BCL metadata and returned
        // text overlap the UTF-8 contract, including supplementary literals.
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options);
        var names = regex.GetGroupNames(); var numbers = regex.GetGroupNumbers();
        Assert.Equal(oracle.GetGroupNames(), names); Assert.Equal(oracle.GetGroupNumbers(), numbers);
        Array.Fill(names, "corrupted"); Array.Fill(numbers, -1);
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "", "00", "01", "012", "missing" }))
            Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromName(name));
        foreach (var number in oracle.GetGroupNumbers().Concat(new[] { -1, 2, 12, int.MaxValue }))
            Assert.Equal(oracle.GroupNameFromNumber(number), regex.GroupNameFromNumber(number));
        var input = Encoding.UTF8.GetBytes(subject);
        var actual = regex.MatchDetailed(input); var expected = oracle.Match(subject);
        Assert.Equal(expected.Success, actual.Success);
        foreach (var number in oracle.GetGroupNumbers())
            Assert.Equal(expected.Groups[number].Value, actual.GetGroup(number).GetValueString());
        Assert.Equal(Encoding.UTF8.GetBytes(oracle.Replace(subject, replacement)), regex.Replace(input, replacement));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
