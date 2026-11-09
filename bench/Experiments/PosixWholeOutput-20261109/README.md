# Omit unobservable POSIX capture histories

Whole-only Match, Count and cursor advances still enumerate earliest-start
acceptance paths to find the longest match. They omit tag collection, undo,
history comparison/copy and closing-tag indexing. This is valid for the admitted
ERE: captures do not affect acceptance. Any requested public capture retains full
hierarchical history ranking, including groups outside a short destination.

Calls with no capture output allocate only repeat state. A flat pattern can need
no covered buffer, so an allowance refusing all acquisitions need not reject it.
Numeric limits still precede actual acquisitions. Cursors reserve full tag-state
capacity because subsequent advances may request captures; aliases can alternate
output windows without reallocating that state or invalidating retained values.
Work/depth/working thresholds can decrease. No public switch or second engine.

One .NET 11 RC3 quiet-VM ordinary-process ABBA completed in **51.777 seconds**,
under a declared 180-second cap. Fixed before is the accepted validated-projection
candidate; after changes only the POSIX workspace/call-site output requirements.
Same Core and worker DLLs throughout; 500 ms warmup, independent 75 ms calibration,
five samples per process and a 20 ms floor. Complete public results and zero
outstanding loans checked outside timing.

| Public operation | Before µs | After µs | Change |
|---|---:|---:|---:|
| Whole nested history, 12 bytes | 295.46 | 55.62 | -81.2% |
| Whole nested history, funded finite/live | 325.90 | 72.74 | -77.7% |
| Ambiguous Count | 1.01 | 0.34 | -66.5% |
| Count with capture ties, 16 records | 11.26 | 5.73 | -49.1% |
| Same Count, funded finite/live | 13.59 | 7.31 | -46.2% |
| Whole-only cursor, 16 records | 12.30 | 6.47 | -47.4% |
| Same cursor, funded finite/live | 15.13 | 8.86 | -41.4% |

These gains hold in both after processes with disjoint sample ranges. Whole
nested allocation falls 224→176 B (funded 8072→1256 B); ambiguous Count 208→144 B;
capture-tie Count 216→176 B (funded 1192→720 B). Unlimited cursor remains 392 B,
including its reserved tag capacity; funded traffic falls 1368→936 B.
Existence is roughly unchanged (-3.3% unlimited, +0.2% funded), with 48 B less
allocation. Full-capture controls move -1.5% to -3.5%, and unchanged Core Count
-1.1%; do not claim those small changes as causal speedups. No confirmation.

[summary.json](summary.json) retains process medians, ranges and allocation data.
The custom funded family deliberately reuses one allowance across four operations:
its recorded peak is cumulative within that family, not each row's isolated peak.
Do not infer individual working-peak reductions from it. Timings and per-call
allocations are independent observations; every call returns outstanding loans0.

Release POSIX checks pass Windows/Linux on both runtimes, including all short
capture spans, independent longest expectations, hidden-history priority, original
interruption/refusals, projection failure, and alternating cursor alias outputs.
Contract documentation records the revised admission requirements. No package or
full catalog rerun, and no new native parity ratio.

Raw worker, patches, fixed binaries and samples:
`artifacts/experiments/posix-whole-output-20261109/v-evidence.tar.gz`, SHA-256
`8AF6C7680E4BF1D32E0F8FF1DC6BABB06D0A9AD1FDC9BA4EBA428D3476F061F0`.
The subsequent Linux check log is alongside it as `tests-linux.txt`.
