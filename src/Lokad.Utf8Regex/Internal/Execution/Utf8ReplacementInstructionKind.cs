namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8ReplacementInstructionKind : byte
{
    Literal = 0,
    Group = 1,
    WholeMatch = 2,
    LeftPortion = 3,
    RightPortion = 4,
    LastGroup = 5,
    WholeString = 6,
}
