using Lokad.Utf8Regex.Tests.Common;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PublicApiSnapshotTests
{
    [Fact]
    public void PublicApiMatchesReviewedSnapshot() =>
        PublicApiSnapshot.AssertMatches(typeof(Utf8Pcre2Regex).Assembly, "Lokad.Utf8Regex.Pcre2.Tests");
}
