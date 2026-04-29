using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

public sealed class Utf8PythonRegex
{
    private delegate byte[] Utf8ReplacementBytesFactory<TState>(ReadOnlySpan<byte> source, PythonReManagedMatchSnapshot snapshot, TState state);
    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Utf8Regex? _utf8Regex;
    private readonly Utf8Regex? _utf8FullRegex;
    private readonly Regex _managedRegex;
    private readonly Regex _managedFullRegex;
    private readonly PythonReTranslation _translation;
    private readonly PythonReNameEntry[] _nameEntries;
    private readonly IReadOnlyDictionary<string, int> _namedGroups;
    private readonly bool _canMatchEmpty;
    private readonly bool _canUseUtf8IterationFastPath;
    private readonly bool _canUseUtf8ReplacementFastPath;
    private readonly PythonReDirectBackendKind _searchBackend;
    private readonly PythonReDirectBackendKind _matchBackend;
    private readonly PythonReDirectBackendKind _fullMatchBackend;
    private readonly PythonReDirectBackendKind _countBackend;
    private readonly PythonReDirectBackendKind _findAllBackend;
    private readonly PythonReDirectBackendKind _replaceBackend;
    private readonly PythonReDirectBackendKind _splitBackend;

    public Utf8PythonRegex(string pattern)
        : this(pattern, PythonReCompileOptions.None)
    {
    }

    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8)
        : this(patternUtf8, PythonReCompileOptions.None)
    {
    }

    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options, TimeSpan matchTimeout = default)
        : this(Encoding.UTF8.GetString(patternUtf8), options, matchTimeout)
    {
    }

    public Utf8PythonRegex(string pattern, PythonReCompileOptions options, TimeSpan matchTimeout = default)
    {
        PythonReCompileValidator.Validate(pattern, options);
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Options = options;
        MatchTimeout = matchTimeout == default ? DefaultMatchTimeout : matchTimeout;

        var parser = new PythonReParser(pattern);
        var parseResult = parser.Parse(options);
        _canMatchEmpty = PythonReTranslator.CanMatchEmpty(parseResult.Root);
        _canUseUtf8IterationFastPath = PythonReTranslator.CanUseUtf8IterationFastPath(parseResult.Root);
        _canUseUtf8ReplacementFastPath = PythonReTranslator.CanUseUtf8ReplacementFastPath(parseResult.Root);
        _translation = PythonReTranslator.Translate(parseResult);
        _managedRegex = CreateManagedRegex(_translation.Pattern, _translation.RegexOptions, MatchTimeout);
        _managedFullRegex = CreateManagedRegex(@"\A(?:" + _translation.Pattern + @")\z", _translation.RegexOptions, MatchTimeout);
        _groupNames = GetPublicGroupNames(_managedRegex, _translation.EmittedGroupNames);
        _nameEntries = GetManagedNameEntries(_managedRegex, _translation.EmittedGroupNames);
        _namedGroups = _nameEntries.ToDictionary(x => x.Name, x => x.Number, StringComparer.Ordinal);

        try
        {
            _utf8Regex = new Utf8Regex(_translation.Pattern, _translation.RegexOptions, MatchTimeout);
            _utf8FullRegex = new Utf8Regex(@"\A(?:" + _translation.Pattern + @")\z", _translation.RegexOptions, MatchTimeout);
        }
        catch (Exception)
        {
            // Fall back to managed Regex if the translated pattern cannot be executed by Utf8Regex.
        }

        _searchBackend = _utf8Regex is not null ? PythonReDirectBackendKind.Utf8Regex : PythonReDirectBackendKind.ManagedRegex;
        _matchBackend = _utf8Regex is not null ? PythonReDirectBackendKind.Utf8Regex : PythonReDirectBackendKind.ManagedRegex;
        _fullMatchBackend = _utf8FullRegex is not null ? PythonReDirectBackendKind.Utf8Regex : PythonReDirectBackendKind.ManagedRegex;
        _countBackend = _utf8Regex is not null && !_canMatchEmpty
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
        _findAllBackend = _utf8Regex is not null && !_canMatchEmpty && _canUseUtf8IterationFastPath
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
        _replaceBackend = _utf8Regex is not null && !_canMatchEmpty && _canUseUtf8ReplacementFastPath
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
        _splitBackend = _utf8Regex is not null && !_canMatchEmpty && _canUseUtf8IterationFastPath
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
    }

    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = value;
    }

    private readonly string[] _groupNames;

    public string Pattern { get; }

    public PythonReCompileOptions Options { get; }

    public TimeSpan MatchTimeout { get; }

    public string[] GetGroupNames() => _groupNames;

    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        return Search(input, startOffsetInBytes).Success;
    }

    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            return Utf8PythonValueMatchFromUtf8Regex(input, _utf8Regex.MatchFromUtf16Offset(input, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes)));
        }

        return SearchViaManagedRegex(input, startOffsetInBytes);
    }

    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var match = _utf8Regex.MatchFromUtf16Offset(input, startOffsetInUtf16);
            if (!match.Success || match.IndexInUtf16 != startOffsetInUtf16)
            {
                return default;
            }

            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        var context = MatchDetailed(input, startOffsetInBytes);
        return context.Value;
    }

    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8FullRegex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var tail = input[startOffsetInBytes..];
            var match = _utf8FullRegex.Match(tail);
            if (!match.Success)
            {
                return default;
            }

            var utf16BaseOffset = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return Utf8PythonValueMatchFromUtf8Regex(input, match, startOffsetInBytes, utf16BaseOffset);
        }

        var context = FullMatchDetailed(input, startOffsetInBytes);
        return context.Value;
    }

    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_countBackend == PythonReDirectBackendKind.Utf8Regex && _utf8Regex is not null && startOffsetInBytes == 0)
        {
            return _utf8Regex.Count(input);
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        return CountManagedMatchesPythonStyle(input, subject, startOffsetInUtf16);
    }

    public Utf8PythonMatchContext SearchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        return CreateMatchContext(input, match);
    }

    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            if (!utf8Match.Success || utf8Match.IndexInUtf16 != utf8StartOffsetInUtf16)
            {
                return default;
            }

            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        return CreateMatchContext(input, match);
    }

    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8FullRegex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8Tail = input[startOffsetInBytes..];
            var utf8Match = _utf8FullRegex.MatchDetailed(utf8Tail);
            if (!utf8Match.Success)
            {
                return default;
            }

            var utf16BaseOffset = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return CreateMatchContextFromUtf8(input, utf8Match, startOffsetInBytes, utf16BaseOffset);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var tail = subject[startOffsetInUtf16..];
        var match = _managedFullRegex.Match(tail);
        if (!match.Success)
        {
            return default;
        }

        return CreateMatchContext(input, match, startOffsetInUtf16);
    }

    public string? SearchToString(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        var match = Search(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    public string? MatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        var match = Match(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    public string? FullMatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        var match = FullMatch(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        return CreateDetailedMatchData(input, match);
    }

    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            if (!utf8Match.Success || utf8Match.IndexInUtf16 != utf8StartOffsetInUtf16)
            {
                return default;
            }

            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        return CreateDetailedMatchData(input, match);
    }

    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        if (_utf8FullRegex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8Tail = input[startOffsetInBytes..];
            var utf8Match = _utf8FullRegex.MatchDetailed(utf8Tail);
            if (!utf8Match.Success)
            {
                return default;
            }

            var utf16BaseOffset = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return CreateDetailedMatchDataFromUtf8(input, utf8Match, startOffsetInBytes, utf16BaseOffset);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var tail = subject[startOffsetInUtf16..];
        var match = _managedFullRegex.Match(tail);
        return match.Success ? CreateDetailedMatchData(input, match, startOffsetInUtf16) : default;
    }

    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_findAllBackend == PythonReDirectBackendKind.Utf8Regex &&
            TryFindAllViaUtf8Regex(input, startOffsetInBytes, out var utf8Matches))
        {
            return utf8Matches;
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var matches = new List<Utf8PythonMatchData>();
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var snapshot = CreateMatchSnapshot(input, match);
            var context = snapshot.ToContext(input, _nameEntries);
            var value = context.Value;
            matches.Add(new Utf8PythonMatchData
            {
                Success = true,
                StartOffsetInBytes = value.StartOffsetInBytes,
                EndOffsetInBytes = value.EndOffsetInBytes,
                StartOffsetInUtf16 = value.StartOffsetInUtf16,
                EndOffsetInUtf16 = value.EndOffsetInUtf16,
                ValueText = value.GetValueString(),
            });

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
            {
                var nonEmptyValue = nonEmptySnapshot.ToContext(input, _nameEntries).Value;
                matches.Add(new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = nonEmptyValue.StartOffsetInBytes,
                    EndOffsetInBytes = nonEmptyValue.EndOffsetInBytes,
                    StartOffsetInUtf16 = nonEmptyValue.StartOffsetInUtf16,
                    EndOffsetInUtf16 = nonEmptyValue.EndOffsetInUtf16,
                    ValueText = nonEmptyValue.GetValueString(),
                });
                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        return matches.ToArray();
    }

    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 0)
        {
            var matches = FindAll(input, startOffsetInBytes);
            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.FullMatch,
                ScalarValues = matches.Select(x => x.ValueText).ToArray(),
                TupleValues = [],
            };
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 1)
        {
            List<string> collected = [];
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                AppendFindAllScalarValue(collected, CreateMatchSnapshot(input, match).ToContext(input, _nameEntries), 1);

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
                {
                    AppendFindAllScalarValue(collected, nonEmptySnapshot.ToContext(input, _nameEntries), 1);
                    searchIndex = nonEmptySnapshot.Groups[0].EndOffsetInUtf16;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = match.Index + 1;
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = collected.ToArray(),
                TupleValues = [],
            };
        }

        List<string[]> tuples = [];
        {
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                AppendFindAllTupleValue(tuples, CreateMatchSnapshot(input, match).ToContext(input, _nameEntries));

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
                {
                    AppendFindAllTupleValue(tuples, nonEmptySnapshot.ToContext(input, _nameEntries));
                    searchIndex = nonEmptySnapshot.Groups[0].EndOffsetInUtf16;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = match.Index + 1;
            }
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples.ToArray(),
        };
    }

    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 0)
        {
            var matches = FindAll(input, startOffsetInBytes);
            var values = new byte[matches.Length][];
            for (var i = 0; i < matches.Length; i++)
            {
                values[i] = input[matches[i].StartOffsetInBytes..matches[i].EndOffsetInBytes].ToArray();
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.FullMatch,
                ScalarValues = values,
                TupleValues = [],
            };
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 1)
        {
            List<byte[]> collected = [];
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                AppendFindAllScalarBytes(collected, input, CreateMatchSnapshot(input, match), 1);

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
                {
                    AppendFindAllScalarBytes(collected, input, nonEmptySnapshot, 1);
                    searchIndex = nonEmptySnapshot.Groups[0].EndOffsetInUtf16;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = match.Index + 1;
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = collected.ToArray(),
                TupleValues = [],
            };
        }

        List<byte[][]> tuples = [];
        {
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                AppendFindAllTupleBytes(tuples, input, CreateMatchSnapshot(input, match));

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
                {
                    AppendFindAllTupleBytes(tuples, input, nonEmptySnapshot);
                    searchIndex = nonEmptySnapshot.Groups[0].EndOffsetInUtf16;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = match.Index + 1;
            }
        }

        return new Utf8PythonFindAllUtf8Result
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples.ToArray(),
        };
    }

    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        List<Utf8PythonDetailedMatchData> matches = [];
        var searchOffset = startOffsetInBytes;
        while (searchOffset <= input.Length)
        {
            var match = SearchDetailedData(input, searchOffset);
            if (!match.Success)
            {
                break;
            }

            matches.Add(match);

            var start = match.Value.StartOffsetInBytes;
            var end = match.Value.EndOffsetInBytes;
            if (end > start)
            {
                searchOffset = end;
                continue;
            }

            if (start >= input.Length)
            {
                break;
            }

            searchOffset = start + GetUtf8RuneLength(input[start]);
        }

        return matches.ToArray();
    }

    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count = 0, int startOffsetInBytes = 0)
    {
        return Encoding.UTF8.GetBytes(SubnToString(input, replacement, count, startOffsetInBytes).ResultText);
    }

    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count = 0, int startOffsetInBytes = 0)
    {
        return SubnToString(input, replacement, count, startOffsetInBytes).ResultText;
    }

    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement, int count = 0, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var plan = PythonReReplacementParser.Parse(replacement, _translation.CaptureGroupCount, _namedGroups);
        if (_replaceBackend == PythonReDirectBackendKind.Utf8Regex &&
            count == 0 &&
            TrySubnToStringViaUtf8Regex(input, plan, startOffsetInBytes, out var utf8Result))
        {
            return utf8Result;
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = new StringBuilder();
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));

        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var snapshot = CreateMatchSnapshot(input, match);
            var context = snapshot.ToContext(input, _nameEntries);
            var value = context.Value;
            if (count == 0 || replaced < count)
            {
                builder.Append(subject.AsSpan(lastIndex, value.StartOffsetInUtf16 - lastIndex));
                builder.Append(plan.Expand(context));
                lastIndex = value.EndOffsetInUtf16;
                replaced++;
            }
            else
            {
                break;
            }

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
            {
                var nonEmptyContext = nonEmptySnapshot.ToContext(input, _nameEntries);
                var nonEmptyValue = nonEmptyContext.Value;
                if (count == 0 || replaced < count)
                {
                    builder.Append(subject.AsSpan(lastIndex, nonEmptyValue.StartOffsetInUtf16 - lastIndex));
                    builder.Append(plan.Expand(nonEmptyContext));
                    lastIndex = nonEmptyValue.EndOffsetInUtf16;
                    replaced++;
                }

                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        builder.Append(subject.AsSpan(lastIndex));
        return new Utf8PythonSubnResult
        {
            ResultText = builder.ToString(),
            ReplacementCount = replaced,
        };
    }

    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement, int count = 0, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var plan = PythonReReplacementParser.Parse(replacement, _translation.CaptureGroupCount, _namedGroups);
        if (_replaceBackend == PythonReDirectBackendKind.Utf8Regex &&
            count == 0 &&
            TrySubnViaUtf8Regex(input, plan, startOffsetInBytes, out var utf8Result))
        {
            return utf8Result;
        }

        return SubnManagedUtf8(input, startOffsetInBytes, count, static (source, snapshot, state) => state.ExpandToUtf8(source, snapshot.Groups), plan);
    }

    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count = 0, int startOffsetInBytes = 0)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count = 0, int startOffsetInBytes = 0)
    {
        return SubnToString(input, state, evaluator, count, startOffsetInBytes).ResultText;
    }

    public Utf8PythonSubnResult SubnToString<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count = 0, int startOffsetInBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ValidateStartOffset(input, startOffsetInBytes);

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = new StringBuilder();
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));

        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var snapshot = CreateMatchSnapshot(input, match);
            var detailed = snapshot.ToDetailedData(input, _nameEntries);
            var value = detailed.Value;
            if (count == 0 || replaced < count)
            {
                builder.Append(subject.AsSpan(lastIndex, value.StartOffsetInUtf16 - lastIndex));
                builder.Append(evaluator(state, detailed));
                lastIndex = value.EndOffsetInUtf16;
                replaced++;
            }
            else
            {
                break;
            }

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
            {
                var nonEmptyDetailed = nonEmptySnapshot.ToDetailedData(input, _nameEntries);
                var nonEmptyValue = nonEmptyDetailed.Value;
                if (count == 0 || replaced < count)
                {
                    builder.Append(subject.AsSpan(lastIndex, nonEmptyValue.StartOffsetInUtf16 - lastIndex));
                    builder.Append(evaluator(state, nonEmptyDetailed));
                    lastIndex = nonEmptyValue.EndOffsetInUtf16;
                    replaced++;
                }

                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        builder.Append(subject.AsSpan(lastIndex));
        return new Utf8PythonSubnResult
        {
            ResultText = builder.ToString(),
            ReplacementCount = replaced,
        };
    }

    public Utf8PythonSubnUtf8Result Subn<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonMatchEvaluator<TState> evaluator, int count = 0, int startOffsetInBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            static (source, snapshot, state) => Encoding.UTF8.GetBytes(state.Evaluator(state.Value, snapshot.ToDetailedData(source, state.NameEntries))),
            (Value: state, Evaluator: evaluator, NameEntries: _nameEntries));
    }

    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonUtf8MatchEvaluator<TState> evaluator, int count = 0, int startOffsetInBytes = 0)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    public Utf8PythonSubnUtf8Result Subn<TState>(ReadOnlySpan<byte> input, TState state, Utf8PythonUtf8MatchEvaluator<TState> evaluator, int count = 0, int startOffsetInBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            static (source, snapshot, state) => state.Evaluator(state.Value, snapshot.ToDetailedData(source, state.NameEntries)),
            (Value: state, Evaluator: evaluator, NameEntries: _nameEntries));
    }

    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit = 0, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_splitBackend == PythonReDirectBackendKind.Utf8Regex &&
            TrySplitToStringsViaUtf8Regex(input, maxSplit, startOffsetInBytes, out var utf8Parts))
        {
            return utf8Parts;
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<string?>();
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        var splitCount = 0;

        while (searchIndex <= subject.Length && (maxSplit == 0 || splitCount < maxSplit))
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            AppendSplitMatch(parts, subject, match, ref lastIndex);
            splitCount++;

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if ((maxSplit == 0 || splitCount < maxSplit) &&
                TryCreateNonEmptySamePositionManagedMatch(subject, match.Index, out var nonEmptyMatch, out var nonEmptyUtf16BaseOffset))
            {
                AppendSplitMatch(parts, subject, nonEmptyMatch, ref lastIndex, nonEmptyUtf16BaseOffset);
                splitCount++;
                searchIndex = nonEmptyUtf16BaseOffset + nonEmptyMatch.Index + nonEmptyMatch.Length;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        parts.Add(subject[lastIndex..]);
        return parts.ToArray();
    }

    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit = 0, int startOffsetInBytes = 0)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<Utf8PythonSplitItem>();
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        var splitCount = 0;

        while (searchIndex <= subject.Length && (maxSplit == 0 || splitCount < maxSplit))
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            AppendSplitDetailedMatch(parts, subject, match, ref lastIndex);
            splitCount++;

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if ((maxSplit == 0 || splitCount < maxSplit) &&
                TryCreateNonEmptySamePositionManagedMatch(subject, match.Index, out var nonEmptyMatch, out var nonEmptyUtf16BaseOffset))
            {
                AppendSplitDetailedMatch(parts, subject, nonEmptyMatch, ref lastIndex, nonEmptyUtf16BaseOffset);
                splitCount++;
                searchIndex = nonEmptyUtf16BaseOffset + nonEmptyMatch.Index + nonEmptyMatch.Length;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        return parts.ToArray();
    }

    private Utf8PythonValueMatch SearchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success)
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, match);
    }

    private int CountManagedMatchesPythonStyle(
        ReadOnlySpan<byte> input,
        string subject,
        int startOffsetInUtf16)
    {
        var count = 0;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            count++;

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptyAtSamePosition))
            {
                count++;
                searchIndex = nonEmptyAtSamePosition.Groups[0].EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        return count;
    }

    private bool TryCreateNonEmptySamePositionMatchSnapshot(
        ReadOnlySpan<byte> input,
        string subject,
        int utf16Position,
        out PythonReManagedMatchSnapshot snapshot)
    {
        if ((uint)utf16Position >= (uint)subject.Length)
        {
            snapshot = default;
            return false;
        }

        foreach (var probe in EnumerateSamePositionNonEmptyProbes(subject, utf16Position))
        {
            var match = _managedRegex.Match(probe.Text, probe.StartIndex);
            if (!match.Success || match.Index != probe.StartIndex || match.Length == 0)
            {
                continue;
            }

            snapshot = CreateMatchSnapshot(input, match, probe.Utf16BaseOffset);
            return true;
        }

        snapshot = default;
        return false;
    }

    private bool TryCreateNonEmptySamePositionManagedMatch(
        string subject,
        int utf16Position,
        out Match match,
        out int utf16BaseOffset)
    {
        if ((uint)utf16Position >= (uint)subject.Length)
        {
            match = System.Text.RegularExpressions.Match.Empty;
            utf16BaseOffset = 0;
            return false;
        }

        foreach (var probe in EnumerateSamePositionNonEmptyProbes(subject, utf16Position))
        {
            var candidate = _managedRegex.Match(probe.Text, probe.StartIndex);
            if (!candidate.Success || candidate.Index != probe.StartIndex || candidate.Length == 0)
            {
                continue;
            }

            match = candidate;
            utf16BaseOffset = probe.Utf16BaseOffset;
            return true;
        }

        match = System.Text.RegularExpressions.Match.Empty;
        utf16BaseOffset = 0;
        return false;
    }

    private static IEnumerable<PythonReSamePositionProbe> EnumerateSamePositionNonEmptyProbes(string subject, int utf16Position)
    {
        var tail = subject[utf16Position..];
        yield return new PythonReSamePositionProbe(tail, 0, utf16Position);
        yield return new PythonReSamePositionProbe("A" + tail, 1, utf16Position - 1);
        yield return new PythonReSamePositionProbe(" " + tail, 1, utf16Position - 1);
        yield return new PythonReSamePositionProbe("\n" + tail, 1, utf16Position - 1);
    }

    internal bool DebugUsesUtf8RegexBackend => _utf8Regex is not null;

    internal string DebugTranslatedPattern => _translation.Pattern;

    internal string DebugDescribeExecutionPlan()
        => $"Search={_searchBackend}, Match={_matchBackend}, FullMatch={_fullMatchBackend}, Count={_countBackend}";

    internal PythonReDirectBackendKind DebugFindAllBackend => _findAllBackend;

    internal PythonReDirectBackendKind DebugReplaceBackend => _replaceBackend;

    internal PythonReDirectBackendKind DebugSplitBackend => _splitBackend;

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

    private static string Decode(ReadOnlySpan<byte> input)
    {
        try
        {
            return Encoding.UTF8.GetString(input);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidOperationException("The input must be valid UTF-8.", ex);
        }
    }

    private static int GetUtf16OffsetOfBytePrefix(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
    }

    private static void ValidateStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if ((uint)startOffsetInBytes > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
        }

        _ = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
    }

    private bool TryFindAllViaUtf8Regex(ReadOnlySpan<byte> input, int startOffsetInBytes, out Utf8PythonMatchData[] matches)
    {
        if (_utf8Regex is null)
        {
            matches = [];
            return false;
        }

        List<Utf8PythonMatchData> collected = [];
        var enumerator = startOffsetInBytes == 0
            ? _utf8Regex.EnumerateMatches(input)
            : _utf8Regex.EnumerateMatchesFromUtf16Offset(input, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));

        foreach (var match in enumerator)
        {
            if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
            {
                matches = [];
                return false;
            }

            collected.Add(new Utf8PythonMatchData
            {
                Success = true,
                StartOffsetInBytes = indexInBytes,
                EndOffsetInBytes = indexInBytes + lengthInBytes,
                StartOffsetInUtf16 = match.IndexInUtf16,
                EndOffsetInUtf16 = match.IndexInUtf16 + match.LengthInUtf16,
                ValueText = Encoding.UTF8.GetString(input.Slice(indexInBytes, lengthInBytes)),
            });
        }

        matches = collected.ToArray();
        return true;
    }

    private bool TrySubnToStringViaUtf8Regex(
        ReadOnlySpan<byte> input,
        PythonReReplacementPlan plan,
        int startOffsetInBytes,
        out Utf8PythonSubnResult result)
    {
        if (_utf8Regex is null)
        {
            result = default;
            return false;
        }

        var tail = input[startOffsetInBytes..];
        var replacedTail = _utf8Regex.ReplaceToString(tail, plan.ToDotNetReplacementString());
        var replacementCount = _utf8Regex.Count(tail);
        result = new Utf8PythonSubnResult
        {
            ResultText = startOffsetInBytes == 0
                ? replacedTail
                : Encoding.UTF8.GetString(input[..startOffsetInBytes]) + replacedTail,
            ReplacementCount = replacementCount,
        };
        return true;
    }

    private bool TrySubnViaUtf8Regex(
        ReadOnlySpan<byte> input,
        PythonReReplacementPlan plan,
        int startOffsetInBytes,
        out Utf8PythonSubnUtf8Result result)
    {
        if (_utf8Regex is null)
        {
            result = default;
            return false;
        }

        var tail = input[startOffsetInBytes..];
        var replacedTail = _utf8Regex.Replace(tail, plan.ToDotNetReplacementString());
        var replaced = new byte[startOffsetInBytes + replacedTail.Length];
        input[..startOffsetInBytes].CopyTo(replaced);
        replacedTail.CopyTo(replaced.AsSpan(startOffsetInBytes));
        result = new Utf8PythonSubnUtf8Result
        {
            ResultBytes = replaced,
            ReplacementCount = _utf8Regex.Count(tail),
        };
        return true;
    }

    private bool TrySplitToStringsViaUtf8Regex(
        ReadOnlySpan<byte> input,
        int maxSplit,
        int startOffsetInBytes,
        out string?[] parts)
    {
        if (_utf8Regex is null)
        {
            parts = [];
            return false;
        }

        var tail = input[startOffsetInBytes..];
        List<string?> collected = [];
        var lastIndexInBytes = 0;
        var splitCount = 0;

        foreach (var match in _utf8Regex.EnumerateMatches(tail))
        {
            if (maxSplit != 0 && splitCount >= maxSplit)
            {
                break;
            }

            if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
            {
                parts = [];
                return false;
            }

            collected.Add(Encoding.UTF8.GetString(tail.Slice(lastIndexInBytes, indexInBytes - lastIndexInBytes)));
            if (_translation.CaptureGroupCount > 0)
            {
                var detailed = _utf8Regex.MatchDetailedFromUtf16Offset(tail, match.IndexInUtf16);
                for (var i = 1; i < detailed.GroupCount; i++)
                {
                    var group = detailed.GetGroup(i);
                    collected.Add(group.Success ? group.GetValueString() : null);
                }
            }

            lastIndexInBytes = indexInBytes + lengthInBytes;
            splitCount++;
        }

        collected.Add(Encoding.UTF8.GetString(tail[lastIndexInBytes..]));
        parts = collected.ToArray();
        return true;
    }

    private Utf8PythonMatchContext CreateMatchContext(ReadOnlySpan<byte> input, Match match, int utf16BaseOffset = 0)
    {
        if (!match.Success)
        {
            return default;
        }

        return CreateMatchSnapshot(input, match, utf16BaseOffset).ToContext(input, _nameEntries);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchData(ReadOnlySpan<byte> input, Match match, int utf16BaseOffset = 0)
    {
        if (!match.Success)
        {
            return default;
        }

        return CreateMatchSnapshot(input, match, utf16BaseOffset).ToDetailedData(input, _nameEntries);
    }

    private Utf8PythonMatchContext CreateMatchContextFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match,
        int byteBaseOffset = 0,
        int utf16BaseOffset = 0)
    {
        if (!match.Success)
        {
            return default;
        }

        var groups = new PythonReGroupData[match.GroupCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = PythonReGroupData.FromUtf8Group(i, match.GetGroup(i), byteBaseOffset, utf16BaseOffset);
        }

        return new Utf8PythonMatchContext(input, groups, _nameEntries);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchDataFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match,
        int byteBaseOffset = 0,
        int utf16BaseOffset = 0)
    {
        if (!match.Success)
        {
            return default;
        }

        var groups = new PythonReGroupData[match.GroupCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = PythonReGroupData.FromUtf8Group(i, match.GetGroup(i), byteBaseOffset, utf16BaseOffset);
        }

        return CreateDetailedMatchData(input, groups, _nameEntries);
    }

    private static PythonReManagedMatchSnapshot CreateMatchSnapshot(ReadOnlySpan<byte> input, Match match, int utf16BaseOffset = 0)
    {
        var groups = new PythonReGroupData[match.Groups.Count];
        for (var i = 0; i < match.Groups.Count; i++)
        {
            groups[i] = PythonReGroupData.FromUtf16(input, i, match.Groups[i], utf16BaseOffset);
        }

        return new PythonReManagedMatchSnapshot(groups);
    }

    private static Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        PythonReGroupData[]? groups,
        PythonReNameEntry[]? nameEntries)
    {
        if (groups is null)
        {
            return default;
        }

        var projectedGroups = new Utf8PythonGroupMatchData[groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            projectedGroups[i] = CreateGroupMatchData(input, groups[i]);
        }

        return new Utf8PythonDetailedMatchData
        {
            Groups = projectedGroups,
            NameEntries = nameEntries ?? [],
        };
    }

    private static string[] GetPublicGroupNames(Regex regex, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        var reverseMap = emittedGroupNames.ToDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);
        var names = regex.GetGroupNames();
        for (var i = 0; i < names.Length; i++)
        {
            if (reverseMap.TryGetValue(names[i], out var original))
            {
                names[i] = original;
            }
        }

        return names;
    }

    private static PythonReNameEntry[] GetManagedNameEntries(Regex regex, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        var entries = new List<PythonReNameEntry>(emittedGroupNames.Count);
        foreach (var pair in emittedGroupNames)
        {
            entries.Add(new PythonReNameEntry
            {
                Name = pair.Key,
                Number = regex.GroupNumberFromName(pair.Value),
            });
        }

        return entries.ToArray();
    }

    private static Utf8PythonValueMatch Utf8PythonValueMatchFromUtf8Regex(
        ReadOnlySpan<byte> input,
        Utf8ValueMatch match,
        int byteBaseOffset = 0,
        int utf16BaseOffset = 0)
    {
        if (!match.Success)
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, PythonReGroupData.FromUtf8Match(match, byteBaseOffset, utf16BaseOffset));
    }

    private static void AppendSplitMatch(List<string?> parts, string subject, Match match, ref int lastIndex, int utf16BaseOffset = 0)
    {
        var absoluteStart = utf16BaseOffset + match.Index;
        var absoluteEnd = absoluteStart + match.Length;
        parts.Add(subject[lastIndex..absoluteStart]);
        for (var i = 1; i < match.Groups.Count; i++)
        {
            parts.Add(match.Groups[i].Success ? match.Groups[i].Value : null);
        }

        lastIndex = absoluteEnd;
    }

    private static void AppendSplitDetailedMatch(List<Utf8PythonSplitItem> parts, string subject, Match match, ref int lastIndex, int utf16BaseOffset = 0)
    {
        var absoluteStart = utf16BaseOffset + match.Index;
        var absoluteEnd = absoluteStart + match.Length;
        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..absoluteStart],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        for (var i = 1; i < match.Groups.Count; i++)
        {
            parts.Add(new Utf8PythonSplitItem
            {
                ValueText = match.Groups[i].Success ? match.Groups[i].Value : null,
                IsCapture = true,
                CaptureGroupNumber = i,
            });
        }

        lastIndex = absoluteEnd;
    }

    private static void AppendFindAllScalarValue(List<string> values, Utf8PythonMatchContext context, int groupNumber)
    {
        values.Add(GetFindAllGroupValue(context, groupNumber));
    }

    private static void AppendFindAllScalarBytes(List<byte[]> values, ReadOnlySpan<byte> input, PythonReManagedMatchSnapshot snapshot, int groupNumber)
    {
        values.Add(GetFindAllGroupBytes(input, snapshot.Groups, groupNumber));
    }

    private void AppendFindAllTupleValue(List<string[]> tuples, Utf8PythonMatchContext context)
    {
        var tuple = new string[_translation.CaptureGroupCount];
        for (var i = 0; i < tuple.Length; i++)
        {
            tuple[i] = GetFindAllGroupValue(context, i + 1);
        }

        tuples.Add(tuple);
    }

    private void AppendFindAllTupleBytes(List<byte[][]> tuples, ReadOnlySpan<byte> input, PythonReManagedMatchSnapshot snapshot)
    {
        var tuple = new byte[_translation.CaptureGroupCount][];
        for (var i = 0; i < tuple.Length; i++)
        {
            tuple[i] = GetFindAllGroupBytes(input, snapshot.Groups, i + 1);
        }

        tuples.Add(tuple);
    }

    private static string GetFindAllGroupValue(Utf8PythonMatchContext context, int groupNumber)
    {
        return context.TryGetGroup(groupNumber, out var group) && group.Success
            ? group.Value.GetValueString()
            : string.Empty;
    }

    private static byte[] GetFindAllGroupBytes(ReadOnlySpan<byte> input, PythonReGroupData[] groups, int groupNumber)
    {
        return (uint)groupNumber < (uint)groups.Length && groups[groupNumber].Success
            ? PythonReValueTextExtractor.GetValueBytes(input, groups[groupNumber])
            : [];
    }

    private static Utf8PythonGroupMatchData CreateGroupMatchData(ReadOnlySpan<byte> input, PythonReGroupData group)
    {
        return new Utf8PythonGroupMatchData
        {
            Number = group.Number,
            Success = group.Success,
            StartOffsetInBytes = group.Success ? group.StartOffsetInBytes : 0,
            EndOffsetInBytes = group.Success ? group.EndOffsetInBytes : 0,
            StartOffsetInUtf16 = group.Success ? group.StartOffsetInUtf16 : 0,
            EndOffsetInUtf16 = group.Success ? group.EndOffsetInUtf16 : 0,
            HasContiguousByteRange = group.HasContiguousByteRange,
            ValueText = PythonReValueTextExtractor.GetValueString(input, group),
        };
    }

    private static int GetUtf8RuneLength(byte firstByte)
    {
        if ((firstByte & 0b1000_0000) == 0)
        {
            return 1;
        }

        if ((firstByte & 0b1110_0000) == 0b1100_0000)
        {
            return 2;
        }

        if ((firstByte & 0b1111_0000) == 0b1110_0000)
        {
            return 3;
        }

        return 4;
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
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        List<byte> builder = [];
        builder.AddRange(input[..startOffsetInBytes].ToArray());

        var replaced = 0;
        var lastIndexInBytes = startOffsetInBytes;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var snapshot = CreateMatchSnapshot(input, match);
            var value = snapshot.Groups[0];
            if (count == 0 || replaced < count)
            {
                builder.AddRange(input[lastIndexInBytes..value.StartOffsetInBytes].ToArray());
                builder.AddRange(replacementFactory(input, snapshot, state));
                lastIndexInBytes = value.EndOffsetInBytes;
                replaced++;
            }
            else
            {
                break;
            }

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(input, subject, match.Index, out var nonEmptySnapshot))
            {
                var nonEmptyValue = nonEmptySnapshot.Groups[0];
                if (count == 0 || replaced < count)
                {
                    builder.AddRange(input[lastIndexInBytes..nonEmptyValue.StartOffsetInBytes].ToArray());
                    builder.AddRange(replacementFactory(input, nonEmptySnapshot, state));
                    lastIndexInBytes = nonEmptyValue.EndOffsetInBytes;
                    replaced++;
                }

                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = match.Index + 1;
        }

        builder.AddRange(input[lastIndexInBytes..].ToArray());
        return new Utf8PythonSubnUtf8Result
        {
            ResultBytes = builder.ToArray(),
            ReplacementCount = replaced,
        };
    }
}

