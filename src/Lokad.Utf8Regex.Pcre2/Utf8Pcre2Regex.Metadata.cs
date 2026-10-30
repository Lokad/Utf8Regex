using System.Buffers;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

public sealed partial class Utf8Pcre2Regex
{
    /// <summary>Gets the number of defined capture slots, excluding whole-match group zero.</summary>
    /// <remarks>Branch-reset groups reuse numbers; duplicate names can denote multiple slots. The count is independent of selected execution and match participation.</remarks>
    public int CaptureGroupCount => _program.CaptureGroupCount;

    /// <summary>Copies every capture number assigned to a strict UTF-8 name, preserving duplicate entries.</summary>
    /// <returns>The number copied; isMore reports additional numbers that did not fit.</returns>
    /// <remarks>Validates the complete name before writing, compares ordinal bytes without normalization, and allocates no key string. The destination belongs to the caller. This explicit metadata lookup observes the supplied token independently of matching limits; cancellation can leave a partial copy.</remarks>
    public int CopyNumbersForUtf8Name(ReadOnlySpan<byte> nameUtf8, Span<int> destination, out bool isMore,
        CancellationToken cancellationToken = default)
    {
        Utf8PatternMetadata.ValidateName(nameUtf8, cancellationToken);
        var count = 0;
        for (var index = 0; index < NameEntries.Length; index++)
        {
            var key = _program.NameEntriesUtf8[index];
            if (key.Length == 0 || !Utf8PatternMetadata.NameEquals(key, nameUtf8, cancellationToken)) continue;
            if (count < destination.Length) destination[count] = NameEntries[index].Number;
            count++;
        }
        cancellationToken.ThrowIfCancellationRequested();
        isMore = count > destination.Length;
        return Math.Min(count, destination.Length);
    }

    /// <summary>Copies the original pattern as strict UTF-8 into caller-owned storage.</summary>
    /// <param name="destination">Receives the complete original source on success.</param>
    /// <param name="bytesRequired">The complete UTF-8 length on Done or DestinationTooSmall; zero on InvalidData.</param>
    /// <param name="cancellationToken">Observed synchronously at bounded copy/conversion checkpoints.</param>
    /// <returns>Done, DestinationTooSmall, or InvalidData for unrepresentable UTF-16 source.</returns>
    /// <remarks>No storage is allocated or retained. InvalidData and DestinationTooSmall leave the destination unchanged; cancellation may leave a partial copy. This explicit metadata operation uses the supplied token, independently of matching timeouts and numeric invocation budgets.</remarks>
    public OperationStatus TryCopyPatternUtf8(Span<byte> destination, out int bytesRequired,
        CancellationToken cancellationToken = default)
        => Utf8PatternMetadata.TryCopy(_program.Request.ConstructionPattern, destination, out bytesRequired, cancellationToken);
}
