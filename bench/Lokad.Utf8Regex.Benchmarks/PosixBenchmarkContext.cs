using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Posix;
using PCRE;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class PosixBenchmarkContext : IDisposable
{
    private readonly PosixBenchmarkCase _case;
    private readonly byte[] _input;
    private readonly Utf8PosixRegex[] _managed;
    private readonly Utf8ValueMatch[] _groups;
    private readonly CancellationTokenSource _token = new();
    private readonly Account _account = new();
    private readonly PcreRegexUtf8 _native;
    private readonly PcreMatchBuffer8Bit _buffer;

    internal PosixBenchmarkContext(PosixBenchmarkCase benchmarkCase)
    {
        _case = benchmarkCase; _input = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        var finite = new Utf8ResourcePolicy { MaxWorkSteps = 10_000_000, MaxDepth = 4096,
            MaxWorkingBytes = 16 << 20, MaxResultBytes = 1 };
        _managed =
        [
            new(benchmarkCase.Pattern),
            new(benchmarkCase.Pattern, default, Timeout.InfiniteTimeSpan, finite),
            new(benchmarkCase.Pattern, default, Timeout.InfiniteTimeSpan, finite),
            new(benchmarkCase.Pattern, default, TimeSpan.FromSeconds(30), finite),
        ];
        if (_managed[0].GroupCount > 4) throw new InvalidOperationException("The POSIX catalog destination supports four captures.");
        _groups = new Utf8ValueMatch[_managed[0].GroupCount + 1];
        // Private benchmark-only PCRE2 NFA comparator over the same valid UTF-8 text;
        // ASCII POSIX classes, dot-all and true-end dollar semantics. Do not
        // reorder alternatives to imitate longest matching; exclude mismatches.
        _native = new PcreRegexUtf8(benchmarkCase.Pattern, PcreOptions.Utf | PcreOptions.Singleline | PcreOptions.DollarEndOnly);
        _buffer = _native.CreateMatchBuffer();
    }

    internal ulong PeakWorkingBytes => _account.Peak;
    internal ulong OutstandingWorkingBytes => _account.Used;

    internal PosixBenchmarkResult Execute(int lane)
    {
        if (lane == 4) return ExecuteNative();
        var regex = _managed[lane];
        var token = lane == 0 ? default : _token.Token;
        var account = lane == 2 ? _account : null;
        switch (_case.Operation)
        {
            case PosixBenchmarkOperation.Count:
                return new(regex.Count(_input, _case.StartOffsetInBytes, token, account), default, default, default);
            case PosixBenchmarkOperation.Captures:
            {
                Utf8ValueMatch whole = default;
                for (var record = 0; record < _case.Records; record++)
                    whole = regex.Match(_input, _groups, _case.StartOffsetInBytes, token, account);
                return Result(whole.Success ? _case.Records : 0, whole);
            }
            default:
            {
                var count = 0;
                PosixBenchmarkResult result = default;
                using (var cursor = regex.CreateMatchCursor(_input, _case.StartOffsetInBytes, token, account))
                {
                    while (cursor.MoveNext(_groups, out var match))
                    { count++; result = Result(count, match); }
                }
                return result;
            }
        }
    }

    internal PosixBenchmarkResult[] ReadTrace(bool native)
    {
        if (_case.Operation == PosixBenchmarkOperation.Captures) return ReadCaptureTrace(native ? 4 : 0);
        var result = new List<PosixBenchmarkResult>();
        if (native)
        {
            foreach (var match in _buffer.Matches(_input, _case.StartOffsetInBytes, PcreMatchOptions.NoUtfCheck, null))
            {
                var value = NativeResult(match, 1);
                result.Add(_case.Operation == PosixBenchmarkOperation.Count ? value with { Group1 = default, Group2 = default } : value);
            }
        }
        else
        {
            using var cursor = _managed[0].CreateMatchCursor(_input, _case.StartOffsetInBytes);
            while (cursor.MoveNext(_groups, out var match))
            {
                var value = Result(1, match);
                result.Add(_case.Operation == PosixBenchmarkOperation.Count ? value with { Group1 = default, Group2 = default } : value);
            }
        }
        return result.ToArray();
    }

    // Untimed preflight observes every independent record. Keep the timed loop's
    // reused destination and last-result consumption unchanged.
    internal PosixBenchmarkResult[] ReadCaptureTrace(int lane)
    {
        var result = new PosixBenchmarkResult[_case.Records];
        for (var record = 0; record < result.Length; record++)
        {
            if (lane == 4) result[record] = NativeResult(_buffer.Match(_input, _case.StartOffsetInBytes, PcreMatchOptions.NoUtfCheck), _case.Records);
            else
            {
                var whole = _managed[lane].Match(_input, _groups, _case.StartOffsetInBytes,
                    lane == 0 ? default : _token.Token, lane == 2 ? _account : null);
                result[record] = Result(whole.Success ? _case.Records : 0, whole);
            }
        }
        if (_account.Used != 0) throw new InvalidOperationException("POSIX preflight leaked working storage.");
        return result;
    }

    private PosixBenchmarkResult ExecuteNative()
    {
        switch (_case.Operation)
        {
            case PosixBenchmarkOperation.Captures:
                PosixBenchmarkResult last = default;
                for (var record = 0; record < _case.Records; record++)
                    last = NativeResult(_buffer.Match(_input, _case.StartOffsetInBytes, PcreMatchOptions.NoUtfCheck), _case.Records);
                return last;
            default:
                var count = 0;
                PosixBenchmarkResult result = default;
                foreach (var match in _buffer.Matches(_input, _case.StartOffsetInBytes, PcreMatchOptions.NoUtfCheck, null))
                {
                    count++;
                    if (_case.Operation == PosixBenchmarkOperation.Cursor)
                        result = NativeResult(match, count);
                }
                return _case.Operation == PosixBenchmarkOperation.Count ? new(count, default, default, default) : result;
        }
    }

    private PosixBenchmarkResult Result(int count, Utf8ValueMatch whole)
        => new(count, whole, _groups.Length > 1 ? _groups[1] : default, _groups.Length > 2 ? _groups[2] : default,
            _groups.Length > 3 ? _groups[3] : default, _groups.Length > 4 ? _groups[4] : default);

    private PosixBenchmarkResult NativeResult(PcreRefMatch8Bit match, int count)
        => !match.Success ? default : new(count,
            Project(true, match.Index, match.Length),
            _groups.Length > 1 ? Project(match[1].Success, match[1].Index, match[1].Length) : default,
            _groups.Length > 2 ? Project(match[2].Success, match[2].Index, match[2].Length) : default,
            _groups.Length > 3 ? Project(match[3].Success, match[3].Index, match[3].Length) : default,
            _groups.Length > 4 ? Project(match[4].Success, match[4].Index, match[4].Length) : default);

    private Utf8ValueMatch Project(bool success, int start, int length) => success
        ? new(true, true, Encoding.UTF8.GetCharCount(_input.AsSpan(0, start)),
            Encoding.UTF8.GetCharCount(_input.AsSpan(start, length)), start, length) : default;

    internal (TimeSpan Elapsed, long AllocatedBytes, PosixBenchmarkResult Result) Measure(int lane, int iterations)
    {
        PosixBenchmarkResult result = default;
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var stamp = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < iterations; iteration++) result = Execute(lane);
        var elapsed = Stopwatch.GetElapsedTime(stamp);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (_account.Used != 0) throw new InvalidOperationException("POSIX benchmark leaked a working loan.");
        GC.KeepAlive(result);
        return (elapsed, allocated, result);
    }

    public void Dispose()
    { _buffer.Dispose(); _token.Dispose(); GC.KeepAlive(_native); }

    private sealed class Account : IUtf8MemoryAllowance
    {
        private readonly object _gate = new();
        internal ulong Used, Peak;
        public bool TryReserve(ulong bytes)
        { lock (_gate) { if (bytes > (16UL << 20) - Used) return false; Used += bytes; Peak = Math.Max(Peak, Used); return true; } }
        public void Release(ulong bytes)
        { lock (_gate) { if (bytes > Used) throw new InvalidOperationException("Invalid POSIX benchmark refund."); Used -= bytes; } }
    }
}
