using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreTemplateByteCeilingTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void EmptyInputCannotPrepareAnOversizedTemplate(bool bytes, bool liveToken)
    {
        var regex = Create("a", 4096);
        var text = new string('x', 65536);
        var template = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        var held = parent.Budget.AllocateArray<byte>(17, false);
        var reserved = parent.Budget.ReservedBytes;
        var maximum = parent.Budget.MaximumBytes;
        Action<ulong> hook = _ => { };
        parent.Budget.CheckpointHook = hook;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var spent = parent.Budget.WorkSteps;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (bytes) _ = regex.Replace([], template, liveToken ? token.Token : default);
                else _ = regex.Replace([], text, liveToken ? token.Token : default);
            });
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.True(failure.Requested > failure.Limit);
            Assert.Equal(0, Cache(regex).Count);
            Assert.Equal(reserved, parent.Budget.ReservedBytes);
            Assert.Equal(maximum, parent.Budget.MaximumBytes);
            Assert.Equal(0ul, parent.Budget.Depth);
            Assert.Same(hook, parent.Budget.CheckpointHook);
            Assert.True(parent.Budget.WorkSteps > spent);
            Assert.Equal(17, held.Length);
        }
        Assert.Empty(regex.Replace([], ""));
        Assert.Equal(1, Cache(regex).Count);
    }

    [Fact]
    public void WarmUtf8LookupStillAdmitsItsDecodedKey()
    {
        var regex = Create("(?<x>a)", 1 << 20);
        var text = new string('x', 512) + "${x}";
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Empty(regex.Replace([], text));
        using var parent = Utf8ConstructionScope.RestrictAdditionalBytes(1);
        var maximum = parent.Budget.MaximumBytes;
        Assert.Empty(regex.Replace([], text)); // An existing text key needs no copy.
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace([], bytes));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(maximum, parent.Budget.MaximumBytes);
        Assert.Equal(1, Cache(regex).Count);
    }

    [Fact]
    public void FundedScalarResultsSurviveAnUnrelatedPreparationFailure()
    {
        var regex = Create("(?<x>😀|a)", 1 << 20);
        var retained = regex.MatchDetailed("😀a"u8);
        Assert.Equal("<😀><a>"u8.ToArray(), regex.Replace("😀a"u8, "<${x}>"));
        Assert.Equal("<😀><a>"u8.ToArray(), regex.Replace("😀a"u8, "<${x}>"u8));
        var count = Cache(regex).Count;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Replace([], new string('x', 1 << 20)));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(count, Cache(regex).Count);
        Assert.Equal("😀", retained.GetGroup("x").GetValueString());
        Assert.Equal("<😀><a>", regex.ReplaceToString("😀a"u8, "<${x}>"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ValidationWorkIsAdmittedBeforeMalformedTemplateOrSubject(bool destination)
    {
        var regex = Create("a", 4096);
        var template = new byte[257]; template[^1] = 255;
        var output = Enumerable.Repeat((byte)0x5a, 16).ToArray();
        using var parent = new Utf8ConstructionScope(0, 4096, 8, 128);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (destination) regex.TryReplace([255], template, output, out _);
            else _ = regex.Replace([255], template);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(8ul, failure.Limit);
        Assert.Equal((ulong)template.Length, failure.Requested);
        Assert.Equal(0ul, parent.Budget.WorkSteps);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(0, Cache(regex).Count);
        Assert.All(output, value => Assert.Equal((byte)0x5a, value));
    }

    [Fact]
    public void StandaloneValidationKeepsItsFixedBackstopBeforeTheMalformedTail()
    {
        var regex = Create("a", 4096);
        var template = new byte[checked((int)Utf8ConstructionScope.DefaultWorkSteps + 1)];
        template[^1] = 255;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace([], template));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(Utf8ConstructionScope.DefaultWorkSteps, failure.Limit);
        Assert.Equal((ulong)template.Length, failure.Requested);
        Assert.Equal(0, Cache(regex).Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundedPreparationPreservesOriginalInterruptionAndParent(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex("(?<x>😀|a)", RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(20), new()
            {
                MaxWorkSteps = 100000, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
            });
        using var token = new CancellationTokenSource();
        using var parent = Utf8ConstructionScope.Enter();
        var maximum = parent.Budget.MaximumBytes;
        var checkpoints = 0;
        Action<ulong> hook = _ =>
        {
            if (++checkpoints != 8) return;
            if (cancel) token.Cancel(); else ticks = 25;
        };
        parent.Budget.CheckpointHook = hook;
        var failure = Record.Exception(() => regex.Replace("😀a"u8, "<${x}>"u8, token.Token));
        if (cancel)
            Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
        {
            var timeout = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(TimeSpan.FromMilliseconds(20), timeout.MatchTimeout);
            Assert.Empty(timeout.Input);
        }
        Assert.Equal(8, checkpoints);
        Assert.Equal(0, Cache(regex).Count);
        Assert.Equal(0ul, parent.Budget.ReservedBytes);
        Assert.Equal(maximum, parent.Budget.MaximumBytes);
        Assert.Same(hook, parent.Budget.CheckpointHook);
        ticks = 0;
        parent.Budget.CheckpointHook = null;
        Assert.Equal("<😀><a>"u8.ToArray(), regex.Replace("😀a"u8, "<${x}>"u8));
    }

    private static Utf8Regex Create(string pattern, ulong working) => new(pattern,
        RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
        new() { MaxWorkSteps = 1000000, MaxDepth = 256, MaxWorkingBytes = working, MaxResultBytes = 1 << 20 });

    private static Utf8ReplacementPlanCache Cache(Utf8Regex regex) =>
        Assert.IsType<Utf8ReplacementPlanCache>(typeof(Utf8Regex).GetField("_replacementCache",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
}
