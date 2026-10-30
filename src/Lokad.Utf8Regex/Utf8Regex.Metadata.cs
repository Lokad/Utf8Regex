using System.Buffers;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    /// <summary>Gets the number of defined capture slots, excluding whole-match group zero.</summary>
    /// <remarks>Sparse numbers and repeated declarations of a shared slot do not add slots. Match participation does not affect this count.</remarks>
    public int CaptureGroupCount => _program.CaptureCatalog.GroupNumbers.Length - 1;

    /// <summary>Copies the original pattern as strict UTF-8 into caller-owned storage.</summary>
    /// <param name="destination">Receives the complete original source on success.</param>
    /// <param name="bytesRequired">The complete UTF-8 length on Done or DestinationTooSmall; zero on InvalidData.</param>
    /// <param name="cancellationToken">Observed synchronously at bounded copy/conversion checkpoints.</param>
    /// <returns>Done, DestinationTooSmall, or InvalidData for an unrepresentable legacy UTF-16 source.</returns>
    /// <remarks>No storage is allocated or retained. InvalidData and DestinationTooSmall leave the destination unchanged; cancellation may leave a partial copy. This explicit metadata operation uses the supplied token, independently of matching timeouts and numeric invocation budgets.</remarks>
    public OperationStatus TryCopyPatternUtf8(Span<byte> destination, out int bytesRequired,
        CancellationToken cancellationToken = default)
        => Utf8PatternMetadata.TryCopy(_patternText is not null ? new Utf8ConstructionPattern(_patternText)
            : new Utf8ConstructionPattern(_patternUtf8!), destination, out bytesRequired, cancellationToken);
}
