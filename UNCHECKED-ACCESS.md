# Bounded checked-access work

The core matches strict UTF-8 scalars. Checked-access conversions must preserve
algorithms, SIMD, allocations, semantics and resource controls, with focused
correctness, Release code-generation and cost evidence. `Unsafe.SizeOf<T>()` is
accepted for actual managed sizes; manual size estimates are not a substitute.
There is one engine and no permanent safe/unsafe selector.

The 2026-10-14 acceptance gate permits up to a 10% execution-time slowdown on
representative affected public workloads. This supersedes the former no-regression
gate. Revisit promising deferred conversions with one bounded before/after
comparison and an unchanged control; allow one noise confirmation, then stop.
Kernel timings are diagnostic and do not substitute for public-operation costs.
Preserve semantics, cancellation, resource admission and managed safety, retaining
algorithms/SIMD where practical. Full platform/package qualification is batched at
a coherent delivery milestone. Historical deferrals below retain their original
evidence; the revised allowance does not designate conversions automatically.

The allowance applies cumulatively to the completed checked batch, against the
fixed pre-C28 source c79f80620ef95a27c43ab8f5619795833feae8d9. Its preserved Core
DLL is artifacts/short-literal-checked-10pct-20261014/core-before.dll, SHA256
8642E9A261E56EDC4B9CFC6D72FF9A9B3ADB9B72BE22AEA40AD5714C555B4202. Focused
predecessor comparisons remain evidence for individual changes, but do not prove
that their combined public-operation cost is within 10%. That final comparison
is unfinished. Keep SizeOf separate: it performs no unchecked memory access;
Lython's blanket rejection requires explicit acceptance-contract reconciliation.

Counts and decisions in the historical pilot records below describe those
snapshots. The current inventory and explicit accepted follow-ups take precedence.

The current source inventory is 18 sites across all shipped projects: **16 Core
sites in 9 files**, plus two PCRE2 null-reference placeholders in one file.
PythonRe has no explicit sites. Earlier "Core55" shorthand included PCRE2;
compiler-generated accesses and whole-DLL API gates remain separate observations.

A bounds review found an admission defect independent of the deferred load
conversions. Unsigned subtraction allowed a literal longer than the subject to
reach Slice. The exact scalar.47 DLL throws for Count of `abcdefgh|xyzzz|ztzzz`
over `abcde`, in ordinary and compiled modes. One widened end guard fixes equality
and known-prefix admission and lets the JIT remove the duplicate Slice check.
Original comparator loads, algorithms and resource controls remain. Code shrinks
356→336 bytes on Linux, but short comparator microbenchmarks still regress; the
selected public family Count control shows no penalty. This correctness repair
does not qualify the previously rejected checked loads or clear the artifact gate.
Evidence: artifacts/literal-admission-bounds-20261014/.

The following are the reviewed access contracts, not independent whole-program
proofs. Keep remaining loads while their conversions lack acceptable evidence;
revisit demonstrated defects or concrete source/JIT changes.

| Remaining Core file / sites | Bounds contract and reason retained |
| --- | --- |
| AsciiSearch /2 | General folded-loop callers pass equal-length admitted slices; each iteration reserves eight literal bytes. The checked long-loop prototype exceeded10% on an affected public workload. |
| AsciiOrderedLiteralWindowExecutor /4 | Final block start reserves both trailing-literal displacement and vector width; scalar remainder scans the rest. No new conversion qualified. |
| Utf8AsciiDeterministicFixedWidthCountExecutor /1 | Checks length equals width3..8; vector limit reserves31 extra starts, bounding each check-offset load. Checked shared-helper conversion exceeds10% on width8 public Count; retained. |
| Utf8AsciiLiteralBetweenNegatedRunsExecutor /2 | Full32/16-byte blocks only; neighbor reads have candidate lower/upper bounds. No new conversion qualified. |
| Utf8AsciiUriTokenExecutor /1 | Checked suffix span and full32-byte admission. Private boundary penalties were observed, but affected public coverage remains missing; unqualified. |
| Utf8EmittedLiteralFamilyCounter /1 | Nonnegative scan index≤length-32; separate candidate comparison/validation handles tails. No new conversion qualified. |
| Utf8InvariantCyrillicLiteralCountStrategy /1 | Full32-byte blocks, scalar/continuation fallback for tails. Checked suffix read regresses affected ASCII-miss public Count25–26%; original restored. |
| Utf8LiteralCompiledEngineRuntime /3 | Production correlated offsets0/1/3 reserve a complete literal plus31 starts. The three diagnostic loads are now checked; these separate production loads remain. |
| Utf8ValidationCore /1 | The common UTF-8 drain admits full32-byte blocks from scalar-boundary offsets. The separate ASCII drain now uses checked BCL range search, removing its two loads and custom loop. |

