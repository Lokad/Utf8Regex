# Changelog

## Unreleased PCRE2 CRLF dot probes (0.4.0-scalar.14-dev)

- Replace byte-count endpoints in the admitted CRLF dot/`\N` and bounded dot
  probes with complete scalar token consumption. Preserve greedy/lazy preference,
  configured/inline DotAll, original candidate/anchor semantics and finite meters.
  Fixed/maxed/lazy full matches remain full at ordinary EOF; hard partial preserves
  uncertainty for a lone final CR. Reuse admitted tokens and result ownership.
  Broader partial profiles/settings remain separate acceptance gates.

## Unreleased PCRE2 literal-dot-star probes (0.4.0-scalar.13-dev)

- Replace the admitted `f.*` probe's hard-coded subject approximation with
  original-subject candidate search and the existing scalar dot semantics.
  Respect DotAll, newline settings and compile/operation anchoring. Soft accepts
  a complete greedy match; hard reports a partial only when it can extend at EOF.
  Preserve owned full results, inline partials, finite limits and interruption.
  Correct one old soft corpus expectation with pinned native evidence; wider
  partial-pattern/settings coverage remains a separate gate.

## Unreleased PCRE2 partial EndAnchored admission (0.4.0-scalar.12-dev)

- Reject partial soft/hard modes combined with compile- or match-time EndAnchored
  as `Pcre2MatchException` / `BadOption`, following PCRE2 10.47. Append the error
  kind without changing existing ordinals; one common check covers all drivers.
  Preserve pre-cancellation precedence and distinct resource errors. Correct valid
  non-partial compiled end anchoring by using existing owned detailed matching for
  later candidates and alternatives, with original limits and retained captures.

## Unreleased PCRE2 partial line/empty flags (0.4.0-scalar.11-dev)

- Observe qualified NotBol/NotEol/NotEmpty/NotEmptyAtStart partial outcomes using
  existing assertions and one invocation. Rejected empty full matches can preserve
  partials or permit later full candidates. Fix dollar rejection before final newline
  in normal matching too. Keep captures, aliases, finite admission and interruption.
- Replace duplicate trailing-repeat scanning with one scalar candidate loop, and
  use existing dot-token semantics for negative-lookahead/star candidates. Remove
  the incorrect literal-suffix partial comparator; partials require a literal prefix.
  Retarget four incorrect corpus outcomes with native API evidence. EndAnchored
  and wider partial setting/flag combinations remain required gates.

## Unreleased PCRE2 anchored partial probes (0.4.0-scalar.10-dev)

- Restrict specialized soft/hard candidates to the requested consumed start when
  operation or compile options specify Anchored. Preserve original-subject anchors,
  contextual word boundaries, lookbehind before the start and `\K` reported ranges.
  Keep original resource limits, token/deadline and retained captures. Other partial
  flags and combinations remain an open gate.
- Keep the first successful lazy/ordered alternative as a full match; remove the
  incorrect longer-partial-continuation check. Correct partial substring-zero offsets
  that included lookbehind display text, using native PCRE2 API evidence. Retarget
  three incorrect corpus expectations; keep their provenance and evidence.

## Unreleased PCRE2 non-partial probe options (0.4.0-scalar.9-dev)

- Forward explicit per-operation flags on non-partial probes even when a curated
  partial helper exists. Fix false successes for Anchored, EndAnchored and NotBol
  by reusing normal owned detailed matching under the original limits/token/deadline.
  Preserve captures, names, aliases and default helper algorithms. Specialized
  soft/hard helpers still ignore flags and remain a separate release gate;
  required native work can deny tight limits.

## Unreleased PCRE2 full-probe captures (0.4.0-scalar.8-dev)

- Restore missing optional, lazy and possessive captures in specialized full
  probes, including named and unset slots. Reuse the admitted native matcher at
  the selected full start under the original invocation allowance. Required
  capture work and owned storage can deny tighter budgets earlier; partial
  selection remains unchanged and retained snapshots stay valid.

## Unreleased PCRE2 shaping correction (0.4.0-scalar.7-dev)

- Accept finite PCRE2 result-byte policies through `ResultLimitInBytes` and report
  `ResultLimit`. Original invocation admission covers major capture/probe arrays,
  replacement staging and returned byte/string capacity cumulatively before
  allocation, without refunds. Preserve retained snapshots and independent calls;
  caller storage, compiled metadata, VM scratch and arbitrary retention are excluded.
  Shared buffer helpers borrow result charging; core's existing charges stay at
  their call sites. Remove the orphan unmetered single-slot detailed array.
- Meter specialized partial probes with their original work/heap/token/deadline,
  including bounded searches, coordinate projection and full result allocation.
  Preserve full/soft/hard scalar outcomes. Restore the missing full `(a)b+`
  capture and retain compiled names; this adds one group-data element to that
  snapshot. Cumulative result admission remains a separate gate.
- Keep literal-probe coordinate projection/full-result admission under the original
  work/heap/token/deadline. Non-partial probes reuse detailed capture storage and
  preserve names, MARK and native match options. Remove their unmetered projection
  and extra capture copy; partial ranges stay inline. Public result budgets and
  specialized partial-probe policy coverage remain unfinished.
- Execute admitted core-grammar scalar programs with the existing PCRE2 slice
  driver instead of independent core calls. Work/depth/heap controls, tokens and
  absolute deadlines now cover discovery, final captures, cursor advances and
  template/callback output together. Tight limits can reject earlier. Preserve
  capture aliases, balancing restoration and retained results; fix global empty
  retry to consider a nonempty match at the same position before scalar advance.
  Remove obsolete delegated cursor modes, rematching and writing helpers.
- Parse parenthesized comments as ignored trivia in existing literal, character
  and generic PCRE2 compilation. A comment no longer changes default ASCII
  shorthand through core delegation. Preserve declaration-order capture and
  template-admission controls that already worked.
  Keep original pattern coordinates, native algorithms and meters, quoted/class
  contexts, quantifier association and scalar empty advancement. Commented literals
  now fund the same endpoint projection as ordinary literals; tight limits can
  deny earlier. Broader profile options and probes retain separate qualification gates.
- Native detailed template replacement and explicit-option slice fallbacks now
  share discovery's work, heap, token and absolute deadline through output.
  No-match byte/string output is admitted before acquisition. String completion
  decodes the existing staging buffer directly, removing its extra final encoded
  byte root. Tight work/heap caps can reject earlier; published captures remain
  valid. Unlimited hits promote the existing invocation owner once; controlled
  calls reuse it instead of creating a second writing owner.
- Literal-driver ReplaceToString validates and decodes output using the original
  discovery budget, deadline and cancellation token. Admit the string while the
  existing byte output and range staging remain live; tight work/heap limits can
  now reject text publication that formerly escaped those limits. Byte replacement
  search, emission and allocations remain unchanged.
