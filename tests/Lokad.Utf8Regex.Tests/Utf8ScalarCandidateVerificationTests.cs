using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCandidateVerificationTests
{
    [Theory]
    [InlineData("foo(?<x>.)\\k<x>", false)]
    [InlineData("foo(?<x>.)\\k<x>", true)]
    [InlineData("foo(?<x>.)(?(x)\\k<x>|bar)", false)]
    [InlineData("foo(?<x>.)(?(x)\\k<x>|bar)", true)]
    [InlineData("[A-Z](?<x>.)\\k<x>", false)]
    [InlineData("[A-Z](?<x>.)\\k<x>", true)]
    public void CandidateRuntimeCountStaysScalarBeyondTheFormerDemotionThreshold(string pattern, bool emitted)
    {
        const RegexOptions options = RegexOptions.CultureInvariant;
        var prepared = Utf8FrontEnd.Compile(pattern, options);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var token = pattern.StartsWith("[A-Z]", StringComparison.Ordinal) ? "A😀😀 " : "foo😀😀 ";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(token, 40)));
        var validation = Utf8Validation.Validate(input);
        Assert.False(prepared.SearchPlan.FirstMatchOperation.Projection.HasValue);
        var regex = new Utf8Regex(pattern, options | (emitted ? RegexOptions.Compiled : 0));
        Assert.Equal(40, regex.Count(input)); Assert.True(regex.IsMatch(input));
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }

    [Fact]
    public void CompiledCandidateRuntimeProjectsScalarsWithoutItsFormerBclMatchPath()
    {
        const string pattern = @"foo(?<x>.)\k<x>";
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var runtime = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("foo😀😀 ", 40)));
        var validation = Utf8Validation.Validate(input);
        Assert.True(runtime.IsMatch(input));
        Assert.Equal(40, runtime.Count(input));
        var match = runtime.Match(input);
        Assert.True(match.Success); Assert.True(match.IsByteAligned);
        Assert.Equal((0, 11, 0, 7), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }
}
