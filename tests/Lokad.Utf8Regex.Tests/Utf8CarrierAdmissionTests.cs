using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CarrierAdmissionTests
{
    [Theory]
    [InlineData(typeof(Utf8PreparedRegex))]
    [InlineData(typeof(Utf8SliceProgram))]
    [InlineData(typeof(Utf8DynamicIlBuilder))]
    public void ConservativeCarrierAdmissionCoversAnIndependentPaddedFieldBound(Type type)
    {
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var allowance = (ulong)type.GetProperty("ObjectBytes", flags)!.GetValue(null)!;
        var size = typeof(Utf8CarrierAdmissionTests).GetMethod(nameof(ValueSize), flags)!;
        ulong padded = 32;
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var bytes = field.FieldType.IsValueType
                ? (int)size.MakeGenericMethod(field.FieldType).Invoke(null, null)!
                : IntPtr.Size;
            var alignment = (ulong)IntPtr.Size;
            padded += ((ulong)bytes + alignment - 1) & ~(alignment - 1);
        }
        // Pointer-sized padding overestimates the individual fields' alignment
        // in these ordinary managed carriers, without a second field descriptor.
        Assert.True(allowance >= padded, $"{type.Name}: allowance {allowance}, padded field bound {padded}.");
    }

    private static int ValueSize<T>() => Unsafe.SizeOf<T>();
}