- Qualify finite result admission across capture/probe and output producers.
  Preserve both the accepted scalar.5 core/PythonRe feed and the private
  scalar.6-dev PCRE2/core pair under their immutable identities.

## Unreleased PCRE2 correction (0.4.0-scalar.6-dev)

- Native literal matching checks the original deadline at entry, existing
  search chunks and return, including short anchored/empty/miss results.
  Single-result endpoint projection borrows the same native budget through
  publication. Literal IsMatch candidate counts remain; Match/MatchDetailed
  now charge required endpoint work and can exhaust a tighter MatchLimit.
- Replace the real-clock 1-ms fast-path success assumption with controlled-clock
  success/expiry evidence across the same operations. Keep Timeout identity,
  original counters, bounded search, SIMD and retained alias/callback contracts.
- Preserve the qualified scalar.5 core/PythonRe feed. This private PCRE2/core
  development pair has an independent acceptance boundary; no new core/PythonRe
  candidate designation or public dependency update is implied.

## 0.4.0-scalar.5 core/PythonRe candidate

- Reuse the existing exclusive VM workspace across PythonRe scalar cursor moves
  instead of reacquiring heads/repeat/stack arrays per match. The runner resets
  attempts; owned snapshots and completion metadata stay independent. Release
  occurs at EOF, failure or disposal with no GC-based credits or new pool.
- Keep private scratch charged while positioned and during projection/host waits.
  Its overlap with publication can require a higher finite working cap. Resource
  admission, cumulative work/results, depth, cancellation and deadlines remain
  authoritative. The persistent cursor owner grows by 104 B in measured x64
  runs; first-result timings are platform dependent. Bulk captured output traffic
  drops substantially; no zero-overhead or all-path speedup claim is made.
- Preserve the qualified immutable scalar.4 input; this candidate requires
  its own package/handoff boundary before downstream designation.

## 0.4.0-scalar.4 core/PythonRe candidate

- Filter LTR scalar-VM searches with an initial mandatory beginning anchor to
  absolute origin or LF line starts. Scans use checked spans in chunks of at
  most 2048 bytes and the original invocation's work, cancellation and deadline
  authority. Match bodies, captures, empty progress and full-subject validation
  retain their scalar semantics. Other entry shapes, RTL and NonBacktracking
  retain their existing search. Fewer futile attempts can lower finite work
  requirements; work-step units remain route/version dependent.
- Preserve the immutable scalar.3 core/PythonRe integration candidate. This
  candidate identity requires its own package/platform/handoff qualification;
  it does not update downstream pins.

## 0.4.0-scalar.3 core/PythonRe candidate

- Preserve the scalar.2 matching, capture-completion, cursor, cancellation and
  finite invocation contracts under a new immutable package identity.
- Reuse the metered native counter for qualified exact-literal Count instead
  of renting unused VM capture slots. Work/validation/search share the original
  invocation; offset maps remain admitted. Route-dependent depth/working needs
  can be lower; other semantic shapes retain their qualified route.
- Replace two fixed-buffer unchecked construction reads with checked span
  reads that generate identical Release instructions. Hot vector-search and
  short-literal conversions remain deferred on measured cost/code-generation
  evidence; preserve SIMD, allocations and Unsafe.SizeOf<T>().
- Qualify exact core/PythonRe package bytes independently of PCRE2. Existing
  candidates remain unchanged; local designation does not publish packages or
  promote downstream dependencies. Common construction/live-retention policies,
  safe defaults and broader profile acceptance remain unfinished.

## 0.4.0-scalar.2 core/PythonRe candidate

- Accept positive finite invocation work, execution depth, modeled working
  capacity and cumulative result-capacity controls in core/PythonRe, individually
  or together. Validation, matching and required output shaping share one
  allowance; cursor advances do not reset work or the absolute deadline.
  Exhaustion reports a structured resource failure. Defaults remain unlimited.
- Provide a persistent PythonRe cursor with explicit disposal, deterministic
  EOF/failure cleanup and owned retained snapshots. Preserve Python capture
  numbering/backreferences and authoritative last-closed LastIndex/LastGroup,
  including nested captures, lookbehind and backtracking rollback.
- Simplify cache charges, construction accounting and cursor reservations:
  conservative per-program charges replace retained-object graph/refcount
  machinery; coarse helper charges replace retaining rollback descriptors;
  numeric cursor reservations replace finalizer leases. Major allocations,
  expansion/work/depth bounds, synchronous cancellation and scalar behavior remain.
- Qualify core/PythonRe as one immutable local feed independently of PCRE2.
  Candidate manifests identify exact package/DLL/source/SourceLink/PDB identities
  and detached consumers. Publication and downstream promotion are separate.
- Invocation byte policies cover modeled major capacity, not arbitrary caller
  retention, callback allocations or process RSS. Compilation/replacement
  preparation retain separate construction safeguards; public construction and
  live-retention controls and safe defaults remain unfinished. Historical
  benchmark snapshots do not qualify this candidate's current performance.

## 0.4.0-scalar.1 candidate

- Give scalar development a distinct prerelease package identity. Published 0.3.0
  retains its former UTF-16 contract; this candidate uses strict UTF-8 Unicode
  scalars with primary byte coordinates. A supplementary scalar is one dot match.
- Remove production BCL matching/result fallbacks, the UTF-16-unit engine and
  obsolete class projections. Captures, references, lookarounds, RTL and empty
  advancement follow the scalar contract; UTF-16 coordinates/string APIs are
  derived interoperability.
- Qualify one immutable local feed with exact package/DLL/source/SourceLink/PE/PDB
  identities and isolated consumers. Candidate qualification does not publish
  packages or constitute complete producer acceptance.
- Common finite construction/invocation/result/retention policies, remaining
  DFA/emission/preparation admission, whole cancellation coverage, full adapter
  acceptance and fresh comparative performance remain unfinished. Core finite
  public resource budgets remain explicitly unsupported.

## Development history

- Reuse the existing bounded native counter for finite-policy Count over
  prepared nonempty exact LTR literals, including ordinary ASCII literal
  captures. Validation/search/count share one original invocation. No VM
  capture slots or continuation scratch are rented; depth/working admission is
  route-dependent and offset maps remain separately charged. Folded/assertion,
  shared/explicit numbering, balancing and unprepared shapes retain their
  qualified route. Keep work/cancellation/deadlines and structured exhaustion.

- Replace two unchecked vector reads from fixed sixteen-byte construction
  buffers with `MemoryMarshal.Read<Vector128<byte>>`. Windows/Linux Release
  code generation is identical; allocation and the byte-set algorithm are
  unchanged. Keep the hot vector-search and short-literal reads after bounded
  checked-load pilots failed to establish equal cost; comments state their
  admitted bounds and the deferral. Expand unaligned literal-view/tail coverage.