The two independent PCRE2 `Unsafe.NullRef` sites denote the inactive arm of a
stack-only borrowed projection authority. `_isSlice` selects the populated arm
before access. This is an invariant, not a claim of checked replacement or a
reason to delay the core/PythonRe delivery boundary. Compiler-generated Core
inline-array parameter/fold staging is now converted; its historical small
construction penalties were rejected under the superseded zero-regression rule.
Three generated helper calls remain in Utf8FallbackTreeQuery.TryShapeAtom; keep
that inventory separate from the sixteen explicit Core source sites.

The short-literal comparator is accepted under the revised 10% public-cost gate.
Its complete candidate admission retains the scalar.48 end-guard repair. Checked
span indices and MemoryMarshal.Read replace the primitive unaligned reads for
four through sixteen bytes; widths, comparison order, overlapping tails and
allocations remain. This removes all 17 explicit unchecked source sites in
Utf8LiteralEquality, while other Core APIs and generated staging remain open.

One bounded actual-DLL public Count comparison plus its one noise confirmation
covers mixed five/eight-, six/seven- and nine/twelve/sixteen-byte families, ordinary
and compiled engines, all four finite controls, and an unchanged literal control.
Confirmation medians for affected operations range about -0.7% to +1.2%; the
unlimited control moves about -7% to -8%, so exact equivalence is not claimed.
All allocations match. These are current public-operation costs, not a conclusion
from the earlier 4–7% isolated kernel penalty. The inspected Linux callers grow
1515→2059 and1532→1927 bytes and retain some bounds branches. Boundary, offset,
tail, known-prefix, malformed-input, cutoff and original-interruption checks pass.
Evidence: artifacts/short-literal-checked-10pct-20261014/. Full platform/package
qualification is reserved for a coherent delivery batch; scalar.48 bytes stay fixed.

The PreparedByteSet vector-search conversion is also accepted under the 10%
public-cost gate. MemoryMarshal.Read on checked suffix spans replaces both
LoadUnsafe calls. The existing forward/reverse loop, SIMD masks, clamped final
overlap, scalar fallback and allocations remain unchanged. FullOpts public-route
code grows1207→1314 bytes on Linux and retains bounds branches.

Public IsMatch of a four-literal family demonstrably reaches the changed Find
method. The one reversed-order noise confirmation covers hits/misses at31,33 and
4096 bytes: affected medians increase about2–4%, with zero allocations in both
versions. Unchanged literal controls move about-0.6% to+1.2% without limits.
Finite controls retain their existing VM route and are not evidence for the
changed vector loop. The initial broader run hit its110-second cap before the
control; its noisy partial samples remain evidence, not acceptance. The single
70-second confirmation completed; no further timing or full package matrix was
run. Focused boundaries, offsets, tails, scalar/resource behavior and managed
safety checks are retained. Evidence: artifacts/byteset-checked-10pct-20261015/.

The separate folded 9–16-byte comparator retains four checked primitive reads.
Eight-byte widths, overlapping tails, folding arithmetic, comparison order and
allocation behavior are unchanged. A broader six-read prototype measured public
Count of mixed matches/near misses in ordinary/compiled modes. Its12-byte rows
increase about5–6% in the sole reversed-order confirmation; unchanged exact-literal
controls move about+0.9% to+1.3%. Allocations match. These are public-operation
costs of that broader prototype, not isolated kernel timings or a fresh timing
of the final subset.

The two general-loop reads are restored: one24-byte compiled row regresses about
11.9% initially and10.1% in the confirmation, exceeding the allowance. No third
timing or new load variant is attempted. The retained short helper and selected
public vector caller/verifier have identical Release instruction listings to the
measured prototype; short-helper IL differs only in resolved-identical method
tokens. The restored general helper has identical baseline IL. Its short dispatch
register allocation differs from the prototype, so final end-to-end equivalence
is not claimed. The short helper grows294→375 bytes on Linux. Focused ASCII-case,
punctuation, offset, tail, Unicode-folding, resource and interruption checks pass.
Evidence: artifacts/folded-comparator-checked-20261015/. Full platform/package
qualification remains batched; the Core whole-DLL gate is still open.

