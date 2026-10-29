# Reject absent ASCII tails inside bounded scalar gaps

afef0bf1 extends the existing class-repeat helper with a necessary-condition
rejection for forward greedy Dot repeats with maximum 8..511 followed by an
unfolded ASCII literal. Strict UTF-8 limits each scalar to four bytes, so the
literal's first byte must occur within `4*maximum+1` bytes of the gap start.
One checked span search covers at most 2045 bytes. Absence fails the attempt;
presence retains normal VM greediness, newline handling, capture trails and
branch selection. Short/unbounded repeats and other shapes keep their paths.

The original invocation charges before/after the scan and observes its token
before returning. No program payload, matcher, public option, unchecked access
or extra accounting system was added. Existing scalar/resource/lifetime contracts
remain; numerical work/depth requirements are route dependent.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; exact 4a8ff88f baseline
versus afef0bf1. One **64.14s** public A/B under a 120s cap, thirteen Count
workloads, one-second warmup, independent calibration and three alternating
~50ms samples. Minimum measured sample was 39.30ms. All public counts match the
same-option predecoded/decode .NET lanes. No timing confirmation was needed.

| Public Count | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Holmes corpus, ordinary | 770.388 | 256.027 | 3552 / 3552 |
| Holmes corpus, compiled | 771.574 | 253.588 | 3552 / 3552 |
| Holmes finite limits/live token | 885.181 | 289.632 | 3664 / 3664 |
| River corpus, shared VM gap behavior | 6720.725 | 5877.744 | 3552 / 3552 |
| ASCII dense misses, native control | 47.689 | 47.048 | 0 / 0 |
| ASCII dense hits, native control | 20.477 | 20.067 | 0 / 0 |
| ASCII present-byte misses, native control | 47.104 | 46.770 | 0 / 0 |
| Unicode dense VM hits | 203.333 | 208.212 | 3552 / 3552 |
| ASCII newline failures, native control | 36.783 | 36.074 | 0 / 0 |
| ASCII captured Count, native control | 4.456 | 4.504 | 0 / 0 |
| Short-repeat control | 27.537 | 27.515 | 0 / 0 |
| Unbounded-repeat control | 982.700 | 1021.602 | 680600 / 680600 |
| Literal control | 75.316 | 74.845 | 0 / 0 |

[Route inspection](routes.txt), [probe](route.cs.txt), [project](route.csproj.txt)
confirm that ASCII dense/newline/captured controls retain NativeOrderedLiteralWindow;
they do not demonstrate the new VM filter's speed. The Unicode dense-hit row
selects the scalar program and measures a **2.4% slowdown** with possible tails.
Other unchanged controls vary -2.0% to +4.0%; negative control changes are not
claimed as improvements. a40bda03 remains the fixed cumulative release baseline.

The [Holmes refresh](holmes-refresh.txt) qualifies compiled **12.46x -> 4.31x**,
still Gap, and ordinary **2.95x -> 0.96x**, Win. The shared
[River refresh](river-refresh.txt) gives **4.43x -> 3.85x** compiled and **0.20x ->
0.19x** ordinary, Gap/Win. Each still allocates 3552 B. All selected V3 durations
exceed 49ms. River retains its previously accepted requested minimum of eight
iterations; no short-duration retry was required. Independent A/B medians and
published comparator ratios remain distinct evidence.

[Parity](parity.txt), [generated Markdown](markdown.txt),
[snapshot comparison](snapshot-diff.txt) and [Release checks](verification.txt)
pass. Only these two cases' ordinary/compiled measurements and parity rows changed;
other identities, operation labels, historical uncertainty and measurements remain.
Complete PythonRe and focused Core/PCRE2 checks pass on .NET 10 and 11. New tests
cover four scalar widths at the final permitted endpoint, 7/8/25/511/512 limits,
independent non-shortcut references, greedy captures, branches, excluded modes,
offsets, retained aliases, every work cutoff/borrowed loan, malformed tails and
original cancellation/deadline after validation. No package/full catalog matrix.

The [first launch failed before timing](setup-failure.txt) because its working
directory could not resolve the corpus. The same binaries/settings then ran from
the checkout. An initial test fixture used the type's ToString instead of its
pattern; the corrected resource assertions pass. Both failures remain archived;
neither is performance evidence or a changed engine contract.

Immutable archives under `artifacts/experiments/bounded-dot-candidates-20261029/`:

- `parity-bounded-dot-afef0bf1.tar.gz`: exact baseline/candidate DLL/PDBs,
  benchmark host, source bundle, drivers/build/preflight/setup/comparison/route
  evidence and final snapshots/reports. SHA256
  `8A177E7B73C1D8B7B9AD68B322D128BB81EB0491879C88F34E588CBB9981B81A`.
- `correctness-logs.zip`: initial/final focused, complete PythonRe, resource,
  final-source and snapshot checks. SHA256
  `5D4703E3A783A70C76AAAE7FFBF58D0836FF18259051E552C862B5169FD2A3C5`.
