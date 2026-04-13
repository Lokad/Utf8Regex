namespace Lokad.Utf8Regex.Internal.Diagnostics;

internal sealed class Utf8SearchDiagnosticsSession
{
    [ThreadStatic]
    private static Utf8SearchDiagnosticsSession? t_current;

    private Utf8SearchDiagnosticsSession()
    {
    }

    public static Utf8SearchDiagnosticsSession Start()
    {
        var session = new Utf8SearchDiagnosticsSession();
        t_current = session;
        return session;
    }

    public static Utf8SearchDiagnosticsSession? Current => t_current;

    public int SearchCandidates { get; private set; }

    public int FixedCheckRejects { get; private set; }

    public int VerifierInvocations { get; private set; }

    public int VerifierMatches { get; private set; }

    public int PrefilterWindows { get; private set; }

    public int PrefilterSkippedWindows { get; private set; }

    public int PrefilterPromotedWindows { get; private set; }

    public int PrefilterSkippedBytes { get; private set; }

    public int PrefilterPromotedBytes { get; private set; }

    public int EngineDemotions { get; private set; }

    public string? ExecutionRoute { get; private set; }

    public void CountSearchCandidate()
    {
        SearchCandidates++;
    }

    public void CountFixedCheckReject()
    {
        FixedCheckRejects++;
    }

    public void CountVerifierInvocation()
    {
        VerifierInvocations++;
    }

    public void CountVerifierMatch()
    {
        VerifierMatches++;
    }

    public void CountPrefilterWindow(bool promoted, int byteCount)
    {
        PrefilterWindows++;
        if (promoted)
        {
            PrefilterPromotedWindows++;
            PrefilterPromotedBytes += byteCount;
        }
        else
        {
            PrefilterSkippedWindows++;
            PrefilterSkippedBytes += byteCount;
        }
    }

    public void CountEngineDemotion()
    {
        EngineDemotions++;
    }

    public void MarkExecutionRoute(string route)
    {
        ExecutionRoute ??= route;
    }

    public void Complete()
    {
        if (ReferenceEquals(t_current, this))
        {
            t_current = null;
        }
    }
}