- Admit all three owned capture projection producers under the shared
  construction budget: map descriptors, retained arrays, scratch span vectors,
  literal text and expansion entries. Work remains spent when a shape declines;
  failures restore parent loans and preserve cancellation/deadline authority.
  Replace recursive literal/optional-order helpers with admitted frames and
  bounded preference masks. Capture identities and scalar results are preserved.
  Broader search metadata, public resource policy and interoperability remain open.

- Remove the redundant BCL capture-slot translator and its five numbering-map
  reconstructions. Grouped-literal and alternation capture maps use the final
  identities assigned by the shared owned catalog. Their existing scalar
  program supplies shared, explicit-number and balancing eligibility before
  scratch allocation; these declarations keep the owned capture-history route.
  Named/numeric/nested/duplicate captures and complete scalar ranges remain
  qualified. The later projection-admission change covers those map producers.

- Replace the eight fixed executor SearchValues holders with immutable value
  tables sharing the owned byte-mask/SIMD kernel. Pattern-dependent sets retain
  their admission and descriptor ownership. The ASCII category table borrows
  128 embedded bytes; its runtime array/producer is removed. Email, identifier,
  operator and category scans preserve byte/scalar results across vector tails
  and scalar execution. This does not close public resource/default/retained
  policy or remaining preparation metadata.

- Dynamic-method emission now owns IL, label/fixup/local/token tables, dispatch
  scratch, stack analysis and wrappers under the shared construction budget.
  Structural class and kernel-lowering arrays use the same owner; borrowed
  literals keep their existing loans. Managed runtime copies and complete
  delegate state are admitted, including conservative serialized-IL instruction
  capacity. Failures restore parent loans, and cancellation/deadline checks
  retain the caller's authority through delegate publication. CLR/JIT metadata
  and complete public resource/default/retained policies remain outside this
  covered producer.

- Byte-safe DFA construction now admits graph storage and descriptors into the
  parent construction budget. Pending NFA and closure frames share depth, and
  graph/proof work remains spent after optimizer declines. Shared resource
  failures and interruption propagate; existing optimizer caps are unchanged.
  This does not complete emission admission or public finite resource policies.

- Development contract: core matching now targets Unicode scalars over strict UTF-8, with byte coordinates as primary. Exact .NET UTF-16 parity is superseded; implementation migration and removal of obsolete parity machinery are in progress. Published 0.3.0 behavior is unchanged by this documentation decision.

- Whole-segment getters on owned split views retain their prepared decoded-string loan after the view or cursor disappears. Decoded text and coordinate maps now have separate leases, so an escaped text alias does not retain unused coordinate storage. Weak alias attachments preserve the existing string identity and release after the last alias is collected. Independent substring results, getter/result limits and registry/object-overhead admission remain open.

- Owned mapped match cursors and split views now share payload leases. Ending one cursor copy leaves another copy's prepared subject and coordinate map charged and usable; abandoned holders release their loans through collection. Split views retain coordinate and decoded payloads after EOF or cancellation. Failed setup releases its own loans immediately. Controlled invocation copies share one aggregate storage ledger. Fixed object overhead, getter/result bounds, other routes and public finite core/Python policies remain open.

- Metered invocation copies now share cumulative work/depth counters from operation entry through lazy cursor advances. Copying a value match or split enumerator keeps its position independent without restarting the work allowance or deterministic cancellation cadence. Infinite `CancellationToken.None` calls without diagnostics retain inline counters unless a forkable cursor needs a shared storage owner. Object-overhead admission, getter/result bounds and public finite core/Python policies remain open.

- The PCRE2 profile rejects capture definitions whose names start with a decimal digit before selecting an execution backend. This corrects accidental .NET-style numeric capture admission and lost sparse capture results in 0.3.0. Ordinary numeric slots and backreferences retain PCRE2 semantics; the core .NET profile continues to support explicit numeric definitions.

- Owned split setup admits coordinate arrays, decoded UTF-16 payload and private unit buffers in one aggregate invocation. Decoding fills bounded scalar-aligned blocks and guards final publication; failed setup restores borrowed storage. Count-one splits retain an invocation holder, and cursor-backed tails and terminal moves observe its token/deadline. Map and decoded storage stay charged while split views need them. Getter/output admission, copied/abandoned ownership, remaining native/fallback setup and public finite policies remain open.

- Owned UTF-16-offset enumeration now starts validation, offset checks and boundary-map admission under one invocation. Qualified forward cursors receive the same work, token and deadline; temporary maps are released before native transfer. Empty, mixed byte-unsafe, surrogate-half and RTL starts retain unit execution with full anchor origin. Exact anchored prefix/suffix cursors use bounded comparison and projection, retaining their native selection. Unserved entry, split decoding, result/copy ownership and public finite policies remain open.

- Owned global match and split entry now start the invocation before bounded UTF-8 validation and boundary-map construction. Map array payloads join aggregate storage admission before allocation, and normal owned match cursors release them at EOF or failure. Mixed simple/structural transfers retain the entry invocation; split maps remain charged while tail projection needs them. UTF-16-offset entry, opaque split decoding, object overhead, abandoned/copied cursor ownership and public finite policies remain open.

- Remaining prepared ASCII simple-pattern cursors now share caller work accounting, deadlines and tokens through candidate scans, branch verification, projection and terminal moves. Branch order and full-subject anchors are retained; denied moves keep the previous result and scan floor. Mixed byte-unsafe and assertion-bearing subjects carry entry validation into the owned unit cursor. Upstream factory validation, mapping/storage admission, cursor copy ownership and public finite policies remain open.

- Deterministic ASCII match cursors carry one invocation through bounded candidate search, verification, incremental UTF-16 projection and terminal moves. Failed work admission retains the previous result and scan state; original deadlines and tokens remain active between moves. Mixed byte-unsafe subjects transfer the same entry invocation into the unit VM. General simple patterns, mapping/storage admission, copied cursor ownership and public finite policies remain open.

## Unreleased

### UTF-8 scalar migration (in progress)

- Deep core patterns now retain owned capture metadata and scalar execution
  beyond the former 64-level parser limit. Preparation walks native capture
  projections and Python scalar trees with explicit frames; the old shared
  nesting fallback gate is removed. Instruction-count admission, Python parsing
  and rendering recursion, general NonBacktracking, remaining BCL consumers and
  public finite construction policies remain migration work.

- Removed the separate ASCII invariant culture plan/runtime and replacement
  implementation, their cached state, facade dispatch and benchmark inspection
  hooks. Culture-sensitive construction now prepares one program. Admitted
  cell/URL families retain complete scalar captures; unserved NonBacktracking
  loops keep their original construction culture on BMP overlap instead of
  replaying through an invariant plan. General owned admission and remaining
  BCL execution still require migration.
