using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8StorageLease
{
    // A whole-subject getter can return the prepared string itself. The alias
    // must retain its loan even after every cursor/view has disappeared. This
    // ephemeron value never roots its string key. Substrings need no attachment:
    // their independent result-materialization admission is a separate gate.
    internal void AttachToStringAlias(string value)
    {
        var owner = StringAliasRoots.Leases.GetOrAdd(value, this);
        if (!ReferenceEquals(owner, this))
            throw new InvalidOperationException("A prepared string already belongs to another storage loan.");
    }

    // Initialize the weak registry only when a getter actually exports an alias.
    // Its entry/table overhead still requires retained-metadata admission before
    // claiming a complete byte cap; payload lease correctness alone is not that cap.
    private static class StringAliasRoots
    {
        internal static readonly ConditionalWeakTable<string, Utf8StorageLease> Leases = new();
    }
}
