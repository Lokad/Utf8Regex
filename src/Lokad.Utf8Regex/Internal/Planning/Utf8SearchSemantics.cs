namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8SearchOverlapPolicy : byte
{
    None = 0,
    Overlapping = 1,
    NonOverlapping = 2,
}

internal enum Utf8SearchExitPolicy : byte
{
    None = 0,
    First = 1,
    CountAll = 2,
    EnumerateAll = 3,
}

internal readonly record struct Utf8SearchSemantics(
    Utf8SearchOverlapPolicy OverlapPolicy,
    Utf8SearchExitPolicy ExitPolicy,
    bool RequiresConfirmation = false,
    bool RequiresProjection = false)
{
    public static Utf8SearchSemantics CandidateScan =>
        new(Utf8SearchOverlapPolicy.Overlapping, Utf8SearchExitPolicy.None);

    public static Utf8SearchSemantics FirstMatch =>
        new(Utf8SearchOverlapPolicy.Overlapping, Utf8SearchExitPolicy.First);

    public static Utf8SearchSemantics CountMatches =>
        new(Utf8SearchOverlapPolicy.NonOverlapping, Utf8SearchExitPolicy.CountAll);

    public static Utf8SearchSemantics EnumerateMatches =>
        new(Utf8SearchOverlapPolicy.NonOverlapping, Utf8SearchExitPolicy.EnumerateAll, RequiresProjection: true);
}