- Admitted culture-sensitive and scoped IgnoreCase patterns now use the owned
  scalar program, with the construction-time I equivalence groups retained in
  classes, assertions and backreferences. Supplementary matches and captures
  remain complete scalars after ambient culture changes. Removed the culture-only
  BCL routing flag and its facade dispatch; prepared adapter operations use the
  same owned program. Invariant native kernels remain where eligible. Unserved
  grammar/admissions, general NonBacktracking and other BCL consumers stay open.
- Flat NonBacktracking graphs now construct and execute through the owned scalar
  program without creating a BCL regex. Native literal discovery remains where
  eligible; loops, shared capture declarations and unsupported constructs retain
  their transitional admission. Native planning now honors impossible atoms from
  the owned parser, so raw lone surrogates cannot become U+FFFD byte literals.
  Removed the obsolete surrogate-literal census and its core/PCRE2 selectors.
  General owned NonBacktracking execution and remaining BCL consumers stay open.
- Owned replacement templates now splice original UTF-8 capture/context ranges
  without a whole-subject UTF-16 map or decoded staging. String evaluators decode
  requested values lazily and encode each returned fragment independently;
  unpaired surrogates become U+FFFD, with no joining across substitutions or
  matches. String and byte results share the scalar output. Removed the separate
  UTF-16 output writer/expansion and half-literal route selector. Destination
  output copies the admitted byte buffer directly after sizing, retaining bounded
  cancellation and unchanged short destinations. Other BCL consumers, admissions
  and common public finite/output/result policies remain open.
- Removed decoded surrogate-half discovery/verification, unit branch-span maps,
  their projection/evaluator bridges and the unit simple-pattern cursor mode.
  Captured simple detailed results and contiguous-origin discovery now use the
  cached scalar VM; qualified native discovery remains available. Search and
  Count retain admitted attempt scratch within one
  call instead of reallocating it for each candidate, reset capture/repeat state
  between attempts and release it on success, failure or cancellation.
  Supplementary operation tests compare complete scalars and reject interior
  UTF-16 starts. Other BCL consumers, template output staging, unserved admissions
  and common public finite/result policies remain migration work.
- Owned boolean, value, detailed, cursor and replacement discovery share one
  scalar-aligned byte scan in both directions. Removed the separate UTF-16 scan
  drivers, unit outcomes/origins and duplicate capture conversion helpers.
  Boolean/value discovery avoids whole-subject coordinate maps. Detailed results
  and byte evaluator contexts decode requested values instead of the full subject;
  derived capture coordinates retain an admitted interoperability map. Capture
  history materialization now admits both scratch arrays and shrink-copy overlap,
  records the actual peak and releases unpublished storage on failure/cancellation.
  Decoded verifiers, UTF-16 template staging, other admissions and common public
  finite/result/getter policies remain migration work.
- Owned core Count scans scalar-aligned byte positions without a whole-subject
  UTF-16 coordinate map. Validation and discovery share its token/deadline.
  Empty-literal counters and cursors publish one match per scalar boundary;
  pure empty literals select the native runtime instead of a BCL counter.
  Removed the old unit Count driver, its map argument and the superseded Python
  empty-count exclusion. Unicode native literal controls remain for infinite,
  no-token calls; live tokens use the shared scalar runner. Other BCL consumers,
  UTF-16-offset entry costs and complete common resource policies remain open.
- Owned matching now traverses the original strict UTF-8 subject. Removed the
  private surrogate-half conversion buffer, its alternate boundary map and cursor
  storage loan, and the `RepeatGreedyUtf16` instruction. Repeats count atom
  iterations; empty scans cannot publish matches inside a scalar. Supplementary
  literal runs and their quantifiers lower to scalar instructions, including RTL.
- Explicit UTF-16 start APIs reject positions inside a supplementary scalar.
  UTF-16 result coordinates remain derived interoperability data.
- Owned categories, shorthands, class unions/subtraction and word boundaries
  classify complete Unicode scalars, including supplementary letters and digits.
  Range complements retain negation above U+FFFF. Removed the surrogate verdict
  helper, half census, astral input fences and whole-loop UTF-16 parity proofs.
  Lookbehind byte bounds include four-byte literals and Unicode class members.
- Supplementary class literals/ranges, unions, subtraction, invariant folding
  and paired escapes now use scalar-valued endpoints. Singleline dot has a
  distinct full-plane payload from an explicit BMP-wide class. Unpaired surrogate
  atoms cannot match UTF-8 or join across groups/separate classes. Case-insensitive
  backreferences compare complete scalars in both directions with each side's
  UTF-8 width, sharing the established invariant relation with literal atoms.
- Direct scalar operation, capture, replacement, cancellation and scratch-admission
  regressions replace the retired unit-subject suite. Wider class construction admission,
  PythonRe's UTF-16 compensation patterns, remaining BCL/unit routes and the wider
  parity test migration are unfinished. This is not full scalar qualification.
- PythonRe now renders native scalar atoms/ranges separately from its remaining
  managed callers. Direct supplementary classes, category complements and word
  boundaries use the owned core; quantified dots keep newline scope on every
  scalar, and separate Python surrogate escapes cannot combine into an atom.
  Empty-capable Count uses Python's scalar retry/advance rule.
- PythonRe now lowers lexical scalar classes and reference policies from its own
  AST to the shared UTF-8 VM. Owned CPython 3.13.2 / Unicode 15.1.0 case data
  separates extra literal partners from simple-lower reference comparisons.
  Reactivated all four case-folding regressions. Byte-start one-shot discovery,
  detailed snapshots and full-match backtracking retain the original subject,
  deadline and token. Deleted the capture-specific ASCII reference expansion,
  host/BMP casing compensation and decoded ASCII-boundary Count/Split drivers.
  Valid native literal/prefix/dot-all controls remain; unserved BCL callers and
  complete finite/result/output policies remain migration work.

- Admitted PythonRe scalar programs now discover captured/global results,
  substitute and split over the original UTF-8 bytes. One invocation preserves
  the timeout/token across discovery and callback boundaries; captures retain
  Python numbering, last-capture semantics and normalized lookbehind ranges.
  Deleted Count's UTF-16 scan/map driver. Remaining unserved BCL
  routes, finite/result/output policies and compensation cleanup stay open.

### Historical UTF-16 parity work (superseded)

- ASCII structural-family enumeration, offset cursors and capture-free splits use
  the shared owned candidate/verifier cursor. It retains the validation invocation,
  pending search state and deadline across matches; failed moves publish neither
  progress nor a new result, and terminal replay keeps checking the same token and
  deadline. Overlapping candidate starts retain native progress; shared leading
  prefixes and mixed subjects retain the owned unit VM under the caller invocation.
  Unit cursors also guard each move and final publication, including short attempts
  and terminal calls that do not reach the VM's regular polling cadence.
  Boundary-map acquisition, output/result storage and other
  native families still need their complete resource and cancellation gates.

