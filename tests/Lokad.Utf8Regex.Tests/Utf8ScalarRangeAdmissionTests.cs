using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarRangeAdmissionTests
{
    [Theory]
    [InlineData("[\U00010400-\uffff]")]
    [InlineData("[\U0001f64f-\U0001f600]")]
    [InlineData(@"[\uD801\uDC00-\uffff]")]
    public void DescendingSupplementaryBoundsRejectScalarOrder(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var failure = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options: options));
            Assert.Equal("pattern", failure.ParamName);
            Assert.Contains("Unicode scalar order", failure.Message);
        }
    }

    [Fact]
    public void CanonicalClassesKeepMixedScalarRangeMembership()
    {
        const string pattern = "[A-Z\U00010400-\U00010402-[B\U00010401]]";
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var root = tree.Root;
        while (root.ChildCount == 1) root = root.Child(0);
        Assert.Equal(Utf8TreeNodeKind.Set, root.Kind);
        Assert.Null(root.Str);
        Assert.NotNull(root.ScalarClass);
        Assert.True(Utf8ScalarAsciiProjector.TryProjectAsciiQuery(root.ScalarClass, out var mask));
        Assert.True(mask.Contains((byte)'A'));
        Assert.False(mask.Contains((byte)'B'));
        Assert.False(mask.Contains((byte)'a'));
        var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.Equal(2, regex.Count(Encoding.UTF8.GetBytes("\U00010400\U00010401\U00010402")));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
