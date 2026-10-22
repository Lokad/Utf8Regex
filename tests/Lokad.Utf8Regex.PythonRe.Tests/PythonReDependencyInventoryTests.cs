using Lokad.Utf8Regex.Tests.Common;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDependencyInventoryTests
{
    [Fact]
    public void ConsumedPythonReReferencesNoExternalMatchers()
    {
        AssemblyDependencyInventory.AssertNoExternalMatchers(typeof(Utf8PythonRegex).Assembly.Location);
    }
}