- Ordered-window value operations and enumeration retain one invocation through
  UTF-8 validation, bounded leading/trailing search, separator and line checks,
  and projection. Denied cursor work leaves the previous result and progression
  intact. Count advances through the same greedy ranges as Match, correcting
  family overcounts. Separator plans preserve their whitespace set and laziness;
  separator newlines remain valid before dot begins, trailing newlines belong to
  the literal, and complete overlapping branches retry
  at the same start. Mixed subjects retain UTF-16 and Unicode unit semantics under
  the caller invocation. These fix fork routing defects against .NET 10; aggregate
  mapping/storage bounds, other native families and public finite policies remain
  open.

- Metered structural candidates share invocation state through prepared searches,
  fixed-distance guards, start transforms and paired windows. Structural family
  value operations retain the validation deadline and token through candidate
  rejection and bounded suffix verification. Mixed UTF-8 subjects and overlapping
  prefixes retain the owned unit verifier under that invocation. Boundary-map
  acquisition, other structural families and common finite policies remain open.
  Cached structural prefix-overlap facts preserve retries such as
  `(?:foo|foobar)[A-Z][A-Za-z0-9_]*` matching `foobarAlpha`, including unlimited
  operations. Live-token replacement templates and UTF-8 destination fast paths
  retain the admitted owned program rather than bypassing it through native/BCL
  splicers. This fixes a fork routing defect; .NET 10 matches the longer branch.
  Unlimited structural family operations also retain unit semantics on mixed
  subjects, preserving UTF-16 offsets after non-ASCII prefixes and Unicode
  whitespace matching (for example, `LogError\u2003(`).

- Native single-literal IsMatch, Count and Match share one invocation from bounded
  UTF-8 validation through search and coordinate projection. Tokens and deadlines
  survive all phases, and denied Count work publishes no partial total. Empty
  literals and capture-sensitive or non-native folding shapes retain their owned
  semantic routes. Public finite-policy accounting remains open.

- Native literal-family enumeration retains one invocation through bounded branch
  search, full-subject confirmation and projection. Deadline-aware family Count,
  IsMatch and Match also use bounded scans; unlimited prepared-search lanes remain.
  Tokens and cumulative work survive successive matches, and cursor state is
  published only after successful projection. Public finite-policy routing and
  other native families remain open.

- Native single-literal cursors share one invocation through search, bounded
  comparisons, boundary confirmation and projection, including successive matches.
  Boundary assertions inspect the original subject in both directions. Token-aware
  literal enumeration also uses this path through bounded UTF-8 validation; other
  native families and complete public finite-policy accounting remain open.

- Loopless native literal, literal-family and boundary-wrapped literal execution
  also serves `NonBacktracking`, with the bounded unit verifier for offsets and
  results. Loops and shared capture declarations retain their migration route.
  Eager BCL constructor validation remains an explicit dependency.

- Empty native literals replace through owned UTF-16 output on every template
  surface, including UTF-8 destinations, preserving fragments between surrogate
  halves before encoding the complete result.

- Native match projection retains its cursor or operation deadline through UTF-8
  gap and match-length validation. Metered validation uses bounded scalar-aligned
  blocks, preserves invalid-input offsets, and checks before publishing results.
  The shared validator accepts the caller invocation for work, token and deadline
  checks; complete native scan and public policy accounting remain open.

- ASCII branch lowering uses explicit continuations while preserving its existing
  depth boundary, branch order, finite-repeat greediness and assertion offsets.
  Literal materialization and branch payload copies obey the speculative work
  and output budgets before acquisition. Declined optimizations retain other
  owned execution routes; full constructor storage accounting remains open.

- ASCII structural verification retains the caller deadline and shares its
  invocation through suffix helpers. Long separators, identifier tails, literal
  comparisons and suffix candidate scans poll work, cancellation and expiry;
  final acceptance checks before publishing its length. The verifier borrows
  prepared storage. Other native callers and common finite policies remain open.

- External lookaround programs run on explicit assertion continuations instead
  of recursive calls. Child repeat arrays and continuation growth share the
  invocation ledger; positive captures commit and other exits roll them back.
  Failed or canceled attempts clear unpublished capture slots. Stack-table
  growth checks aggregate admission before allocating its replacement array.
  Public depth admission and full call-wide resource-policy work remain open.

- Lazy-DFA compilation walks optional transitions and emits nested NFA programs
  without CLR recursion. Speculative construction admits continuation data and
  enforces its existing state/transition caps before extending graphs, including
  deterministic routes. Oversized optimizations retain the owned linear engine.
  Complete public construction storage accounting remains open.

- Byte-safe linear and DFA programs retain individual absolute, contiguous,
  line and strict/final-newline anchor facts. DFA matching keeps the last valid
  accepting position when a greedy suffix fails later, checks shared work/token/
  deadline during execution, and retains the structural caller's deadline.
  Public depth admission and remaining call-wide resource-policy work are unchanged.

- Nested byte-safe linear programs now execute with explicit loop frames and
  aggregate storage admission. Failed body trials discard their partial input;
  projected Unicode compatibility requests propagate through optional parents.
  Long atoms and continuations poll the shared work/token/deadline, and structural
  calls retain the original deadline. Reference-bearing small loans clear their
  slots before bounded retention. Public depth admission remains unchanged;
  full call-wide accounting and the other remaining engines are still open.

- Expanded ASCII branch verification retains the prepared final-newline fact
  for absolute `$`/`\Z`, so native replacement finds anchored hits before a
  final LF. Strict `\z` and multiline line-end semantics remain distinct.

- General byte-safe prefix matching now walks paired instructions with explicit
  active-node versions and saved choices. Enclosing suffixes can reconsider
  group-internal alternatives and repeats; lazy capture growth no longer reuses
  rolled-back future checkpoints. Absolute, contiguous and final-newline anchors
  retain their individual .NET meanings under multiline options.

- General matching accounts continuation versions, choices and capture mutations
  in one invocation ledger, admitting exact extents and growth overlap before
  allocation. Buffer work and rollback poll the shared token/deadline. Exact
  small-buffer pools cap retained data/table bytes at 16 KiB per thread and type;
  finite-storage calls do not add retained pool storage. Failure returns every
  loan and clears captures. The remaining native, construction, result and
  common finite-policy work is still open; public depth admission is unchanged.

- Fixed deterministic capture programs cache their final capture layout during
  preparation and match paired instructions iteratively. Matching borrows the
  prepared layout and caller slots without a runtime continuation stack or
  projection allocation. Slot clearing and long literals use bounded checks;
  failures clear unpublished captures and retain the caller's deadline.

- Primary matching and match enumeration with a live cancellation token now
  use the cached owned unit program where admitted. Cancellation reaches the
  matching loop on native-pattern families while calls without tokens retain
  their existing native selection. Detailed matching with a token now maps an
  expired owned deadline to the public RegexMatchTimeoutException contract.
  Unadmitted and culture-sensitive paths still
  require the remaining ownership and cancellation work.

