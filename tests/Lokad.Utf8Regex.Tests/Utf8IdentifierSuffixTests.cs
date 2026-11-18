using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8IdentifierSuffixTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeStructuralFamiliesKeepScalarByteOperationsWithoutBcl(bool compiled)
    {
        foreach (var pattern in new[] { @"(?:call|invoke)[ \t]+[A-Z][a-z]*\(", @"call[\s\S]{0,3}Result",
            @"(?:call[\s\S]{0,3}Result|invoke[\s\S]{0,3}Other)", @"call(?=[ \t]+Result)" })
        {
            var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var subject = pattern.Contains("[A-Z]") ? "😀call Name(中invoke Name("
                : pattern.Contains("?=") ? "😀call Result中call Result" : "😀call xResult中call xResult";
            var bytes = Encoding.UTF8.GetBytes(subject); Assert.True(regex.IsMatch(bytes)); Assert.Equal(2, regex.Count(bytes));
            var match = regex.Match(bytes); Assert.True(match.Success); Assert.Equal(4, match.StartOffsetInBytes);
            Assert.Equal(match.StartOffsetInBytes, Utf8MatchSequence.First(regex.CreateMatchCursor(bytes)).Value.StartOffsetInBytes);
            var count = 0; foreach (var item in regex.EnumerateMatches(bytes)) { Assert.True((item.EndOffsetInBytes - item.StartOffsetInBytes) > 0); count++; }
            Assert.Equal(2, count); Assert.Contains("X", Encoding.UTF8.GetString(regex.Replace(bytes, "X")));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