internal enum PythonReDirectBackendKind
{
    ManagedRegex,
    Utf8Regex,
}

internal readonly record struct PythonReSamePositionProbe(string Text, int StartIndex, int Utf16BaseOffset);

internal readonly record struct PythonReManagedMatchSnapshot(PythonReGroupData[] Groups)
{
    public Utf8PythonMatchContext ToContext(ReadOnlySpan<byte> input, PythonReNameEntry[] nameEntries)
        => new(input, Groups, nameEntries);

    public Utf8PythonDetailedMatchData ToDetailedData(ReadOnlySpan<byte> input, PythonReNameEntry[] nameEntries)
    {
        var projectedGroups = new Utf8PythonGroupMatchData[Groups.Length];
        for (var i = 0; i < Groups.Length; i++)
        {
            var group = Groups[i];
            projectedGroups[i] = new Utf8PythonGroupMatchData
            {
                Number = group.Number,
                Success = group.Success,
                StartOffsetInBytes = group.Success ? group.StartOffsetInBytes : 0,
                EndOffsetInBytes = group.Success ? group.EndOffsetInBytes : 0,
                StartOffsetInUtf16 = group.Success ? group.StartOffsetInUtf16 : 0,
                EndOffsetInUtf16 = group.Success ? group.EndOffsetInUtf16 : 0,
                HasContiguousByteRange = group.HasContiguousByteRange,
                ValueText = PythonReValueTextExtractor.GetValueString(input, group),
            };
        }

        return new Utf8PythonDetailedMatchData
        {
            Groups = projectedGroups,
            NameEntries = nameEntries,
        };
    }
}