- The byte-safe linear compiler now suspends node, sequence and nested-loop
  program construction on explicit frames. Fixed-text, optional-alternation,
  leading-mask and checked program-width walks are iterative; completed
  subprograms share width facts. Speculative failure diagnostics and partial
  append behavior remain unchanged. Compact childless optional loops no
  longer probe themselves recursively. Public depth admission remains while
  runtime nested-program calls and other consumers are still being converted.

- Absolutely anchored simple patterns that would force BCL verification now
  use their cached owned unit program. Qualified direct/native and line paths
  retain their selection. Bounded overlapping optional loops preserve complete
  operations, captures, RTL, newline edges and supplementary-input behavior.

- Tree-to-VM conversion and instruction emission now use explicit frames,
  including suspended compilation of nested assertion programs. Shared
  construction facts use reference identity for record nodes and retain
  empty-match, capture-write and saturating byte-width semantics. Direction,
  instruction order, jump patching and assertion metadata remain unchanged.
  The existing depth boundaries remain while other construction and execution
  consumers are still recursive.

- Search analysis and deterministic prefilter preparation now traverse with
  explicit frames, including required-literal collection, quoted runs, finite
  windows, prefix guards, literal families and byte masks. Nested prefixes
  retain their local stopping rules, branch order and existing width/line
  overflow behavior. The pattern-depth boundary remains while VM and specialized
  projection consumers are still recursive.

- Owned find facts now compute lengths, anchors, prefixes, and fixed-distance
  literals and sets with explicit traversal frames. Nested sequences retain
  their product order, stopped-branch behavior and saturating length formulas.
  Scoped-case scanning and execution-tree/instruction preparation are also
  iterative, retaining child order, payloads and paired enter/exit positions.
  The existing pattern-depth boundary remains until all remaining preparation
  and VM compilation passes can handle deeper input.

- General groups, assertions and conditionals now parse with explicit
  continuations. Capture/reference normalization, per-node tree analysis,
  feature collection and native literal-family planning also use iterative
  traversal. The existing grammar-depth boundary remains while downstream
  search-fact derivation and VM compilation still use recursive passes.

- Nested class subtraction now parses iteratively and constructs its flat
  predicate chain once, retaining each segment's negation and case rules.
  Class nesting is independent of the remaining general-group depth limit.
  Deep valid classes retain their semantics without reproducing .NET 10's
  worker-stack overflow in its recursive character-class parser.
  Pattern length above 65,536 characters no longer forces BCL routing;
  admitted long patterns retain owned matching and result operations.

- Admitted ECMAScript patterns now use the shared owned parser and VM for matching,
  histories, offsets, enumeration, splitting and replacement. The profile keeps
  its fixed shorthands and boundaries, empty unmatched references, declaration
  order for numeric binding, shortened octal escapes and identity escapes.
  Decimal scanning retains .NET's interpreted capture-ceiling and overflow
  consumption quirks. Unbraced replacement numbers bind their longest valid
  group prefix; ordinary .NET and braced-reference behavior remain unchanged.

- VM compilation now lowers the authoritative owned tree, retaining scoped
  whitespace, case folding, capture identities and assertion direction. The
  duplicate compatibility parser and its unresolved VM nodes are removed.
  The canonical grammar now also covers surrogate and U+FFFF class units,
  octal range bounds, lookbehind references, explicit expression conditionals,
  uppercase/plus option clusters and balancing pops under ExplicitCapture.
  Class ranges ending at U+FFFF retain the final UTF-16 unit without overflow;
  folded range unions are sorted and overlap-normalized. Explicit conditional
  tests preserve .NET's transparent unnamed grouping and metadata numbering.
  Scoped verbose patterns now stay owned across matching, capture histories,
  enumeration, splitting and replacement, including the Python-compatible
  adapter. Kelvin folding also stays owned through those result operations.
  Pattern whitespace follows .NET's ASCII set; vertical tab and Unicode space
  characters remain literals. Raw group names retain construction failures,
  incomplete whitespace-containing braces remain literals, class octal escapes
  wrap to a byte, and undefined-name conditional expressions retain direction.

- POSIX-looking classes now execute with .NET's literal-member semantics in
  the owned VM, including literal closing brackets, captures, subtraction and
  reverse execution. PCRE2 keeps its own profile programs instead of receiving
  the .NET literal-member interpretation.
  Native shapes without complete detailed capture projection now use their
  cached owned VM. UTF-16 evaluator replacements also use that program,
  retaining capture histories and right-to-left callback order.

- Lookbehind bodies now execute backward from their exact assertion edge,
  while lookahead bodies execute forward, including nested assertions in both
  ordinary and right-to-left patterns. Assertions share capture openings,
  histories, balancing nodes and rollback with their caller. Numeric and named
  references, conditional tests and balancing groups inside lookbehind now run
  owned. Reference reads normalize captures from either execution direction;
  balancing sources remain available through their bodies and pop at successful
  close, with interval captures using the completed body's span. Assertion entries
  count toward the shared depth budget, and scratch state releases on failure,
  limit breach and cancellation. PCRE2 retains its own forward-lookbehind
  program where order dependencies differ and normalizes shared capture spans.

- Multi-digit bare numeric escapes now bind against the complete capture table
  before octal fallback in owned execution. Octal prefixes wrap to one byte and
  retain their remaining literal digits, including the correct trailing-quantifier
  boundary. Group-zero references and balancing pops fail within the pattern.
  The front end now also preserves these octal values, discarded-quantifier
  prefixes and leading-zero group-zero references, correcting previous parse
  facts to .NET 10 behavior. Oversized decimal references retain construction errors.
  Admitted ASCII and Unicode UTF-16 start operations use the cached owned program;
  split enumeration now keeps an owned cursor for count zero and live tokens.
  Count-one splits retain the whole-input `Regex.Split` result, avoiding the
  .NET 10.0.12 split enumerator's negative-position defect.
  Native detailed contexts accept dense leading-zero numeric group names.
  Remaining admitted template fallbacks use the owned program and operation token.
  Native ASCII class runs now project byte positions after Unicode prefixes into
  their correct UTF-16 coordinates.

- Capture-writing finite lazy ranges admitted by the parser now run in the owned VM in both ordinary
  and compiled modes. Both follow .NET 10 interpreted abandonment and capture
  rollback, deliberately avoiding .NET 10.0.12 compiled-engine phantom progress,
  leaked captures and non-progressing enumeration. This also applies when a
  native plan exists; unrelated qualified native routes remain available.
  Owned reverse conditional/capture-reference loops also retain interpreted
  positions, avoiding a diagnosed .NET 10.0.12 compiled-engine range mismatch
  after a later match. The concrete witness is recorded in code and tests.

