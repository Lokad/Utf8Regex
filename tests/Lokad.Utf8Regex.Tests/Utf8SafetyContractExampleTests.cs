namespace Lokad.Utf8Regex.Tests;

/// <summary>Executes the cancellation example published in SPEC.md.</summary>
public sealed class Utf8SafetyContractExampleTests
{
    [Fact]
    public void CanceledTokenThrowsBeforeIsMatch()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var regex = new Utf8Regex("cat|horse");
        var exception = Assert.Throws<OperationCanceledException>(() => regex.IsMatch("cat horse"u8, canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
    }
}
