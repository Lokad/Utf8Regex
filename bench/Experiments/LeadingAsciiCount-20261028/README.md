# Mandatory leading ASCII scalars in Count

bab1a8f5 recognizes a mandatory first exact ASCII literal in the admitted scalar
instructions, after capture markers. Count searches for possible starts with
checked spans and then runs the unchanged VM. This complements covering-family
facts when the planner selected fixed sets or another portfolio. It is not a
Ruff-specific recognizer. The optional proof scan stops after 64 markers; deeper
wrappers retain ordinary VM discovery. RTL, NB, empty/contiguous and folded cases
keep their existing fences.

Finite/token/deadline/hook scopes use 2048-byte charged search windows and the
original publication check. Unlimited scopes use checked span IndexOf, as the
existing native searches do. Full strict validation precedes discovery; captures,
references, alternation and scalar dot remain VM decisions. No heap payload,
unchecked operation, accounting framework or permanent mode was introduced.

[Comparison](comparison.json), [console](comparison.txt), [driver](compare.cs.txt),
[project](compare.csproj.txt), [selective refresh](readme-refresh.txt).
Same quiet VM, .NET 11 RC3, CPU2, VM-built a40bda03 versus bab1a8f5. Earlier prefix
fixes do not serve this fallback/fixed-set shape. Public results match .NET before
and during timing. One <=120s comparison completed in **34.00 seconds**, with
four independent lanes, warmup, three alternating samples, raw times and
allocations. No confirmation or favorable rerun.

| Public Count | Before | After | Speedup | Allocation B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Ruff tweaked, 32.5 MB, ordinary | 2066.93 ms | 13.523 ms | 152.8x | 3600 / 3600 |
| Compiled | 2062.00 ms | 13.637 ms | 151.2x | 3600 / 3600 |
| 32 KiB, finite limits and live token | 4248.01 us | 42.360 us | 100.3x | 224 / 224 |
| Mixed-scalar dense hits, 1024 matches | 1561.85 us | 812.65 us | 1.9x | 3600 / 3600 |
| Unchanged compiled English literal | 70.682 us | 70.016 us | -0.9% elapsed | 0 / 0 |

The independent five-sample README refresh reports compiled **2.80x** against
predecoded .NET (was 447.46x), ordinary **1.49x** (was 257.48x). Its costs are
12.939/12.323 ms; the paired experiment above remains separate.

Focused Release checks cover byte offsets/tails, false candidates, capture
retention, scalar-dot semantics, deep wrappers, work/working admission, final-step
cancellation/deadlines, NB and RTL. The first full Core run had one stale fixture:
[original failure](core-full-initial-net11.txt). It requested cancellation after
256 VM steps on a subject with no prefixes. The new search correctly finished
before that hook. The fixture now uses repeated actual failing captures (abx/xbc
according to direction), preserving its intended storage-release assertion.
The corrected test passes on [.NET 10](cancellation-net10.txt) and
[.NET 11](cancellation-net11.txt); the subsequent
[full .NET 11 Core suite](core-full-net11.txt) passes.
Affected [PythonRe](python-net11.txt) and [PCRE2](pcre-net11.txt) checks pass too. Historical failure evidence is retained.

Measured binaries/PDBs, driver, raw outputs and selective snapshots are in the
[local archive](../../../artifacts/experiments/leading-ascii-count-20261028/parity-leading-bab1a8f5.tar.gz),
SHA256 `EB87B23D320FD4D2A8348808743C51EAAA6BCE14693F266E5037EADBBF3E59D7`.
The later fixture correction changes no measured production code. No package
matrix or complete catalog refresh was repeated.
