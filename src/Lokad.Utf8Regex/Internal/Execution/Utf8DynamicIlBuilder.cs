using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8IlLabel(Utf8DynamicIlBuilder Owner, int Index);
internal readonly record struct Utf8IlLocal(Utf8DynamicIlBuilder Owner, int Index);

/// <summary>Owns the managed construction inputs to a CLR dynamic method.
/// Code, labels, fixups, locals, token caches and stack analysis share the
/// enclosing owner. Runtime parameter/code/signature copies are admitted too.
/// CLR reflection/resolution/JIT metadata remains the declared exclusion.</summary>
internal sealed class Utf8DynamicIlBuilder : IDisposable
{
    private readonly record struct LabelPoint(int Offset, int Instruction);
    private readonly record struct Fixup(int Offset, int Label, int Width);
    private readonly record struct Instruction(int Pops, int Pushes, int Target, FlowControl Flow);
    private readonly record struct MethodToken(int Token, int Parameters, int Returns, bool Instance);
    private readonly record struct ScratchArray(Array Value, ulong Bytes);
    private readonly record struct ConstructionState(Utf8ConstructionScope Scope, Utf8ConstructionBudget Budget,
        DynamicMethod Method, DynamicILInfo Info, Utf8ConstructionBuffer<byte> Code,
        Utf8ConstructionBuffer<LabelPoint> Labels, Utf8ConstructionBuffer<Fixup> Fixups,
        Utf8ConstructionBuffer<Instruction> Instructions, Utf8ConstructionBuffer<Type> Locals,
        Utf8ConstructionMap<MethodBase, MethodToken> Methods, Utf8ConstructionMap<Type, nint> Types,
        Utf8ConstructionBuffer<ScratchArray> Scratch,
        bool Returns, bool Completed, bool Disposed);

    private readonly Utf8ConstructionScope _scope;
    private readonly Utf8ConstructionBudget _budget;
    private DynamicMethod? _method;
    private DynamicILInfo? _info;
    private readonly Utf8ConstructionBuffer<byte> _code;
    private readonly Utf8ConstructionBuffer<LabelPoint> _labels;
    private readonly Utf8ConstructionBuffer<Fixup> _fixups;
    private readonly Utf8ConstructionBuffer<Instruction> _instructions;
    private readonly Utf8ConstructionBuffer<Type> _locals;
    private readonly Utf8ConstructionMap<MethodBase, MethodToken> _methods;
    private readonly Utf8ConstructionMap<Type, nint> _types;
    private readonly Utf8ConstructionBuffer<ScratchArray> _scratch;
    private readonly bool _returns;
    private bool _completed;
    private bool _disposed;

    private Utf8DynamicIlBuilder(ConstructionState state)
    {
        _scope = state.Scope; _budget = state.Budget; _method = state.Method; _info = state.Info;
        _code = state.Code; _labels = state.Labels; _fixups = state.Fixups;
        _instructions = state.Instructions; _locals = state.Locals;
        _methods = state.Methods; _types = state.Types; _scratch = state.Scratch; _returns = state.Returns;
        _completed = state.Completed; _disposed = state.Disposed;
    }

    [InlineArray(4)]
    private struct ParameterTypes { private Type _first; }

    internal static Utf8DynamicIlBuilder Create(string name, Type result, Type owner, Type p0)
    {
        ParameterTypes parameters = default; parameters[0] = p0;
        return Create(name, result, owner, parameters[..1]);
    }
    internal static Utf8DynamicIlBuilder Create(string name, Type result, Type owner, Type p0, Type p1)
    {
        ParameterTypes parameters = default; parameters[0] = p0; parameters[1] = p1;
        return Create(name, result, owner, parameters[..2]);
    }
    internal static Utf8DynamicIlBuilder Create(string name, Type result, Type owner, Type p0, Type p1, Type p2)
    {
        ParameterTypes parameters = default; parameters[0] = p0; parameters[1] = p1; parameters[2] = p2;
        return Create(name, result, owner, parameters[..3]);
    }
    internal static Utf8DynamicIlBuilder Create(string name, Type result, Type owner, Type p0, Type p1, Type p2, Type p3)
    {
        ParameterTypes parameters = default; parameters[0] = p0; parameters[1] = p1; parameters[2] = p2; parameters[3] = p3;
        return Create(name, result, owner, parameters[..4]);
    }