The fused ASCII exact-literal Count loop now uses a checked suffix span and
MemoryMarshal.Read<Vector256<byte>> in place of its single LoadUnsafe. The same
32-byte loop admission, anchor filtering, complete candidate verification,
non-ASCII decline, scalar tail and zero-allocation behavior remain. Linux Release
FullOpts code grows814→817 bytes and retains bounds checks.

One bounded before/after public Count comparison covers64-byte and4096-byte
sparse subjects and a4032-byte dense subject, ordinary and compiled options.
Affected medians change about-0.6% to+4.0%, within the10% allowance. Unchanged
short-literal controls move-0.9% to+0.1%; the finite control moves+0.2%, with a
noisy20% sample spread. Allocations match in every lane. Public calls compile and
execute the changed kernel; finite-policy calls retain their owned scalar route
and are controls only. These are public-operation timings, not isolated kernel
estimates. Borrowed offsets, every block tail, malformed/non-ASCII input and
finite/interruption checks pass. No confirmation, additional variant or per-helper
package matrix is needed. Evidence: artifacts/exact-count-checked-20261015/.

The fixed-width class Count pilot is deferred. Passing the subject span into
CreateMatchMask and reading a checked suffix preserves SIMD, masks, nonoverlap,
final overlapping blocks, scalar suffix fallback and allocations. Public Count
executes the helper for both ordinary and compiled patterns. One bounded actual-
DLL comparison finds width4 medians about5.7–5.8% slower, but width8 about19.6–20.5%
slower; unchanged literal controls move1.3–2.5%. The width8 result exceeds10%
without needing a confirmation. Linux FullOpts helper code grows588→635 bytes
and retains guards. The original source is restored; no width-specific safe/
unsafe selector or further variant is added. New widths3..8/borrowed-offset/tail
tests remain, alongside predicate, decline and interruption coverage. Core Release
6777 and Debug76 checks pass on the candidate. A subsequent fresh-build signature
audit confirms the restored ref-byte helper, and20 matching-binary checks pass;
the immediate incremental restoration run had reused a stale DLL because the
restored source kept its timestamp. Evidence: artifacts/fixed-width-checked-20261015/
and artifacts/category-public-checked-20261015/restoration-audit.json. Scalar.50 remains
immutable; there is no new package qualification for this pilot.

The folded prepared-search conversion is accepted. Four checked suffix reads
replace its128/256-bit LoadUnsafe calls and remove the ref origin. Correlated
first/preferred masks, nint stepping, final overlapping clamp, full verification,
folding fences and allocations remain. Linux Release FullOpts code grows
1077→1149 bytes with some bounds checks retained.

One bounded actual-public Count comparison covers a31B128-bit hit, a4096B256-bit
miss and dense12/24-byte literals in ordinary/compiled modes. A separate untimed
wide-miss-only audit confirms that row executes the vector helper. Affected
medians change about-2.4% to+3.0%; allocations stay0B. Unchanged unlimited literal
controls move about0%, and the finite control moves-1.5% with112B unchanged.
Some short/control sample ranges reach16–25%, so exact parity is not claimed.
All samples exceed40ms. The margins support the10% gate without a confirmation.
Finite folded preflights retain their owned route and agree; they are not changed-
kernel timing evidence. Independent offset/clamped-tail/false-candidate oracles,
scalar/case-folding and original resource/interruption checks pass: CoreRelease433,
Debug128 and PythonRe179 plus174 folding checks. Evidence:
artifacts/folded-search-checked-20261015/. Full package/platform checks remain
batched; no new package or benchmark Status is designated.

The packed multi-literal finder now uses checked suffix spans for its128/256-bit
loads. Its AVX helper receives the subject span; mask offsets, nibble shuffles,
pending candidates, verification, scalar tails and allocations remain. Public
Count-only audits separately compile the finder for the38-byte SSE subject,
wide Chinese hit/miss subjects and folded ASCII hit/miss families. Release FullOpts
code grows1275→1420 bytes on Linux and retains bounds guards.

One70-second-capped public before/after comparison completes with affected
ordinary/compiled medians about-5.2% to+3.2%, within the10% allowance. Unchanged
literal controls move-0.1% to+0.5%; the all-four-finite control moves+1.5%.
Allocations remain0B and112B respectively. Samples exceed33ms; some compiled
folded-hit samples spread36–45%, so no exact parity or general speedup is claimed.
No confirmation or additional variant is needed. Candidate ordering, every
vector lane/tail, borrowed offsets0/1/17/31, scalar/folding and resource checks
pass. Evidence: artifacts/packed-search-checked-20261015/. Full package/platform
qualification remains batched; immutable scalar.50 is unchanged.

