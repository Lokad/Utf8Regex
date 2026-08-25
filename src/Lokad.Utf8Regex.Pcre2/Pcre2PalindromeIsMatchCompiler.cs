using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2PalindromeIsMatchDirectProgram : IPcre2DirectProgram
{
    internal Pcre2PalindromeIsMatchDirectProgram(
        bool allowsEmpty,
        Pcre2BacktrackingProgram fallback)
    {
        AllowsEmpty = allowsEmpty;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2PalindromeIsMatch;

    internal bool AllowsEmpty { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2PalindromeIsMatchAnalyzer
{
    internal static Pcre2PalindromeIsMatchDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            request.Settings.Newline != Pcre2NewlineConvention.Default ||
            root is not Pcre2SequenceBacktrackingNode
            {
                Children:
                [
                    Pcre2TokenBacktrackingNode { Token: var beginning },
                    Pcre2CaptureBacktrackingNode { Slot: 1, Body: var body },
                    Pcre2TokenBacktrackingNode { Token: var end },
                ],
            } ||
            beginning.Kind != Pcre2CharacterTokenKind.BeginningOfLine ||
            beginning.Options != Pcre2CharacterOptions.None ||
            end.Kind != Pcre2CharacterTokenKind.EndOfLine ||
            end.Options != Pcre2CharacterOptions.None)
        {
            return null;
        }

        if (IsRequiredRecursionWithOptionalBase(body))
        {
            return new Pcre2PalindromeIsMatchDirectProgram(allowsEmpty: true, fallback);
        }

        return IsOptionalRecursionWithRequiredBase(body)
            ? new Pcre2PalindromeIsMatchDirectProgram(allowsEmpty: false, fallback)
            : null;

        static bool IsRequiredRecursionWithOptionalBase(IPcre2BacktrackingNode candidate)
        {
            return candidate is Pcre2AlternationBacktrackingNode
                {
                    Alternatives:
                    [
                        Pcre2SequenceBacktrackingNode
                        {
                            Children:
                            [
                                Pcre2CaptureBacktrackingNode { Slot: 2, Body: var captured },
                                Pcre2SubroutineCallBacktrackingNode { Target: var callTarget },
                                Pcre2BackreferenceBacktrackingNode { Target: var referenceTarget },
                            ],
                        },
                        Pcre2RepeatBacktrackingNode
                        {
                            Body: var optionalBody,
                            Minimum: 0,
                            Maximum: 1,
                            Preference: Pcre2RepeatPreference.Greedy,
                        },
                    ],
                } &&
                IsDot(captured) &&
                IsDot(optionalBody) &&
                IsAbsoluteTarget(callTarget, 1) &&
                IsAbsoluteTarget(referenceTarget, 2);
        }

        static bool IsOptionalRecursionWithRequiredBase(IPcre2BacktrackingNode candidate)
        {
            return candidate is Pcre2AlternationBacktrackingNode
                {
                    Alternatives:
                    [
                        var requiredBase,
                        Pcre2SequenceBacktrackingNode
                        {
                            Children:
                            [
                                Pcre2CaptureBacktrackingNode { Slot: 2, Body: var captured },
                                Pcre2RepeatBacktrackingNode
                                {
                                    Body: Pcre2SubroutineCallBacktrackingNode { Target: var callTarget },
                                    Minimum: 0,
                                    Maximum: 1,
                                    Preference: Pcre2RepeatPreference.Greedy,
                                },
                                Pcre2BackreferenceBacktrackingNode { Target: var referenceTarget },
                            ],
                        },
                    ],
                } &&
                IsDot(requiredBase) &&
                IsDot(captured) &&
                IsAbsoluteTarget(callTarget, 1) &&
                IsAbsoluteTarget(referenceTarget, 2);
        }

        static bool IsDot(IPcre2BacktrackingNode node) =>
            node is Pcre2TokenBacktrackingNode
            {
                Token:
                {
                    Kind: Pcre2CharacterTokenKind.Any,
                    Options: Pcre2CharacterOptions.None,
                },
            };

        static bool IsAbsoluteTarget(Pcre2BackreferenceTarget target, int number) =>
            target.Kind == Pcre2BackreferenceTargetKind.Absolute && target.Number == number;
    }
}

internal static class Pcre2PalindromeIsMatchRunner
{
    internal static bool IsMatch(
        Pcre2PalindromeIsMatchDirectProgram program,
        ReadOnlySpan<byte> input)
    {
        // Both recognized recursive languages are scalar palindromes. Under
        // default newline rules, the end anchor may also precede one final LF.
        var end = input.Length;
        if (end != 0 && input[end - 1] == (byte)'\n')
        {
            end--;
        }

        if (end == 0)
        {
            return program.AllowsEmpty;
        }

        var left = 0;
        var right = end - 1;
        while (left < right)
        {
            var leftByte = input[left];
            var rightByte = input[right];
            if (leftByte >= 0x80 || rightByte >= 0x80)
            {
                return IsUnicodePalindrome(input[..end]);
            }

            if (leftByte == (byte)'\n' || rightByte == (byte)'\n' || leftByte != rightByte)
            {
                return false;
            }

            left++;
            right--;
        }

        return input[left] != (byte)'\n';

        static bool IsUnicodePalindrome(ReadOnlySpan<byte> value)
        {
            var left = 0;
            var right = value.Length;
            while (left < right)
            {
                if (Rune.DecodeFromUtf8(value[left..right], out var leftScalar, out var leftWidth) != OperationStatus.Done ||
                    Rune.DecodeLastFromUtf8(value[left..right], out var rightScalar, out var rightWidth) != OperationStatus.Done ||
                    leftScalar.Value == '\n' ||
                    rightScalar.Value == '\n' ||
                    leftScalar != rightScalar)
                {
                    return false;
                }

                left += leftWidth;
                right -= rightWidth;
            }

            return true;
        }
    }
}
