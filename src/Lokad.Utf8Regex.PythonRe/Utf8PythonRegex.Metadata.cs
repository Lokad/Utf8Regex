using System.Buffers;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex
{
    /// <summary>Copies the original pattern as strict UTF-8 into caller-owned storage.</summary>
    /// <param name="destination">Receives the complete original source on success.</param>
    /// <param name="bytesRequired">The complete UTF-8 length on Done or DestinationTooSmall; zero on InvalidData.</param>
    /// <param name="cancellationToken">Observed synchronously at bounded copy/conversion checkpoints.</param>
    /// <returns>Done, DestinationTooSmall, or InvalidData for an unrepresentable legacy UTF-16 source.</returns>
    /// <remarks>No storage is allocated or retained. InvalidData and DestinationTooSmall leave the destination unchanged; cancellation may leave a partial copy. Funded expressions retain program admission during the call and reject calls after close. This explicit metadata operation uses the supplied token independently of matching timeouts and numeric invocation budgets.</remarks>
    public OperationStatus TryCopyPatternUtf8(Span<byte> destination, out int bytesRequired,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = EnterProgramUse();
        return Utf8PatternMetadata.TryCopy(_patternSource is string text ? new Utf8ConstructionPattern(text)
            : new Utf8ConstructionPattern((byte[])_patternSource), destination, out bytesRequired, cancellationToken);
    }
}
