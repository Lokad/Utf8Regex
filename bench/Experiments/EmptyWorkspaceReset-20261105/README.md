# Avoid reset calls on empty VM state

**Accepted with local costs.** Actual Unicode Split has no repeats, captures or
balancing, but TryMatchAt called Array.Fill on their empty arrays at every
candidate. Seven length guards skip only empty resets; nonempty arrays retain
the same Fill. No helper, runner, prepared flag, allocation or accounting change.
Workspace admission/lifetimes, captures/rollback/history/assertions, RTL/NB,
strict UTF-8/scalars and original cancellation/deadlines remain unchanged.

One **107.515-second** separate Default-process A0/B0/B1/A1 compares the exact
accepted CountValidationMetadata AFTER Core with parent `48180b61` plus the
archived one-file runtime patch. Baseline is reused without rebuilding; six
BC Full adapter/benchmark DLL/PDB companions and the typed worker are identical.
Normal eight-CPU startup then CPU2, producer warm1s/min16, BCL250ms/min4,
independent60ms calibration and five alternating samples. All180 producer batches
exceed45.2217ms. Complete strings/arrays/counts are consumed after timing.
BCL predecoded/decode-then lanes are equal-result diagnostics, not CPython
comparisons or catalog Status updates. [All observations](cost-summary.json).

| Public operation | Before us | After us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core literal control | 2.438 | 2.464 | +1.06% | 0 |
| Actual Unicode Split | 96.839 | 90.600 | -6.44% | 19512 |
| Same finite/live | 129.836 | 130.359 | +0.40% | 23712 |
| Same deadline/live | 177.446 | 171.844 | -3.16% | 23760 |
| Captured Unicode FindAll | 211.516 | 205.929 | -2.64% | 21216 |
| Long astral Split | 1340.864 | 1369.446 | +2.13% | 49272 |
| Lookbehind Split neighbor | 414.970 | 403.998 | -2.64% | 6736 |
| Captured ASCII Split | 144.420 | 149.166 | +3.29% | 84728 |
| Core terminal-category Count | 227.788 | 221.057 | -2.96% | 64 |

Both after-process medians90.31/91.00us beat both before96.04/97.31us on the
actual3200B/385-string Unicode Split. The finite row's after-process observations
123.28/138.64us straddle before127.07/132.26us; retain its pooled+0.40% cost.
Long astral and captured ASCII rows retain+2.13/+3.29% costs despite wide after
variation. Their after-process medians are1215/1508us and143.64/153.88us,
respectively. Captured FindAll/deadline movements are also uneven; do not claim
stable causal gains for those rows. Unchanged Core literal and terminal-category
Count controls move+1.06/-2.96%; no normalization or gain credit. The row named
assertion-driver-control is a lookbehind Split neighbor affected by the resets.
Allocations are unchanged everywhere. No confirmation or source tuning follows.

Focused Release Windows/Linux per .NET10/11: Core1697/Python1130/PCRE309 PASS.
Existing tests cover nonempty repeat/capture resets, balancing/assertion rollback,
histories, aliases/cursors, finite resource admission and original interruption.
No new implementation-existence tests. Current FullOpts confirms zero-length
guards before the retained Fill calls; TryMatchAt grows1078->1391 code bytes.
That code-size cost is retained, not an isolated speedup claim. No public contract
revision, package qualification or Full refresh for this pilot.

Immutable raw under artifacts/experiments/empty-workspace-reset-20261105,
verified off VM:

- evidence.tar.gz: `A4057F0C21CE250D8EA3DF89CC823FFD7C67E5BC2050CB68C253E5BE87DDBE2E`.
- windows-correctness.zip: `78FF8B7F69F91CABA68FA2C1425CB00FCBE20D3A5CF4FC04CB524787D551828D`.

Declaration `80100C5BAC20931A50132C2C894F41325AE2AB7D885022F3729405750797760A`
and runtime patch
`0FE7B6AA0AA08A84E509A2A887EBEA1E728ACD15CAC5CE1F3410EEBD686E766A`
were frozen before preflight/timing. Documentation follows measurement; runtime
is unchanged. Dev35 packages, raw and accepted producer graphs stay immutable.
