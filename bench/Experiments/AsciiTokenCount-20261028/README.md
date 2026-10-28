# Reuse the admitted ASCII token Count kernel

3f5f0847 runs the existing checked dotted-decimal Count kernel before the
generic scalar selector. The planner already proves the complete positive ASCII
language and ordered octet alternatives; this adds no grammar or matcher.
Full strict UTF-8 validation precedes the kernel. Wider scalars cannot contain
ASCII digit or separator bytes. RTL, NonBacktracking, finite policies, live
tokens, deadlines and diagnostic checkpoint hooks retain their original routes.

[Raw comparison](comparison.json), [console](comparison.txt),
[driver](compare.cs.txt), [project](compare.csproj.txt),
[five-sample README refresh](refresh.txt), [verification](verify.txt).
Same quiet VM, .NET 11 RC3, CPU2, VM-built frozen a40bda03 versus 3f5f0847.
Four public Count lanes use independent calibration, 250 ms warmup and three
alternating samples, targeting at least 50 ms per lane. Slow baseline calls
retain whole-operation durations. Results agree with .NET before and during
timing. One comparison completed in **22.51 seconds**, within its 120-second
cap. No confirmation or favorable rerun.

| Public Core Count | Before | After | Change | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Full 6,839,410-byte corpus, ordinary, 5 matches | 1109.947 ms | 2.675 ms | 414.9x faster | 3568 / 0 |
| Compiled | 1108.892 ms | 2.648 ms | 418.8x faster | 3568 / 0 |
| 32 KiB slice, finite limits and live token | 6219.70 us | 5893.55 us | -5.2% elapsed | 3680 / 3680 |
| Dense mixed-scalar hits, 1024 matches | 4144.25 us | 60.422 us | 68.6x faster | 3568 / 0 |
| Unchanged compiled English literal | 70.851 us | 70.286 us | -0.8% elapsed | 0 / 0 |

Controlled calls intentionally retain the VM: their much higher operating cost
remains visible. This change does not claim a finite-limit speedup. The separate
five-sample README refresh reports **0.75x** compiled against predecoded .NET
(was 313.98x), **0.31x** ordinary (was 128.35x), both qualified Win. Its
2.656/2.654 ms costs remain distinct from the paired comparison above.

Focused Release checks pass on [Core .NET 10](core-net10.txt),
[Core .NET 11](core-net11.txt), [PythonRe](python-net11.txt) and
[PCRE2](pcre-net11.txt). Coverage includes actual native selection, aliases,
ordered branches, nonzero span offsets, scalar surroundings and tails, dense
false candidates, full malformed-tail validation, RTL/NB exclusions, finite
work, original live-token cancellation, deadlines and retained results.

[PCRE2 route inspection](pcre2-route.txt) independently proves its public Count
uses Pcre2Slice. It does not call Core public Count; no PCRE2 improvement is
claimed or its unrelated snapshot refreshed. The 49.58x native gap stays active.

Exact measured DLLs/PDBs, driver, raw results and generated snapshots are in the
[local immutable archive](../../../artifacts/experiments/ascii-token-count-20261028/parity-ipv4-3f5f0847.tar.gz),
SHA256 `C7E4746BF7D577C14CB3A77B99DC424CACADC2D6732014DD4FD569D605C09853`.
No package matrix or complete catalog refresh was repeated.
