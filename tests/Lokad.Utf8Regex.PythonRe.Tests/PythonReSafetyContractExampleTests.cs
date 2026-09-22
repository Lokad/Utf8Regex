using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
/// <summary>Executes the cancellation example published in SPEC-PYTHONRE.md.</summary>
public sealed class PythonReSafetyContractExampleTests
{
    [Fact]
    public void CanceledTokenThrowsBeforeCount()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
        var exception = Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
        Assert.Equal(canceled.Token, exception.CancellationToken);
    }
}