    private static Utf8DynamicIlBuilder Create(string name, Type result, Type owner, ReadOnlySpan<Type> parameters)
    {
        var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        var entered = false;
        try
        {
            budget.EnterDepth(); entered = true;
            var copied = budget.AllocateArray<Type>(parameters.Length, false);
            try
            {
                budget.ChargeWork((ulong)parameters.Length); parameters.CopyTo(copied);
                AdmitRuntimeCopy<Type>(budget, copied.Length, false);
                var method = new DynamicMethod(name, result, copied, owner, skipVisibility: false);
                var info = method.GetDynamicILInfo();
                var state = new ConstructionState(scope, budget, method, info,
                    Utf8ConstructionBuffer<byte>.Create(budget, true),
                    Utf8ConstructionBuffer<LabelPoint>.Create(budget),
                    Utf8ConstructionBuffer<Fixup>.Create(budget),
                    Utf8ConstructionBuffer<Instruction>.Create(budget),
                    Utf8ConstructionBuffer<Type>.Create(budget),
                    Utf8ConstructionMap<MethodBase, MethodToken>.Create(budget, EqualityComparer<MethodBase>.Default),
                    Utf8ConstructionMap<Type, nint>.Create(budget, EqualityComparer<Type>.Default),
                    Utf8ConstructionBuffer<ScratchArray>.Create(budget),
                    result != typeof(void), false, false);
                return budget.AllocateObject(state, static value => new Utf8DynamicIlBuilder(value));
            }
            finally { budget.ReleaseArray(copied, false); }
        }
        catch
        {
            if (entered) budget.LeaveDepth();
            scope.Dispose();
            throw;
        }
    }

    private static void AdmitRuntimeCopy<T>(Utf8ConstructionBudget budget, int count, bool instructions)
    {
        if (instructions) budget.CheckSlots((ulong)count);
        var bytes = Utf8ConstructionBudget.ArrayBytes<T>(count);
        budget.CheckBytes(bytes); budget.ChargeWork((ulong)count);
        if (instructions) budget.Reserve((ulong)count);
        budget.ReserveBytes(bytes);
    }

    internal Utf8ConstructionBudget Budget => _budget;
    internal int ILOffset => _code.Count;

    internal T[] AllocateScratch<T>(int count)
    {
        CheckMutable();
        var array = _budget.AllocateArray<T>(count, false);
        try { _scratch.Add(new(array, Utf8ConstructionBudget.ArrayBytes<T>(count))); }
        catch { _budget.ReleaseArray(array, false); throw; }
        return array;
    }

