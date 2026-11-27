using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSharedPrefixAdmissionTests
{
    [Fact]
    public void SharedPrefixSkipsFalseHeadsWithinTheOriginalFinitePolicy()
    {
        var input = Encoding.UTF8.GetBytes(new string('i', 65536) + "tem-12");
        using var regex = new Utf8PythonRegex("item-([0-9]+)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1UL << 20,
                MaxResultBytes = 1UL << 20
            }
        });
        using var cancellation = new CancellationTokenSource();
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, cancellation.Token))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        var result = snapshots1;
        Assert.Equal("12", Assert.Single(result.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray()));
        ReadOnlyMemory<byte> snapshots2Input = ("no match"u8).ToArray();
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        _ = snapshots2;
        Assert.Equal("12", result.Select(captureSnapshot2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).Span)).ToArray()[0]);
    }
}
