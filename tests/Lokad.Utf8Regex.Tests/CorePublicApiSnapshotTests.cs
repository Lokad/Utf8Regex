using Lokad.Utf8Regex.Tests.Common;

namespace Lokad.Utf8Regex.Tests;

public sealed class CorePublicApiSnapshotTests
{
    [Fact]
    public void PublicApiMatchesReviewedSnapshot() =>
        PublicApiSnapshot.AssertMatches(typeof(Utf8Regex).Assembly, "Lokad.Utf8Regex.Tests");
}
