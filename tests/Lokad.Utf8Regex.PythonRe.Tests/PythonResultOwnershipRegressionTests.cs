using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// PLAN.md C09: compiled name metadata escaped through result arrays, so mutating
// one result corrupted the regex instance for subsequent operations. Results and
// lookup APIs now hand out owned arrays (empty entry arrays stay shared: they have
// no slots to mutate).
public sealed class PythonResultOwnershipRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void MutatingDetailedNameEntriesDoesNotLeakIntoRegex()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var first = regex.SearchDetailedData(Utf8("foo"));
        Assert.Equal("word", first.NameEntries[0].Name);
        first.NameEntries[0] = new PythonReNameEntry { Name = "hacked", Number = 99 };

        var second = regex.SearchDetailedData(Utf8("foo"));
        Assert.Equal("word", second.NameEntries[0].Name);
        Assert.Equal(1, second.NameEntries[0].Number);
        Assert.True(second.TryGetFirstSetGroup("word", out var named));
        Assert.Equal("foo", named.ValueText);
        var context = regex.MatchDetailed(Utf8("foo"));
        Assert.True(context.TryGetFirstSetGroup("word", out var contextNamed));
        Assert.Equal("foo", contextNamed.GetValueString());
    }

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
        Assert.Equal("foo", named.GetValueString());
    }

    [Fact]
    public void MutatingDetailedGroupsDoesNotLeakAcrossResults()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var first = regex.SearchDetailedData(Utf8("foo"));
        first.Groups[1] = default;

        var second = regex.SearchDetailedData(Utf8("foo"));
        Assert.Equal("foo", second.Groups[1].ValueText);
        Assert.True(second.TryGetFirstSetGroup("word", out var named));
        Assert.Equal("foo", named.ValueText);
    }

    [Fact]
    public void MutatingDetailedNameEntriesDoesNotLeakAcrossEntryPoints()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        regex.MatchDetailedData(Utf8("foo")).NameEntries[0] = new PythonReNameEntry { Name = "hacked", Number = 99 };
        regex.FullMatchDetailedData(Utf8("foo")).NameEntries[0] = new PythonReNameEntry { Name = "hacked", Number = 99 };

        var found = Assert.Single(regex.FindIterDetailed(Utf8("foo")));
        Assert.Equal("word", found.NameEntries[0].Name);
        Assert.Equal("foo", found.Groups[1].ValueText);
    }

    [Fact]
    public void MutatingInsideEvaluatorCallbacksDoesNotLeakIntoRegex()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        Utf8PythonUtf8MatchEvaluator<string> evaluator = static (_, match) =>
        {
            match.NameEntries[0] = new PythonReNameEntry { Name = "hacked", Number = 99 };
            match.Groups[1] = default;
            return Encoding.UTF8.GetBytes("x");
        };
        var result = regex.Subn(Utf8("foo foo"), "-", evaluator);
        Assert.Equal("x x", Encoding.UTF8.GetString(result.ResultBytes));
        Assert.Equal(2, result.ReplacementCount);
        Assert.Equal(["0", "word"], regex.GetGroupNames());
        Assert.Equal("word", regex.SearchDetailedData(Utf8("foo")).NameEntries[0].Name);
    }


    [Fact]
    public void ConcurrentReadsSeeStableMetadata()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var failures = 0;
        System.Threading.Tasks.Parallel.For(0, 64, _ =>
        {
            if (regex.SearchDetailedData(Utf8("foo")).NameEntries[0].Name != "word") { System.Threading.Interlocked.Increment(ref failures); }
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
        var first = regex.SearchDetailedData(Utf8("ab"));
        Assert.Empty(first.NameEntries);
        Assert.Equal(["0", "1", "2"], regex.GetGroupNames());
        var second = regex.SearchDetailedData(Utf8("ab"));
        Assert.Empty(second.NameEntries);
        Assert.True(second.Success);
    }
}
