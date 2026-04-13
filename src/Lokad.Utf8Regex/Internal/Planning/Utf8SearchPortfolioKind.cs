namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8SearchPortfolioKind : byte
{
    None = 0,
    ExactLiteral = 1,
    IgnoreCaseLiteral = 2,
    ExactDirectFamily = 3,
    ExactTrieFamily = 4,
    ExactAutomatonFamily = 5,
    ExactPackedFamily = 6,
    AsciiIgnoreCaseFamily = 7,
    ExactEarliestFamily = 8,
}