- Explicitly numbered and sparse captures now execute in the owned VM with
  dense internal slots and their original public numbers, including complete
  histories, numeric references, conditions, balancing, and every replacement
  surface. Forward and self-balancing targets bind against the complete catalog.
  Leading-zero capture spellings alias an existing number without declaring
  another group. Undefined numeric replacement references keep their original
  spelling. PCRE2 retains its separate positional capture contract.

- Named backreferences, forward references, and duplicate named captures now use
  owned execution. The front end and VM parser share capture numbering; opening
  positions and completed captures remain separate through nesting, backtracking,
  balancing and capture-history projection. Reference comparison observes the
  shared invocation checkpoints. Public group-name queries retain .NET's exact
  distinction between aliases and numeric references, including sparse metadata;
  returned metadata arrays are independent copies. PCRE2 keeps distinct slots for
  duplicate names through its own profile programs.

- Character-class subtraction, including nested subtraction, now runs in the
  owned VM. Each class applies its own folding and negation before subtraction;
  matching retains UTF-16 unit semantics, shares invocation cancellation and
  charges work for each class segment. Control escapes also work as class members
  and range endpoints. Existing literal, shorthand and category routes remain.
- Owned pattern atoms now include raw and escaped BMP characters, literal
  surrogate halves, and UTF-16 character-class members/ranges. Quantifiers bind
  to individual pattern units, including the low half of a supplementary literal;
  captures and replacements retain those units. Unicode classes reuse owned
  range and invariant case-equivalence tables while retaining compact ASCII masks.
  Unicode group names and escaped punctuation are also admitted. Python scalar
  translations now use the admitted unit subject during owned Count, preserving
  scalar advancement and empty/nonempty adjacency. Qualified native BMP literal
  lanes and the Cyrillic Count kernel remain available. For greedy class loops
  ending at U+FFFF, both owned lanes follow .NET 10 interpreted semantics;
  they deliberately avoid .NET 10.0.12 compiled-engine false misses and invalid
  position exceptions. Other supported syntax still requires migration work.
- The owned slice VM now handles supplementary characters as .NET UTF-16 code
  units for matching, complete capture histories, enumeration, and splits,
  including starts inside surrogate pairs. Byte ranges still refer to the
  original UTF-8 input and remain unavailable for split scalars. Conversion
  admits its storage before allocation and observes cancellation and deadlines.
  Non-ASCII ordered-window subjects also use this owned execution path.
  For patterns admitted by the slice, template replacement and UTF-16 evaluators
  use the same owned unit execution and retain lone surrogate halves until the
  finished result is encoded. This
  preserves pairs formed across captures, template literals, and original gaps.
  Right-to-left callbacks run in discovery order; output growth, copying, and
  encoding share the invocation deadline and cancellation token. Unsupported
  syntax still has migration work remaining; public input must still be
  well-formed UTF-8.
- An undersized replacement destination remains untouched. Cancellation or an
  expired deadline during the final copy may leave a written prefix; owned
  destination copying checks both between bounded blocks and releases its
  temporary storage on failure.
- UTF-8 writer callbacks now use the owned unit program where available,
  preserving complete captures and the whole-match scalar-alignment requirement.
  Raw bytes are validated after the complete output is assembled, so fragments
  can form a UTF-8 sequence across callbacks. Writer copies share their buffer;
  resetting the writer clears accumulated output. Scope, buffer, growth overlap,
  and result storage are admitted, with cancellation/deadline checks during
  append, reordering, validation, and copying. Callback-local discarded buffers
  are reclaimed at the callback boundary; nested operations restore the outer
  invocation. Supported right-to-left callbacks now follow oracle encounter
  order and produce correctly ordered output instead of slicing gaps forward.

### Parser ownership

- The regex front end is now fully owned: the previous `System.Text.RegularExpressions`
  parser fork (`Internal/FrontEnd/Runtime`) was removed by forward deletion, and the
  owned parser (`Utf8OwnedTreeParser`) is the sole parse-tree source. No Microsoft
  regex source is vendored anywhere; BCL `Regex` remains the semantic oracle for
  behavior and the compatibility fallback for declined shapes.
- Conformance bug fix: `(?n:(?<-x>a))` (balancing pop of undefined group `x`),
  `(?<=(?<1a>b))c` (invalid capture name `1a`), and the class escapes `[\\A]+`,
  `[\\z]+`, `[\\B]+` (anchor escapes BCL rejects inside classes) were accidentally
  accepted by the old fork and are now rejected at construction with the identical
  .NET 10 `RegexParseException` (error and offset included), like every other
  invalid pattern. Valid named/numeric groups and balancing pops keep constructing
  unchanged. The expression-conditional `(?(?=a)b|c)` still matches BCL-exactly
  through the fallback, but its inspection reason is now the generic
  `unsupported_pattern`: the owned tree parser declines expression conditionals,
  so no tree features refine the taxonomy.

### Capture conformance

- Conformance bug fix: named groups under ExplicitCapture (for example
  (?<x>ab)(?<y>cd)) were lowered as non-capturing, so owned replacement
  resolved $1/${x} to empty while .NET fills them (<> on xxabcdxx
  gives xx<abcd>xx). Named and numbered definitions now capture exactly like
  BCL; only unnamed groups go slotless under ExplicitCapture. Unnamed-group
  suppression, numbering, metadata, and the balancing-pop construction errors
  are unchanged.

### Character-class conformance

- Conformance bug fix: negated shorthands and properties (`\D`, `\W`,
  `[^\s]`, `\P{...}`) could match characters their .NET meaning excludes.
  Most visibly, ordered-literal windows admitted `\D` and `[^\s]` gaps as
  any-text and reported hits where .NET misses (`a\D{2,5}b` on `xa12b`).
  Decoding now matches the observed shorthand wire bytes with oracle parity
  across the category inventory on both lanes (`\P{Nd}` shares `\D` bytes),
  with regression batteries pinning the window decline and the decoder.

## 0.3.0 - 2026-08-28

This release completes and qualifies the selected managed PCRE2 profile while
preserving the public API and .NET 10 semantic contract of `Lokad.Utf8Regex`.

### Managed PCRE2 Profile

- Completed the generic managed execution of branching, repetition, captures,
  backreferences, assertions, scoped options, branch-reset groups, duplicate
  names, recursion and subroutines, conditionals, atomic and backtracking
  controls, `\K`, the selected Unicode atoms, and opted-in `\C` behavior.
- Completed `IsMatch`, `Match`, detailed matches, count, enumeration,
  `MatchMany`, partial probing, and PCRE2 substitution for the selected
  profile.
- Aligned global empty-match progress, reported-versus-consumed `\K` ranges,
  duplicate-name capture lookup, recursion, backtracking controls, and
  replacement behavior with the selected PCRE2 10.47 standard-matcher
  semantics.
- Made unsupported operations and constructs fail explicitly instead of
  falling through to a similar-looking .NET behavior.
