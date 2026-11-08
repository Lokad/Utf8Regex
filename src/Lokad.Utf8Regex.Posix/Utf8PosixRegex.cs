using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Posix;

/// <summary>Matches the invariant POSIX ERE subset over strict UTF-8 Unicode scalars.</summary>
/// <remarks>Selected matches are leftmost-longest with hierarchical subpattern priority. Instances are immutable and reusable; there is no native or BCL matcher fallback.</remarks>
public sealed class Utf8PosixRegex
{
    private readonly PosixProgram _program;
    private readonly Utf8ExecutionLimits _limits;

    /// <summary>Compiles a case-sensitive ERE with absolute anchors and unlimited execution policy.</summary>
    public Utf8PosixRegex(string pattern) : this(pattern, PosixCompileOptions.None, Regex.InfiniteMatchTimeout, default) { }

    /// <summary>Compiles an ERE with explicit newline semantics, timeout and resource policy.</summary>
    public Utf8PosixRegex(string pattern, PosixCompileOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if ((options & ~PosixCompileOptions.Newline) != 0) throw new ArgumentOutOfRangeException(nameof(options));
        MatchTimeout = Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));
        resourcePolicy.Validate(nameof(resourcePolicy));
        Pattern = pattern; Options = options;
        _limits = new(resourcePolicy.MaxWorkSteps ?? 0, resourcePolicy.MaxDepth ?? 0,
            resourcePolicy.MaxWorkingBytes ?? 0, resourcePolicy.MaxResultBytes ?? 0);
        _program = PosixProgram.Compile(pattern, options);
    }

    /// <summary>Compiles with separate bounded eager construction and execution controls.</summary>
    public static Utf8PosixRegex Compile(string pattern, PosixCompileOptions options, TimeSpan matchTimeout,
        Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        constructionPolicy.Validate(nameof(constructionPolicy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var owner = Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken);
        try
        {
            var result = new Utf8PosixRegex(pattern, options, matchTimeout, resourcePolicy);
            owner.Budget.ChargeWork(0);
            owner.Complete();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new RegexMatchTimeoutException(string.Empty, pattern, constructionTimeout); }
    }

    /// <summary>Gets the original ERE pattern.</summary>
    public string Pattern { get; }
    /// <summary>Gets the admitted compile options.</summary>
    public PosixCompileOptions Options { get; }
    /// <summary>Gets the timeout for one invocation, including cursor advances.</summary>
    public TimeSpan MatchTimeout { get; }
    /// <summary>Gets the number of capturing parentheses, excluding whole-match slot zero.</summary>
    public int GroupCount => _program.CaptureTags.Length - 1;

    /// <summary>Tests existence after whole-input strict validation, with original interruption and optional working admission.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        var invocation = StartInvocation(cancellationToken, workingMemoryAllowance);
        PosixWorkspace? workspace = null;
        try
        {
            ValidateInput(input, 0, ref invocation);
            workspace = CreateWorkspace(ref invocation);
            return workspace.Find(input, 0, true, ref invocation, out _, out _);
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateTimeout(); }
        finally { workspace?.Release(ref invocation); }
    }

    /// <summary>Finds the selected whole match at or after a scalar-aligned byte floor.</summary>
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
        => Match(input, Span<Utf8ValueMatch>.Empty, startOffsetInBytes, cancellationToken, workingMemoryAllowance);

    /// <summary>Finds the selected match and writes available capture slots zero onward into caller-owned storage.</summary>
    /// <remarks>Short output cannot change selection. Excess slots are untouched. Written slots clear on no match or interrupted publication; retained values need no producer lifetime.</remarks>
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input, Span<Utf8ValueMatch> groups, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        var invocation = StartInvocation(cancellationToken, workingMemoryAllowance);
        var output = OutputWindow(groups);
        PosixWorkspace? workspace = null;
        try
        {
            ValidateInput(input, startOffsetInBytes, ref invocation);
            workspace = CreateWorkspace(ref invocation);
            if (!workspace.Find(input, startOffsetInBytes, false, ref invocation, out var start, out var end))
            { ClearOutput(output, ref invocation); return default; }
            return Project(input, start, end, workspace, output, ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException) { output.Clear(); throw CreateTimeout(); }
        catch { output.Clear(); throw; }
        finally { workspace?.Release(ref invocation); }
    }

    /// <summary>Counts nonoverlapping selected matches, advancing one scalar after each empty match.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        var invocation = StartInvocation(cancellationToken, workingMemoryAllowance);
        PosixWorkspace? workspace = null;
        try
        {
            ValidateInput(input, startOffsetInBytes, ref invocation);
            workspace = CreateWorkspace(ref invocation);
            var count = 0; var position = startOffsetInBytes;
            while (workspace.Find(input, position, false, ref invocation, out var start, out var end))
            {
                count = checked(count + 1);
                if (start == end && end == input.Length) break;
                position = start == end ? Utf8SliceRunner.AdvancePastScalar(input, end, false) : end;
            }
            PosixWorkspace.Finish(ref invocation);
            return count;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateTimeout(); }
        finally { workspace?.Release(ref invocation); }
    }

    /// <summary>Creates an exclusive disposable cursor borrowing unchanged input memory.</summary>
    /// <remarks>The original token, deadline and cumulative limits remain active across advances. Reference aliases share one cursor; disposal refunds covered workspace synchronously.</remarks>
    public Utf8PosixMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
        => new(this, input, startOffsetInBytes, cancellationToken, workingMemoryAllowance);

    internal Utf8SliceInvocation StartInvocation(CancellationToken token, IUtf8MemoryAllowance? allowance)
    {
        token.ThrowIfCancellationRequested();
        return new(_limits, MatchTimeout, token, allowance);
    }

    internal PosixWorkspace CreateWorkspace(ref Utf8SliceInvocation invocation) => new(_program, ref invocation);

    internal static void ValidateInput(ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation)
    {
        if ((uint)start > (uint)input.Length) throw new ArgumentOutOfRangeException("startOffsetInBytes");
        var breach = Utf8Validation.TryValidateOnly(input, ref invocation);
        if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
        if (start < input.Length && (input[start] & 0xc0) == 0x80)
            throw new ArgumentOutOfRangeException("startOffsetInBytes", "The start must be a Unicode scalar boundary.");
        PosixWorkspace.Finish(ref invocation);
    }

    internal Span<Utf8ValueMatch> OutputWindow(Span<Utf8ValueMatch> output) => output[..Math.Min(output.Length, GroupCount + 1)];

    internal Utf8ValueMatch Project(ReadOnlySpan<byte> input, int start, int end,
        PosixWorkspace workspace, Span<Utf8ValueMatch> output, ref Utf8SliceInvocation invocation)
    {
        ClearOutput(output, ref invocation);
        var whole = ProjectRange(input, start, end, ref invocation);
        if (!output.IsEmpty) output[0] = whole;
        for (var slot = 1; slot < output.Length; slot++)
        {
            PosixWorkspace.Charge(ref invocation);
            if (workspace.GetCapture(slot, ref invocation, out var from, out var to))
                output[slot] = ProjectRange(input, from, to, ref invocation);
        }
        PosixWorkspace.Finish(ref invocation);
        return whole;
    }

    private static Utf8ValueMatch ProjectRange(ReadOnlySpan<byte> input, int from, int to, ref Utf8SliceInvocation invocation)
    {
        var startBreach = Utf8Validation.TryValidate(input[..from], ref invocation, out var prefix);
        if (startBreach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
        var lengthBreach = Utf8Validation.TryValidate(input[from..to], ref invocation, out var content);
        if (lengthBreach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
        return new(true, true, prefix.Utf16Length, content.Utf16Length, from, to - from);
    }

    internal static void ClearOutput(Span<Utf8ValueMatch> output, ref Utf8SliceInvocation invocation)
    {
        for (var offset = 0; offset < output.Length;)
        {
            PosixWorkspace.Charge(ref invocation);
            var count = Math.Min(2048, output.Length - offset);
            output.Slice(offset, count).Clear(); offset += count;
        }
        PosixWorkspace.Finish(ref invocation);
    }

    // Timeout diagnostics deliberately omit subject text; formatting cannot
    // allocate an unadmitted subject-sized decode after interruption.
    internal RegexMatchTimeoutException CreateTimeout() => new(string.Empty, Pattern, MatchTimeout);
}
