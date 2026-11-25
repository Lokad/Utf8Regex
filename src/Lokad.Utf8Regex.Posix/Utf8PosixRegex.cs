using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Posix;

/// <summary>Matches the invariant POSIX ERE subset over caller-valid UTF-8 text.</summary>
/// <remarks>Selected matches are leftmost-longest with hierarchical subpattern priority. Instances are immutable and reusable; there is no native or BCL matcher fallback.</remarks>
public sealed class Utf8PosixRegex : IDisposable
{
    /// <summary>Constructs an ERE with immutable construction, execution and optional program-funding settings.</summary>
    /// <remarks>Null policy timeouts mean infinite. Default construction borrows existing internal admission; explicit construction/funding controls own one scope. Funded expressions reject new matching calls after close, while already-admitted calls/cursors retain program funding. Subjects remain caller-valid unchanged UTF-8.</remarks>
    public Utf8PosixRegex(string pattern, PosixCompileOptions options = PosixCompileOptions.None,
        Utf8RegexPolicy? policy = null) : this(pattern, options, policy, default, explicitCompile: false) { }

    private Utf8PosixRegex(string pattern, PosixCompileOptions options, Utf8RegexPolicy? policy,
        CancellationToken token, bool explicitCompile)
    {
        token.ThrowIfCancellationRequested();
        policy ??= Utf8RegexPolicy.Default;
        var constructionTimeout = policy.ConstructionTimeout ?? Regex.InfiniteMatchTimeout;
        var constructionPolicy = policy.ConstructionPolicy;
        var account = policy.ProgramMemoryAllowance;
        constructionPolicy.Validate(nameof(policy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(policy));
        ArgumentNullException.ThrowIfNull(pattern);
        if ((options & ~(PosixCompileOptions.Newline | PosixCompileOptions.IgnoreCase)) != 0) throw new ArgumentOutOfRangeException(nameof(options));
        MatchTimeout = Utf8MatchTimeout.Validate(policy.MatchTimeout ?? Regex.InfiniteMatchTimeout, nameof(policy));
        var resources = policy.ResourcePolicy;
        resources.Validate(nameof(policy));
        Pattern = pattern; Options = options;
        _limits = new(resources);
        var controlledConstruction = account is not null ||
            constructionPolicy != default || constructionTimeout != Regex.InfiniteMatchTimeout;
        var ownsConstruction = explicitCompile || controlledConstruction;
        var enableExistenceTier = !controlledConstruction && !Utf8ConstructionScope.IsActive &&
            MatchTimeout == Regex.InfiniteMatchTimeout && _limits == Utf8ExecutionLimits.Unlimited &&
            (options & PosixCompileOptions.IgnoreCase) == 0;
        // PosixProgram already owns default construction and inherits a parent.
        // Explicit controls need one outer owner, never a replacement parent.
        using var construction = !ownsConstruction ? null : account is null
            ? Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token)
            : Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token, account);
        try
        {
            _program = PosixProgram.Compile(pattern, options, enableExistenceTier);
            GroupCount = _program.CaptureTags.Length - 1;
            if (construction is not null)
            {
                if (account is not null)
                {
                    var owner = construction.Budget.AllocateObject(account,
                        static allowance => new Utf8FundedStorage(allowance, nameof(Utf8PosixRegex)));
                    construction.Budget.ChargeWork(0);
                    owner.Retain(construction.Budget.TransferFundedBytes());
                    _programMemory = owner;
                }
                else construction.Budget.ChargeWork(0);
                construction.Complete();
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (ownsConstruction)
        { throw new RegexMatchTimeoutException(string.Empty, pattern, constructionTimeout); }
    }

    // Synchronous state remains bounded and admitted by the workspace.
    private const int StackStateLimit = 128;
    private readonly PosixProgram _program;
    internal Utf8SliceProgram ScalarProgram => _program.Scalar;
    private readonly Utf8ExecutionLimits _limits;
    private readonly Utf8FundedStorage? _programMemory;

    /// <summary>Compiles an ERE eagerly under one policy and an independent construction token.</summary>
    /// <remarks>Entry cancellation precedes policy/pattern work. One construction owner covers admission and publication. The token is not retained for matching; later operations use their own deadlines and cancellation.</remarks>
    public static Utf8PosixRegex Compile(string pattern, PosixCompileOptions options = PosixCompileOptions.None,
        Utf8RegexPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(pattern, options, policy, cancellationToken, explicitCompile: true);
    }

    /// <summary>Closes a caller-funded program after its active calls/cursors finish.</summary>
    /// <remarks>No-op without program funding. Closing rejects new matching calls but does not cancel existing ones. Retained scalar metadata and copied ranges remain valid. There is no finalizer.</remarks>
    public void Dispose() => _programMemory?.Close();

    /// <summary>Gets the original ERE pattern.</summary>
    public string Pattern { get; }
    /// <summary>Gets the admitted compile options.</summary>
    public PosixCompileOptions Options { get; }
    /// <summary>Gets the timeout for one invocation, including cursor advances.</summary>
    public TimeSpan MatchTimeout { get; }
    /// <summary>Gets the number of capturing parentheses, excluding whole-match slot zero.</summary>
    public int GroupCount { get; }

    /// <summary>Tests existence over valid UTF-8 text, with original interruption and optional working admission.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        InitializeInvocation(out var invocation, cancellationToken, workingMemoryAllowance);
        PosixWorkspace workspace = default;
        try
        {
            CheckStart(input, 0, ref invocation);
            var scalar = ScalarProgram;
            // POSIX's small-input loop avoids the shared search setup.
            if (input.Length > Utf8SliceRunner.ShortSingleOriginProofInputLimit &&
                (scalar.ObservesProofWork || scalar.TieredProof is not null) &&
                invocation.IsUnmetered && invocation.CheckpointHook is null)
            {
                var result = Utf8SliceRunner.TryFindFirst(scalar, input, 0, [], [], false, ref invocation, existenceOnly: true);
                if (result.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                return result.Found;
            }
            InitializeWorkspace(out workspace, ref invocation, captureHistory: false);
            Span<int> state = workspace.StateLength <= StackStateLimit ? stackalloc int[workspace.StateLength] : new int[workspace.StateLength];
            var found = workspace.Find(input, state, 0, true, ref invocation, out _, out _);
            PosixWorkspace.Finish(ref invocation);
            return found;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateTimeout(); }
        finally { workspace.Release(ref invocation); }
    }

    /// <summary>Finds the selected match and writes available capture slots zero onward into caller-owned storage.</summary>
    /// <remarks>Short output cannot change selection. Excess slots are untouched. Written slots clear on no match or interrupted publication; retained values need no producer lifetime.</remarks>
    public Utf8CaptureRange Match(ReadOnlySpan<byte> input, int startOffsetInBytes = 0, Span<Utf8CaptureRange> captures = default,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        InitializeInvocation(out var invocation, cancellationToken, workingMemoryAllowance);
        var output = OutputWindow(captures);
        PosixWorkspace workspace = default;
        try
        {
            CheckStart(input, startOffsetInBytes, ref invocation);
            var captureHistory = output.Length > 1;
            InitializeWorkspace(out workspace, ref invocation, captureHistory);
            Span<int> state = workspace.StateLength <= StackStateLimit ? stackalloc int[workspace.StateLength] : new int[workspace.StateLength];
            if (!workspace.Find(input, state, startOffsetInBytes, false, ref invocation, out var start, out var end, captureHistory))
            { ClearOutput(output, ref invocation); return default; }
            var match = WriteCaptures(start, end, in workspace, state, output, ref invocation);
            PosixWorkspace.Finish(ref invocation);
            return match;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { output.Clear(); throw CreateTimeout(); }
        catch { output.Clear(); throw; }
        finally { workspace.Release(ref invocation); }
    }

    /// <summary>Counts nonoverlapping selected matches, advancing one scalar after each empty match.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        InitializeInvocation(out var invocation, cancellationToken, workingMemoryAllowance);
        PosixWorkspace workspace = default;
        try
        {
            CheckStart(input, startOffsetInBytes, ref invocation);
            InitializeWorkspace(out workspace, ref invocation, captureHistory: false);
            Span<int> state = workspace.StateLength <= StackStateLimit ? stackalloc int[workspace.StateLength] : new int[workspace.StateLength];
            var count = 0; var position = startOffsetInBytes;
            while (workspace.Find(input, state, position, false, ref invocation, out var start, out var end, captureHistory: false))
            {
                count = checked(count + 1);
                if (start == end && end == input.Length) break;
                position = start == end ? Utf8SliceRunner.AdvancePastScalar(input, end, false) : end;
            }
            PosixWorkspace.Finish(ref invocation);
            return count;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateTimeout(); }
        finally { workspace.Release(ref invocation); }
    }

    /// <summary>Creates an exclusive disposable cursor borrowing unchanged input memory.</summary>
    /// <remarks>The original token, deadline and cumulative limits remain active across advances. Reference aliases share one cursor; disposal refunds covered workspace synchronously.</remarks>
    public Utf8PosixMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes = 0,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var programUse = _programMemory?.Enter();
        try { return new(this, input, startOffsetInBytes, cancellationToken, workingMemoryAllowance, programUse); }
        catch { programUse?.Dispose(); throw; }
    }

