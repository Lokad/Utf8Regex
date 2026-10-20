# Ordinal backreference span comparison — rejected and reverted

Parent `bf4f54f1`, development 119. The historical R09 decision to inherit span
backreference equality through BCL execution no longer applies to the owned engine.
A prototype replaced its ordinal per-byte comparisons with the existing bounded
`Utf8SearchExecutor.TryCompareLiteral`, without capture copies or a new helper,
signature, owner, cache or execution mode. RTL retained comparison chunks ordered
from the right. Folded references kept their scalar walks and different-width
equivalence, including Kelvin. Candidate comparisons checked original cancellation
and absolute deadlines before each chunk of at most 2,048 bytes.

The proposed per-byte-to-chunk work/cadence change is **not promoted**. Production
source and the DLL are restored byte-for-byte to the baseline; existing resource
accounting, native paths and lifetime contracts remain. New semantic/resource
tests are retained. Immutable118, older/failed bytes and original handoffs are intact.

## Public operating-cost decision

One initial pair and its sole reversed-order confirmation used identical frozen
Core DLLs, driver, inputs, controls and protocol. Only Core differed between phases.
The quiet Xeon 8573C CPU 2 ran .NET 10.0.12 FullOpts/R2R off. Each phase warmed
for 120 ms, calibrated to 80 ms and took five alternating lane-order samples;
phase/batch caps were 80/180 seconds. The first pair took 85.890 s, the confirmation
86.075 s. All 680 samples per pair exceeded 62.713/68.738 ms respectively.
There is no further timing, candidate tuning or package qualification.

Each pair covers 68 public operations: short/long/first-/last-difference, supplementary
and empty references, RTL, ordinary/compiled, finite/live/deadline, selected detailed
and complete-value operations, folded/non-reference controls and 20 strict-decode /
predecoded .NET Count controls. Detailed calls consume root/group byte ranges;
their stack-only contexts do not escape. The .NET controls compare overlapping
Count results, not equivalent detailed-output materialization. All 28 root/group /
subject and 68 operation preflights agree in every phase; four actual plans prove
owned Backref instructions with the appropriate ordinal/folded flags.

Representative public medians, microseconds:

| Operation | Initial before → after | Confirmation before → after |
| --- | --- | --- |
| Long LTR hit, unlimited Count | 255.153 → 221.870 | 254.773 → 222.498 |
| Long LTR hit, finite Count | 263.672 → 234.932 | 265.590 → 235.921 |
| Long RTL hit, finite Count | 276.567 → 248.219 | 275.614 → 248.438 |
| Long LTR hit, finite Detailed | 264.566 → 235.868 | 264.388 → 236.263 |
| Supplementary LTR capture, finite Count | 95.787 → 69.007 | 96.472 → 68.729 |
| Supplementary RTL capture, finite Count | 101.451 → 70.159 | 100.972 → 70.049 |
| **Unchanged unlimited literal Count control** | **3.661 → 5.017 (+37.04%)** | **3.686 → 5.195 (+40.95%)** |
| Finite literal Count control | 3.859 → 3.827 | 3.848 → 3.839 |

The long ASCII captures contain 8,193 bytes; supplementary captures contain 6,150
bytes. All allocation medians are unchanged. Despite the useful observed reference
gains, the literal-control shift repeats with small within-phase spreads (about
1.01–1.02x). Its cause is unproved; this fails the declared stable-control/public-cost
gate. Do not attribute that slowdown specifically to the span primitive or claim
acceptable overall cost. The unchanged .NET supplementary control also moves
about −10.5% in both pairs, another limitation on causal interpretation. Short
Core rows have spreads up to 1.162x; no fixed short-case speedup is claimed.

## Correctness, restoration and remaining work

Candidate Release checks pass: Core394 plus1721 resource/ECMA/RTL checks,32
backreference differentials, Python1556, PCRE104. Existing PCRE duplicate-ID
discovery warnings remain; recorded results have no skips or failures.
The restored production source passes394 focused checks, including the14 new
ordinal-reference cases. They cover scalar byte ranges and supplementary captures
across 2,047/2,048/2,049-byte boundaries, empty references, first/last differences,
all work cutoffs, typed public denials, caller loans and retained capture views.
Original cancellation/expiry is injected during comparison of an already-completed
600 KB capture; the caller's reservation survives cleanup. Folded Kelvin references
retain their independent byte widths.

One new test initially assumed internal Count progress is cleared when a denial
is returned. The internal outcome carries both fields; public callers must throw.
The corrected assertion checks the public typed failure and preserved caller loan.
Existing tests were not weakened. The initial failed-test log is retained privately.

Baseline/restored Core:
`38405E89308F734DB90FC4A1483D024BCD7CAB858C32658896111E9DAD53E1A1`.
Rejected Core:
`84902953FD86617E9A0F4F51BF3D475D7163F4ADA7D2CC8BCAD9B32EE8DD4245`.
[Both complete raw observations, order, identities and checks](measurement.json),
[rejected source patch](rejected.patch), [public fixture](Program.cs).
Archives: initial `C2CC0C4AAD35AE0867B1414DB024AF185483DE99031A89E0C04CD750B14F3F82`;
confirmation `EC46C6A36C316BA9DF9C66E2E8DC8E07DB678EF89B56CB1938D6297FB32413C2`.
Local source/predeclaration/failures/logs: `artifacts/ordinal-backreference-20261020`.

A separate next lead is that Count retries starts ruled out by a mandatory initial
RTL strict-end anchor. Its proof must preserve terminal authority, start floors,
empty/scalar/capture and soft/multiline/alternation behavior. No change or speedup
is claimed for it here. It does not reopen this closed span-comparison prototype.
Source/test CI follows separately; the original broad R01–R10 goal remains active.
