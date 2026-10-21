using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <summary>Construction metadata for library-created cached instances.</summary>
public sealed partial class Utf8Regex
{
    // One conservative facade allowance, independent of field layout. Major
    // referents retain separate admission; this is not a physical heap census.
    internal static ulong CachedObjectBytes => 512;

    // One conservative allowance covers borrowed immutable empty metadata and
    // the four literal-sized frontend arrays; no per-helper collector is needed.
    internal const ulong ProgramFixedMetadataBytes = 16 * 1024;
    private readonly ulong _cacheProgramBytes;
    internal ulong CacheProgramBytes => _cacheProgramBytes;

    // The global owner covers this facade, complete scalar/prepared graphs
    // and its bounded replacement child, program metadata and closed facade
    // helpers and all owned runtime state, including direct delegate metadata.
    // CLR method/resolver/JIT descendants remain outside this declared subgraph.
    internal static Utf8Regex CreateAdmittedForCache(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.CheckBytes(CachedObjectBytes);
        owner.Budget.ChargeWork(2);
        owner.Budget.ReserveBytes(CachedObjectBytes);
        var result = new Utf8Regex(pattern, options, matchTimeout);
        owner.Complete(); return result;
    }
}
