namespace Lokad.Utf8Regex.Internal.Input;

internal readonly struct Utf8InputAnalysis
{
    public Utf8InputAnalysis(Utf8ValidationResult validation, Utf8BoundaryMap boundaryMap)
    {
        Validation = validation;
        BoundaryMap = boundaryMap;
    }

    public Utf8ValidationResult Validation { get; }

    public Utf8BoundaryMap BoundaryMap { get; }
}
