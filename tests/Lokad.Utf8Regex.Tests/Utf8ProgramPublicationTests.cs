using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProgramPublicationTests
{

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)] [InlineData(1, false)]
    [InlineData(1, true)] [InlineData(2, false)] [InlineData(2, true)]
    public void OwnedPublicationKeepsConfigurationOrFailsEagerlyAndRecovers(int mode, bool valid)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var options = RegexOptions.CultureInvariant | (mode == 1 ? RegexOptions.Compiled :
            mode == 2 ? RegexOptions.NonBacktracking : RegexOptions.None);
        if (!valid)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ReservedBytes;
            ArgumentException? first = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Utf8Regex? unpublished = null;
                var workBefore = owner.Budget.DiagnosticCheckpoints;
                var error = Assert.ThrowsAny<ArgumentException>(() => unpublished = new Utf8Regex("(", options: options));
                Assert.Null(unpublished);
                if (first is not null) { Assert.Equal(first.Message, error.Message); Assert.Equal(first.ParamName, error.ParamName); }
                first = error;
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth); Assert.True(owner.Budget.DiagnosticCheckpoints > workBefore);
            }
            Assert.Equal(17, parent.Value);
            var recovered = new Utf8Regex("(?<x>a)", options: options);
            Assert.Equal(1, recovered.Count("😀a"u8));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(recovered.CreateMatchCursor("😀a"u8.ToArray())).GetGroupMemory(recovered.GroupNumberFromUtf8Name("x"u8)).Span));
            return;
        }
        var regex = new Utf8Regex("(?<x>a)", options: options);
        var prepared = regex.Inspection.PreparedRegex;
        var scalar = prepared.ScalarProgram;
        Assert.NotNull(scalar);
        Assert.Same(prepared.SemanticRegex.CaptureCatalog, scalar.CaptureCatalog);
        Assert.Equal("(?<x>a)", regex.Pattern); Assert.Equal(options, regex.Options);
        Assert.Equal(Regex.InfiniteMatchTimeout, regex.MatchTimeout);
        Parallel.For(0, 32, _ =>
        {
            Assert.Same(prepared, regex.Inspection.PreparedRegex);
            Assert.Same(scalar, regex.Inspection.AvailableOwnedSliceProgram);
            Assert.True(regex.IsMatch("😀a"u8));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor("😀a"u8.ToArray())).GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        });
    }

    private sealed class Box(int value) { internal readonly int Value = value; }
}
