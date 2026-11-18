# Release notes

## Unreleased — 0.5.0

- Remove PCRE2 MatchDetailed and its duplicate invocation wrapper. Use primary
  caller-owned capture ranges or persistent cursor snapshots; actual partial
  probes and their metadata remain supported. Recompile consumers.

- Preserve PCRE2 MARK on primary Match results, including misses and short caller
  capture output, without another search or result array. Remove the redundant
  regex-level TryGetFirstSetGroup matcher; use capture slots or cursor metadata.

- Unify PCRE2 construction/funding on Utf8RegexPolicy and disposable Utf8Pcre2Regex;
  remove Create, long Compile families and the program-owner facade. Preserve
  admitted calls/persistent cursors; funded span enumeration borrows an open
  producer. Shared working defaults inherit program funding. Recompile consumers.

- Fold Core program funding into disposable Utf8Regex and immutable Utf8RegexPolicy;
  remove the separate program-owner type. Preserve admitted calls/persistent
  cursors after close. Funded span enumerators borrow an open regex. Prepay bounded
  replacement-cache capacity before publication; recompile and migrate controls.

- Add optional caller-owned PCRE2 capture slots to the primary Match signature
  and funded expressions. Share existing capture publication without a result array;
  preserve byte-fragment status, limits and lifetimes. Name former positional
  match flags/tokens and recompile consumers.

- Consolidate PCRE2 IsMatch/Count onto one optional shared working allowance.
  Null inherits program funding, or keeps numeric controls when unfunded; admission remains
  terminal on refusal and cannot bypass through an unmetered literal shortcut.

- Unify POSIX creation on Utf8RegexPolicy, including construction controls and
  program funding. Funded expressions close new operations on Dispose while
  admitted calls/cursors retain storage. Consolidate Match and cursor MoveNext
  onto one signature each; recompile callers and migrate positional arguments.

- Simplify PythonRe constructors/Compile to one immutable Utf8RegexPolicy for
  execution, construction and program funding. Compile keeps a separate token;
  funded constructors prepare retained programs eagerly. Recompile callers and
  move legacy control arguments into policy properties.

- Unify PythonRe value matching on Match with explicit Search/Match/FullMatch mode,
  optional caller-owned capture slots, byte end bound and shared working admission.
  Search/FullMatch methods are removed; recompile callers and name positional tokens.
  Closing-order metadata remains on capture cursors.

- Replace PythonRe detailed/context APIs with capture cursor Search/Match/FullMatch
  modes and optional scalar-aligned byte end bounds. Preserve Python closing-order
  metadata, immutable snapshots and borrowed-input lifetimes. Null cursor working
  admission now inherits program funding/numeric limits; recompile consumers.

- Fix escaped literal punctuation as character-class range endpoints, including
  the backslash member of the escaped bracket range. Preserve escaped-hyphen and
  shorthand/subtraction rules, scalar byte ranges and original cancellation.

- Consolidate Core operation signatures, retaining native/token-aware workers.
  IsMatch/Count accept optional shared working admission; null uses numeric engine
  limits and non-null refusal remains terminal. Name positional enumeration tokens
  and recompile old binaries.

- Unify Core Match with optional caller-owned capture slots and direction-aware
  byte starts. Preserve whole-match fast paths; recompile old callers and name
  positional cancellation tokens.

- Consolidate PCRE2 operation overloads onto optional token-bearing signatures;
  retain unmetered literal shortcuts and controlled paths. Recompile old callers
  and name TryReplace's former options-only argument `substitutionOptions:`.

- Consolidate PythonRe short operation overloads onto existing optional
  token-bearing signatures. Recompile old callers and adapt shorter method-group
  delegates; preserve cancellation, funding, scalar and byte-output controls.

- Consolidate constructors to one optional signature per supported pattern
  representation. Recompile old callers; a bare default timeout now inherits the
  profile default, while an explicitly zero TimeSpan remains invalid. Preserve
  construction/resource controls and remove obsolete validation switches.

- Remove PythonRe's owned-text detailed Data APIs, FindIterDetailed and their two
  text carriers. Borrowed capture contexts and range cursors remain. Host code
  owns decoding/collections; obsolete invocation string sizing and decoding are
  removed without changing byte-result admission or matching controls.

- Remove PythonRe's cursor text Current and no-output MoveNext overloads. Advance
  into byte ranges with an optional token; retained ranges and cumulative controls
  remain. Hosts own text decoding and keep borrowed subjects unchanged while read.

