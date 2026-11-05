# Wider checked packed-nibble scanning

**Accepted.** The live Chinese-family Count shortcut scans three nibble-mask
positions at a time. On AVX512BW it now checks64 starts using the existing lookup
values, with checked MemoryMarshal.Read and native byte shuffles. Existing32/16/
scalar paths remain. No retained tables, allocations, mode or accounting changes.
The32-bit pending state stays compact: lower-half false candidates can require an
upper-half rescan. Full verification and nonoverlapping match order remain.

Strict complete-subject UTF-8/scalars, captures, RTL/NonBacktracking, retained
aliases, original cancellation/deadlines and work/depth/working/result admission
remain. This changes the live SIMD scan, not the bypassed prepared hybrid search
or the closed validation-fusion/first-scalar experiments.

One **70.062-second** separate Default-process A0/B0/B1/A1 compares the exact
accepted EmptyWorkspaceReset AFTER Core with parent `0988fd64` plus the archived
runtime/tests patch. Six BC Full adapter/benchmark DLL/PDB companions and the
typed caller are identical. Normal eight-CPU startup thenCPU2; producer warm1s/
min16, BCL250ms/min4, independent60ms calibration and five alternating samples.
All120 producer batches exceed51.0534ms. Complete counts are consumed after timing.
Equal-result predecoded/decode-then BCL lanes are diagnostics, not new catalog
Status or resource-policy equivalents. [All observations](cost-summary.json).

| Public Count workload | Before us | After us | Change | B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core literal control | 2.450 | 2.265 | -7.56% | 0 / 0 |
| Actual Chinese family | 143.575 | 119.166 | -17.00% | 0 / 0 |
| Same compiled | 143.631 | 120.019 | -16.44% | 0 / 0 |
| Same compiled finite/live | 44569.275 | 44622.950 | +0.12% | 640 / 640 |
| Dense family matches | 35.088 | 32.104 | -8.51% | 0 / 0 |
| Dense three-scalar-prefix misses | 280.975 | 231.175 | -17.72% | 0 / 0 |

Actual corpus813478B/SHA F129E819 returns207 matches. Ordinary after-process
medians118.71/119.56us beat before144.28/143.36us; compiled119.92/120.19us beat
144.10/143.58us. No control normalization: the unchanged literal's B1 process is
faster (2.12us versus2.44-2.45 elsewhere), and its pooled-7.56% is not credited.
The target's process separation supports acceptance without a confirmation run.

Dense workloads have large process movement. Matches' before42.95/27.40us bracket
after31.91/32.45us. Misses fall across the process order326.16/258.61/204.10/
221.06us; their pooled decrease is not a stable causal gain. Finite/live Count
remains roughly44.6ms,+0.12%,640B unchanged. Its much larger absolute cost is a
separate investigation priority, not a claimed improvement here. No source tuning,
confirmation, package qualification or Full refresh follows this pilot.

Focused Release Windows/Linux per .NET10/11: Core5829/Python1130/PCRE309 PASS.
Both platforms also pass77 targeted fallback tests per runtime with AVX512F
disabled, reusing matching binaries. Seven added cases exercise false candidates
across compact-state halves; existing borrowed-offset/tail cases extend through
96 prefixes and64 tails. Ordering/overlap, folded families, captures, strict input,
resource admission and original interruption retain their checks.

FullOpts confirms native zmm byte shuffles/shifts and retained narrower paths;
TryFindNextCandidate grows2069->2802 code bytes. That code-size cost and possible
upper-half rescans are retained. Neither codegen nor the earlier212-candidate/
207-match census is a public speedup measurement.

Immutable artifacts/experiments/packed-nibble-wide-20261105, verified off VM:

- evidence.tar.gz: `07CF42C92707CC84D0CA216A495F349DF4B76A389D3B618FFDC7EF1F2FA902D6`.
- windows-correctness.zip: `E923ABC0A115CFAECE58843F75C6963CD08CEB994E282A9CB99C206B97DBC017`.

Declaration `304A407127F9727E0BFB1DE7D6A592AD3E6311B4913B460010343BD646F32A86`
and runtime/tests patch
`5D51B0A306B7B0FF579810C71B6A4E9053E0F5B3B31F1F7EA6B29B6F3AD678E2`
were fixed before timing. Documentation follows measurement; runtime is unchanged.
Dev35 packages, snapshots, old raw and accepted producer graphs remain immutable.
