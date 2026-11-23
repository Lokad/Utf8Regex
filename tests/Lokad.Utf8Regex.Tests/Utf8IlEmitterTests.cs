using System.Reflection;
using System.Reflection.Emit;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8IlEmitterTests
{
    private delegate int DummySpanDelegate(object? _, ReadOnlySpan<byte> input);
    private delegate int DummySpanIndexDelegate(object? _, ReadOnlySpan<byte> input, int index);
    private delegate int DummySpanIndexLengthDelegate(object? _, ReadOnlySpan<byte> input, int index, int length);

    private static readonly MethodInfo s_getSpanLengthMethod =
        typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;

    private static readonly MethodInfo s_getSpanItemMethod =
        typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;

    [Fact]
    public void Utf8IlEmitterCanLoadSpanLengthAndConstants()
    {
        using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_Test_LengthPlusConstant", typeof(int), typeof(Utf8IlEmitterTests), typeof(object), typeof(ReadOnlySpan<byte>));
        var il = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod);

        il.LoadInputLength();
        il.LdcI4(7);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanDelegate>();
        Assert.Equal(12, del.Callback(null, "hello"u8));
    }

    [Fact]
    public void Utf8IlEmitterCanLoadInputByteWithOffset()
    {
        using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_Test_LoadInputByteOffset", typeof(int), typeof(Utf8IlEmitterTests), typeof(object), typeof(ReadOnlySpan<byte>), typeof(int));
        var il = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadInputByte(indexLocal, 1);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal((int)(byte)'b', del.Callback(null, "zabc"u8, 1));
    }

    [Fact]
    public void Utf8IlEmitterCanEnsureAvailableAndCompareLiteral()
    {
        using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_Test_EnsureAvailableLiteral", typeof(int), typeof(Utf8IlEmitterTests), typeof(object), typeof(ReadOnlySpan<byte>), typeof(int), typeof(int));
        var il = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();
        var lengthLocal = il.DeclareLocal<int>();
        var enoughLabel = il.DefineLabel();
        var matchLabel = il.DefineLabel();
        var failLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadArg(3);
        il.StoreLocal(lengthLocal);
        il.EmitEnsureAvailable(indexLocal, lengthLocal, 3, enoughLabel, failLabel);

        il.MarkLabel(enoughLabel);
        il.EmitSpanLiteralEquals(indexLocal, "abc"u8, skipPrefixCount: 1, matchLabel, failLabel);

        il.MarkLabel(matchLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(failLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexLengthDelegate>();
        Assert.Equal(1, del.Callback(null, "zabc"u8, 1, 4));
        Assert.Equal(0, del.Callback(null, "zaxc"u8, 1, 4));
        Assert.Equal(0, del.Callback(null, "za"u8, 1, 2));
    }

    [Fact]
    public void Utf8IlEmitterCanDispatchOnByteValues()
    {
        using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_Test_ByteDispatch", typeof(int), typeof(Utf8IlEmitterTests), typeof(object), typeof(ReadOnlySpan<byte>), typeof(int));
        var il = new Utf8IlEmitter(dynamicMethod, s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();
        var valueLocal = il.DeclareLocal<byte>();
        var aLabel = il.DefineLabel();
        var zLabel = il.DefineLabel();
        var defaultLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadInputByte(indexLocal);
        il.StoreLocal(valueLocal);
        il.EmitByteDispatch(valueLocal, [(byte)'a', (byte)'z'], [aLabel, zLabel], defaultLabel);

        il.MarkLabel(aLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(zLabel);
        il.LdcI4(2);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(defaultLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal(1, del.Callback(null, "abc"u8, 0));
        Assert.Equal(2, del.Callback(null, "za"u8, 0));
        Assert.Equal(0, del.Callback(null, "mx"u8, 0));
    }
}