- Remove PythonRe's legacy structural FindAll collector and Utf8PythonMatchData.
  Use range cursors for whole-match metadata and FindAllToUtf8 for Python-shaped
  byte values. Host aggregates and decoding remain outside regex controls.

- Remove PCRE2 callback replacement, evaluator delegates and Core's former shared
  callback writer. Hosts compute custom output over range cursors. Byte templates
  retain their controls and substitution behavior; obsolete thread-local writer
  discovery and buffer text-conversion helpers are removed.

- Remove PCRE2 template-to-string replacement and its text-only accounting.
  Byte-template replacement and destination output retain their controls; hosts
  decode valid output explicitly.

- Remove PCRE2 standalone value/group/match string getters. Byte views and slice
  status remain; hosts decode valid slices explicitly.

- Remove PythonRe callback replacement, evaluator delegates and string substitution
  output. Byte-template Replace/Subn and retained byte results remain; hosts use
  the range cursor for custom output and decode returned bytes when needed.

- Remove PythonRe FindAllToStrings and its text-only construction/canonicalization
  machinery. FindAllToUtf8 retains Python-shaped owned byte results.

- Remove PythonRe SplitToStrings, SplitDetailed and their text result type.
  SplitRanges retains segment/capture metadata; hosts decode selected bytes.

- Remove PythonRe borrowed-result string getters. Access value/capture bytes and
  decode in the host when needed; matching and capture lifetimes are unchanged.

- Remove PythonRe cursor scalar-coordinate queries and their optional prefix
  index. Captures retain byte coordinates; Unicode scalar matching is unchanged.

- Remove PythonRe SearchToString, MatchToString and FullMatchToString. Use the
  corresponding byte-result operation and decode in the host when needed.

- Remove PCRE2's static IsMatch, Match and Replace convenience overloads. Retain
  compiled instances in the host; their matching and resource controls are unchanged.

- Retire PCRE2's separate character-sequence execution and parse, including
  single-class counters and cursor state. Preserve partial probes through the
  existing parsed tree, and keep PCRE2 caret/end-only-dollar semantics when Core
  execution would differ. Resource requirements and execution costs can change.

- Remove PCRE2's standalone token-repeat program, counters and cursor variant.
  Existing shared scalar/profile programs preserve quantifier and global-empty
  semantics, grapheme/newline/byte-token behavior, captures and controls. Resource
  requirements can change.

- Retire PCRE2's multiline-prefix, prefix/repeat and alternation/repeat overlays,
  including their global cursor state. Keep shared literal helpers with their real
  consumer. Existing scalar/backtracking paths preserve results and controls;
  numerical costs change, and fixed-scratch line matching can need less depth/heap.

- Remove PCRE2's bounded-ASCII, regular-ASCII and anchored-DFA Boolean overlays,
  including the private DFA compiler. Existing profile and shared scalar programs
  preserve supported matching and controls; numerical resource needs and costs
  can change. Keep literal mechanisms and general search/backtracking foundations.

- Retire PCRE2's palindrome, leading-dot-star/literal and separated-run Boolean
  overlays. Existing profile backtracking or shared scalar execution preserves
  matching, captures and original controls; operating and numerical resource costs
  can increase. Literal/search helpers and PCRE2 semantic foundations remain.

- Remove PythonRe's constructor-selected prefix/digit Match and DotAll
  prefix/suffix FullMatch shortcuts. Existing scalar execution preserves values,
  captures, byte offsets and interruption; cold FullMatch preparation and numerical
  work/storage costs can change. Pure literal paths remain.

- Retire the remaining nonliteral identifier-family engines, verifiers and cursor
  mode, including their helper-only preparation accounting and benchmark replays.
  Preserve shared search and literal paths; scalar execution retains captures,
  interruption and major-buffer limits, with different work and scratch costs.

- Correct identifier-family suffix matching when the suffix begins at the end of
  a greedy identifier tail, keeping Count and cursors consistent with Match.

- Keep shared-prefix boundary literals on literal execution and consolidate their
  emitted/fused counter factory. Preserve existing kernels, Unicode boundaries,
  ordered alternatives and controlled matching while removing structural-emitter
  plumbing.

- Retire Core's ordered literal-window execution, cursor mode and PCRE2 Count
  wrapper. Keep shared literal/candidate search and the PCRE2 lazy-gap compatibility
  guard. Scalar programs preserve matching and interruption; execution costs and
  numerical resource requirements can increase.

