# Immutable subject validation policy

Core/PythonRe/PCRE2/Posix now select `inputValidation` once at construction or
factory creation and expose read-only `InputValidation`. Default **Validate**
checks the entire subject at most once per operation/cursor lifetime.
**AssumeValid** omits that pass; the caller guarantees valid, unchanged borrowed
UTF-8. Pattern/template checks, checked scalar matching, byte coordinates,
interruption and resource admission remain. See the [contract](../../../docs/SPEC.md#subject-input-validation).

API/docs/tests commit `4d4a42a493bc122d526849e628dfb784d3c23470`;
measured final source `40509f3e0c7ddcfa01a339cd7b16c057785fa44d`.
No package was rebuilt or published; immutable dev65 evidence is unchanged.

## Bounded public comparison

Quiet Linux VM, .NET11 RC3, eight processors visible at startup, then affinity
CPU2. One ordinary process/policy; ABBA Validate/AssumeValid/AssumeValid/Validate.
Same frozen producer in every slot, verified four DLL/PDB identities and source
revision. Each row independently calibrates to75ms, warms500ms and retains five
samples/process. Result arrays are allocated outside timing; every result is
checked/consumed afterward. Both smoke passes verify expected results and equal
input hashes before timing. No tiering overrides, native comparator or source
checkout changes. Outer cap180s; each worker45s. Final run58.299s,280samples,
minimum74.734ms. These compare **different input contracts**, not producer
versions or .NET parity. The isolated validity helper is phase attribution,
not public matching throughput; do not subtract it from unrelated rows.

Median microseconds per public operation, Validate / AssumeValid. Allocations
were identical in every lane. Full per-process medians, ranges, input hashes,
checksums and initial results are retained in [summary.json](summary.json).

| Public operation | Validate µs | AssumeValid µs | Change | Allocated B/op | Sample ranges overlap |
|---|---:|---:|---:|---:|:---:|
| core/ismatch-early | 7.2347 | 0.0700 | -99.03% | 0 | No |
| core/count-unicode-literal | 90.8439 | 90.2343 | -0.67% | 0 | Yes |
| core/count-ascii-dense | 3551.1457 | 3530.9109 | -0.57% | 0 | Yes |
| core/count-unicode-finite | 149.9586 | 96.1000 | -35.92% | 112 | No |
| core/replace | 567.9655 | 570.3581 | +0.42% | 29936 | Yes |
| core/cursor | 661.5985 | 652.5972 | -1.36% | 18896 | No |
| python/search-early | 4.4883 | 0.0681 | -98.48% | 0 | No |
| python/findall-captures | 821.9212 | 809.6565 | -1.49% | 30024 | Yes |
| python/split | 118.4438 | 118.0060 | -0.37% | 45136 | Yes |
| pcre/count-branch-reset | 392.1833 | 407.2034 | +3.83% | 0 | No |
| pcre/ismatch-grapheme | 4.3333 | 0.0615 | -98.58% | 0 | No |
| posix/captures-prefix | 534.1103 | 401.0214 | -24.92% | 64 | No |

Early Core/Python literal search and PCRE2 lone-grapheme Boolean avoid a full
subject scan. Finite Unicode Count saves validity work; POSIX still performs
required capture-coordinate scans. Unmetered literal Count retains its existing
fused matching/ASCII-eligibility kernel, so its whole-operation change is small.
Dense counts and output/capture-heavy operations remain dominated by matching
and materialization. Retain PCRE2 branch-reset Count's **+3.83%** measured cost;
its two AssumeValid process medians differ by about5.4%. No normalization or
extra confirmation was used. Small overlapping changes are inconclusive.

The unchanged strict-small Count control is0.1070/0.1073µs (+0.23%, overlapping);
the unchanged strict-validity helper on the Unicode Count subject is76.8454/
77.8016µs (+1.24%, overlapping). Removing validation does not remove required
coordinate/classification or matching work. These results do not establish an
across-version performance guarantee for the default Validate policy.

## Initial findings retained

The first58.196s comparison on4d4a42a4 exposed AssumeValid unlimited literal
Count taking a slower general counter (+53.53%) and PCRE2 Boolean deriving
discarded UTF16 metadata (+99.10%). The scoped correction preserves the existing
specialized Count algorithm and moves the existing lone-grapheme Boolean proof
before metadata preparation. It retains actual byte/scalar-floor checks and
basic candidate metering. Final literal Count is90.8439/90.2343µs; grapheme
Boolean4.3333/0.0615µs. These two pairs are within each producer; they are not a
controlled before/after source comparison. The initial unchanged strict control
moved+12.33% between policy contexts; retain that observation rather than adjust
the timings. Only the initial and one corrected comparison were run.

## Correctness and retained evidence

Existing signatures remain (zero removed API snapshot entries), including
method-group and bare-default bindings. Tests cover default malformed suffix
rejection, both-policy valid ASCII/BMP/astral/capture/empty equivalence, derived
and funded programs, byte/text templates, cursors, invalid policies, scalar-floor
offsets, original-token cancellation, absolute deadlines and finite admission.
Subject-validation counters assert at most one strict pass, zero in AssumeValid;
pattern/template validation remains independent. Release affected controls pass
on both targets: Core6534/Python384/PCRE215/Posix8, then corrected routes
Core1405/PCRE275 and grapheme policy/boundaries7. Main API source CI42800442276
passes Windows/Linux and disabled-dynamic-code checks; later CI is recorded in
PLAN.md, not a claim of package qualification.

Raw archives below remain under ignored artifacts; the driver archive contains
the exact C# worker/project and bounded run/summarization scripts. Project refs
assume the original `tmp/input-validation-20261110/worker/` position in a source
checkout. The experiment is a receipt, not another benchmark framework.

| Archive | SHA256 |
|---|---|
| `artifacts/experiments/input-validation-20261110/evidence.tar.gz` | `A241F308B179B72469A23D8C9EA5D25FEAF05CD6E014ECF526FB8AEA44475945` |
| `artifacts/experiments/input-validation-20261110/evidence-final.tar.gz` | `653A38E59444480743FC8FA0EAC8669F97C89BD50C2D783FB6A4F4EC66D2A909` |
| `artifacts/experiments/input-validation-20261110/driver.tar.gz` | `5849CD56DE0111046FD0F2ADDE0470112E7D0E94D4BD650A2E778A8B54B5C677` |
