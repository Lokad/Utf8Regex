using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

// Read-only plan/runtime metadata. Tooling and tests execute production implementations directly.
/// <summary>Read-only execution metadata for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    internal readonly struct Utf8RegexInspection
    {
        private readonly Utf8Regex _owner;

        public Utf8RegexInspection(Utf8Regex owner)
        {
            _owner = owner;
        }

        public NativeExecutionKind ExecutionKind => _owner.ExecutionKind;

        public bool DebugUsesOwnedSliceProgram => _owner._preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex;

        internal Utf8SliceProgram? OwnedSliceProgram => DebugUsesOwnedSliceProgram ? _owner._ownedSliceProgram : null;

        // Profile adapters can use the admitted scalar program even when the
        // core's default route selects a native plan for this pattern.
        internal Utf8SliceProgram AvailableOwnedSliceProgram => _owner._ownedSliceProgram;

        public Utf8PreparedRegex PreparedRegex => _owner.PreparedRegex;

        public Utf8SearchPlan SearchPlan => _owner.SearchPlan;

        public Utf8StructuralSearchPlan StructuralSearchPlan => _owner.StructuralSearchPlan;

        public Utf8SearchPortfolioKind SearchPortfolioKind => _owner.SearchPortfolioKind;

        public Utf8CompiledEngineKind CompiledEngineKind => _owner.CompiledEngineKind;

        // Friend tooling borrows the existing runtime; no replay or extra owner
        // is retained in the shipped matcher.
        internal Utf8CompiledEngineRuntime CompiledRuntime => _owner._compiledEngineRuntime;

        public Utf8CompiledExecutionBackend CompiledExecutionBackend => _owner.CompiledExecutionBackend;

        public string DebugCompiledEngineRuntimeType => _owner._compiledEngineRuntime.GetType().Name;

        public bool DebugUsesEmittedKernelMatcher => _owner._compiledEngineRuntime.UsesEmittedKernelMatcher;

        public string DebugFallbackDirectFamilyKind => _owner._preparedRegex.FallbackDirectFamily.Kind.ToString();

        public bool DebugHasInvariantCyrillicLiteralCountStrategy =>
            _owner._invariantCyrillicLiteralCountStrategy is not null;

        public bool DebugInvariantCyrillicCountUsesCorrelatedPrefilter =>
            _owner._invariantCyrillicLiteralCountStrategy?.UsesCorrelatedPrefilter == true;

        public bool DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch =>
            _owner._invariantCyrillicLiteralCountStrategy?.UsesDirectSingleLiteralSearch == true;

        public bool DebugHasPackedNibbleLiteralFamilyCountStrategy =>
            _owner._packedNibbleLiteralFamilyCountStrategy is not null;

        public bool DebugPrioritizesBoundaryLiteralFamilyCount =>
            _owner._prioritizeBoundaryLiteralFamilyCount;

        public bool DebugHasFusedAsciiBoundaryLiteralFamilyCount =>
            _owner._fusedAsciiBoundaryLiteralFamilyCounter is not null;

        public Utf8StructuralLinearProgramKind StructuralLinearProgramKind =>
            _owner.StructuralLinearProgramKind;

        public Utf8StructuralVerifierPlan StructuralVerifierPlan => _owner.StructuralVerifierPlan;

        public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan =>
            _owner.StructuralIdentifierFamilyPlan;

        public AsciiSimplePatternPlan SimplePatternPlan => _owner.SimplePatternPlan;

        public string? FallbackReason => _owner.FallbackReason;

        public bool DebugCanUseFusedCompiledUtf8LiteralCount =>
            _owner.CanUseFusedCompiledUtf8LiteralCount();

        public bool DebugCanUseFusedCompiledUtf8LiteralFamilyCount =>
            _owner.CanUseFusedCompiledUtf8LiteralFamilyCount();

        public bool DebugSupportsWellFormedOnlyMatch => _owner._compiledEngineRuntime.SupportsWellFormedOnlyMatch;

        public bool DebugWellFormedOnlyMatchMissIsDefinitive =>
            _owner._compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive;

        public bool DebugSupportsThrowIfInvalidOnlyCount =>
            _owner._compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount;

        public bool DebugUsesEmittedWholeMatcher =>
            _owner._compiledEngineRuntime.UsesEmittedWholeMatcher;

        public bool DebugCanGuideFallbackVerification => _owner.CanGuideFallbackVerification();

        public int DebugReplacementCacheEntryCount => _owner._replacementCache.Count;
    }
}