- Remove token-window, repeated-segment and quoted-relation whole-pattern plans,
  their instruction carrier and the finite repeated-segment Count shortcut.
  Existing scalar execution preserves results and interruption; execution costs
  and numerical resource requirements can increase, including on misses.

- Retire Core's nonliteral fallback classifiers, payloads and shortcuts, including
  token/URI/IP, word/category counting and anchored validators. Matching uses the
  existing scalar programs; adapters no longer borrow the retired IP proof.
  Preserve qualified anchored-literal search and shared helpers with real consumers.
  Numerical work/storage needs and execution costs can increase; scalar results,
  capture lifetimes, synchronous interruption and resource admission remain.

- Remove the nonliteral simple-pattern engine, its standalone deterministic/
  fixed-token programs, selectors, cursor modes and replay benchmarks. Keep
  bounded finite-literal expansion and shared structural helpers. These patterns
  use the general scalar engine; numerical work/storage and execution costs can
  increase while scalar matching, captures, lifetime and limit contracts remain.

- Retire anchored class/head-tail validators and their whole-pattern dispatch
  and construction plans. Existing scalar execution preserves matching and
  captures; numerical work/storage requirements can change with the program.

- Remove the date-specific matchers and duplicate compiled-family routing.
  Existing matchers preserve field, separator, boundary and capture semantics;
  numerical resource requirements and execution costs can change.

- Keep repeated-digit alternative results consistent when a cancellation token is
  supplied. Remove the specialized digit-group proof and executor that could fill
  excluded repeat lengths. Captures and interruption remain on existing matchers;
  numerical resource needs and execution costs can change.

- Fix bounded-repeat alternatives that could accept excluded intermediate lengths.
  Remove the bounded-suffix specialization and retain exact branch semantics in
  the remaining matchers. Unmetered costs and numerical resource needs can change.

- Remove the optional-field/postcode specialization, its construction proof and
  dispatch arms. Existing general matching paths preserve the language, captures
  and controls; unmetered costs and numerical resource requirements can change.

- Reuse bounded necessary-prefix discovery in Core Boolean matching, capture-slot
  matching and memory cursors. Scalar verification, captures and cumulative
  interruption/resource controls remain. Sparse searches can avoid repeated attempts;
  dense candidates can cost more. No extra subject pass or allocation is introduced.

- All profiles now require caller-valid, unchanged UTF-8 subjects. Remove
  `Utf8InputValidation`, policy arguments/properties, mandatory subject validation
  and PCRE2's validation-window analysis. Host text boundaries own validation;
  pattern/template/output checks remain independent. Scalar semantics, byte ranges,
  original interruption and useful resource limits remain. Numerical work/storage
  requirements can decrease. Malformed-subject matching results are unspecified.

- Fix `Singleline` alternatives that split gap lengths across branches: preserve
  the first successful alternative instead of merging its range with later ones.
  Remove the redundant symmetric-window matcher and counter. Matching uses the
  existing general paths; numerical resource requirements and unmetered costs can change.

- Avoid scanning unused subject suffixes after ASCII eligibility is already known.
  Remove unused supplementary-scalar metadata and discarded split preparation.
  Strict pattern/template checks, matching semantics and interruption/admission remain.

- Core whole-match results and span enumeration now use `Utf8CaptureRange` with
  byte start/end endpoints. Remove `Utf8ValueMatch`, subject coordinate maps and
  UTF-16 cursor scans. Default ranges remain distinct from successful empty hits;
  scalar semantics, aliases and original interruption/resource controls remain.
  Modeled work and working/result capacity requirements can decrease.

- Remove Core `MatchDetailed` and its three legacy match/group/capture views.
  Use the memory capture cursor for immutable byte ranges and capture histories.
  Delete detailed-only UTF-16 projections and capture-map preparation. Misses
  return default results; regex metadata remains available from the instance.
  Cursor timeout diagnostics omit decoded input, and budget needs follow the cursor.

- Remove the duplicate non-literal replacement worker and its UTF-16 capture
  shaping. These replacements use the existing scalar byte-template engine;
  literal replacement paths remain. Route-dependent costs and budget needs can change.

- Core split results expose only byte start and length. Controlled scalar splits
  no longer prepare or retain UTF-16 coordinate maps. Empty/count/RTL ordering,
  borrowed-view lifetimes and cumulative interruption/resource controls remain.

