# Preparation delivery: dev.81

Core, PythonRe, Pcre2 and Posix **0.5.0-dev.81**, exact Core pins, .NET10/.NET11.
Frozen producer `877dc8367fd7d147e1ba58e98d7de56c34705cd0`; SDK11.0.100/.NET11.0.0. Packages and symbols are in
`artifacts/nuget/`. No publication, consumer checkout edits or downstream adoption.

## Acceptance

- One clean matching-source Full: **46m58s /2817.906s**, all85
  existing steps passed within3600s. This includes one build, both-runtime and
  dynamic-disabled tests, all catalogs, paired references, lifecycle/scaling,
  resource checks and verification. [Run receipt](full-refresh.json).
- Windows and SAME-byte Linux: all4 profiles and both targets, API, managed
  safety, resources and10 detached fixtures perOS. Original copied Awk19,
  Lython118 and public API probes pass pertarget/OS, final root loans0. The1565
  original source files changed only3 exact pins; Linux audits28 restores/14 TRX.
- All500 older assets and8 fresh package/symbol files preserve their hashes.
  No producer rebuild/repack on Linux. Raw archives were hash-verified offVM
  before releasing the SSH keepers. [Acceptance details](acceptance.json).
- Existing offline writers and both-runtime source snapshot guards pass on
  Windows/Linux. Later edits correct migration wording, a stale Python cursor
  XML exception and test labels; executable behavior and frozen package bytes
  remain unchanged. Frozen-source CI43455044743 and documentation CI43456923798
  are green/closed. Presentation CI43464836579 and subsequent Count source
  CI43466179663 are also green/closed; the latter does not change these packages.

## Current costs and remaining priorities

These are complete-catalog observations, not causal before/after percentages.
Core includes default strict validation and compares predecoded .NET. PythonRe
uses caller-valid text versus official long-lived CPython str. PCRE2 checks UTF
in both lanes. POSIX uses **PCRE.NET ordered PCRE2 NFA, UTF+NO_UTF_CHECK**, equal
options/results/requested coordinates and reusable output storage. It is not a
POSIX conformance oracle; unequal selection/history cases stay excluded.

| Public case | Managed µs/call | Reference µs/call | Ratio, lower better | Status |
|---|---:|---:|---:|---|
| Core / literal/sherlock-zh | 95.112 | 23.387 | 4.067 | Gap |
| Posix / capture/sku007 | 0.619 | 0.157 | 3.933 | NativeFaster |
| Core / industry/mariomka-email-count | 1016.692 | 453.302 | 2.243 | Gap |
| PythonRe / findall/many-capture-strings | 645.553 | 332.598 | 1.945 | CpythonFaster |
| PythonRe / findall/one-capture-strings | 403.205 | 207.562 | 1.943 | CpythonFaster |
| PythonRe / split/captures | 388.041 | 257.687 | 1.509 | CpythonFaster |
| Core / industry/leipzig-river-window-count | 2167.853 | 1543.870 | 1.404 | Gap |
| Core / industry/leipzig-symbol-count | 1617.049 | 1249.760 | 1.294 | Gap |
| PythonRe / findall/full-strings | 430.618 | 362.807 | 1.191 | CpythonFaster |
| Pcre2 / simple/httpclient-caseless | 1.271 | 1.095 | 1.160 | NativeFaster |

SKU is normalized from4096 calls. All allocation, finite/token/deadline/funded,
lifecycle/scaling and uncertainty observations remain in the summaries/raw.
PythonRe: 5 Unqualified, 9 Inconclusive; Pcre2: 10 Inconclusive, 25 Excluded; Posix: 8 Excluded. Preserve these qualifications and separate-run adverse observations;
local regressions can be accepted for major gains elsewhere. No universal10% gate
or complete profile/native-throughput equivalence is claimed.

This batch preserves the [profile contracts](../ProfileInputContracts-20261113/README.md)
and [Python search/output improvements](../PythonStringSizing-20261113/README.md),
then removes more unnecessary preparation:

- [ASCII classification](../AsciiClassification-20261113/README.md): governed
  Unicode-first Search14.213→0.316us,176B unchanged. ASCII14.179→14.534us is an
  accepted local cost; differing CPUs/control movement limit exact attribution.
  Core's prior default whole-input validation remains.
- [PCRE2 window Count](../PcreWindowPreparation-20261113/README.md): mixed-Unicode
  miss441.868→42.352us, ASCII31.056→31.081us,0B unchanged. Native miss controls
  moved about1%; the hit control moved66%, so no precise hit-speedup claim follows.
- [Scalar queries](../ScalarQueryPreparation-20261113/README.md): first-block
  queries avoid an unused whole-subject index; the ASCII predicate omits unused
  UTF16 totals. Distant mixed queries keep the existing index/admission/lifetime.
  Correctness and admission checks pass; the bounded timing attempt plus one
  confirmation are unqualified. No throughput claim or further run follows.

The remaining material work is Core family search/strict validity, Python
capture-producing progression and owned string/output costs. Existing detailed
coordinate maps have real consumers. General POSIX selection can retain
quadratic memo state; locale/grammar growth is separate. Retain the earlier
[gap dispositions](../GapDisposition-20261113/README.md): no replay of removed
width/density/threshold, fusion, state-init or capture-hint trials without a new
demonstrated cause. Native attribution does not justify removing basic metering.

Next bounded investigation: PCRE2's exact literal-family Count may omit its
description without changing its counter. The existing five-portfolio test now
proves prepared Count over undescribed borrowed input at a nonzero scalar start,
on both runtimes, with frozen producer hashes preserved. Runtime changes and their
public costs belong to the subsequent focused experiment, not these packages.

Raw Full SHA256 `86EE9A97CF0D9095D2E887D0086E11AE9BB7EF5A8D00979934990A185D5CE815`; Linux `255E0A7414F2B25DA1BBD265C8E7A1FCFFC9AED11023F0825AE969C3949A4BA4`.
Canonical evidence: `artifacts/experiments/delivery-dev81-20261113/`.
The first Linux shell launch had CRLF line endings and stopped before any work;
the corrected launch reused identical inputs. An early snapshot waiter likewise
stopped before tests because Full had not finished. Neither caused repeated
consumer qualification or benchmark measurements.
