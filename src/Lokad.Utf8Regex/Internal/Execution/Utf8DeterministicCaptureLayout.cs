namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8DeterministicCaptureLayout
{
    public Utf8DeterministicCaptureLayout(int matchLength, KeyValuePair<int, Utf8DeterministicCaptureSlice>[] captures)
    {
        MatchLength = matchLength;
        Captures = captures;
    }

    public int MatchLength { get; }

    // Published once by program construction and never mutated. Compact array
    // projection needs neither a retained hash table nor a boxed enumerator.
    public KeyValuePair<int, Utf8DeterministicCaptureSlice>[] Captures { get; }
}

internal readonly record struct Utf8DeterministicCaptureSlice(int Offset, int Length);
