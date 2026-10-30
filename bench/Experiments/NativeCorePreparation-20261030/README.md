# Native literal preparation and required byte families

Measured producer **f56f4f862e4c22df3ad37cf134fb081c5b595991** against frozen
green baseline **69f7159071ea17e22f4f6dafe69eeda1d0891d49**. Includes deferred
cursor pattern diagnostics (4cadd587), consumed-byte/ASCII query support (2b866060)
and required byte families (f56f4f86). Public native Compile remains unfinished.

Width proofs and borrowed ASCII direct-family queries read native literal payloads
without decoding. Required-family collection now retains byte arrays, reuses
already admitted native payloads and removes One-node strings and selected-family
re-encoding. Its clean-branch/common-family/disjunction algorithm is unchanged.
Byte sort/length heuristics affect only optional prefilter selection; scalar match
and capture priority remain authoritative. Text candidates encode under existing
admission before selection, potentially charging more transient capacity; native
payloads remain borrowed under their original owner. No new matcher, collector,
resource mode or unchecked access.

Release checks on .NET 10/11: native query/width selection **1296/runtime**,
required-family/admission selection **562/runtime**, final full Core
**40491/runtime**, affected PCRE2 **270/runtime**, PythonRe **317/runtime**.
Checks include actual public Unicode Count/cursors, independent scalar VM family
coverage in both directions, byte/scalar/fold bounds, native buffer reuse, borrowed
payload ownership, malformed/excluded literals and all existing work/byte/depth
refusals. [Verification](verification.txt).

One [public A/B](comparison-first.json), **46.30s**, and its single predeclared
[noise confirmation](confirmation.json), **55.07s**, each under a 120s cap. CPU mask
4, independent 80ms calibration targets and three alternating samples. Initial
warmup 2s/lane; confirmation 4s/lane for the unsettled rows and controls. Minimum
actual samples **26.49ms / 51.94ms**. Environment: Ubuntu 24.04.4, Xeon Platinum
8573C, .NET 11 RC3. Result/checksum consumption follows timing; working/program
balances are checked between samples and after close. [Initial driver](Compare-first.cs.txt),
[confirmation driver](Compare-confirmation.cs.txt).

| Public operation | Run | Before / after (us) | Change | Current funded program/default working (us) |
| --- | --- | ---: | ---: | ---: |
| Literal Count control, 8198 B | Confirmation | 0.544 / 0.534 | -1.92% | 0.672 |
| Scalar Count, 128 matches | Initial | 155.872 / 157.118 | +0.80% | 152.369 |
| Unicode common-family Count, 256 matches | Confirmation | 105.698 / 110.218 | +4.28% | 105.976 |
| Finite scalar Count, 128 matches | Confirmation | 174.509 / 163.006 | -6.59% | 163.121 |
| Scalar text compilation | Confirmation | 50.974 / 51.309 | +0.66% | 54.474 including close |
| Unicode common-family text compilation | Initial | 111.173 / 110.938 | -0.21% | 118.603 including close |

The initial Unicode Count median was +15.50%, with samples drifting from roughly
130/134us to 77/78us; scalar construction's +36.6% median also ended near equality.
The confirmation settles the bounded decision without establishing precise
equivalence. Its before Unicode Count still ranges 78.32–109.18us, and the
unchanged finite control shifts markedly between runs. Preserve all raw samples;
do not derive a cross-run speedup or comparator benchmark Status. No further
campaign, catalog refresh or package matrix was performed.

Count allocations are unchanged: literal 0 B/op, scalar 3872, Unicode 1872,
finite scalar 3992; current funded program lanes add 144/144/144/24 respectively.
Both construction cases allocate **104 B less**: scalar **55496 -> 55392**, Unicode
**253920 -> 253816**. Funded compilation adds the existing 112 B and its explicit
lifetime guarantees; it is not a timing-equivalent comparator. Finite policy uses
16M work steps, depth 4096, 4 MiB working/result limits, with no live-token claim.

Later source **09c9909d** shares leading-option lexing with borrowed pattern input;
native callers receive only a consumed byte offset and suffix. Its 408 focused
checks/runtime pass, including Unicode/NUL and zero modeled native payload
acquisition. That later prelude change is not included in f56f4f86 timings.

CI 41061538838 subsequently caught an option-publication defect in that later
prelude: text normalization changed ref options before retained-text admission.
The wrapper now stages options until the copy succeeds; existing lexical-storage,
prelude and related guards pass **707/runtime**. The f56f4f86 experiment predates
the prelude and is unaffected. Failed CI remains evidence; fresh CI is required.

Immutable evidence in artifacts/experiments/native-core-preparation-20261030/:

- native-core-preparation-f56f4f86.tar.gz: exact before/after Core DLL/PDB pairs,
  source bundles, builds, both drivers/preflights and raw measurements. SHA256
  `2540D5FD17385C61D753571DFE84ED66F4E1A4915D9F12B283B43C1CA275602A`.
- correctness-logs.zip, including initial fixture compile failures and passing
  checks, SHA256 `C03D6910EDD32E37F3DDD011AF0CEE8C082BB9AEA267F3DE8E76EB77486E5B2C`.

Continue native pattern ownership, metadata and remaining preparation bridges.
Final fixed-baseline cumulative cost, current sub-hour refresh and immutable
package/actual consumer acceptance remain delivery gates. Existing archives and
published/staged packages are unchanged.