    internal void InitializeInvocation(out Utf8SliceInvocation invocation, CancellationToken token,
        IUtf8MemoryAllowance? allowance)
    {
        token.ThrowIfCancellationRequested();
        // The call or reference cursor owns the budget and lends it by reference.
        Utf8SliceInvocation.InitializeExclusive(out invocation, _limits, MatchTimeout, token,
            allowance ?? _programMemory?.Allowance);
    }

    internal void InitializeWorkspace(out PosixWorkspace workspace, ref Utf8SliceInvocation invocation, bool captureHistory = true)
        => PosixWorkspace.Initialize(out workspace, _program, ref invocation, captureHistory);

    internal static void CheckStart(ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation)
    {
        if (!Utf8BytePosition.IsScalarBoundary(input, start))
        {
            if ((uint)start > (uint)input.Length) throw new ArgumentOutOfRangeException("startOffsetInBytes");
            throw new ArgumentOutOfRangeException("startOffsetInBytes", "The start must be a Unicode scalar boundary.");
        }
        PosixWorkspace.Finish(ref invocation);
    }

    internal Span<Utf8CaptureRange> OutputWindow(Span<Utf8CaptureRange> output) => output[..Math.Min(output.Length, GroupCount + 1)];

    internal Utf8CaptureRange WriteCaptures(int start, int end,
        in PosixWorkspace workspace, scoped ReadOnlySpan<int> state, Span<Utf8CaptureRange> output, ref Utf8SliceInvocation invocation)
    {
        var whole = new Utf8CaptureRange(start, end);
        if (!output.IsEmpty)
        {
            PosixWorkspace.Charge(ref invocation);
            output[0] = whole;
        }
        for (var slot = 1; slot < output.Length; slot++)
        {
            PosixWorkspace.Charge(ref invocation);
            output[slot] = workspace.GetCapture(state, slot, ref invocation, out var from, out var to)
                ? new Utf8CaptureRange(from, to) : default;
        }
        return whole;
    }

    internal static void ClearOutput(Span<Utf8CaptureRange> output, ref Utf8SliceInvocation invocation)
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
