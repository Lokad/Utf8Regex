# RTL strict-end Count candidate filtering

Parent `122db791`, source119-dev. An initial mandatory RTL `Anchor/VeryEnd` opcode
can match only at the subject's EOF. Count formerly retried later positions after
a failed, empty or nonempty match, and created VM scratch for impossible start
floors. A local initial-instruction predicate now returns through the existing
`Utf8InvocationBufferControl.TryChargeWorkspaceStep` at an ineligible position.
No helper, field, owner, cache, API, signature, execution mode or algorithm is added.

The terminal step preserves original post-hook cancellation/deadline checks and
typed work denial. Full-input UTF-8 validation still precedes matching. Captures,
empty/scalar results, start floors, `AnchorOriginByte` and retained-result lifetimes
keep their scope. Soft/multiline ends and anchors merely within alternatives or
assertions do not prove an initial strict end. NonBacktracking keeps its separate
engine. Strict `\z` remains absolute under multiline options.

**Intentional accounting revision:** removing impossible attempts lowers modeled
work. Impossible floors can avoid VM counts/positions scratch; caller capture
slots retain their admission. Major allocation, depth, backtracking/comparison and
result controls remain. Ordinal references still compare per byte: the rejected
span prototype is absent and remains closed. Immutable118 is unchanged.

## One bounded public pair — accepted

Quiet Xeon8573C CPU2/.NET10.0.12, FullOpts/R2R off. Only Core DLL differs; same
driver/input/control bytes. Reuse the existing68 ordinal-reference public lanes
and calibrated loop, adding12 strict-empty/offset/soft-end/alternative and .NET
rows before timing. Warmup120ms, target80ms, five alternating-order samples,
80s phase/180s batch caps. One101.310s pair,800samples>=55.249ms. No confirmation
or further timing is needed. All28 root/group/subject and80 operation preflights
agree. Count comparators include24 strict-decode/predecoded .NET lanes; detailed
views consume root/group ranges, not a fabricated BCL materialization equivalent.

Public medians in microseconds:

| Operation | Before → after | Allocation B/op before → after |
| --- | --- | --- |
| RTL long miss, first difference, unlimited |1765.698 →265.602|105816 →105816|
| Same, finite |1820.650 →276.920|105928 →105928|
| RTL long miss, last difference, finite |1782.256 →238.934|105928 →105928|
| Pure strict-end Count,64KiB, unlimited |5874.664 →3.363|0 →0|
| Same, finite |6042.257 →2.760|112 →112|
| Impossible offset, unlimited |738.354 →0.671|128 →64|
| Same, finite |766.270 →0.699|240 →176|
| Same, finite/live |747.016 →0.758|240 →176|

Long captures contain8193bytes. RTL misses improve about85–87%; matched long
Count/detailed/enumeration rows stay within1% of the original costs. The largest
observed Core regression is2.29% in the unchanged generic-VM finite control.
Core allocation medians are unchanged except the64B scratch reduction at impossible
floors. The native unlimited literal control moves+0.79%; its finite neighbor
−8.43%, and the root-alternative control−7.05%. Their gains are not attributed to
the strict-end mechanism. True .NET controls range−6.57..+2.32%. Short Core rows
have spreads up to1.163x; no fixed tiny-case speedup is claimed.

The direct production work probe confirms strict RTL miss65691→32920steps,
wrong-floor16390→1, and empty-EOF32775→4. Soft/alternative instruction guards
and results retain their original counters. This is work evidence, separate from
the public timings. These source FullOpts observations do not qualify package
119/default tiers, CPython Status, full-runtime coverage or the closed checked52
campaign. Different source images/experiments cannot be merged into causal package
claims or used to reopen the rejected ordinal-span trial.

## Correctness and provenance

Focused Release checks pass: Core232 plus1618 resource/ECMA/capture and additional
malformed-offset checks,33differententials, Python1556, PCRE104. Existing PCRE
duplicate-ID discovery warnings remain; recorded results have no failures/skips.
The new24 cases cover strict/soft/multiline/alternation/assertion/contiguous shapes,
ordinary/compiled and finite/live public Count and CountFromUtf16Offset, valid
scalar offsets, borrowed slices, retained captures, malformed complete subjects,
bounded empty Count, scratch avoidance, every work cutoff and caller loans.
Deterministic clock/token injection on the final step verifies original authority
before publishing either a completed Count or a wrong-floor zero.

Caller audit finds Core, prepared-scalar and PCRE's proven-LTR nonempty Count.
Each uses only the Count/breach outcome and releases its admitted capture slots;
none projects their contents as a Count result. PCRE's LTR proof declines this
RTL shortcut. No caller ownership contract is changed.

Baseline Core `90301FE6F42FF36F16AE848130117AD24C216229E324FD9E06F2860949F5D080`;
candidate `3BDAAAE315D6175672A3A1AD889743C09CD1682D53F24E8ECD6B70518E78C88E`.
[All raw samples, preflights, identities and work/plan proof](measurement.json).
Archive `685F142A5A0A32E2D01AE0249EA96C7D8A143297BA0967255C7139C85FCDF513`.
Local predeclaration/source/logs: `artifacts/rtl-strict-end-20261020`. A new fixture
local function was initially placed after record declarations; it was moved before
them before any preflight/timing. That setup failure remains preserved.
Source CI follows separately. Broader original R01–R10 remains open; the next
coherent package boundary can batch the two accepted source119 Count changes.
