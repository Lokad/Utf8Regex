using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// PLAN.md D09: managed-backend engines, split from the Python facade along the backend ownership boundary.
/// <summary>Managed-backend engines for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    private Utf8PythonValueMatch SearchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Value.Match(subject, startOffsetInUtf16);
        if (!match.Success)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return Utf8PythonValueMatch.Create(input, PythonReGroupData.FromUtf16(0, match.Groups[0], indexMap));
    }

    private int CountManagedMatchesPythonStyle(
        string subject,
        int startOffsetInUtf16)
    {
        var count = 0;
        var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
        while (cursor.TryMoveNext(out _))
        {
            count++;
        }

        return count;
    }

    private static Regex CreateManagedRegex(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        try
        {
            return new Regex(pattern, options, matchTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new PythonRePatternException(ex.Message);
        }
    }

    private Regex CreateManagedNonEmptyAtSamePositionRegex() => CreateManagedRegex(
        @"\G(?:(?:" + _translation.Pattern + @"))(?!\G)",
        _translation.RegexOptions,
        MatchTimeout);

    private Utf8PythonValueMatch FullMatchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        // The `\G` wrapper pins the span while `\z` pins the end; matching the whole subject from the start offset keeps `^` and lookbehind context intact (Python `pos` semantics).
        if (!_lazyManagedFullRegex.Value.IsMatch(subject, startOffsetInUtf16))
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, new PythonReGroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = input.Length,
            StartOffsetInUtf16 = startOffsetInUtf16,
            EndOffsetInUtf16 = subject.Length,
        });
    }

    private Utf8PythonSubnResult SubnToStringViaManagedRegex(
        ReadOnlySpan<byte> input,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInBytes)
    {
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        // PLAN.md D03: bounded requests only need matches up to the cap; the early-stopping
        // allocation-free enumeration replaces the full Count scan. Pinned by TemplateSubnHonorsLimits.
        int CountUpToCap()
        {
            var total = 0;
            foreach (var _ in _managedRegex.Value.EnumerateMatches(subject.AsSpan(startOffsetInUtf16)))
            {
                total++;
                if (total >= count)
                {
                    break;
                }
            }

            return total;
        }

        var maximumReplacements = count == 0 ? int.MaxValue : count;
        // PLAN.md D03: a single requested replacement needs only existence, not a full count; IsMatch explores a prefix of the Count exploration.
        var replacementCount = count == 1 && _managedRegex.Value.IsMatch(subject, startOffsetInUtf16)
            ? 1
            : !_canMatchEmpty && count > 1
                ? CountUpToCap()
                : Math.Min(_managedRegex.Value.Count(subject, startOffsetInUtf16), maximumReplacements);
        return new Utf8PythonSubnResult
        {
            ResultText = ReplaceManagedSubject(subject, plan, count, startOffsetInUtf16),
            ReplacementCount = replacementCount,
        };
    }

    private string ReplaceManagedSubject(
        string subject,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInUtf16)
    {
        var maximumReplacements = count == 0 ? int.MaxValue : count;
        return _managedRegex.Value.Replace(
            subject,
            plan.ToDotNetReplacementString(_managedGroupNumbersByPythonGroup),
            maximumReplacements,
            startOffsetInUtf16);
    }

    private Utf8PythonSubnUtf8Result SubnManagedUtf8<TState>(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        int count,
        Utf8ReplacementBytesFactory<TState> replacementFactory,
        TState state)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = input.IsEmpty
            ? new ArrayBufferWriter<byte>()
            : new ArrayBufferWriter<byte>(input.Length);
        AppendUtf8(builder, input[..startOffsetInBytes]);

        var replaced = 0;
        var lastIndexInBytes = startOffsetInBytes;
            var cursor = new PythonReManagedMatchCursor(subject, _managedRegex, _lazyManagedNonEmptyAtSamePositionRegex, startOffsetInUtf16);
            while (cursor.TryMoveNext(out var match))
            {
                var value = PythonReGroupData.FromUtf16(0, match, indexMap);
                if (count == 0 || replaced < count)
                {
                    AppendUtf8(builder, input[lastIndexInBytes..value.StartOffsetInBytes]);
                    AppendUtf8(builder, replacementFactory(input, match, indexMap, 0, state));
                    lastIndexInBytes = value.EndOffsetInBytes;
                    replaced++;
                }
                else
                {
                    break;
                }
            }

        AppendUtf8(builder, input[lastIndexInBytes..]);
        return new Utf8PythonSubnUtf8Result
        {
            ResultBytes = builder.WrittenSpan.ToArray(),
            ReplacementCount = replaced,
        };
    }
}
