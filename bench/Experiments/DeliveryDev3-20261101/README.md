# Current performance and hosting candidate

Frozen engine `a1986746bb8bfa95367eef1c12c3e916fbb40bc0`, common **0.5.0-dev.3**,
Core/PythonRe/PCRE2 with net10 and net11 RC3 assets. These are immutable staged
packages, not a published release. Published0.4.0 and earlier staged bytes remain
unchanged. This boundary includes accepted literal projection and NonBacktracking
preparation, ancestry and candidate-scan improvements since8d/dev.2.

Windows qualification passed in **264.260s**, with two graph builds and one pack
per profile, exact Core pins, API/source/PDB/asset/feed/cache/lock checks and107006
passing detached tests. See [candidate manifest](candidate.json). Packages and
symbols are in `artifacts/nuget/`; original qualification remains under
`artifacts/package-candidates/0.5.0-dev.3/`.

[Windows](windows-consumers.json) and [Linux](linux-consumers.json) consume the
same frozen compatible Awk and Lython fixtures:19 and118 checks per runtime,
including Lython's cancellation workers. Only three dependency project versions
changed in copied fixtures; external consumer checkouts were untouched. The
existing compatible Lython revision is3bee54c34d0da29c3b95b22368138847221ebdc7.
This proves compatibility, not downstream adoption or general AWK/POSIX support.

Public native/funded API probes pass on both platforms and runtimes, with all
loaded DLLs matching package assets and final root loans zero. Their scopes cover
native patterns/templates/metadata, program/working/result owners, retained aliases,
terminal refusal and original-token cancellation. Controls model major capacity
and overlap; they do not measure total heap/RSS. Original462 Windows PDB source
documents, including generated inputs, are archived with matching hashes.

[Linux package checks](linux-packages.json) pass all107006 detached tests against
those exact Windows-produced packages. The [identity audit](linux-identity-audit.json)
checks six assets/PDBs/SourceLink and24 feed/cache/lock observations. No Linux
repacking; its LF checkout is distinct from the original Windows compilation bytes.

The complete source VM refresh passed **2544.593s (42m24.6s)**. All83steps passed:
one23.489s Release build, both-runtime correctness and disabled dynamic code,
89Core cases/178rows,90 paired Python operations with4 lifecycle/7 scaling families,
126PCRE2 rows/16 scaling families and all65 paired commands,650 resource samples,
then all verification. The60-minute target is met. Package/consumer work remains
outside that clock. See [full receipt](full-refresh.json). No rows were omitted
or resampled to improve classifications.

Current uncertainty is explicit: Core105Win,2TieCandidate,67Gap,4Unqualified;
Python27ManagedFaster,46CpythonFaster,14Inconclusive,3Unqualified; native PCRE2
71ManagedFaster,1Equivalent,16NativeFaster,13Inconclusive,25Excluded,0Unqualified.
Core unqualified rows are river-window/symbol-count, ordinary and compiled.
Python greedy miss, lookahead hit and possessive miss exceed the removable CPython
harness-floor gate (15.76%,11.39%,16.18%). Do not promote their point estimates.

[Original/final ratios](ratio-summary.json) compare the real archived a40 baseline
with this full refresh. They include comparator movement; causal gains remain in
the focused public A/B receipts. Examples from the compiled Core rows:

| Case | Original ratio | Current ratio |
| --- | ---: | ---: |
| Region Count |172.18x|38.83x|
| Lexer identifier |6.32x|1.78x|
| Lexer string |6.55x|0.64x|
| Lexer operator run |6.86x|1.89x|

Region remains the largest Core gap: ordinary38.57x,compiled38.83x. Keyword-family
9.59x,capitals9.33x,Chinese literal7.16x and compiled imports7.15x retain their
previous distinct-cause deferrals. Python's qualified queue starts atomic miss6.49x,
literal early5.57x,date miss4.77x and Unicode FullMatch4.65x.

PCRE2 assertion-capture is newly **qualified NativeFaster at438.51x**,
95% interval[384.71,479.62],903.922us managed versus2.072us native. Its previous
399.57x estimate was Inconclusive; this is not evidence of a new438x regression.
Pattern `(?=(?<token>[a-z]{3}[0-9]{3}))abc123` searches4096 x's followed by a hit.
Investigate mandatory literal candidate search through the leading capture assertion
using existing helpers, preserving replay and all controls, before smaller PCRE2
gaps. Date miss/match11.58x/11.47x and earlier failed pilots stay visible. Unlike
comparators are not one interchangeable global ranking.

The [fixed-a40 public comparison](cost-result.json) took **109.073s**:26existing
operations, unchanged typed lane IL, warm1.5s/min1000calls, independent60ms calibration,
five alternating samples, minimum59.018ms; complete results consumed after timing.
[Pairs/spreads/allocations](cost-summary.json) retain all observations. Mapped
text/native/string/destination operations improve48-52%, no-match60%; finite
identifier18.6%, ASCII Boolean73%, scalar-NB Count18.1%, Python possessive miss89.7%
and lookbehind49.9%, PCRE2 Unicode Count10%/enumeration31%. Largest adverse median
among covered workloads is identifier unlimited+5.77%, below10%. No confirmation
was needed. Several operations add8-16bytes; mapped template allocations fall
substantially. New funded APIs have no a40 counterpart and retain their separate
feature-cost evidence. This is representative coverage, not universal equivalence.

Existing final snapshot guards pass on both runtimes (Core3,PythonRe1,PCRE2 1).
A local Windows codegen check showed full Tier1 already inlines scalar decoding;
the canonical region corpus has28 non-ASCII bytes. Neither an explicit decoding
shortcut nor an ASCII-only boundary map has a demonstrated benefit for that primary
case. These are diagnostics, not VM timing or performance claims.

Raw evidence under `artifacts/experiments/delivery-dev3-20261101/`:

- `full-refresh-a1986746.tar.gz`: frozen source/binaries, all measurements,
  full-refresh and cumulative comparison, SHA256
  `72064DCA93C810D69DF6BAA38ADED1F10D5F118B0B16B82C8D65F811D2B33EC4`.
- `linux-consumers.tar.gz`: exact package and actual consumer checks,
  `F91B5FAC7BAB2FE08480C9405421CDE0B93FF4F9B431C910D966440774428B06`.
- `windows-consumers.zip`: original fixtures/adaptations/results,
  `80DC25D614A19CD6B65937451D800F5B863F1AC1207DDD85BC18BBED350EE5C2`.
- `windows-compilation-inputs.zip`: all462 PDB compilation documents,
  `5B2916373AC71087E00BC5C030B87BFA023AC5D001C4AAF769467588A561B16E`.

- `final-verification.zip`: final checks and bounded codegen/corpus diagnostics,
  `004613FBC6E520D1BCD48C82E62EBA9D6F49C8066E29A3A902FA033F7A5F2742`.

Sourcea198 CI41288159238 passed Windows/Linux. Following commits import only
these generated data and receipts. This completes this coherent package/performance
boundary; remaining N4 gaps, optional PCRE2 scalar projection and POSIX remain open.
Publication and downstream adoption are separate actions.