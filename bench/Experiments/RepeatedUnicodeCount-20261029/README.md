# Deferred mixed-UTF-8 repeated-segment Count

**Reverted.** Prototype 883c60df reused the proved greedy/disjoint repeated-
segment scanner for fully validated mixed UTF-8 Count. It retained the separator's
ASCII/Unicode-whitespace distinction, decoded only non-ASCII separators and used
checked span range search for contiguous ASCII start masks. Other masks kept
membership scanning. Value/capture/coordinate operations and finite-policy,
token/deadline, RTL/NB and diagnostic-checkpoint calls retained existing routes.
No executor, grammar, unchecked access or public mode was added.

[One public comparison](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt): exact accepted 47dbeeeb
versus 883c60df, .NET 11 RC3, quiet VM CPU2, symmetric load contexts, 500ms
warmup, independent calibration and three alternating samples. **37.58s** under
the 120s cap, minimum timed batch **47.22ms**. All producer, predecoded .NET
and decode-then-.NET result preflights and timed checks pass.

| Public Count | Before us | Prototype us | Change | Before / prototype B/op |
| --- | ---: | ---: | ---: | ---: |
| Full capitals, ordinary | 132129.100 | 12734.875 | -90.4% | 11880 / 0 |
| Full capitals, compiled | 132563.500 | 12690.575 | -90.4% | 11880 / 0 |
| 32 KiB, finite limits/live token | 574.218 | 535.870 | -6.7% | 3680 / 3680 |
| Dense Unicode separators | 104.107 | 5.971 | -94.3% | 18208 / 0 |
| Incomplete extra segment | 15.780 | 16.491 | +4.5% | 0 / 0 |
| Required-separator miss | 0.301 | 0.217 | -28.1% | 0 / 0 |
| ASCII-only separators, mixed subject | 159.370 | 24.782 | -84.5% | 3568 / 0 |
| No leading-class member, 4 KiB | 0.956 | 0.648 | -32.2% | 0 / 0 |
| Dense false starts, 16 KiB | 97.077 | 216.657 | **+123.2%** | 0 / 0 |
| Unchanged literal-prefix control | 11200.060 | 11343.380 | +1.3% | 3600 / 3600 |
| Existing ASCII native case, affected path | 3.968 | 4.875 | **+22.9%** | 0 / 0 |
| Unchanged literal control | 70.021 | 70.561 | +0.8% | 0 / 0 |

The corpus gain demonstrates useful mixed-input route potential, but does not
justify doubling dense-false-start cost or slowing an already native ASCII
workload. Unchanged controls are close; these are not ambiguous noise points.
The finite lane is unchanged source and its negative variation is not credited.
**No confirmation or tuning cycle.** Revert b654ea46 restores `src/`, `tests/`
and CHANGELOG.md to accepted b1642f88 exactly, preserving its repeated-segment
correctness fixes and green CI 40917142472. No snapshots or packages refreshed.

[Both-runtime checks](verification.txt) pass Unicode whitespace and negative
membership, ASCII-only masks, zero/upper-bound/noncontiguous candidate masks,
greedy endpoints, derived coordinates, slices, captures/aliases, malformed tails,
NB/RTL, finite admission and original cancellation/deadlines, plus affected
Core/PythonRe/PCRE2 route/resource guards. Initial byte-field/property and exact
exception-expectation fixture corrections, plus a missing VM bundle prerequisite,
remain archived. No semantic failure was hidden by the revert.

The qualified **9.64x compiled / 2.87x ordinary** capitals gap stays visible,
deferred for the current bounded pass. A future design must preserve the existing
ASCII and necessary-prefix false-start routes before admitting mixed Count;
simply admitting the current scanner is insufficient. Do not reopen this trial
or report its unshipped gains as a new benchmark verdict. Next Core gap: doc-line.

Immutable files under `artifacts/experiments/repeated-unicode-count-20261029-deferred/`:

- `parity-repeated-unicode-883c60df-deferred.tar.gz`: exact DLL/PDBs, host,
  source bundle, driver/binaries and build/preflight/raw costs.
  SHA256 `D57051C5DAE0E54AADD2CC8F954F9597D8FDF037C15B31A9A60D7B242FB230A2`.
- `correctness-and-setup.zip`: initial/final focused checks and preparation logs.
  SHA256 `17F5E3B219DD91D1BFF20BF3B99084357045715E224559E44C023C90E4F6EC56`.
