# Native pattern and capture metadata

Producer `03ce3861` against fixed `face0e34`: public Core/PythonRe/PCRE2
pattern export, capture-name lookup, cold compilation and finite/live-token
Count. New metadata avoids intermediate strings and returns caller-owned data.
It preserves Core sparse/unnamed-first numbering, Python opening order and
PCRE2 duplicate name entries. Existing string conveniences remain available.

One **86.023s** quiet-VM run used fourteen cases / forty lanes, Release .NET 11
RC3, affinity mask 4, 1.5s/at least 1,000 warmup calls per lane, independent
80ms calibration and three alternating samples. Every actual sample exceeds
20ms. Caller destinations/source encoding and Python legacy name-table setup
are outside timing; complete results/content/funding refunds are checked after
it. Python legacy lookup uses a host dictionary cached from public GetGroupNames,
with UTF-8 name-to-string conversion inside each query. Legacy export performs
Pattern -> UTF-8 bytes -> caller destination, including both temporary objects.

The sole focused confirmation took **65.910s** under a 75s cap: four cases /
eleven lanes, 4s/at least 1,000 warmup calls, independent 150ms calibration and
five alternating samples. It addresses observed PCRE2 compilation/Python string-
export variability, retaining literal and finite/live-token matching controls.
All statistics below are medians calculated from raw JSON. The confirmation
driver's stderr progress print retains the original three-sample order statistic;
it is not the five-sample summary. No additional runs are authorized by this receipt.

## Public metadata costs (initial run)

| Operation | Before string interoperability (ns) | After string interoperability (ns) | After native metadata (ns) | Temporary allocation before → native (B/op) |
| --- | ---: | ---: | ---: | ---: |
| Core-source-copy | 186.6 | 177.8 | 22.7 | 1664 → 0 |
| Core-name-lookup | 55.5 | 56.5 | 30.4 | 32 → 0 |
| Python-source-copy | 180.6 | 199.5 | 24.3 | 1664 → 0 |
| Python-name-lookup | 41.2 | 41.4 | 27.6 | 32 → 0 |
| Pcre-source-copy | 279.2 | 291.3 | 35.2 | 1664 → 0 |
| Pcre-name-lookup | 38.4 | 38.6 | 30.5 | 32 → 0 |

Confirmation Python export is **183.5 / 188.1 / 21.9 ns** for the same three
lanes, 1,664 B/op through strings versus zero through the new caller-buffer
copy. These selected metadata operations are substantially cheaper; this is
not a matching-throughput improvement or a benchmark catalog Status.

## Construction and matching controls

| Public operation | Before / after text (us) | Before / after native (us) | Allocation before → after, text / native (B/op) |
| --- | ---: | ---: | ---: |
| Core-compile | 94.017 / 93.940 | 94.111 / 94.544 | 50944 → 51176 / 50680 → 50912 |
| Python-compile | 147.079 / 147.602 | 151.063 / 144.696 | 81784 → 82104 / 81832 → 82152 |
| Pcre-compile | 97.655 / 103.535 | 112.476 / 126.458 | 61896 → 62200 / 61680 → 61984 |

New native keys add **232 B/Core, 320 B/PythonRe and 304 B/PCRE2** per sampled
compilation. They are owned/admitted once under the existing construction/program
account, without helper-object precision or another lifetime mechanism.
Core funded native compile+close is **91.278 → 90.229 us**,
**50,792 → 51,024 B/op**. These are feature/construction costs, not Count costs.

Initial PCRE2 native compilation shows +12.4%, with large drift. Confirmation
medians are **73.270 → 74.017 us** (+1.0%); text **76.804 → 72.250 us**.
Large samples still span different timing levels, so neither a stable >10%
regression nor precise equivalence is established. Keep the uncertainty; stop
this experiment rather than turn it into a tuning campaign.

Finite/live-token native Count medians (us), with unchanged allocations:

| Profile | Before | After | B/op |
| --- | ---: | ---: | ---: |
| Core-finite-live-count | 55.161 | 56.127 | 3672 |
| Python-finite-live-count | 66.412 | 62.155 | 5864 |
| Pcre-finite-live-count | 137.327 | 137.853 | 1600 |

Confirmation PCRE2 finite/live Count is **137.880 → 139.729 us** (+1.3%),
unchanged 1,600 B/op. The unchanged literal control is **0.440 → 0.421 us**,
zero allocation. Initial Core/Python Count samples also vary. No matching
catalog or cumulative release-baseline claim follows from these selected controls.

## Contracts and validation

- CaptureGroupCount excludes group zero, counts defined identities rather than
  match participation, preserves sparse Core numbers and PCRE2 branch resets.
- TryCopyPatternUtf8 returns required capacity; capacity/invalid legacy surrogate
  failure writes nothing. Current-token cancellation can leave a partial copy.
  Caller destinations need no new program alias/lease and survive owner close.
- Complete strict UTF-8 name validation precedes lookup/writes. Encoded key
  vectors compare ordinal bytes without normalization or temporary strings.
  Lookups are linear and interruptible. Funded owners hold existing program uses.
- Explicit metadata copies/lookups use their token, independently of matching
  timeout/numeric invocation budgets, like existing standalone getters. Retained
  keys are admitted during compilation; caller destinations stay caller-owned.
- Core 40,782, PythonRe 4,315 and PCRE2 6,347 Release tests pass per runtime
  (.NET 10/11), including reviewed API snapshots, ownership, resource refusal,
  scalar boundaries, no-allocation native queries and legacy text validation.
  The benchmark project builds both targets. CI **41089939867 is GREEN** on
  Windows/Linux, both runtimes and disabled dynamic code.
- Initial preflight expected Python-style group 1 for Core's named word group.
  Core correctly uses group 2 after unnamed-first numbering. Only the driver
  expectation changed; no timing preceded this failed setup. Its source/log are
  archived separately. Production naming semantics were preserved throughout.

## Evidence and remaining delivery work

- [Initial raw samples](comparison.json), [sole confirmation](confirmation.json),
  [first driver](Compare.cs.txt), [confirmation driver](Compare-confirmation.cs.txt).
- Immutable exact DLL/PDB/driver/build/preflight/raw archive:
  `artifacts/experiments/native-metadata-20261030/utf8regex-native-metadata-03ce3861.tar.gz`.
  SHA-256: `590C61DBBAD15D747B581BB53EB75F765596D2E0184897D17DA3A878B0B811A0`.
- Companion `correctness-source-logs.zip` preserves source bundles, focused/full
  source test logs, intermediate failures and VM identity.

No package bytes, published benchmark catalogs or historical evidence changed.
Native template parsing/cache keys, funded replacements, final immutable
consumer acceptance, current-source full refresh and cumulative performance
qualification remain delivery work. These results close this experiment only.
