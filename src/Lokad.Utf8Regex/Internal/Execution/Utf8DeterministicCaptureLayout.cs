namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8DeterministicCaptureLayout
{
    public Utf8DeterministicCaptureLayout(int matchLength, IReadOnlyDictionary<int, Utf8DeterministicCaptureSlice> captures)
    {
        MatchLength = matchLength;
        Captures = captures;
    }

    public int MatchLength { get; }

    public IReadOnlyDictionary<int, Utf8DeterministicCaptureSlice> Captures { get; }
}

internal readonly record struct Utf8DeterministicCaptureSlice(int Offset, int Length);