- Kept the companion fully managed: it has no native PCRE2 binary, P/Invoke,
  RID-specific asset, external matcher, or non-BCL dependency other than
  `Lokad.Utf8Regex`.

### Core Engine

- Preserved the existing `Lokad.Utf8Regex` public API and its .NET 10 `Regex`
  semantics.
- Reduced repeated UTF-8 validation, coordinate projection, candidate scans,
  match materialization, and replacement assembly across matching, counting,
  enumeration, and replacement paths.
- Improved byte-offset execution, scalar-boundary handling, timeout polling,
  pooled backtracking state, capture rollback, and transactional destination
  writes.
- Kept PCRE2 syntax, options, captures, errors, and replacement rules outside
  the .NET-compatible core.

### Performance And Reliability

- Performance is materially ahead of 0.2.0: affected hot paths are commonly
  **2–10× faster**, with gains approaching **100×** where former fallback,
  repeated-validation, or result-projection layers were eliminated. Untouched
  or fallback-heavy paths may remain flat.
- Across the current semantically matched benchmark catalogs, the case-median
  speedups are approximately **3× versus predecoded .NET Regex** [1.4× versus
  compiled; 5×/3.4× when per-operation UTF-8 decoding is included], **4× versus
  PCRE.NET/native PCRE2 NFA** [JIT disabled], and **2.4× versus predecoded
  CPython `re`/`_sre` public operations**. These are directional workload
  summaries, not per-pattern guarantees.
- Added reproducible core, PCRE2, and PythonRe comparative snapshots with
  source/runtime provenance, bounded warmup, per-case checkpointing, and
  adversarial 1×/2×/4×/8× scaling coverage.
- Reused prepared core literal-family search for compatible PCRE2 value
  operations and added AST-certified required literals, leading-byte sets,
  and bounded literal windows before PCRE2 verification.
- Added direct global counting for exact greedy excluded-ASCII repeats,
  removing the former multi-millisecond PCRE2 `[^\n]*` path while preserving
  the generic runner for metered and semantically richer cases.
- Extended PCRE2 required-literal candidate search to unbounded leading runs
  that can also consume the delimiter, while retaining PCRE2 backtracking as
  the final matcher.
- Added a shared zero-allocation core split cursor for eligible capture-free
  patterns and applied it consistently to ordinary and compiled execution.
- Shared immutable prepared data across copied ignore-case literal-family
  plans, correlated rejected candidates with AVX2/SSE/scalar prefilters, and
  reused the same prepared ranges and emitted kernels during global ordinary
  and compiled execution.
- Added guarded vector counting for dense deterministic ASCII patterns of
  width three through eight. Ordinary and compiled `Count` share the same
  selector and retain scalar execution for sparse, short, finite-timeout, and
  unsupported-hardware cases.
- Used direct byte search for eligible exact scalar matches, literal
  boundaries, trailing literals, and composite literal lookahead, avoiding a
  full enumerator or verifier pass when the requested result is only a boolean
  or first match.
- Admitted ASCII inputs to native simple-pattern plans for Unicode-category
  intersections, with explicit Unicode fallback guards across matching and
  output operations.
- Decode non-ASCII case-insensitive fallback subjects once for capture-free
  core `Count` and `IsMatch` operations that cannot use native byte matching.
- Removed PythonRe's quadratic pure-zero-width suffix probes and reranked
  non-empty replacement through its translated managed regex after the new
  comparative catalog exposed both losses.
- Made PythonRe reject malformed UTF-8 patterns and subjects consistently
  across direct and managed routes, including byte starts inside a scalar.
- Corrected invariant ignore-case ASCII literal and literal-family matching
  for the Kelvin sign (`U+212A`), including matching, enumeration, splitting,
  and replacement, without penalizing subjects that do not contain it.
- Routed PythonRe full-match patterns that would use the core's managed
  fallback directly through the already-prepared PythonRe regex, avoiding a
  duplicate fallback layer and scalar match projection.
- Materialized capture-free PythonRe find-all string and UTF-8 results from
  one shared core range pass, removing an intermediate match-data array and
  discarded string decoding.
- Reduced PythonRe result shaping with pooled capture-free ranges, direct
  captured/detailed projection, lazy full-match preparation, evaluator output
  writers, and late UTF-8 coordinate-map construction; required public result
  storage remains explicit.
- Removed copied search-plan and structural-program state from core
  enumeration, projected eligible ASCII fallback and right-to-left matches
  directly, and reused already-validated subject lengths in exact UTF-8 split
  output.
- Removed preventable temporary allocation from warmed non-result PCRE2
  operations and bounded pooled invocation state and replacement-plan caches.
- Reused prepared literal-family ranges for PCRE2 global/output operations,
  pruned leading word-boundary candidates, and projected final capture slots
  without redundant endpoint arrays or public match materialization.
- Kept candidate search, Unicode coordinate projection, zero-width progress,
  capture rollback, and replacement growth linear across the qualified
  scaling workloads.
- Improved failure-path cleanup, concurrent compiled-regex reuse, resource
  limits, and deterministic return of pooled buffers.
- Retained and documented the remaining compatible and predecoded-baseline
  gaps; this release does not claim general throughput parity with
  predecoded `.NET Regex`.

### Compatibility Boundaries

- `Probe` remains a curated partial-match surface rather than a promise that
  every normally matchable pattern supports partial matching.
- The DFA matcher, invalid-UTF subject mode, callouts, split, full capture
  history, and substitution forms outside the selected profile remain out of
  scope.
- Opted-in `\C` can report byte ranges that split a UTF-8 scalar, and
  lookaround `\K` can report non-monotone ranges; operations that cannot make
  safe forward progress reject those results explicitly.

## 0.2.0 - 2026-04-29

This release aligned the core and optional regex-flavor packages at version
0.2.0 and substantially broadened UTF-8-native execution coverage.

### Packages And APIs

- Added the optional `Lokad.Utf8Regex.PythonRe` package with a parser-first,
  managed CPython-`re`-style surface over UTF-8 inputs.
- Expanded the optional `Lokad.Utf8Regex.Pcre2` package and its managed PCRE2
  compatibility surface.
- Added host-facing PythonRe search, detailed-match, find-all, iteration, and
  substitution APIs while keeping Python-specific result types separate from
  the core and PCRE2 surfaces.
- Clarified the relationship between UTF-16-compatible coordinates and
  byte-aligned match coordinates in the core API.

### Execution And Performance

- Broadened native UTF-8 eligibility and fallback coverage for the core and
  PythonRe profiles.
- Added and refined structural, literal-family, trailing-literal, and
  candidate-search optimizations for common matching and counting workloads.

## 0.1.0 - 2026-04-13

Initial preview release of `Lokad.Utf8Regex` for .NET 10, with a UTF-8
`ReadOnlySpan<byte>` / `Span<byte>` surface and
`System.Text.RegularExpressions.Regex` as its semantic reference.
