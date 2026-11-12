# A reachable whole-match boundary in POSIX

Accept the PosixWorkspace-only implementation on `d35a80e3`. When the existing
global atom test cannot exclude the next scalar, a bounded reachability analysis
can exclude atoms that cannot occur there. It relaxes every repeat to body/exit
and ignores captures, giving a superset of concrete paths. No reachable consumer
across the boundary proves that the saved whole endpoint is maximal. The existing
hierarchical history selector then prunes its losing prefixes normally.

This uses four local ulong sets, the existing 64-instruction scope and at most
2048 accepted-prefix bytes. No retained metadata, allocation, cache, additional
owner, capture executor, public option or dependency. Every visited instruction
and class lookup uses the original invocation's work/interruption authority.
Unknown/cutoff cases retain physical EOF. Scalar predicates, actual anchors,
conditional strict validation, captures, aliases, admission and refunds retain
their contracts. Proof work changes numerical work thresholds. SPEC and release
notes record the behavior; no general polynomial/history-parity claim follows.

An independent AST-language model checks actual lowered programs: 40 programs,
267 subjects, 40600 endpoint comparisons, 652892 abstract-state visits and 19821
valid ceiling proofs in 0.452 seconds. Its 1733 extra abstract endpoints confirm
intentional relaxation, not exact execution. This is a bounded model, not a general
formal proof of capture dominance. Transition inclusion was reviewed for all
twelve admitted opcode kinds, including anchors and nullable/finite repeats.
The unchanged caller also compares 21320 complete whole/capture observations
against frozen producer bytes on each platform. Eight actual 32-scalar public
calls refuse100001/100000 work before and return correct matches afterward, under
both input policies. Existing independent occurrence-oracle tests remain.

Windows/Linux Release POSIX369 tests pass per .NET10/.NET11, with Linux software
execution also369 per target. Both producer PE/PDB identities and 342 Core/10
POSIX documents per side verify, including twelve generated observations and only
one changed production source. The common Core pair is frozen explicitly.
Untimed checks on the measured binaries cover instruction lengths62/63/64/65 and
ASCII/BMP/astral accepted-prefix cutoffs around2048 bytes:22 observations agree
on Windows, Linux and Linux software execution. No library rebuild or retiming.

One **114.387-second** fourteen-row, two-policy ordinary-process public ABBA:
560 samples, minimum68.017ms, maximum42.648MiB retained batch. Normal eight-CPU
startup thenCPU2, .NET11.0.0/default tiering/PGO, per-row warmup,75ms target/20ms
floor. Complete results are consumed after timing. Distinct POSIX DLL/PDB pairs,
common Core/caller/.NET hashes, inputs and frozen source verify throughout.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Dot tail,16 scalars |737.134→3.708|743.906→3.530|
| Negated tail,16 scalars |701.252→3.395|700.539→3.292|
| BMP captures,12 scalars |106.746→3.078|111.065→2.860|
| Astral captures,12 scalars |107.690→3.208|111.888→2.981|
| Finite/live captures |895.364→4.429|884.526→4.451|
| Funded/deadline captures |964.787→5.660|962.457→5.517|
| Cursor/aliases |905.442→5.240|886.108→5.174|
| Whole-only Count |354.760→2.227|351.607→2.138|

All target ranges are disjoint,97.0–99.5% reductions. Ordinary allocation remains
0B. Finite3936→2608B, funded3984→2656B and cursor4304→2976B; modeled shared working
peak2689→1985B includes original parent17B, with final loans0. Modeled admission
is not total heap/RSS. No normalization or composed recovery of earlier costs.

Accept the **earlier-shorter control cost**43.382→50.834µs(+17.18%) and
43.261→48.821µs(+12.85%), both disjoint: its reachable continuation prevents the
new proof, adding work before general competition. Also retain finite-counter
.762→.903µs(+18.40%)/.754→.882(+17.01%), overlapping, and SKU+7.30/+5.28%,
overlapping. Global-boundary−1.16/+1.53%, .NET+.33/+.72% overlap. Unchanged Core
−6.42%strict/disjoint and+2.31%trusted/overlap receive no causal credit. These
local costs are intentionally accepted for the much larger affected gains;
there is no blanket10% veto or neutrality claim. [All observations](cost-summary.json).
No confirmation, source tuning, full catalog or package qualification follows.

Canonical hash-verified offVM artifacts/experiments/posix-language-ceiling-20261112:

- Model/baseline: `1C7A3CF9365C1957F1B00500B5563C7E150195210DBE4264908A443F3CEDB27E`.
- Gate: `97937F6824061DBB3991327F19D6FF53B6AAC7FF98908FAF4C4C659AB8D36DFE`.
- Public: `2E35B63F3B7455A802A788D3194925D9950D895DEAAE967FB44EFF90066408F9`.
- Windows: `2B545316346ECD8E2FC103217FD0637820C2428EECE6C81E5DC144C53A54F30C`.
- Linux boundaries: `195BD446AB5DBAB2DB9EC991BD86D2996DD768B86B6CB0C98CA9679EF564152A`.
- Windows boundaries: `818F7042A95FC8072158480526B94B13A3827336861947B564FDE54D77B38775`.

Initial private probe preparation corrected NoMatch coordinate access; the boundary
probe corrected an unsupported value comparison operator. Initial test imports
and obsolete trailing-count/history-phase fixtures were repaired before freezing
the source. The general-history fixture now uses an unresolved parent and still
injects original cancellation/deadline in actual competition. Preparation corrections and build/test logs
are retained; no production workaround or repeated timing. All workers are terminal,
keepers released after offVM verification; dev.74/all452 staged assets stay intact.
Unresolved parent histories, long-program/cutoff cases and broader Core/Python
costs remain active work.
