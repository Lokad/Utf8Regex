namespace Lokad.Utf8Regex.Tests;

// Thread allocation counters include runtime and test-host work. Keep these
// measurements outside parallel fixtures without changing their byte limits.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Utf8RuntimeAllocationCollection
{
    public const string Name = "Runtime allocation";
}