The fused Cyrillic Count read is deferred. One checked suffix read preserves
the256-bit masks, continuation carry, candidate verification, wider-scalar resume,
nonoverlap, tails and zero allocations. Public Count compiles the kernel; a
separate untimed ASCII-miss-only audit confirms coverage for that row. One bounded
ordinary/compiled comparison finds dense Cyrillic and mixed-width medians within
4%, but4096-byte ASCII misses regress25–26.5% against literal controls about1–2%.
The ordinary32-byte hit is noisy (148–167% sample spread), so it is not parity
evidence. The clear ASCII-miss penalty exceeds10% without a confirmation.

Linux Release FullOpts code shrinks1810→1808 bytes while bounds guards remain;
smaller code does not establish equivalent cost. Restore the exact original
source, keep the expanded borrowed-offset/every-tail validation and explicit
finite/live-token checks, and stop. No safe/unsafe mode, further variant, package
replay or new candidate follows. Evidence: artifacts/cyrillic-count-checked-20261015/.

The original ASCII validation drain prototype was restored. Counted checked vector spans keep
the same SIMD reduction and scalar tails and eliminate per-vector range checks,
but add entry/setup/index work. Linux FullOpts code grows454→533 bytes; long
kernel controls regress30–39% and the internal ValidateOnly helper12–15%, with unchanged
allocations. Public unlimited Match measures -2.2% (noisy) at4KiB and +1.9% at128KiB;
finite public lanes do not show the cited12–15% penalty either. These are separate
driver lanes. The earlier "mixed public validation" classification was incorrect:
this evidence does not establish a >10% affected-public-operation regression.
Current public-route coverage through the changed helper must still be demonstrated
before acceptance; the two loads and a checked BCL range-search alternative are
reopened by the 2026-10-15 review. Production method IL matches baseline. New
borrowed-offset, Unicode-stop, malformed-tail and exact work-cutoff checks remain.
One short kernel sample missed the timing floor and is not parity evidence; the
long controls explain private costs but cannot close the public-cost gate. Evidence:
artifacts/validation-ascii-drain-checked-20261014/. The55-site inventory and Core
unchecked API gate remain open; no safety/performance equivalence claim follows.

A separate checked BCL follow-up replaces the entire ASCII drain with bounded
IndexOfAnyInRange(0x80,0xff). It removes two LoadUnsafe/ref-arithmetic sites,
the custom vector/word/tail loops and the unused BinaryPrimitives import. The
runtime retains SIMD implementation; scalar stop/error coordinates, validation
flags, allocations and resource/cancellation authority remain. Prefix reuse is
deliberately separate and not part of this measurement.

Actual public Count-only probes over an invariant folded literal compile the
old drain and the new validator's BCL range-search call, in ordinary/compiled
lanes on Windows10.0.13 and Linux10.0.12. This is affected public coverage, not the
earlier private ValidateOnly lane or a route descriptor alone. One bounded warm
pair over short/4KiB/128KiB late-Unicode, alternating-width and Kelvin subjects
measures affected medians -1.49%..+2.98%; exact-literal and all-four-finite controls
move -1.59%/-0.61%. Allocations stay0B/112B. Samples exceed51ms; one compiled4KiB
row has46–76% spread, so no exact equivalence or general speedup is claimed.
No confirmation or extra variant is needed. Linux validator code1404→1456 bytes
and Windows1494→1534, with the drain now inlined; the old standalone454/510-byte
body is gone. Shared BCL code is outside those sizes. Final whitespace-only
cleanup retains identical drain/validator IL. Boundary/offset/tail, malformed
UTF-8, work/cancellation and source checks pass. Evidence:
artifacts/ascii-drain-bcl-20261015/. The original fixed cumulative gate and whole
Core DLL gate remain unfinished; no new package or snapshot refresh follows.

The subsequent prefix-reuse change removes a duplicate ASCII-prefix scan in
ValidateOnly. The suffix starts at the already discovered first non-ASCII byte;
the result restores whole-input byte/UTF-16 lengths and errors restore the original
byte offset. Supplementary/Kelvin flags are unchanged, because an ASCII prefix
cannot contain either. This adds no owner, policy, allocation or invocation meter.
The checked BCL drain itself is unchanged. Borrowed-offset/boundary and malformed
suffix tests verify coordinates and flags independently of the implementation.

