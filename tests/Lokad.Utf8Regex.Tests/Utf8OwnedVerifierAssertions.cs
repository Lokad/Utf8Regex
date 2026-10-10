using System.Reflection;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

internal static class Utf8OwnedVerifierAssertions
{
    private static readonly FieldInfo PreparedOwner = typeof(Utf8VerifierRuntime)
        .GetField("_preparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StructuralOwner = typeof(Utf8StructuralVerifierRuntime)
        .GetField("_regexPlan", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void RetainsPreparedOwner(Utf8VerifierRuntime runtime)
    {
        var prepared = Assert.IsType<Utf8PreparedRegex>(PreparedOwner.GetValue(runtime));
        Assert.Same(prepared, StructuralOwner.GetValue(runtime.StructuralVerifierRuntime));
        Assert.Equal(prepared.StructuralVerifier.Kind, runtime.StructuralVerifierPlan.Kind);
    }
}
