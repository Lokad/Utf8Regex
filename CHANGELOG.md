# Changelog

## Unreleased

### Owned UTF-16 execution

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
