using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeclarationIdentifierTests
{



    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualOrdinaryAndCompiledConsumersKeepUnicodeBoundaryRanges(bool compiled)
    {
        const string pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
        byte[] input = Encoding.UTF8.GetBytes("😀 class Worker_State; érecord Bogus; record CustomerExportJob; struct worker_state");
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        Assert.Equal(2, regex.Count(input));
        var first = regex.Match(input); Assert.Equal(5, first.StartOffsetInBytes);
        Assert.Equal("class Worker_State"u8.ToArray(), input.AsSpan(first.StartOffsetInBytes, (first.EndOffsetInBytes - first.StartOffsetInBytes)).ToArray());
    }
}
