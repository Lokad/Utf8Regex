using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal;

// Pattern and projection factories share the enclosing construction authority. Scratch
// vectors/text release on every exit; completed arrays and retained descriptors keep
// their loans. The scope, rather than this session, owns speculative rollback.
internal sealed class Utf8ConstructionSession(
    Utf8ConstructionBudget budget,
    Utf8ConstructionArena arrays,
    Utf8ConstructionBuffer<IDisposable> scratch) : IDisposable
{
    internal Utf8ConstructionBudget Budget => budget;

    internal static Utf8ConstructionSession Create(Utf8ConstructionBudget owner)
    {
        owner.EnterDepth();
        Utf8ConstructionArena? arrays = null;
        Utf8ConstructionBuffer<IDisposable>? scratch = null;
        try
        {
            arrays = Utf8ConstructionArena.Create(owner);
            scratch = Utf8ConstructionBuffer<IDisposable>.Create(owner);
            return owner.AllocateObject((owner, arrays, scratch), static state =>
                new Utf8ConstructionSession(state.owner, state.arrays, state.scratch));
        }
        catch
        {
            scratch?.Dispose();
            arrays?.Dispose();
            owner.LeaveDepth();
            throw;
        }
    }

    internal Utf8ConstructionBuffer<T> Buffer<T>() => Track(Utf8ConstructionBuffer<T>.Create(budget));
    internal Utf8ConstructionText Text() => Track(Utf8ConstructionText.Create());

    private T Track<T>(T value) where T : IDisposable
    {
        try { scratch.Add(value); return value; }
        catch { value.Dispose(); throw; }
    }

    internal T[] Allocate<T>(int count) => Allocate<T>(count, true);
    internal T[] Allocate<T>(int count, bool retained) => arrays.Allocate<T>(count, retained);

    internal void Retain(Array array) => arrays.Retain(array);

    internal T[] Copy<T>(Utf8ConstructionBuffer<T> values, bool retained)
    {
        var result = Allocate<T>(values.Count, retained);
        budget.ChargeWork((ulong)values.Count);
        values.Span.CopyTo(result);
        return result;
    }

    internal T[] Copy<T>(Utf8ConstructionBuffer<T> values)
    {
        return Copy(values, true);
    }

    internal string Concat(string left, string right)
    {
        if (left.Length == 0) return right;
        if (right.Length == 0) return left;
        using var text = Utf8ConstructionText.Create();
        return text.Append(left).Append(right).ToText();
    }

    // Replacement text repairs invalid UTF-16 inside each independent literal
    // fragment. Pattern encoding remains strict; this is shared interop work.
    internal byte[] EncodeRepairedUtf8(ReadOnlySpan<char> text)
        => EncodeRepairedUtf8(text, default(Utf8ConstructionTextNoControl));

    internal byte[] EncodeRepairedUtf8<TControl>(ReadOnlySpan<char> text, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var count = 0;
        for (var index = 0; index < text.Length;)
        {
            control.Check();
            budget.ChargeWork(1);
            var rune = ReadScalar(text, ref index);
            count = checked(count + rune.Utf8SequenceLength);
        }
        var result = Allocate<byte>(count);
        var written = 0;
        for (var index = 0; index < text.Length;)
        {
            control.Check();
            budget.ChargeWork(1);
            var rune = ReadScalar(text, ref index);
            budget.ChargeWork((ulong)rune.Utf8SequenceLength);
            written += rune.EncodeToUtf8(result.AsSpan(written));
        }
        return result;
    }

    private static Rune ReadScalar(ReadOnlySpan<char> text, ref int index)
    {
        var status = Rune.DecodeFromUtf16(text[index..], out var rune, out var consumed);
        index += Math.Max(1, consumed);
        return status == OperationStatus.Done ? rune : Rune.ReplacementChar;
    }

    internal void Complete() => arrays.Complete();

    public void Dispose()
    {
        // Cleanup must not allocate, charge work, or observe cancellation.
        for (var index = 0; index < scratch.Count; index++) scratch[index].Dispose();
        scratch.Dispose();
        arrays.Dispose();
        budget.LeaveDepth();
    }
}

// Generic interop encoding shares construction admission while a caller can
// additionally observe its synchronous operation control without a delegate.
internal interface IUtf8ConstructionTextControl
{
    void Check();
}
internal readonly struct Utf8ConstructionTextNoControl : IUtf8ConstructionTextControl
{
    public void Check() { }
}