One bounded public Count pair reuses the same twelve lanes as the drain pilot.
Two apparent +10.1%/+14.2% rows have106–166% sample spreads. The single allowed
confirmation runs only those two workload families plus the exact-literal control
on the quiet VM, with the same frozen DLLs and candidate-first order. Their
affected medians move -0.33%..-6.42%; allocations match and the apparent regressions
do not persist. The initial other affected rows are within-4.64%..+0.70%; finite
control-0.60%. Quiet confirmation's control is-2.23%, with some ordinary/control
rows still noisy. No exact equivalence or general speedup claim follows, and no
further timing is performed. The actual public caller's analyzer grows159→245
bytes on Linux while the validator remains1456 bytes. Final Release/Debug,
cancellation/source guards and focused PythonRe checks pass. Evidence:
artifacts/validation-prefix-reuse-20261015/. No package/whole-DLL gate claim follows.

The correlated-literal diagnostic census conversion was historically deferred. Direct checked
reads keep the existing SIMD algorithm but add guards and increase inspected
code692→766 bytes on Windows and718→776 on Linux. Long diagnostic replays regress
about10–11%, with equal zero allocations; the timed production kernel retains
identical code578/568 bytes. Production is restored and census boundary fixtures
remain. Evidence: artifacts/census-checked-load-20261013/. No runtime speedup or
whole-DLL safety claim follows; the55-site inventory remains unchanged at that
historical boundary. The2026-10-15 review reopens these three diagnostic loads:
diagnostic replay overhead is an explicit diagnostic tradeoff, and unchanged
production matching throughput does not need another benchmark campaign.

The current-source follow-up accepts the three diagnostic checked reads. The
existing census and selected-runtime fixtures pass in Release and Debug, including
borrowed offsets, tails, nonoverlap, finite public controls and malformed input.
An untimed actual-DLL replay agrees at seven short/long boundaries. Windows
.NET10.0.13 Release FullOpts production instructions remain identical apart from
relocated addresses (578 bytes both); diagnostic code grows692→766 bytes. The
historical diagnostic slowdown is accepted as a diagnostic tradeoff, without a
new matching timing or performance-equivalence claim. This removes three explicit
sites and one diagnostic origin reference, preserving SIMD masks, candidates,
verification, ordering and allocations. No new package or whole-DLL compliance
claim follows. Evidence: artifacts/diagnostic-census-checked-20261015/.

The original category math-symbol reduction pilot was deferred. Checked vector-span
indexing removed per-vector checks while preserving widths, comparison order,
popcounts, tails and allocations. Short kernels improved 4–10%, but the 4 KiB
kernel retained a 0.3–1% penalty in normal/reversed runs, with larger helper setup.
Production is restored and boundary fixtures remain. Public Core Count selected
another route, so its timings do not qualify this kernel. Evidence:
`artifacts/category-checked-load-20261013/`. Its55-site inventory is historical.

The category conversion is now accepted with actual public PCRE2 Count evidence.
The existing Pcre2CharacterDirectProgram calls the shared Core math-symbol
reduction; no selector or integration framework is added. Counted checked
ReadOnlySpan<Vector128<byte>>/ReadOnlySpan<Vector256<byte>> views remove both
unaligned Unsafe reads/ref origins. SIMD widths, six comparisons, popcounts,
consumed index, scalar tails and allocations remain. Finite/token/deadline paths
keep their original admission and interruption authority.

One bounded before/after uses immutable scalar.50 Core versus the new source
with the identical PCRE2 DLL in both lanes. Public Count medians change about
-0.7% to+4.1% on128B/4096B ASCII and3328B mixed BMP/astral subjects; unchanged
literal/finite controls move-0.1%/+1.4%. Allocations match. The mixed after lane
has a21% sample range, so exact parity is not claimed; the observed medians fit
the10% allowance without a confirmation. These timings exercise256-bit public
selection;128-bit correctness is covered directly, without a separate public
128-bit cost claim. The inlined reduction caller grows428→450 bytes on Linux
FullOpts and has no per-vector range checks; the outer category loop stays673B.
Core Release369/Debug22, PCRE2 Release81/Debug52 and the full PythonRe suite pass.
Evidence: artifacts/category-public-checked-20261015/. No Core Count speedup,
benchmark Status, full platform coverage or new package designation is implied.

