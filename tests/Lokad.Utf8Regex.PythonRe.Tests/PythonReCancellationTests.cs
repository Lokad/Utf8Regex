using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReCancellationTests
{
    private static readonly byte[] s_input = Encoding.UTF8.GetBytes("cat horse");

    private static CancellationToken CanceledToken()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    private static Utf8PythonRegex Create() => new("cat|horse", options: PythonReCompileOptions.None);

    [Fact]
    public void PreCanceledTokenThrowsBeforeIsMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSearch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, cancellationToken: token, mode: Utf8PythonMatchMode.Search));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFullMatch()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Match(s_input, cancellationToken: token, mode: Utf8PythonMatchMode.FullMatch));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCount()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Count(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSearchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            Utf8PythonRangeMatch snapshot1 = default;
            using (var captureCursor1 = regex.CreateMatchCursor(s_input, cancellationToken: token, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out snapshot1);
            }

            _ = snapshot1;
        });
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeMatchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            Utf8PythonRangeMatch snapshot2 = default;
            using (var captureCursor2 = regex.CreateMatchCursor(s_input, cancellationToken: token, mode: Utf8PythonMatchMode.Match))
            {
                captureCursor2.MoveNext(out snapshot2);
            }

            _ = snapshot2;
        });
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeFullMatchDetailed()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            Utf8PythonRangeMatch snapshot3 = default;
            using (var captureCursor3 = regex.CreateMatchCursor(s_input, cancellationToken: token, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor3.MoveNext(out snapshot3);
            }

            _ = snapshot3;
        });
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCaptureCursor()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            ReadOnlyMemory<byte> snapshots1Input = s_input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, token))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            return snapshots1;
        });
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeCursorCreation()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() =>
            regex.CreateMatchCursor(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }
    [Fact]
    public void PreCanceledTokenThrowsBeforeSplitRanges()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.SplitRanges(s_input, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeReplace()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace(s_input, "pet"u8, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeSubn()
    {
        var regex = Create();
        var token = CanceledToken();
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Subn(s_input, "pet"u8, cancellationToken: token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void PreCanceledTokenIsReportedAtEntry()
    {
        var regex = Create();
        var token = CanceledToken();
        var invalid = new byte[] { 0xFF, 0xFF };
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, 0, token));
        Assert.Equal(token, exception.CancellationToken);
    }

    [Fact]
    public void OptionalArgumentsPreserveNamedOffsetsAndDefaultCounts()
    {
        var regex = Create();
        var token = CancellationToken.None;
        Assert.True(regex.IsMatch(s_input, startOffsetInBytes: 4, cancellationToken: token));
        Assert.Equal(1, regex.Count(s_input, startOffsetInBytes: 4, cancellationToken: token));
        Assert.Equal("horse", Encoding.UTF8.GetString(regex.Match(s_input, startOffsetInBytes: 4).GetValueBytes()));
        Assert.Equal("cat pet", Encoding.UTF8.GetString(regex.Replace(s_input, "pet"u8, startOffsetInBytes: 4)));
        Assert.Equal("pet pet", Encoding.UTF8.GetString(regex.Replace(s_input, "pet"u8, cancellationToken: token)));
    }
}