    private void CheckMutable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) throw new InvalidOperationException("An emitted method cannot change after publication.");
        _budget.ChargeWork(1);
    }

    internal Utf8IlLocal DeclareLocal(Type type)
    {
        CheckMutable();
        if (_locals.Count > ushort.MaxValue) throw new InvalidOperationException("The CLI local index cannot represent another local.");
        var index = _locals.Count; _locals.Add(type);
        return new(this, index);
    }
    internal Utf8IlLabel DefineLabel()
    {
        CheckMutable();
        var index = _labels.Count; _labels.Add(new(-1, -1));
        return new(this, index);
    }
    internal void MarkLabel(Utf8IlLabel label)
    {
        CheckMutable(); Check(label);
        if (_labels[label.Index].Offset >= 0) throw new InvalidOperationException("The emitted label is already marked.");
        _labels[label.Index] = new(ILOffset, _instructions.Count);
    }
    private void Check(Utf8IlLabel label)
    {
        if (!ReferenceEquals(label.Owner, this) || (uint)label.Index >= (uint)_labels.Count)
            throw new ArgumentException("The label belongs to another emitted method.", nameof(label));
    }
    private void Check(Utf8IlLocal local)
    {
        if (!ReferenceEquals(local.Owner, this) || (uint)local.Index >= (uint)_locals.Count)
            throw new ArgumentException("The local belongs to another emitted method.", nameof(local));
    }

    internal void Emit(OpCode opcode) => Write(opcode, [], -1, -1, -1);
    internal void Emit(OpCode opcode, int value)
    {
        Span<byte> operand = stackalloc byte[4];
        var width = opcode.OperandType switch
        {
            OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI => 4,
            _ => throw new ArgumentException("The opcode does not take an integer operand.", nameof(opcode))
        };
        if (width == 1) operand[0] = checked((byte)(opcode.OperandType == OperandType.ShortInlineI ? unchecked((byte)(sbyte)value) : value));
        else if (width == 2) BinaryPrimitives.WriteUInt16LittleEndian(operand, checked((ushort)value));
        else BinaryPrimitives.WriteInt32LittleEndian(operand, value);
        Write(opcode, operand[..width], -1, -1, -1);
    }
    internal void Emit(OpCode opcode, byte value) => Emit(opcode, (int)value);
    internal void Emit(OpCode opcode, sbyte value) => Emit(opcode, (int)value);
    internal void Emit(OpCode opcode, Utf8IlLocal local)
    {
        Check(local);
        if (opcode == OpCodes.Ldloc && local.Index < 4)
            Emit(local.Index switch { 0 => OpCodes.Ldloc_0, 1 => OpCodes.Ldloc_1, 2 => OpCodes.Ldloc_2, _ => OpCodes.Ldloc_3 });
        else if (opcode == OpCodes.Stloc && local.Index < 4)
            Emit(local.Index switch { 0 => OpCodes.Stloc_0, 1 => OpCodes.Stloc_1, 2 => OpCodes.Stloc_2, _ => OpCodes.Stloc_3 });
        else if (opcode == OpCodes.Ldloc && local.Index <= byte.MaxValue) Emit(OpCodes.Ldloc_S, local.Index);
        else if (opcode == OpCodes.Stloc && local.Index <= byte.MaxValue) Emit(OpCodes.Stloc_S, local.Index);
        else if (opcode == OpCodes.Ldloca_S && local.Index > byte.MaxValue) Emit(OpCodes.Ldloca, local.Index);
        else Emit(opcode, local.Index);
    }
    internal void Emit(OpCode opcode, Utf8IlLabel label)
    {
        Check(label);
        var width = opcode.OperandType switch
        {
            OperandType.InlineBrTarget => 4,
            OperandType.ShortInlineBrTarget => 1,
            _ => throw new ArgumentException("The opcode does not take a branch operand.", nameof(opcode))
        };
        var offset = ILOffset + opcode.Size;
        Span<byte> empty = stackalloc byte[4]; empty.Clear();
        Write(opcode, empty[..width], label.Index, -1, -1);
        _fixups.Add(new(offset, label.Index, width));
    }
    internal void Emit(OpCode opcode, MethodInfo method) => EmitMethod(opcode, method);
    internal void Emit(OpCode opcode, ConstructorInfo constructor) => EmitMethod(opcode, constructor);

    private void EmitMethod(OpCode opcode, MethodBase method)
    {
        CheckMutable();
        if (!_methods.TryGetValue(method, out var shape))
        {
            // Reflection's parameter records and CLR token/resolver state are
            // runtime metadata, not a library-created program buffer.
            var parameters = method.GetParameters().Length;
            _budget.ChargeWork((ulong)parameters + 1);
            var info = _info ?? throw new ObjectDisposedException(nameof(Utf8DynamicIlBuilder));
            var declaring = method.DeclaringType;
            var token = declaring is not null && (declaring.IsGenericType || declaring.IsArray)
                ? info.GetTokenFor(method.MethodHandle, declaring.TypeHandle)
                : info.GetTokenFor(method.MethodHandle);
            shape = new(token, parameters, method is MethodInfo function && function.ReturnType != typeof(void) ? 1 : 0, !method.IsStatic);
            _methods.Add(method, shape);
        }
        var pops = shape.Parameters + (shape.Instance && opcode != OpCodes.Newobj ? 1 : 0);
        var pushes = opcode == OpCodes.Newobj ? 1 : shape.Returns;
        Span<byte> operand = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(operand, shape.Token);
        Write(opcode, operand, -1, pops, pushes);
    }

    private void Write(OpCode opcode, ReadOnlySpan<byte> operand, int target, int methodPops, int methodPushes)
    {
        CheckMutable();
        var pops = opcode == OpCodes.Ret ? (_returns ? 1 : 0) : StackCount(opcode.StackBehaviourPop, methodPops);
        var pushes = StackCount(opcode.StackBehaviourPush, methodPushes);
        var size = opcode.Size + operand.Length;
        Span<byte> bytes = stackalloc byte[6];
        var value = unchecked((ushort)opcode.Value);
        if (opcode.Size == 2) { bytes[0] = (byte)(value >> 8); bytes[1] = (byte)value; }
        else bytes[0] = (byte)value;
        operand.CopyTo(bytes[opcode.Size..]);
        _code.AddRange(bytes[..size]);
        _instructions.Add(new(pops, pushes, target, opcode.FlowControl));
    }

    private static int StackCount(StackBehaviour behaviour, int variable) => behaviour switch
    {
        StackBehaviour.Pop0 or StackBehaviour.Push0 => 0,
        StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref or StackBehaviour.Push1
            or StackBehaviour.Pushi or StackBehaviour.Pushi8 or StackBehaviour.Pushr4 or StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
        StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi
            or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8
            or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi or StackBehaviour.Push1_push1 => 2,
        StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_pop1 or StackBehaviour.Popref_popi_popi
            or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,
        StackBehaviour.Varpop or StackBehaviour.Varpush when variable >= 0 => variable,
        _ => throw new InvalidOperationException("The opcode needs an explicit stack shape.")
    };

    private int ComputeMaxStack()
    {
        if (_instructions.Count == 0) throw new InvalidProgramException("The emitted method is empty.");
        using var pending = Utf8ConstructionBuffer<int>.Create(_budget);
        var heights = _budget.AllocateArray<int>(_instructions.Count, false);
        try
        {
            _budget.ChargeWork((ulong)heights.Length); Array.Fill(heights, -1);
            SetHeight(0, 0, heights, pending);
            var maximum = 0;
            while (pending.TryPop(out var index))
            {
                _budget.LeaveDepth(); _budget.ChargeWork(1);
                var instruction = _instructions[index];
                var before = heights[index];
                if (before < instruction.Pops) throw new InvalidProgramException("The emitted evaluation stack underflows.");
                var after = checked(before - instruction.Pops + instruction.Pushes);
                maximum = Math.Max(maximum, Math.Max(before, after));
                if (instruction.Flow is FlowControl.Return or FlowControl.Throw)
                {
                    if (after != 0) throw new InvalidProgramException("The emitted method leaves values on its evaluation stack.");
                    continue;
                }
                if (instruction.Target >= 0)
                    SetHeight(_labels[instruction.Target].Instruction, after, heights, pending);
                if (instruction.Flow != FlowControl.Branch)
                    SetHeight(index + 1, after, heights, pending);
            }
            return maximum;
        }
        finally
        {
            while (pending.TryPop(out _)) _budget.LeaveDepth();
            _budget.ReleaseArray(heights, false);
        }
    }
    private void SetHeight(int index, int height, int[] heights, Utf8ConstructionBuffer<int> pending)
    {
        _budget.ChargeWork(1);
        if ((uint)index >= (uint)heights.Length) throw new InvalidProgramException("The emitted branch has no instruction target.");
        if (heights[index] >= 0)
        {
            if (heights[index] != height) throw new InvalidProgramException("The emitted branch joins incompatible evaluation stacks.");
            return;
        }
        _budget.EnterDepth();
        try { pending.Push(index); }
        catch { _budget.LeaveDepth(); throw; }
        heights[index] = height;
    }

    private nint LocalTypeHandle(Type type)
    {
        _budget.ChargeWork(1);
        if (_types.TryGetValue(type, out var handle)) return handle;
        // Root the runtime type in the dynamic scope as well as this temporary
        // cache. Local signatures use runtime handles, not module TypeDef rows.
        _ = (_info ?? throw new ObjectDisposedException(nameof(Utf8DynamicIlBuilder))).GetTokenFor(type.TypeHandle);
        handle = type.TypeHandle.Value;
        _types.Add(type, handle);
        return handle;
    }
    private void WriteLocalType(Type type, Utf8ConstructionBuffer<byte> signature)
    {
        _budget.ChargeWork(1);
        if (type.IsByRef)
        {
            signature.Add(0x10);
            type = type.GetElementType() ?? throw new InvalidOperationException("A by-reference local has no element type.");
        }
        var primitive = type == typeof(bool) ? 0x02 : type == typeof(char) ? 0x03
            : type == typeof(sbyte) ? 0x04 : type == typeof(byte) ? 0x05
            : type == typeof(short) ? 0x06 : type == typeof(ushort) ? 0x07
            : type == typeof(int) ? 0x08 : type == typeof(uint) ? 0x09
            : type == typeof(long) ? 0x0a : type == typeof(ulong) ? 0x0b
            : type == typeof(float) ? 0x0c : type == typeof(double) ? 0x0d
            : type == typeof(string) ? 0x0e : type == typeof(nint) ? 0x18
            : type == typeof(nuint) ? 0x19 : type == typeof(object) ? 0x1c : -1;
        if (primitive >= 0) { signature.Add((byte)primitive); return; }
        // Dynamic local signatures encode ELEMENT_TYPE_INTERNAL followed by a
        // native runtime handle. A dynamic token interpreted as a module row can
        // silently resolve to an unrelated type. No SignatureHelper is retained.
        signature.Add(0x21);
        var handle = LocalTypeHandle(type);
        Span<byte> pointer = stackalloc byte[IntPtr.Size];
        MemoryMarshal.Write(pointer, in handle); // Native endian, process-local.
        signature.AddRange(pointer);
    }
    private void WriteCompressed(uint value, Utf8ConstructionBuffer<byte> signature)
    {
        _budget.ChargeWork(1);
        if (value <= 0x7f) signature.Add((byte)value);
        else if (value <= 0x3fff)
        {
            signature.Add((byte)((value >> 8) | 0x80)); signature.Add((byte)value);
        }
        else if (value <= 0x1fffffff)
        {
            signature.Add((byte)((value >> 24) | 0xc0)); signature.Add((byte)(value >> 16));
            signature.Add((byte)(value >> 8)); signature.Add((byte)value);
        }
        else throw new InvalidOperationException("The CLI signature cannot represent the value.");
    }

    internal TDelegate CreateDelegate<TDelegate>() where TDelegate : Delegate
    {
        CheckMutable();
        foreach (var fixup in _fixups)
        {
            _budget.ChargeWork(1);
            var target = _labels[fixup.Label].Offset;
            if (target < 0) throw new InvalidProgramException("The emitted branch label is not marked.");
            var distance = checked(target - fixup.Offset - fixup.Width);
            if (fixup.Width == 1) _code[fixup.Offset] = unchecked((byte)checked((sbyte)distance));
            else for (var index = 0; index < 4; index++) _code[fixup.Offset + index] = unchecked((byte)(distance >> (index * 8)));
        }
        var maximumStack = ComputeMaxStack();
        using var signature = Utf8ConstructionBuffer<byte>.Create(_budget);
        signature.Add(0x07); WriteCompressed((uint)_locals.Count, signature);
        foreach (var type in _locals) WriteLocalType(type, signature);
        var code = _code.Take();
        byte[] localSignature = [];
        try
        {
            localSignature = signature.Take();
            AdmitRuntimeCopy<byte>(_budget, code.Length, true);
            AdmitRuntimeCopy<byte>(_budget, localSignature.Length, false);
            var info = _info ?? throw new ObjectDisposedException(nameof(Utf8DynamicIlBuilder));
            info.SetCode(code, maximumStack); info.SetLocalSignature(localSignature);
            var method = _method ?? throw new ObjectDisposedException(nameof(Utf8DynamicIlBuilder));
            // Complete Delegate/MulticastDelegate state: target, method base,
            // method pointer/auxiliary pointer, invocation list/count.
            var result = _budget.AllocateObject((target: method, methodBase: default(object),
                pointer: nint.Zero, auxiliary: nint.Zero, invocations: default(object), count: nint.Zero),
                static state => state.target.CreateDelegate<TDelegate>());
            // The CLR call is opaque: recheck the same authority before publishing.
            _budget.ChargeWork(0);
            _completed = true; _scope.Complete();
            return result;
        }
        finally
        {
            _budget.ReleaseArray(code, true); _budget.ReleaseArray(localSignature, false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _code.Dispose(); _labels.Dispose(); _fixups.Dispose(); _instructions.Dispose(); _locals.Dispose();
        _methods.Dispose(); _types.Dispose();
        foreach (var array in _scratch) _budget.ReleaseBytes(array.Bytes);
        _scratch.Dispose();
        _method = null; _info = null;
        _budget.LeaveDepth(); _scope.Dispose();
    }
}