The original vector-search and short-literal pilots were deferred under the
former no-regression gate. Checked vector loads retained branches and regressed
7–38%; short-literal variants retained tail checks or inconclusive/regressing
4–7% timings. The short comparator is now accepted under the revised public-cost
gate above; the original measurements remain historical evidence.
Two fixed 16-byte construction-buffer reads were converted to
`MemoryMarshal.Read<Vector128<byte>>` with identical FullOpts instructions on
Windows and Linux. Evidence: `artifacts/unchecked-pilots-20261012/`.

The syntactic inventory has 58 unchecked/ref-access sites in 18 core files.
All 58 recorded source lines still correspond after scalar.33 cleanup. Vector
loads rely on admitted widths and clamped offsets; the short comparator relies on
same-length admitted spans and length dispatch. Other sites require further review.
This inventory is not a complete compiled-code or memory-safety proof.

A 2026-10-13 cold construction preflight considered three manually sized
`MemoryMarshal.CreateReadOnlySpan(ref value, 1)` calls for replacement by the
one-element `ReadOnlySpan` constructor. Before any conversion, it exposed a
core DFA optional-loop compiler defect: duplicate transitions and violated
inner-repeat bounds. The correctness repair removes that unqualified shortcut,
its duplicate proof/helper and one of these span sites; two remained at that boundary.
No checked conversion or no-regression claim follows from this removal. Public
constructor/first-Count probes passed before the repair; this is a demonstrated
compiler boundary defect, not evidence of a public crash. At that boundary the
syntactic inventory had 57 sites in the same 18 core/adapter files. Raw failures, focused
construction costs and code generation are in
`artifacts/singleton-span-pilot-20261013/`.

The follow-up singleton conversion is accepted separately at scalar.39-dev.
The NFA class unit and initial closure state use the one-element `ReadOnlySpan`
constructor, removing both caller-supplied lengths. All four inspected compiler
method instruction listings are identical before/after on Windows and Linux;
JIT inline-count comments differ. Four quiet-VM construction controls retain
identical allocations and show no measured regression. The apparent improvements
also occur in the unchanged literal control, so no speedup is claimed.
Evidence: `artifacts/singleton-span-conversion-20261013/`. The frozen syntactic
inventory now has 55 remaining sites in 16 files. Other unchecked/implicit
accesses and the downstream whole-pair artifact gate remain open.

A bounded direct-fill experiment for dynamic IL parameter staging was deferred
under the former zero-regression gate; it is now prioritized for qualification.
It removed the custom four-element inline array and its fourteen generated
helper calls while filling the existing admitted `Type[]`, with the same work,
storage and heap allocations. The reversed-order repeat retained a roughly
3.36% three-parameter and 2.79% four-parameter construction penalty, despite
smaller inspected builder code. The original implementation is restored; no
performance equivalence or helper-removal claim lands. Arity, null rejection,
delegate execution after disposal and original-owner cleanup remain covered.
Evidence: `artifacts/il-parameter-staging-20261013/`.

The parser's seven-int case-fold set staging was also deferred under that old
gate and is now prioritized for qualification. A fixed stack span
removed eight generated inline-array helper calls with unchanged wire values,
work/capacity admission and heap allocations. The reversed-order repeat retained
about 0.8–1.3% parser penalties against roughly 0.5% movement in the unchanged
plain control. Inspected stack frames grew16 bytes and gained a stack guard.
Whole-constructor comparisons had large control drift and do not qualify cost
equivalence. Restore the original collection expression; no helper-removal or
speedup claim lands. Evidence: `artifacts/folded-set-staging-20261013/`.

Those historical construction costs fit the revised10% allowance, but are not
current-source qualification. Reuse existing arity/folding/resource checks, inspect
actual generated helpers and run one bounded public construction comparison with
an unchanged control. Do not seek exact equivalence or treat private parser/builder
timings as matching-workload regressions. PLAN.md records the finite implementation
order, BCL/prefix-reuse candidates, fixed-width admitted-block hypothesis and one
cumulative performance/package boundary; none is automatically accepted.

The current-source staging follow-up removes all fourteen parameter and eight
fold-set generated helper calls from the actual DLL. Parameter overloads fill the
same admitted Type[] directly; unused positions reuse p0 and are never copied.
Supplied nulls still reach CLR validation. The seven-int wire set uses a fixed
stack span. Work, capacity, heap allocations, arity, disposal and delegate validity
remain unchanged. Constructor-only probes reach both changed helpers, without a
private replay or first-match call providing the route witness.

