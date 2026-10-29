# Reuse complete number-prefix ranges for bounded value Match

4a8ff88f admits the existing complete `AnchoredNumberPrefix` proof/helper to the
existing value-Match envelope: origin zero, whole-subject ASCII validation,
<=2048 bytes, owned program, no folds/RTL/NonBacktracking. The helper preserves
optional fraction/exponent rollback; it supplies the selected whole range without
capture workspace. Detailed captures, other origins and wider subjects retain
scalar execution. No matcher, grammar, unchecked access or execution mode was added.

The original invocation charges before and after the bounded scan and observes
cancellation before projection/publication. Fixed coordinate values need no
working/result arrays; numerical denial thresholds can therefore change, as
documented in the Core specification. Finite work still bounds long reverse
searches. No admission is transferred to another scope or reset on decline.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; exact d458c0ec baseline
versus 4a8ff88f. One **53.99s** public A/B under a 120s cap, eleven workloads,
one-second warmup, independent >=20ms calibration and three alternating ~50ms
samples. The shortest timed sample was 45.78ms. Per-input lengths and timed-loop
checksums agree with the matching-option .NET lanes. The catalog loop makes
**80 Match calls over 340 bytes**, rather than a Count call or detailed capture
materialization. No A/B confirmation was needed.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Catalog Match loop, ordinary | 103.259 | 11.009 | 463360 / 0 |
| Catalog Match loop, compiled | 88.440 | 11.042 | 463360 / 0 |
| Finite limits/live token | 108.320 | 16.616 | 472320 / 8960 |
| ASCII misses | 6.768 | 0.926 | 25648 / 0 |
| Optional-tail rollback | 10.835 | 1.233 | 52128 / 0 |
| 2047/2048-byte subjects | 133.221 | 0.370 | 176176 / 0 |
| Unicode control | 4.240 | 4.365 | 18912 / 18912 |
| Long-subject control | 261.393 | 244.454 | 344464 / 344464 |
| Count control | 96.434 | 92.335 | 288000 / 288000 |
| Literal control | 0.263 | 0.270 | 0 / 0 |
| Trimmed-prefix control | 0.488 | 0.429 | 0 / 0 |

Unchanged controls vary -12.1% to +2.9%, with unchanged allocations. Their
negative differences are not claimed as producer improvements. a40bda03 remains
the fixed cumulative release baseline.

The selected five-sample [refresh](refresh.txt) qualifies compiled
**12.69x -> 1.347x**, still Gap, and matching-flag ordinary **0.717x**, Win; both
allocate zero B/loop. All V3 samples exceed 42ms. The old ordinary 12.53x used a
compiled comparator, so that ratio change is not a like-for-like producer gain.
The independent A/B establishes the actual ~8-9x producer improvement.
[Parity](parity.txt), [generated Markdown](markdown.txt) and
[snapshot/API/dependency checks](verification.txt) pass.

The generated parity report had also labelled every script workload `Count`.
The reporter now reads the existing catalog model: thirty ordinary/compiled
prefix-loop rows become `MatchLoop`. [Verification](metadata-verification.txt)
and the [snapshot comparison](snapshot-diff.txt) show that only the numeric case's
two measurements changed; other parity changes are operation labels. Historical
timings, binary identities and uncertainty remain intact. No unchanged case was
remeasured for this reporting correction.

Focused Release checks pass on .NET 10 and 11: independent scalar/BCL ranges,
optional-tail failure, padded spans, boundary lengths, Unicode/long/fold/RTL/NB
routes, nonzero anchor origins, detailed captures, retained cursor aliases,
work/working denial, malformed tails and original token/deadline publication.
Core resource/runtime and PythonRe/PCRE2 invocation/admission checks also pass.
Initial fixture errors (an unavailable enumerator Dispose method and an expected
work denial on a long reverse search) were corrected; their logs remain archived.
The long reverse case now explicitly asserts its work limit. No package or full
catalog matrix was repeated.

Immutable archives under `artifacts/experiments/number-prefix-value-match-20261029/`:

- `parity-number-value-match-4a8ff88f.tar.gz`: exact baseline/candidate DLL/PDBs,
  benchmark host, source bundle, driver, build/preflight/comparison, selected
  refresh and original VM reports before the operation-label correction. SHA256
  `03F1C9D217C2DA730083B1A764BF9BD57C82285A44C012758E7521465BD863C0`.
- `correctness-and-reporting.zip`: initial/final validation, corrected generated
  parity report and source patch, local reporter build and snapshot checks. SHA256
  `25D2D9A30758DB63540F52293AE709D9E4AFCA8D6ECECC9FFB2A9E520ABAF5C4`.
