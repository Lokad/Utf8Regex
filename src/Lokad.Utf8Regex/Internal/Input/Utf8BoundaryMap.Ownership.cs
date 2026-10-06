using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

internal readonly partial struct Utf8BoundaryMap
{
    private readonly Utf8StorageLease? _storageLease;

    private Utf8BoundaryMap(Utf8BoundaryMap source, Utf8StorageLease? lease)
    {
        this = source;
        _storageLease = lease;
    }

    internal Utf8StorageLease? StorageLease => _storageLease;

    internal Utf8BoundaryMap WithStorageLease(Utf8StorageLease? lease) => new(this, lease);

    internal void KeepStorageAlive() => GC.KeepAlive(_storageLease);

    public Utf16Boundary Resolve(int utf16Offset)
    {
        var result = ResolveCore(utf16Offset);
        KeepStorageAlive();
        return result;
    }

    public int GetUtf16OffsetForByteOffset(int byteOffset)
    {
        var result = GetUtf16OffsetForByteOffsetCore(byteOffset);
        KeepStorageAlive();
        return result;
    }

    public bool TryGetUtf16OffsetForByteOffset(int byteOffset, out int utf16Offset)
    {
        var result = TryGetUtf16OffsetForByteOffsetCore(byteOffset, out utf16Offset);
        KeepStorageAlive();
        return result;
    }
}