One fixed-sixteen-instance public-constructor comparison and its single noise
confirmation cover emitted family/fixed-width routes, ASCII/Greek/escaped/long
folded patterns and an unchanged plain control. Confirmation affected medians
range -2.0% to +4.2%, with equal allocations; control moves +6.4%. Short sample
floors are0.6–1.9ms and some rows remain very noisy, so this is neither exact
equivalence nor cumulative qualification. Dynamic construction was not calibrated
into an unbounded loop. The confirmation forces GC outside each timed lane and
excludes that collection time; the initial samples retain the ordinary GC costs.

An initial source guard rejected three null-forgiving expressions. The final
unused-argument repair removes them without weakening the guard or changing
supplied-parameter validation. Its constructor argument setup was not retimed;
final-source construction cost remains part of the cumulative delivery gate.
The measured candidate's Linux builder body shrinks1457→1414 bytes and folded
helper grows383→414; Windows1248→1179 and395→444. Final Windows helper sizes
remain1179/444, without an instruction-equivalence claim. Final semantic/resource
and source checks pass. These implementation changes advance the checked batch;
they do not qualify the whole Core DLL or designate a package. Evidence:
artifacts/core-staging-checked-20261015/. No further pilot timing follows.

The 2026-10-13 vector-span follow-up also remains deferred. Checked span indexing
removed redundant per-vector checks from the forward loops while preserving block
order and the overlapping terminal vector. Four bounded code-generation variants
were investigated. Split width helpers improved long misses about 43–46%, but
short reverse misses regressed 5–28%. Inlining reduced the gains and retained short
reverse regressions. A forward-only shared method still altered reverse code.
Finally, directional forward AVX2 entry points retained the original `Find` code
(1205 bytes Windows/1242 Linux) and improved wide forward scans, but 31-byte forward
fallbacks slowed 8–15%. All measured allocations stayed zero; existing boundary,
offset, tail and except checks passed. Every prototype is restored; no conversion,
new selector or speedup claim lands. Evidence: `artifacts/vector-span-pilot-20261013/`.
Loop-bound elimination is proved useful; eliminating the short-path dispatch/layout
cost is still necessary before accepting this approach.

## PythonRe compiler-generated accesses

DLL inspection found `Unsafe.Add`, `As` and `AsRef` in generated inline-array
helpers even though PythonRe source contains no explicit Unsafe calls. Two raw
escape-text collection expressions and expanded three-digit octal span parameters
were their callers. A source-only search would miss them.

Scalar.34-dev borrows bounds-checked slices of the immutable pattern for the two
raw escape-text paths. It removes those two temporary inline arrays and their six
generated helper calls. Existing `CopyText` still owns/funds the same two-character
string; allocation, construction charges and escape semantics remain unchanged.
Supplementary pattern prefixes and mixed escaped operators/punctuation are covered.

The broader octal conversion was restored and deferred. Its initial result was
near the baseline, but a reversed-first repeat showed a persistent roughly 1.7%
paired parser penalty. The original variadic helper remains; no equivalence claim
or API gate closure follows from the attempted all-checked adapter.

The final escape-only change was measured separately using actual before/after
DLLs on the quiet VM, Release FullOpts, nine alternating samples, bounded calibration
and retained warmup. Heap allocations were identical. Parser medians moved +0.55%
for operators, +0.56% for punctuation and +0.52% for the unchanged plain control;
whole-constructor medians moved +1.09% for operators and +1.02% for the plain control.
These small movements track the unchanged controls; the escaped paths did not
show a persistent penalty across the bounded runs. The partial conversion is
accepted within this experiment's resolution, without claiming exact equivalence
or a speedup. Windows FullOpts escape code changes 1642→1647 bytes; octal caller
and callee stay 354/628 bytes. Additional checked-slice guards remain.

Raw DLL identities, IL callers, code generation, samples and failed observations:
`artifacts/checked-adoption-20261013/`. The initial fixed-stack-span variant,
broader borrowed-span trial, octal repeat and final selected source are retained.
An attempted blanket metadata guard is kept as experiment evidence, not shipped:
the final adapter still references Unsafe through octal preparation.

At scalar.34-dev the downstream whole-pair unchecked-API gate remained open:
core hot paths and the adapter's octal helper retained references. The consumer's
blanket prohibition also includes `SizeOf`, which is stricter than the user's
accepted sizing rule. That earlier borrowed-span octal experiment stays deferred.

## Fixed-arity octal preparation

