using System.Reflection;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

internal static class Utf8OwnedVerifierAssertions
{
    internal static void RetainsPreparedOwner(Utf8Regex regex)
    {
        var program = Assert.IsType<Utf8RegexProgram>(typeof(Utf8Regex)
            .GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        RetainsPreparedOwner(Assert.IsType<Utf8VerifierRuntime>(program.VerifierRuntime));
    }

    private static readonly FieldInfo StructuralOwner = typeof(Utf8StructuralVerifierRuntime)
        .GetField("_regexPlan", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void RetainsPreparedOwner(Utf8VerifierRuntime runtime)
    {
        var prepared = Assert.IsType<Utf8PreparedRegex>(StructuralOwner.GetValue(runtime.StructuralVerifierRuntime));
        Assert.Equal(prepared.StructuralVerifier.Kind, runtime.StructuralVerifierPlan.Kind);
    }
}
