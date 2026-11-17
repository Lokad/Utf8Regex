using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProgramPublicationTests
{

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)] [InlineData(1, false)]
    [InlineData(1, true)] [InlineData(2, false)] [InlineData(2, true)]
    public void OwnedPublicationKeepsConfigurationOrFailsEagerlyAndRecovers(int mode, bool valid)
    {
        var options = RegexOptions.CultureInvariant | (mode == 1 ? RegexOptions.Compiled :
            mode == 2 ? RegexOptions.NonBacktracking : RegexOptions.None);
        if (!valid)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ReservedBytes;
            ArgumentException? first = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Utf8Regex? unpublished = null;
                var workBefore = owner.Budget.WorkSteps;
                var error = Assert.ThrowsAny<ArgumentException>(() => unpublished = new Utf8Regex("(", options));
                Assert.Null(unpublished);
                if (first is not null) { Assert.Equal(first.Message, error.Message); Assert.Equal(first.ParamName, error.ParamName); }
                first = error;
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth); Assert.True(owner.Budget.WorkSteps > workBefore);
            }
            Assert.Equal(17, parent.Value);
            var recovered = new Utf8Regex("(?<x>a)", options);
            Assert.Equal(1, recovered.Count("😀a"u8));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(recovered.CreateMatchCursor("😀a"u8.ToArray())).GetGroupMemory(recovered.GroupNumberFromName("x")).Span));
            return;
        }
        var regex = new Utf8Regex("(?<x>a)", options);
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
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor("😀a"u8.ToArray())).GetGroupMemory(regex.GroupNumberFromName("x")).Span));
        });
    }

    private sealed class Box(int value) { internal readonly int Value = value; }
}

internal static class Utf8PreparedTestPlans
{

    internal static Utf8PreparedRegex WithSearchPlan(Utf8PreparedRegex p, Utf8SearchPlan search)
=> Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, p.ExecutionKind, p.ExecutionBackend, p.CompiledPatternCategory, p.ExecutionTree, p.ScalarProgram, Utf8OperationMatchCursorPlans.Create(search), p.LiteralUtf8, p.FallbackReason, p.AnchoredLiteral);}