A separate bounded follow-up replaces variadic span arguments with one- and
three-character arguments. It removes three-digit inline-array staging and the
single-digit CopyTo, preserving base-eight arithmetic, digit consumption, range
errors and construction charges. The three-digit error borrows the already
consumed pattern slice; the one-digit continuation keeps its checked stack span.
No manual sizes, helper framework, execution mode or SIMD algorithm changes.

The actual candidate PythonRe DLL has no Unsafe or LoadUnsafe references and no
inline-array helper calls, compared with eight such calls and three Unsafe member
references in its baseline. This does not close the core dependency's gate or
designate a new compliant package. The 55-site core inventory remains separate.

All 000–777 octals are checked as raw atoms and class members, with empty and
supplementary prefixes, valid byte values, range-error messages/positions and
tails. Numeric-reference ambiguity, finite matching and affected interruption/
construction checks pass. Nine actual-DLL parser probes preserve exact work,
reserved/peak bytes, depth and instruction totals, including malformed escapes.

Release FullOpts code on Windows and Linux removes span staging from the numeric
caller (354→284 / 322→287 bytes). The one-character callee is smaller than the old
shared callee (628→582 / 655→557); the separate three-character callee is 351/341
bytes. Aggregate code grows because the arities are separate; inspected hot
paths are smaller. Heap allocations remain identical in all measured lanes.

The first warm quiet-VM parser medians improve about 2–4%; the reversed repeat is
near baseline for three-digit parsing. Constructor drift is about +1.5% in both
runs while the unchanged plain control moves +2.0%/+2.5%. No persistent penalty
beyond control movement is observed within this pilot's resolution; no speedup
or exact equivalence is claimed. The conversion is accepted on that bounded
evidence. Raw observations/code/identities: artifacts/octal-fixed-arity-20261013/.

The whole-pair artifact gate remains open because core hot paths retain unchecked
references, including the user-approved SizeOf. Further work follows the revised
bounded public-cost gate above; kernel timings alone do not establish acceptance.

## URI vector-load pilot — deferred

The URI kernel already creates a checked tail span before its LoadUnsafe call.
A direct MemoryMarshal.Read conversion retains an extra width guard (Windows
511→539 code bytes). Advancing a remaining byte span still retains the guard
(542 bytes). A counted vector span removes per-vector checks but grows inspected
code to 549 bytes on Windows and 524 on Linux, versus 511/499 baseline bytes.
Vectors, masks, order, stop-byte handling and scalar tails stay the same.

One bounded warm VM run plus a reversed-first repeat measures nine vector-boundary
lengths and selective public Count/finite-control rows. Heap allocations are zero
for the kernel and unchanged for public calls. The 34-byte kernel regresses about
1.6% initially and 2.9% in the repeat; the short repeat also regresses about 3.3%.
Long scans improve, which does not qualify the slower boundaries. Restore the
exact production source and defer this path; no conversion or equivalence claim.

Retain the new offset/tail/stop-byte/scalar-fallback fixtures. Original and candidate
focused checks pass, including ordinary/compiled and finite-policy controls.
The restored Release DLL is rebuilt and audited to contain the original URI
LoadUnsafe call. The 55-site/16-file inventory and whole-pair gate are unchanged.
Evidence and all three source/code-generation variants:
artifacts/uri-checked-load-20261013/.

A bounded revisit under the 10% allowance is deferred for missing public-route
coverage. Public Count selects the owned scalar VM before URI direct dispatch,
even though its inspection descriptor identifies the URI family. Observed public
medians within the allowance therefore do not qualify the changed vector loop.
The candidate passes346 focused Release checks; exact original source is restored
and all raw samples are preserved in artifacts/uri-checked-10pct-20261014/.

## Fixed literal-byte indices — deferred

Replacing the literal-side trailing byte reads for five- and seven-byte literals
with checked indices preserves existing exhaustive bounds/offset/mismatch checks.
Inspected EqualsAt instructions are identical after address normalization on
Windows (331 bytes) and Linux (281 bytes), with no added bounds branches.
Nevertheless, the Linux reverse-order repeat retains about 3.4–4.6% slower
selected timings; unchanged lengths also move 4.4–8.9%. Windows samples contain
large control noise. All kernel allocations remain zero. These observations do
not establish the requested performance gate or a causal regression. Production
is restored in that historical trial; no equivalence claim follows from it.
The complete short comparator was subsequently accepted under the revised
public-cost gate above. Evidence: artifacts/literal-fixed-byte-index-20261014/.
