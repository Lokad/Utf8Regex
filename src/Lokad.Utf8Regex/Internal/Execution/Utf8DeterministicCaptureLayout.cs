namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8DeterministicCaptureLayout
{
    public Utf8DeterministicCaptureLayout(int matchLength, Dictionary<int, Utf8DeterministicCaptureSlice> captures)
    {
        MatchLength = matchLength;
        Captures = captures;
    }

    public int MatchLength { get; }

    // Published once by program construction and never mutated. Keeping the
    // concrete dictionary avoids boxing its enumerator during final projection.
    public Dictionary<int, Utf8DeterministicCaptureSlice> Captures { get; }
}

internal readonly record struct Utf8DeterministicCaptureSlice(int Offset, int Length);
