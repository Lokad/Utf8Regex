using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Immutable cursor name metadata stays stable across independent traversals.
public sealed class PythonResultOwnershipRegressionTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

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
            Span<byte> name = stackalloc byte[4];
            if (regex.TryCopyGroupNameUtf8(1, name, out var required) != System.Buffers.OperationStatus.Done ||
                required != 4 || !name.SequenceEqual("word"u8))
                System.Threading.Interlocked.Increment(ref failures);
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
        Assert.Equal(2, regex.CaptureGroupCount);
        Utf8PythonRangeMatch second = default;
        using (var captureCursor6 = regex.CreateMatchCursor(Utf8("ab"), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor6.MoveNext(out second);
        }
        Assert.Empty(second.NameEntries.ToArray());
        Assert.True(second.Success);
    }
}
