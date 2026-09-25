namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned structural node-kind vocabulary for regex parse trees. Members and
// numeric values mirror the fork wire encoding exactly (including the
// opcode-aliased loop kinds and the lowercase-L lazy spelling) so trees keep
// identical shape identity across the migration; no behavior lives here.
internal enum Utf8TreeNodeKind : byte
{
    Unknown = 0,
    Oneloop = 3,
    Notoneloop = 4,
    Setloop = 5,
    Onelazy = 6,
    Notonelazy = 7,
    Setlazy = 8,
    One = 9,
    Notone = 10,
    Set = 11,
    Multi = 12,
    Backreference = 13,
    Bol = 14,
    Eol = 15,
    Boundary = 16,
    NonBoundary = 17,
    Beginning = 18,
    Start = 19,
    EndZ = 20,
    End = 21,
    Nothing = 22,
    Empty = 23,
    Alternate = 24,
    Concatenate = 25,
    Loop = 26,
    Lazyloop = 27,
    Capture = 28,
    Group = 29,
    PositiveLookaround = 30,
    NegativeLookaround = 31,
    Atomic = 32,
    BackreferenceConditional = 33,
    ExpressionConditional = 34,
    ECMABoundary = 41,
    NonECMABoundary = 42,
    Oneloopatomic = 43,
    Notoneloopatomic = 44,
    Setloopatomic = 45,
    UpdateBumpalong = 46,
}