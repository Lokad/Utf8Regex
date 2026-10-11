using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SharedCaptureMetadataTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_captureCatalog")]
    private static extern ref Utf8CaptureCatalog? ContextCatalog(ref Utf8MatchContext context);

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
        var catalog = program.PreparedRegex.SemanticRegex.CaptureCatalog!;
        Assert.NotNull(catalog); Assert.Same(catalog, program.CaptureCatalog);
        // Program metadata borrows the canonical catalog directly. Public array
        // copies, sparse lookups, captures and replacement below share its data.
        Assert.Same(catalog, program.PreparedRegex.ScalarProgram?.CaptureCatalog);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(program.VerifierRuntime);

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
        var preparedCatalog = regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog;
        Assert.NotNull(preparedCatalog);
        Assert.Equal(preparedCatalog.GroupNumbers.Length - 1, regex.Inspection.AvailableOwnedSliceProgram.GroupCount);
        Assert.Same(preparedCatalog, ContextCatalog(ref actual));
        Assert.Same(regex.Inspection.PreparedRegex.ScalarProgram, regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(expected.Success, actual.Success);
        foreach (var number in oracle.GetGroupNumbers())
            Assert.Equal(expected.Groups[number].Value, actual.GetGroup(number).GetValueString());
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "", "00", "01", "012", "missing" }))
        {
            Assert.Equal(expected.Groups[name].Value, actual.GetGroup(name).GetValueString());
            Assert.Equal(oracle.GroupNumberFromName(name) >= 0, actual.TryGetGroup(name, out _));
        }
        var miss = regex.MatchDetailed([]);
        Assert.False(miss.Success);
        Assert.Same(preparedCatalog, ContextCatalog(ref miss));
        Assert.Equal(1, miss.GroupCount);
        Assert.True(miss.TryGetGroup(0, out var groupZero));
        Assert.False(groupZero.Success);
        Assert.True(miss.TryGetGroup("0", out _));
        Assert.Equal(Encoding.UTF8.GetBytes(oracle.Replace(subject, replacement)), regex.Replace(input, replacement));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
