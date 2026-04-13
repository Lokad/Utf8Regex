using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8VerifierRuntime
{
    public Utf8VerifierRuntime(
        Utf8StructuralVerifierPlan structuralVerifierPlan,
        Utf8StructuralVerifierRuntime structuralVerifierRuntime,
        Utf8FallbackCandidateVerifier fallbackCandidateVerifier)
    {
        StructuralVerifierPlan = structuralVerifierPlan;
        StructuralVerifierRuntime = structuralVerifierRuntime;
        FallbackCandidateVerifier = fallbackCandidateVerifier;
    }

    public Utf8StructuralVerifierPlan StructuralVerifierPlan { get; }

    public Utf8StructuralVerifierRuntime StructuralVerifierRuntime { get; }

    public Utf8FallbackCandidateVerifier FallbackCandidateVerifier { get; }

    public static Utf8VerifierRuntime Create(Utf8RegexPlan regexPlan, string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        return new Utf8VerifierRuntime(
            regexPlan.StructuralVerifier,
            regexPlan.StructuralVerifier.CreateRuntime(),
            regexPlan.FallbackVerifier.CreateRuntime(pattern, options, matchTimeout));
    }
}
