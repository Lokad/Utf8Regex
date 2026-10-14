# Bounded checked-access work

The core matches strict UTF-8 scalars. Checked-access conversions must preserve
algorithms, SIMD, allocations, semantics and resource controls, with focused
correctness, Release code-generation and cost evidence. `Unsafe.SizeOf<T>()` is
accepted for actual managed sizes; manual size estimates are not a substitute.
There is one engine and no permanent safe/unsafe selector.

The current source inventory is 55 sites across all shipped projects: **53 Core
sites in 15 files**, plus two PCRE2 null-reference placeholders in one file.
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
| Utf8LiteralEquality /17 | Complete candidate Slice before length-dispatched 4/6/8-byte reads and overlapping tails. End admission is repaired; checked-load pilots remain deferred. |
| AsciiSearch /6 | Serving callers pass equal-length checked slices; folded 9–16-byte dispatch supplies both 8-byte windows. This internal precondition remains necessary. |
| PreparedByteSet /2 | Width chosen from subject length; positions clamped to0..length-width, including final overlap. Closed pilots regress at short boundaries. |
| PreparedSearchPrimitives /4 | Correlated windows stop at length-compareOffset-width; candidate confirmation slices the full admitted literal. No new conversion qualified. |
| PreparedMultiLiteralSearch /2 | Constructor selects offsets below shortest literal length; block start≤length-shortest-(width-1), bounding every offset window. No new conversion qualified. |
| AsciiOrderedLiteralWindowExecutor /4 | Final block start reserves both trailing-literal displacement and vector width; scalar remainder scans the rest. No new conversion qualified. |
| Utf8AsciiDeterministicFixedWidthCountExecutor /1 | Checks length equals width3..8; vector limit reserves31 extra starts, bounding each check-offset load. No new conversion qualified. |
| Utf8AsciiExactLiteralCountExecutor /1 | Each32-byte load has index≤length-32; candidate full literals are admitted separately. No new conversion qualified. |
| Utf8AsciiLiteralBetweenNegatedRunsExecutor /2 | Full32/16-byte blocks only; neighbor reads have candidate lower/upper bounds. No new conversion qualified. |
| Utf8AsciiUriTokenExecutor /1 | Checked suffix span and full32-byte admission; closed URI conversions regress at boundaries. |
| Utf8EmittedLiteralFamilyCounter /1 | Nonnegative scan index≤length-32; separate candidate comparison/validation handles tails. No new conversion qualified. |
| Utf8InvariantCyrillicLiteralCountStrategy /1 | Full32-byte blocks, scalar/continuation fallback for tails. No new conversion qualified. |
| Utf8LiteralCompiledEngineRuntime /6 | Runtime and diagnostic correlated offsets0/1/3 reserve a complete literal plus31 starts. Diagnostic conversion regressed; timed kernel remains. |
| Utf8UnicodeCategoryExecutor /2 | Full32/16-byte blocks from current nonnegative scan index. Category pilot retains a long-input penalty. |
| Utf8ValidationCore /3 | Full32/16-byte blocks from scalar-boundary offsets. ASCII-drain conversion regressed; common UTF-8 drain remains unconverted. |

The two independent PCRE2 `Unsafe.NullRef` sites denote the inactive arm of a
stack-only borrowed projection authority. `_isSlice` selects the populated arm
before access. This is an invariant, not a claim of checked replacement or a
reason to delay the core/PythonRe delivery boundary. Compiler-generated Core
inline-array staging also remains deferred after the recorded cost penalties.

The ASCII validation drain pilot is deferred. Counted checked vector spans keep
the same SIMD reduction and scalar tails and eliminate per-vector range checks,
but add entry/setup/index work. Linux FullOpts code grows454→533 bytes; long
kernel controls regress30–39% and mixed public validation12–15%, with unchanged
allocations. Production is restored and its method IL matches baseline. New
borrowed-offset, Unicode-stop, malformed-tail and exact work-cutoff checks remain.
One short kernel sample missed the timing floor and is not parity evidence; the
long controls do not need a repeat to decline this conversion. Evidence:
artifacts/validation-ascii-drain-checked-20261014/. The55-site inventory and Core
unchecked API gate remain open; no safety/performance equivalence claim follows.

The correlated-literal diagnostic census conversion is deferred. Direct checked
reads keep the existing SIMD algorithm but add guards and increase inspected
code692→766 bytes on Windows and718→776 on Linux. Long diagnostic replays regress
about10–11%, with equal zero allocations; the timed production kernel retains
identical code578/568 bytes. Production is restored and census boundary fixtures
remain. Evidence: artifacts/census-checked-load-20261013/. No runtime speedup or
whole-DLL safety claim follows; the55-site inventory remains unchanged.

The category math-symbol reduction pilot is deferred. Checked vector-span
indexing removed per-vector checks while preserving widths, comparison order,
popcounts, tails and allocations. Short kernels improved 4–10%, but the 4 KiB
kernel retained a 0.3–1% penalty in normal/reversed runs, with larger helper setup.
Production is restored and boundary fixtures remain. Public Core Count selected
another route, so its timings do not qualify this kernel. Evidence:
`artifacts/category-checked-load-20261013/`. The 55-site inventory is unchanged.

The original vector-search and short-literal pilots are closed. Checked vector
loads retained branches and regressed 7–38%; short-literal variants retained tail
checks or inconclusive/regressing 4–7% timings. Those hot paths remain deferred.
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

A bounded direct-fill experiment for dynamic IL parameter staging is deferred.
It removed the custom four-element inline array and its fourteen generated
helper calls while filling the existing admitted `Type[]`, with the same work,
storage and heap allocations. The reversed-order repeat retained a roughly
3.36% three-parameter and 2.79% four-parameter construction penalty, despite
smaller inspected builder code. The original implementation is restored; no
performance equivalence or helper-removal claim lands. Arity, null rejection,
delegate execution after disposal and original-owner cleanup remain covered.
Evidence: `artifacts/il-parameter-staging-20261013/`.

The parser's seven-int case-fold set staging is also deferred. A fixed stack span
removed eight generated inline-array helper calls with unchanged wire values,
work/capacity admission and heap allocations. The reversed-order repeat retained
about 0.8–1.3% parser penalties against roughly 0.5% movement in the unchanged
plain control. Inspected stack frames grew16 bytes and gained a stack guard.
Whole-constructor comparisons had large control drift and do not qualify cost
equivalence. Restore the original collection expression; no helper-removal or
speedup claim lands. Evidence: `artifacts/folded-set-staging-20261013/`.

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
references, including the user-approved SizeOf. Further work needs a concrete
source/JIT reason; do not rerun the deferred loads or expand this benchmark pilot.

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
artifacts/uri-checked-load-20261013/. Do not extend this closed pilot into another
benchmark campaign without a concrete new reason.

## Fixed literal-byte indices — deferred

Replacing the literal-side trailing byte reads for five- and seven-byte literals
with checked indices preserves existing exhaustive bounds/offset/mismatch checks.
Inspected EqualsAt instructions are identical after address normalization on
Windows (331 bytes) and Linux (281 bytes), with no added bounds branches.
Nevertheless, the Linux reverse-order repeat retains about 3.4–4.6% slower
selected timings; unchanged lengths also move 4.4–8.9%. Windows samples contain
large control noise. All kernel allocations remain zero. These observations do
not establish the requested performance gate or a causal regression. Production
is restored; no conversion or equivalence claim lands. The broader comparator
loads remain deferred. Evidence: artifacts/literal-fixed-byte-index-20261014/.
