# Bounded checked-access work

The core matches strict UTF-8 scalars. Checked-access conversions must preserve
algorithms, SIMD, allocations, semantics and resource controls, with focused
correctness, Release code-generation and cost evidence. `Unsafe.SizeOf<T>()` is
accepted for actual managed sizes; manual size estimates are not a substitute.
There is one engine and no permanent safe/unsafe selector.

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

The downstream whole-pair unchecked-API gate remains open. Core's hot paths and
the adapter's octal helper retain references; the consumer's blanket prohibition
also includes `SizeOf`, which is stricter than the user's accepted sizing rule.
No compliant core/PythonRe package or consumer promotion is claimed. Further work
needs a concrete JIT/loop-structure reason and another bounded proposal, rather
than repeating the same checked loads or expanding the benchmark campaign.
