# Complete Unicode word-run Count — accepted

Producer `a9700d149dc740f78e217f1a74c4de7b0eb8ffa4`, baseline fbf58099. Extend the existing complete greedy
word-run counter across valid UTF-8 using Core's shared scalar word predicate.
Keep its ASCII lookup/run algorithm and <=32-byte scalar-aligned work blocks;
remove whole-input ASCII eligibility and mixed-text VM scratch. Positive paired
boundaries distinguish joiners U+200C/U+200D, which are boundary members without
being consumed by \w. One local run-edge flag, no new executor/state owner/cache/
prepared flag/mode. Internal kind/diagnostic labels now describe word runs;
no ASCII compatibility alias or dormant counter remains.

Both ordinary and caller-funded Count share this counter and original invocation.
Count ignores ordinary capture values; Match/results/cursors retain their current
executors. RTL/NB/ECMA/lazy/finite-repeat/extended-class exclusions remain. Validate
checks the whole subject once; AssumeValid trusts valid unchanged borrowed text.
Managed bounds, scalar units, synchronous original-token cancellation/deadlines,
basic metering and admission remain. No pattern/replacement policy change.
Mixed work/depth/working requirements intentionally fall; [Core](../../../docs/SPEC.md)
documents the change. Both policies' one-byte working/result, one-level depth
probe changes working-limit refusal to Count1024 without VM storage. Funded calls
preserve/refund their loans; no total heap/RSS guarantee is implied.

Windows.NET10/11 and Linux.NET11 each pass1923 focused Release checks. Tests include
independent BMP BCL results and a separate owned NFA execution oracle, supplementary
scalar counts, combining classes, joiners, captures,32-byte cuts/borrowed offsets,
bad tails, original interruption, finite admission and caller funding. No new
implementation-mirroring or test-only shipped executor.

ONE110.650s/180s ordinary-process ABBA,15 public rows/five150ms-target samples per
process,300 samples above20ms;0 confirmations. Fixed equal-result inputs/options/
policies per row, normal startup then existing processor placement, .NET11GA and
default tiering/PGO. One common Core-only caller, frozen distinct DLL/PDB pairs,
exact compiler-source checksums and all returned counts/funding checked after
timing. [All process medians, ranges and identities](acceptance.json).

| Public Count | Before us | After us | Observed change | B/op before/after |
|---|---:|---:|---:|---:|
| ascii-strict | 14.740 | 18.570 | +25.99% | 112/112 |
| ascii-trusted | 14.586 | 18.308 | +25.51% | 112/112 |
| mixed-strict | 869.778 | 50.828 | -94.16% | 1168/112 |
| mixed-trusted | 866.659 | 48.790 | -94.37% | 1168/112 |
| mixed-zero-strict | 382.731 | 26.423 | -93.10% | 640/112 |
| mixed-zero-trusted | 383.400 | 25.299 | -93.40% | 640/112 |
| supplementary-strict | 300.969 | 43.135 | -85.67% | 1168/112 |
| supplementary-trusted | 300.265 | 40.689 | -86.45% | 1168/112 |
| joined-boundaries | 752.469 | 43.421 | -94.23% | 1168/112 |
| joined-bare | 113.934 | 43.948 | -61.43% | 112/112 |
| captured-mixed | 901.014 | 50.730 | -94.37% | 1168/112 |
| mixed-unlimited | 854.308 | 51.670 | -93.95% | 1056/0 |
| funded-mixed | 866.053 | 51.685 | -94.03% | 1168/112 |
| unchanged-core-literal-count | 12.241 | 12.219 | -0.18% | 0/0 |
| unchanged-dotnet-count | 22.420 | 22.396 | -0.11% | 0/0 |

Retain the separated ASCII costs: strict+25.99%, trusted+25.51%, about3.8us for
15360B/1024matches. Accept them for mixed positive/zero/captured/funded reductions
of about93–94%, supplementary85–86% and bare joined61%; all affected sample ranges
are separated. Unlimited mixed allocation1056→0B; finite/funded positive1168→112B;
zero640→112B. Core and.NET controls-0.18/-0.11% overlap. No control normalization,
composed gains, universal10% gate or claim of new canonical parity. No follow-up
selector, annotation sweep or tuning campaign; this experiment is closed.

Preflight refused Git-normalized source overlays that differed from the physical
line endings in compiler PDB checksums. No timing had run. Preserve that failed
archive. Prepare only exact PDB-matching LF/CRLF source bytes, then freeze and
strictly verify overlays in every measured process. The same library pairs were
reused; only the common private verification caller rebuilt. No producer or
performance run was repeated. Source/lookup/semantic code did not change.

Raw verified offVM before keeper release under
`artifacts/experiments/unicode-word-count-20261114/evidence.tar.gz`, SHA256 `23515E7794C8A78467C5A73A10A8A4CDA5072F563D09B0ADDBB0C875DB3B892A`;
preflight `F2B03EB66C5F6CCA38CC821A8EBD89021393B9FAAEC2D83241FB0BA7C7C47E07`. All workers/keepers terminal0 after the
preserved preflight exit1. No package/Full refresh or publication; old dev87
package/symbol bytes remain unchanged. Next coherent delivery can qualify this
accepted Core batch and selectively refresh affected published word rows.
