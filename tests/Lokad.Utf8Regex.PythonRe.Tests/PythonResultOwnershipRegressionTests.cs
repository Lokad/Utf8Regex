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
        Utf8PythonRangeMatch first = default;
        using (var captureCursor1 = regex.CreateMatchCursor(Utf8("foo"), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor1.MoveNext(out first);
        }
        first.NameEntries.ToArray()[0] = new PythonReNameEntry { Name = "hacked", Number = 99 };

        Utf8PythonRangeMatch second = default;

        using (var captureCursor2 = regex.CreateMatchCursor(Utf8("foo"), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor2.MoveNext(out second);
        }
        Assert.Equal("word", second.NameEntries.ToArray()[0].Name);
        Assert.True(second.TryGetFirstSetGroup("word", out var named));
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((named.Success ? second.GetGroupMemory(named.Number).Span : ReadOnlySpan<byte>.Empty)));
    }


    [Fact]
    public void ConcurrentReadsSeeStableMetadata()
    {
        var regex = new Utf8PythonRegex("(?P<word>foo)");
        var failures = 0;
        System.Threading.Tasks.Parallel.For(0, 64, _ =>
        {
            Utf8PythonRangeMatch snapshot3 = default;
            using (var captureCursor3 = regex.CreateMatchCursor(Utf8("foo"), mode: Utf8PythonMatchMode.Search))
            {
                captureCursor3.MoveNext(out snapshot3);
            }
            if (snapshot3.NameEntries.ToArray()[0].Name != "word")
            {
                System.Threading.Interlocked.Increment(ref failures);
            }
            if (!regex.GetGroupNames().SequenceEqual(["0", "word"])) { System.Threading.Interlocked.Increment(ref failures); }
            Utf8PythonRangeMatch snapshot4 = default;
            using (var captureCursor4 = regex.CreateMatchCursor(Utf8("foo"), mode: Utf8PythonMatchMode.Match))
            {
                captureCursor4.MoveNext(out snapshot4);
            }
            if (!snapshot4.TryGetFirstSetGroup("word", out var ignoredGroup))
            {
                System.Threading.Interlocked.Increment(ref failures);
            }
        });
        Assert.Equal(0, failures);
        Assert.Equal(["0", "word"], regex.GetGroupNames());
    }

    [Fact]
    public void PatternsWithoutNamedGroupsShareNothingMutable()
    {
        var regex = new Utf8PythonRegex("(a)(b)");
        Utf8PythonRangeMatch first = default;
        using (var captureCursor5 = regex.CreateMatchCursor(Utf8("ab"), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor5.MoveNext(out first);
        }
        Assert.Empty(first.NameEntries.ToArray());
        Assert.Equal(["0", "1", "2"], regex.GetGroupNames());
        Utf8PythonRangeMatch second = default;
        using (var captureCursor6 = regex.CreateMatchCursor(Utf8("ab"), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor6.MoveNext(out second);
        }
        Assert.Empty(second.NameEntries.ToArray());
        Assert.True(second.Success);
    }
}
