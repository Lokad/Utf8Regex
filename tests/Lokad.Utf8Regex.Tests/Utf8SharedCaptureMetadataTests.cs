using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

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
        var program = Utf8RegexProgram.Compile(pattern, options, prepareRuntime: true);
        var catalog = program.PreparedRegex.SemanticRegex.CaptureCatalog!;
        Assert.NotNull(catalog); Assert.Same(catalog, program.CaptureCatalog);
        // Program metadata borrows the canonical catalog directly. Public name-array
        // copies, sparse lookups, captures and replacement below share its data.
        Assert.Same(catalog, program.PreparedRegex.ScalarProgram?.CaptureCatalog);

        // These expressions capture whole scalars, so BCL metadata and returned
        // text overlap the UTF-8 contract, including supplementary literals.
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        var copiedNames16 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot16 = 0; captureSlot16 < copiedNames16.Length; captureSlot16++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot16, [], out var nameLength16));
            var nameBytes16 = new byte[nameLength16];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot16, nameBytes16, out var written16));
            Assert.Equal(nameLength16, written16);
            copiedNames16[captureSlot16] = System.Text.Encoding.UTF8.GetString(nameBytes16);
        }
        var names = copiedNames16; var numbers = Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray();
        Assert.Equal(oracle.GetGroupNames(), names); Assert.Equal(oracle.GetGroupNumbers(), numbers);
        Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "", "00", "01", "012", "missing" }))
            Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromUtf8Name(System.Text.Encoding.UTF8.GetBytes(name)));
        var input = Encoding.UTF8.GetBytes(subject);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input)); var expected = oracle.Match(subject);
        var preparedCatalog = regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog;
        Assert.NotNull(preparedCatalog);
        Assert.Equal(preparedCatalog.GroupNumbers.Length - 1, regex.Inspection.AvailableOwnedSliceProgram.GroupCount);

        Assert.Same(regex.Inspection.PreparedRegex.ScalarProgram, regex.Inspection.AvailableOwnedSliceProgram);
        Utf8MatchSequence.AssertCaptures(expected, actual, input, subject);
        foreach (var name in oracle.GetGroupNames())
        {
            Assert.Equal(expected.Groups[name].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromUtf8Name(System.Text.Encoding.UTF8.GetBytes(name))).Span));
            Assert.True(actual.TryGetGroup(name, out _));
        }
        Assert.False(actual.TryGetGroup("missing", out _));
        var miss = Utf8MatchSequence.First(regex.CreateMatchCursor(ReadOnlyMemory<byte>.Empty));
        Assert.False(miss.Success);

        Assert.Empty(miss.GroupNumbers.ToArray());
        Assert.False(miss.TryGetGroup(0, out var groupZero));
        Assert.False(groupZero.Success);
        Assert.False(miss.TryGetGroup("0", out _));
        Assert.Equal(Encoding.UTF8.GetBytes(oracle.Replace(subject, replacement)), regex.Replace(input, replacement));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
