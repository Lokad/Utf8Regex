using System.Buffers;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex
{
    /// <summary>Finds a named capture number from a strict UTF-8 name without allocating a key string.</summary>
    /// <param name="nameUtf8">The complete strict UTF-8 name, compared ordinally without normalization.</param>
    /// <param name="number">The validated capture number on success, or zero when undefined.</param>
    /// <param name="cancellationToken">Observed during validation and bounded lookup work.</param>
    /// <remarks>Numeric strings are not named captures. Funded expressions retain program admission during access and reject calls after close. This explicit metadata operation is independent of matching timeouts/numeric limits.</remarks>
    public bool TryGetGroupNumber(ReadOnlySpan<byte> nameUtf8, out int number,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = EnterProgramUse();
        Utf8PatternMetadata.ValidateName(nameUtf8, cancellationToken);
        number = 0;
        for (var index = 0; index < _nameEntriesUtf8.Length; index++)
            if (Utf8PatternMetadata.NameEquals(_nameEntriesUtf8[index], nameUtf8, cancellationToken))
            { number = _nameEntries[index].Number; return true; }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

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