- PCRE2 results now expose byte endpoints only. Delete UTF-16 projection tables,
  sorting, scans and cursor caches. Preserve `\C` byte fragments, `\K` ranges,
  captures, lifetimes and bounded publication; existing finite budgets may change.

- PythonRe match/group results now expose byte coordinates only. Remove UTF-16
  offsets, alignment flags and result-prefix scans; explicit scalar queries retain
  a scalar-only lazy index. Captured text, lifetimes and resource admission stay
  enforced; numerical budget requirements can change.

- POSIX `Match`, capture destinations and cursor advances now use
  `Utf8CaptureRange` with byte start/end offsets. Remove UTF-16 projection and
  its cursor cache; selected captures and resource controls remain unchanged,
  while modeled work requirements can decrease.

- Remove seven parser node kinds that no production parser generates and their
  redundant lowering/analysis branches; real atomic, lazy and negated-class
  semantics remain on the existing scalar nodes.

- Omit unused execution-tree preparation for capture-free native literals and
  redundant capture-predicate walks for programs without capture slots, reducing
  construction allocations while preserving literal search and resource bounds.

- Remove Core `ReplaceToString` and its text-output execution branches. Hosts can
  decode byte replacement results; their additional string allocation is outside
  the regex's resource limits.

- Remove Core callback replacement overloads and evaluator delegates. Custom
  substitutions use the byte capture cursor; scalar byte templates avoid input UTF-16
  preparation. Shared PCRE2 writer support remains.

- Remove Core's static cached operation families and global pattern cache. Retain
  a compiled instance in the caller; replacement preparation stays bounded.

- Remove Core `CountFromUtf16Offset`. Partial counts aggregate byte-start
  `EnumerateMatches` under one invocation; whole-subject `Count` remains.
  Iterator coordinate/result work can increase partial-count costs and budget needs.
- Add byte-start Core `Match` into caller-owned capture slots, including an
  optional end bound, cancellation and working-memory admission. It creates no
  coordinate map or captured-text snapshot. Compact slots resolve through
  `GroupNumberFromCaptureSlot`; funded program owners expose both operations.
  Replace `IsMatchFromUtf16Offset` and `MatchFromUtf16Offset` with byte-start
  `Match`. Eligible literal families retain bounded search through eight alternatives.
- Replace `MatchDetailedFromUtf16Offset` with the byte-start memory capture cursor.
  Its byte ranges and capture histories survive disposal, without a coordinate map.
- Replace Core `EnumerateMatchesFromUtf16Offset` with byte-start
  `EnumerateMatches(input, startOffsetInBytes, cancellationToken)`. Starts must
  be scalar boundaries; anchors/lookbehind retain original-subject context,
  with the same cumulative cancellation, deadline and resource controls.
- Fix an out-of-range read in `Count` for bounded suffix patterns when a
  non-ASCII subject ends with the searched literal, such as `é\ting`.
- Use the general matcher for balancing-group counts, removing a BEGIN/END-specific
  counter. These patterns now use the general engine's work, depth and storage
  charges; existing finite budgets may need adjustment.
- Remove whole-pattern, identifier/window and fixed-token shape IL emitters,
  reducing construction work and storage. Their operations use existing owned
  interpreters/search helpers. Literal-family emission and shared literal search
  counting remain; `Compiled` does not yet compile general patterns to IL.
- Support .NET 10 and .NET 11 across Core and all three adapters. .NET 11 is the
  primary benchmark baseline. Adapters require the exact matching Core version.
- PythonRe models Python `re` over `str`; PythonRe and POSIX require already-valid
  UTF-8 text and omit regex-operation validity passes. Validate external bytes at
  the text boundary. Required coordinates and output may still inspect text.
- Add Lokad.Utf8Regex.Posix: a scalar ERE subset with leftmost-
  longest matching and hierarchical capture priority. Match into caller-owned
  capture spans, count, or use a disposable memory cursor. Named classes are fixed
  ASCII; locale collation, backreferences and lookarounds are excluded. General
  capture selection may use quadratic interval storage; linear matching is not
  promised.
- Add `PosixCompileOptions.IgnoreCase`: invariant scalar case equivalence with
  leftmost-longest matching, hierarchical captures and original byte offsets.
