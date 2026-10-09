using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReEngineArchitectureTests
{
    [Fact]
    public void ShippedAdapterReferencesNoBclMatchingEngineOrResults()
    {
        // Inspect the consumed assembly, so this also guards detached packages.
        // RegexOptions and RegexMatchTimeoutException remain shared API types.
        using var stream = File.OpenRead(typeof(Utf8PythonRegex).Assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        string[] engineTypes = ["Regex", "Match", "Group", "Capture", "MatchCollection", "GroupCollection", "CaptureCollection"];
        foreach (var handle in metadata.TypeReferences)
        {
            var reference = metadata.GetTypeReference(handle);
            if (metadata.GetString(reference.Namespace) == "System.Text.RegularExpressions")
                Assert.DoesNotContain(metadata.GetString(reference.Name), engineTypes);
        }
    }
}
