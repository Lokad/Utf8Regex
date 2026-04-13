namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8BoundaryRequirement : byte
{
    None = 0,
    Boundary = 1,
    NonBoundary = 2,
}