- Preserve POSIX `Newline` matches when a whitespace run must rewind before LF
  to satisfy `$`. Proved flat runs reuse scalar capture selection and reject
  impossible starts before recording capture events.
- Extend proved POSIX runs to single repeats and fixed scalar pieces, avoiding
  unnecessary rewind storage. Valid-text coordinate projection reuses bounded
  counting. Numerical work/depth/working requirements can decrease on these routes.
- Single-scalar POSIX repeats reuse first acceptance through fixed prefixes and
  suffixes, avoiding redundant capture charts. Capture-only terminal tails omit
  unneeded exit frames; required endpoint rewind remains active.
- Add direct UTF-8 pattern/template APIs, byte capture-name lookup and caller-
  buffer pattern metadata in Core, PythonRe and PCRE2. Add caller-funded program
  ownership, persistent memory cursors and explicit retained-result/output
  admission. Existing value cursors keep their documented copy/alias contracts.
- Core value `Match` paths reuse subject validation and avoid discarded whole-
  input UTF-16 metadata for exact literals and literal families. Returned byte/
  UTF-16 coordinates remain relative to the original input; `Validate` still
  rejects malformed tails.
- Finite-limit value `Match` reuses bounded search for literal families of up to
  eight alternatives, reducing work and temporary storage on those routes.
  Detailed capture results retain their separate admission requirements.
- Reduce repeated preparation, search, capture traversal, coordinate scans and
  output copies. Results and numerical resource requirements depend on the route;
  some workloads use more time or storage. Recheck finite policies when upgrading.
- Preserve scoped Python verbose spaces and Unicode class/folding rules. Correct
  PCRE2 class quoting of supplementary scalars and literal Count on a trailing
  partial Unicode needle. Null replacement evaluators reject even on misses;
  explicit pre-cancellation retains precedence.

Resource controls admit modeled major capacity and growth overlap, not total heap,
RSS, arbitrary callbacks/getters or host retention. Defaults remain unlimited.
Work/depth/working/result requirements can change between versions; temporary loans
refund synchronously. Original-token cancellation, absolute deadlines, scalar
semantics, captures and retained-result lifetimes remain enforced.

Execution remains owned, managed and checked. No blanket speedup or complete
.NET/Python/PCRE2/POSIX equivalence is promised. See the
[migration guide](docs/MIGRATION.md) and [profile contracts](README.md#choose-a-profile).

## 0.4.0 — 2026-10-27

Published on NuGet: [Core](https://www.nuget.org/packages/Lokad.Utf8Regex/0.4.0),
[PythonRe](https://www.nuget.org/packages/Lokad.Utf8Regex.PythonRe/0.4.0) and
[PCRE2](https://www.nuget.org/packages/Lokad.Utf8Regex.Pcre2/0.4.0).

- **Breaking:** matching uses Unicode scalars over strict UTF-8, with primary byte
  coordinates. Dot consumes one complete scalar; malformed UTF-8 anywhere in the
  subject is rejected. UTF-16 coordinates remain derived conveniences.
- Core, PythonRe and PCRE2 use owned managed execution, replacing BCL matching
  fallbacks. Core retains .NET-style grammar and its supported NonBacktracking subset.
- Instances support synchronous cancellation, one absolute invocation deadline,
  and finite work, depth, working-capacity and result-capacity limits. Defaults
  remain unlimited; byte caps cover modeled capacities rather than process memory.
  Compile provides separate construction limits, timeout and cancellation.
- Captures, references, lookarounds, RTL and empty-match advancement preserve scalar
  boundaries. PythonRe follows its documented capture numbering, completion metadata
  and result shapes; PCRE2 follows its separate grammar and capture-slot contract.
- Owned results survive cursor advance/disposal. Borrowed inputs require a live owner
  and unchanged bytes. Replacement writers accept scoped local spans.
- Superseded UTF-16/fallback machinery and unchecked engine access are removed.
  Actual Unsafe.SizeOf queries remain.

Upgrade selected packages together; adapters require the matching Core version.
Read the [migration guide](docs/MIGRATION.md) and profile specifications for
supported behavior and resource-control scope.

## 0.3.0 — 2026-08-28

[Published on NuGet](https://www.nuget.org/packages/Lokad.Utf8Regex/0.3.0).

- .NET 10 matching, counting, enumeration and replacement over UTF-8 spans.
- .NET Regex semantics with UTF-16 coordinates and byte-aligned UTF-8 fast paths.
