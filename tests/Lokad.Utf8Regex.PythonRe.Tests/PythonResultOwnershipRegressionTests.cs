using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// compiled name metadata escaped through result arrays, so mutating
// one result corrupted the regex instance for subsequent operations. Results and
// lookup APIs now hand out owned arrays (empty entry arrays stay shared: they have
// no slots to mutate).
public sealed class PythonResultOwnershipRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void MutatingGroupNamesDoesNotLeakIntoRegex()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var names = regex.GetGroupNames();
        Assert.Equal(["0", "word"], names);
        names[1] = "hacked";
        Assert.Equal(["0", "word"], regex.GetGroupNames());
    }

    [Fact]
    public void MutatingContextNameEntriesDoesNotLeakIntoRegex()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var first = regex.MatchDetailed(Utf8("foo"));
        first.GetNameEntries()[0] = new PythonReNameEntry { Name = "hacked", Number = 99 };

        var second = regex.MatchDetailed(Utf8("foo"));
        Assert.Equal("word", second.GetNameEntries()[0].Name);
        Assert.True(second.TryGetFirstSetGroup("word", out var named));
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(named.Value.GetValueBytes()));
    }


    [Fact]
    public void ConcurrentReadsSeeStableMetadata()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var failures = 0;
        System.Threading.Tasks.Parallel.For(0, 64, _ =>
        {
            if (regex.SearchDetailed(Utf8("foo")).GetNameEntries()[0].Name != "word") { System.Threading.Interlocked.Increment(ref failures); }
            if (!regex.GetGroupNames().SequenceEqual(["0", "word"])) { System.Threading.Interlocked.Increment(ref failures); }
            if (!regex.MatchDetailed(Utf8("foo")).TryGetFirstSetGroup("word", out var ignoredGroup)) { System.Threading.Interlocked.Increment(ref failures); }
        });
        Assert.Equal(0, failures);
        Assert.Equal(["0", "word"], regex.GetGroupNames());
    }

    [Fact]
    public void PatternsWithoutNamedGroupsShareNothingMutable()
    {
        var regex = new Utf8PythonRegex("(a)(b)");
        var first = regex.SearchDetailed(Utf8("ab"));
        Assert.Empty(first.GetNameEntries());
        Assert.Equal(["0", "1", "2"], regex.GetGroupNames());
        var second = regex.SearchDetailed(Utf8("ab"));
        Assert.Empty(second.GetNameEntries());
        Assert.True(second.Success);
    }
}
