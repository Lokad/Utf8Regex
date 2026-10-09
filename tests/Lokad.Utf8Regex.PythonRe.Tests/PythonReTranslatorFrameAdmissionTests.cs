using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Collections;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTranslatorFrameAdmissionTests
{
    [Fact]
    public void BorrowedAstAndClassListsNeedNoEnumeratorForInspectionWidthOrEmission()
    {
        var exact = new PythonReSequenceNode(new IndexedOnlyList<PythonReNode>([new PythonReLiteralNode('b'), new PythonReLiteralNode('c')]));
        var alternate = new PythonReAlternationNode(new IndexedOnlyList<PythonReNode>([new PythonReLiteralNode('a'), exact]));
        var group = new PythonReGroupNode(PythonReGroupKind.NonCapturing, alternate, null, PythonReCompileOptions.None, PythonReCompileOptions.None);
        var scalarClass = new PythonReCharacterClassNode(false,
            new IndexedOnlyList<PythonReCharacterClassItem>([new PythonReCharacterClassLiteralItem(0x1f600)]));
        var root = new PythonReSequenceNode(new IndexedOnlyList<PythonReNode>([group, scalarClass]));
        var parsed = new PythonReParser("").Parse(PythonReCompileOptions.None) with { Root = root };
        Assert.False(PythonReTranslator.CanMatchEmpty(root, parsed));
        Assert.True(PythonReTranslator.GetWidth(root, parsed).Max > 0);
        Assert.True(PythonReTranslator.CanUseUtf8IterationFastPath(root));
        Assert.True(PythonReTranslator.IsExactLiteral(exact));
        Assert.False(PythonReTranslator.IsExactLiteral(root));
        var translation = PythonReTranslator.Translate(parsed);
        Assert.Equal("(?:a|bc)[😀]", translation.NativePattern);
        Assert.Equal("[😀]", PythonReTranslator.RenderNativeAtom(scalarClass));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InspectionAdmissionFailurePreservesParentAndAllowsSameOwnerRetry(bool depth)
    {
        var root = new PythonReSequenceNode(new IndexedOnlyList<PythonReNode>([new PythonReLiteralNode('a')]));
        ulong literalPeak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Assert.True(PythonReTranslator.CanUseUtf8IterationFastPath(new PythonReLiteralNode('a')));
            literalPeak = measured.Budget.PeakBytes;
        }
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, depth ? ulong.MaxValue : literalPeak + 48,
            ulong.MaxValue, depth ? 1ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => PythonReTranslator.CanUseUtf8IterationFastPath(root));
        Assert.Equal(depth ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Null(owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps;
        Assert.True(PythonReTranslator.CanUseUtf8IterationFastPath(new PythonReLiteralNode('a')));
        Assert.True(owner.Budget.WorkSteps > spent);
        owner.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void TranslationAndRenderingHaveNoGeneratedYieldOrCapturedClosureTypes()
    {
        Assert.DoesNotContain(typeof(PythonReTranslator).GetNestedTypes(System.Reflection.BindingFlags.NonPublic),
            type => type.Name.Contains("d__", StringComparison.Ordinal) || type.Name.Contains("DisplayClass", StringComparison.Ordinal));
    }

    private sealed class IndexedOnlyList<T>(T[] items) : IReadOnlyList<T>
    {
        public int Count => items.Length;
        public T this[int index] => items[index];
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Borrowed AST enumeration would allocate an unadmitted iterator.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
