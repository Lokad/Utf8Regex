using System.Buffers;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Explicit metadata projection into caller-owned storage. This owns no cache,
// allocation or lifetime; funded callers hold their existing program-use lease.
internal static class Utf8PatternMetadata
{
    internal static void ValidateName(ReadOnlySpan<byte> name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var position = 0; position < name.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = position + Math.Min(2048, name.Length - position);
            do
            {
                if (System.Text.Rune.DecodeFromUtf8(name[position..], out _, out var consumed) != OperationStatus.Done)
                    throw Utf8Validation.CreateInvalidUtf8Exception(position);
                position += consumed;
            } while (position < end);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static bool NameEquals(ReadOnlySpan<byte> key, ReadOnlySpan<byte> name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (key.Length != name.Length) return false;
        for (var position = 0; position < key.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(2048, key.Length - position);
            if (!key.Slice(position, count).SequenceEqual(name.Slice(position, count))) return false;
            position += count;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    internal static OperationStatus TryCopy(Utf8ConstructionPattern source, Span<byte> destination,
        out int bytesRequired, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bytesRequired = 0;
        if (source.IsUtf8)
        {
            bytesRequired = source.Length;
            if (destination.Length < bytesRequired) return OperationStatus.DestinationTooSmall;
            for (var position = 0; position < source.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(2048, source.Length - position);
                source.Utf8.Slice(position, count).CopyTo(destination.Slice(position, count));
                position += count;
            }
        }
        else
        {
            // Prove strict representability and capacity before changing the
            // destination. Legacy text patterns may contain unpaired surrogates;
            // exporting them must never silently replacement-encode the source.
            for (var position = 0; position < source.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var end = position + Math.Min(2048, source.Length - position);
                do
                {
                    if (source.DecodeScalar(position, out var scalar, out var consumed) != OperationStatus.Done)
                    { bytesRequired = 0; return OperationStatus.InvalidData; }
                    bytesRequired = checked(bytesRequired + scalar.Utf8SequenceLength);
                    position += consumed;
                } while (position < end);
            }
            if (destination.Length < bytesRequired) return OperationStatus.DestinationTooSmall;
            var written = 0;
            for (var position = 0; position < source.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var end = position + Math.Min(2048, source.Length - position);
                do
                {
                    _ = source.DecodeScalar(position, out var scalar, out var consumed);
                    written += scalar.EncodeToUtf8(destination[written..]);
                    position += consumed;
                } while (position < end);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return OperationStatus.Done;
    }
}
