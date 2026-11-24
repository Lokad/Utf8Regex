using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8IlEmitter
{
    private readonly Utf8DynamicIlBuilder _il;
    private readonly MethodInfo _getSpanLengthMethod;
    private readonly MethodInfo _getSpanItemMethod;
    private readonly byte _inputArgument;

    public Utf8IlEmitter(Utf8DynamicIlBuilder il, MethodInfo getSpanLengthMethod, MethodInfo getSpanItemMethod,
        byte inputArgument = 1)
    {
        _il = il;
        _getSpanLengthMethod = getSpanLengthMethod;
        _getSpanItemMethod = getSpanItemMethod;
        _inputArgument = inputArgument;
    }

    public Utf8IlLocal DeclareLocal<T>() => _il.DeclareLocal(typeof(T));

    internal T[] AllocateScratch<T>(int count) => _il.AllocateScratch<T>(count);

    internal void ChargeWork(ulong steps) => _il.Budget.ChargeWork(steps);

    public Utf8IlLabel DefineLabel() => _il.DefineLabel();

    public void MarkLabel(Utf8IlLabel label) => _il.MarkLabel(label);

    public void Emit(OpCode opcode) => _il.Emit(opcode);

    public void Emit(OpCode opcode, Utf8IlLabel label) => _il.Emit(opcode, label);

    public void Emit(OpCode opcode, MethodInfo method) => _il.Emit(opcode, method);

    public void Emit(OpCode opcode, int value) => _il.Emit(opcode, value);

    public void LoadArg(byte index)
    {
        switch (index)
        {
            case 0:
                Emit(OpCodes.Ldarg_0);
                break;
            case 1:
                Emit(OpCodes.Ldarg_1);
                break;
            case 2:
                Emit(OpCodes.Ldarg_2);
                break;
            case 3:
                Emit(OpCodes.Ldarg_3);
                break;
            default:
                _il.Emit(OpCodes.Ldarg_S, index);
                break;
        }
    }

    private void LoadInputAddress() => _il.Emit(OpCodes.Ldarga_S, _inputArgument);

    public void LoadLocal(Utf8IlLocal local) => _il.Emit(OpCodes.Ldloc, local);

    public void StoreLocal(Utf8IlLocal local) => _il.Emit(OpCodes.Stloc, local);

    public void LoadInputLength()
    {
        LoadInputAddress();
        Emit(OpCodes.Call, _getSpanLengthMethod);
    }

    public void LoadInputByte(Utf8IlLocal indexLocal) => LoadInputByte(indexLocal, 0);

    internal void LoadInputByte(int index)
    {
        LoadInputAddress();
        LdcI4(index);
        Emit(OpCodes.Call, _getSpanItemMethod);
        Emit(OpCodes.Ldind_U1);
    }

    internal void LdcI8(ulong value) => _il.EmitInt64(unchecked((long)value));

    public void LoadInputByte(Utf8IlLocal indexLocal, int offset)
    {
        LoadInputAddress();
        LoadLocal(indexLocal);
        if (offset != 0)
        {
            LdcI4(offset);
            Emit(OpCodes.Add);
        }

        Emit(OpCodes.Call, _getSpanItemMethod);
        Emit(OpCodes.Ldind_U1);
    }

    public void EmitEnsureAvailable(Utf8IlLocal startLocal, Utf8IlLocal inputLengthLocal, int requiredLength, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        LoadLocal(inputLengthLocal);
        LoadLocal(startLocal);
        Emit(OpCodes.Sub);
        LdcI4(requiredLength);
        Emit(OpCodes.Bge, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitSpanLiteralEquals(Utf8IlLocal startLocal, ReadOnlySpan<byte> literal, int skipPrefixCount, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        for (var i = skipPrefixCount; i < literal.Length; i++)
        {
            LoadInputByte(startLocal, i);
            LdcI4(literal[i]);
            Emit(OpCodes.Bne_Un, failureLabel);
        }

        Emit(OpCodes.Br, successLabel);
    }

    public void EmitByteDispatch(Utf8IlLocal valueLocal, ReadOnlySpan<byte> keys, ReadOnlySpan<Utf8IlLabel> labels, Utf8IlLabel defaultLabel)
    {
        if (keys.Length != labels.Length)
        {
            throw new ArgumentException("Dispatch keys and labels must have the same length.");
        }

        for (var i = 0; i < keys.Length; i++)
        {
            EmitEqualityBranch(valueLocal, keys[i], labels[i]);
        }

        Emit(OpCodes.Br, defaultLabel);
    }

    public void LdcI4(int value)
    {
        switch (value)
        {
            case -1:
                Emit(OpCodes.Ldc_I4_M1);
                break;
            case 0:
                Emit(OpCodes.Ldc_I4_0);
                break;
            case 1:
                Emit(OpCodes.Ldc_I4_1);
                break;
            case 2:
                Emit(OpCodes.Ldc_I4_2);
                break;
            case 3:
                Emit(OpCodes.Ldc_I4_3);
                break;
            case 4:
                Emit(OpCodes.Ldc_I4_4);
                break;
            case 5:
                Emit(OpCodes.Ldc_I4_5);
                break;
            case 6:
                Emit(OpCodes.Ldc_I4_6);
                break;
            case 7:
                Emit(OpCodes.Ldc_I4_7);
                break;
            case 8:
                Emit(OpCodes.Ldc_I4_8);
                break;
            default:
                if (value is >= sbyte.MinValue and <= sbyte.MaxValue)
                {
                    _il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                }
                else
                {
                    Emit(OpCodes.Ldc_I4, value);
                }

                break;
        }
    }

    public void EmitEqualityBranch(Utf8IlLocal valueLocal, int expected, Utf8IlLabel successLabel)
    {
        LoadLocal(valueLocal);
        LdcI4(expected);
        Emit(OpCodes.Beq, successLabel);
    }
}
