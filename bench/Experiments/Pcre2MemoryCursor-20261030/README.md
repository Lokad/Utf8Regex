# PCRE2 persistent byte-range cursors

Initial producer **7b7e26bb6d5e728dc7b94556e359e4788ba97c68**; final producer
**27e28db0b13570b94a163770599549a28ccc15e7**; frozen original
**84f21325da5a8141733003dba1f0a135c12b8dce**. This is source qualification,
not a package release or a refreshed comparator catalog.

Raw expressions and funded program owners create disposable memory cursors with
absolute byte starts and PCRE2 match flags. They validate borrowed unchanged
memory once, preserve full-subject assertion context and PCRE2 empty progression,
and return immutable last-capture byte ranges, duplicate-preserving names and
MARK. Opted-in `\C` retains its explicit slice-validity flags. Retained receipts
transfer admission to the same or a distinct result account before working
staging refunds. Returned aliases survive advance and close; borrowed subject
storage remains the caller's responsibility. No captured strings or UTF-16 map.

Existing native finders, scalar runner and PCRE2 VM remain the execution routes.
The production replacement cursor shares its existing global progression through
one concrete helper. Current-call tokens are restored after synchronous advances;
creation tokens and the original deadline remain authoritative across awaits.
EOF/failure/Dispose drop private roots and refund exclusive loans/program use.
There is no finalizer, new matcher or resource framework.

The final source intentionally retains admitted scalar slots/workspace at its
high-water capacity while positioned, including host waits. PCRE2 VM scratch
remains lexical per advance. This replaces the initial cursor's all-per-advance
scratch contract; it does not alter a published package. Numeric work/depth/heap/
result controls, strict input validation and terminal refusal remain enforced.
[Contract](../../../docs/SPEC-PCRE2.md).

Release checks pass on .NET 10 and 11: initial full PCRE2 **6269/runtime**,
affected Core **144/runtime**, final scalar-route/reuse focused **119/runtime**,
then full PCRE2 **6272/runtime**, including all **41 new cursor cases** and
API/managed/source guards. Cases include native/scalar/VM routes, captures in
lookbehind, `\G`, `\K`, NUL, Unicode, flags, CRLF empty retry, malformed tails,
byte starts, 500k-byte iteration, finite/cumulative controls, separate/same-account
receipts, actual cancellation/refusal, deadline waits, owner close and concurrent/
reentrant access. The initial full run caught a private enum naming guard; the
fixed full run passes. Failed and successful logs are preserved.
[Verification](verification.txt).

The first [public pilot](comparison-first.json) took **46.63s** and found a real
new-path cost: captured ASCII ranges took **164.45 us**, versus repeated detailed
calls **106.68 us**. The final source uses the already eligible scalar program,
normalizes final byte slots directly and retains its private admitted workspace.
The one [follow-up](comparison-scalar.json) after that fix took **29.23s**. Both
runs have a 120s cap, CPU mask 4, 600ms warmup/lane, independent calibration toward
80ms and three alternating samples. Actual samples are at least **49.89ms** /
**48.75ms**. Environment: Ubuntu 24.04.4, Xeon Platinum 8573C, .NET 11 RC3.
Destinations are preallocated; all capture participation/ranges/flags and checksum
consumption are checked after timing. Funding balances are checked between
samples and after close. [Initial driver](Compare-first.cs.txt),
[follow-up driver](Compare-scalar.cs.txt).

| Follow-up public workload, 32 matches | Repeated detailed (us) | Raw cursor (us) | Shared working (us) | Funded program (us) | Program + separate retained result (us) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Captured ASCII, 96 slots | 170.910 | 165.057 | 104.271 | 99.997 | 107.085 |
| Reset-start control, 96 slots | 77.527 | 67.661 | 73.803 | 83.373 | 80.518 |
| Captured ASCII, finite/live | 233.406 | 206.719 | 210.064 | 210.420 | 221.219 |

The follow-up's before/after existing Count controls changed **-0.34% to +1.19%**;
existing replacement controls changed **+0.40% / +0.83%**, with unchanged allocated
bytes. These are separate operations, not capture-throughput comparators.
Repeated detailed calls validate/project per call; memory cursors validate once
and have cumulative invocation/lifetime guarantees. Shared funding and retained
receipts add guarantees and cost: reset-start funded iteration is slower than
repeated detailed calls. Raw/funded captured ASCII medians also differ markedly;
do not advertise a universal cursor speedup. Initial and follow-up detailed
timings differ substantially, so cross-run absolute differences do not establish
a quantified optimization. No noise retry or further campaign was performed.

Follow-up allocation B/op, repeated/raw/shared/program/retained:

- Captured ASCII: **3584 / 3328 / 80285.98 / 80312 / 82616**.
- Reset-start: **3584 / 3328 / 66558.07 / 66584 / 68888**.
- Finite/live: **84504 / 80288 / 80288 / 80312 / 82616**.

These are allocations, not modeled peak working admission. Existing pooled
storage can be borrowed by funded lanes and dropped when their admission ends;
the fractional refills remain in the raw results. Retaining workspace does not
establish allocation equivalence or eliminate all VM allocation. Literal/Unicode
rows were measured only on the initial source; they are not final-source timing.

Immutable archive under artifacts/experiments/pcre2-memory-cursor-20261030/:

- pcre2-memory-cursor-27e28db0.tar.gz: original/initial/final Core and PCRE2
  DLL/PDB pairs, both source bundles, both driver versions, build/preflight/raw
  measurements, including failed TMPDIR setup and rejected wrong-revision
  preflight. SHA256
  `2B3353DB6DCED2B9B23711D7F9B5965F245D6BC06DF8D040F1D549EF3BF89E90`.
- correctness-logs.zip SHA256
  `E7745E2EA78B4F8A1972573646D9442B75587D9DDF3DC0546B741C32E5D3A6B9`.

Optional PCRE2 scalar projection, funded replacement, native pattern/template
preparation and metadata/cache bridges remain unfinished H2/H3 work. The final
fixed-baseline cumulative cost, current sub-hour refresh and immutable packages/
actual consumers remain release gates. Historical artifacts are unchanged.
