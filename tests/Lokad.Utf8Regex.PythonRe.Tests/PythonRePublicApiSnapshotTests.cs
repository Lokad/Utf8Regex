using Lokad.Utf8Regex.Tests.Common;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePublicApiSnapshotTests
{
    [Fact]
    public void PublicApiMatchesReviewedSnapshot() =>
        PublicApiSnapshot.AssertMatches(typeof(Utf8PythonRegex).Assembly, "Lokad.Utf8Regex.PythonRe.Tests");
}
