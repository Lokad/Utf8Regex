# Current native hosting and performance candidate

Frozen source `8d5545276e603f208071198f6f25de10106eb8d9`, common
**0.5.0-dev.2**, Core/PythonRe/PCRE2 with .NET10 and .NET11RC3 assets.
These are immutable staged packages, not a published release. Published0.4.0
and stageddev.1 bytes remain unchanged; this candidate includes the literal
Count tail correction and the subsequent accepted performance batch.

Windows qualification passed in **254s**, using the existing qualifier: two
graph builds, one pack/profile, exact Core pins, source/PDB/DLL/package/feed/
cache/lock identities and106878 detached test passes. See [manifest](candidate.json).
Packages/symbols are in `artifacts/nuget/`; original qualification is under
`artifacts/package-candidates/0.5.0-dev.2/`.

[Actual Windows consumer fixtures](windows-consumers.json) pass both assets:
Awk19/runtime, Lython118/runtime (including five cancellation worker stages).
These reuse the existing compatible frozen Awk/Lython scenarios; only producer
package references change. Lython source is `3bee54c34d0da29c3b95b22368138847221ebdc7`.
Consumer checkouts were untouched. Compatibility is separate from downstream
adoption and general AWK/POSIX support.

The package-only public native/funded API probe passes on [net10](public-probe-windows-net10.json)
and [net11](public-probe-windows-net11.json), with zero final root loans. Every
loaded producer DLL matches its package asset. Program/working/result owners,
native patterns/templates/metadata, retained aliases, terminal denial and original
cancellation are covered; this is not a total heap/RSS accounting claim.

Raw Windows evidence under `artifacts/experiments/delivery-dev2-20261101/`:

- `windows-consumers.zip`: original fixture source, dependency adaptations and
  results, SHA256 `5CB5C9253175D1B73F44C99F1AEE7AF457A26089C93D8514F35F8E674771ACB4`.
- `windows-compilation-inputs.zip`: exact bytes of all462 unique documents in
  the six existing qualified PDBs, including generated inputs; every file matches
  its recorded document hash. SHA256
  `6064C4843FED5F0B0F6EA285ED211B5BFB21661144A44951C0882B70069CABD3`.
  Windows CRLF compilation bytes and Linux LF source builds stay distinct.

Linux consumes the exact Windows-produced assets: all 106878 detached checks,
the same Awk/Lython scenarios and the public probe pass on both runtimes.
[Package results](linux-packages.json), [consumer results](linux-consumers.json)
and [identity audit](linux-identity-audit.json) cover six package/symbol/PDB assets
and 24 feed/cache/lock observations. Packages were not repacked. Original Windows
compilation bytes above provide the PDB source evidence; the Linux checkout has LF.

The complete source VM refresh passed in **2529.752s (42m09.8s)**, from
2026-11-01 03:46:43Z to 04:28:53Z. The existing runner completed all 83 steps:
one Release build, both-runtime correctness/disabled dynamic code, 89 Core cases/
178 rows, 90 paired Python operations, four lifecycle/seven scaling families,
126 PCRE2 rows/16 scaling families, all 65 PCRE2 paired commands, resource costs
and final verification. No rows were stopped or omitted. The 3600s source-refresh
target is met; package/consumer qualification remains outside that clock.
See [full phase receipt](full-refresh.json).

Current uncertainty remains explicit: six Core unqualified rows; Python 27 managed
faster, 49 CPython faster, 12 inconclusive and two unqualified; native PCRE2 72
managed faster, one equivalent, 15 native faster, 13 inconclusive and 25 exclusions.
The two Python unqualified rows are lookahead hit and possessive miss, whose
CPython removable harness floors exceed the existing gate. The runner's
remaining-unqualified=0 counter means no cases remain unmeasured. It does not
override these verdicts. No resampling was used to improve classifications.

[Original/final ratios](ratio-summary.json) retain statuses, intervals, allocations
and source identities. These are comparator-relative observations, including
comparator movement; causal public A/B gains live in their individual receipts.

| Core compiled case | Original ratio | Current ratio |
| --- | ---: | ---: |
| AWS quick | 1070.25x | 1.43x |
| Keyword family | 495.80x | 9.82x |
| One-node backtracking | 390.51x | 2.54x |
| IPv4 Count | 313.98x | 0.76x |
| AWS full | 258.81x | 0.52x |
| Region marker | 172.18x | 105.74x |
| Backtracking | 99.71x | 4.37x |
| Rust word Count | 58.16x | 0.13x |
| FTP | 29.97x | 3.71x |

The largest residual Core gaps remain region viability preparation, capitals,
keyword family, imports and Chinese literal scans. Their failed/bounded pilots
and distinct-cause deferrals remain in the existing receipts. Python atomic miss,
date miss and Unicode FullMatch retain their failed-pilot deferrals. Newly
qualified literal early Search is 5.62x on a 65542-byte subject; it requires
full-input validation even for an immediate hit and needs bounded attribution.
One-capture FindAll remains 4.14x. PCRE2 dates remain 11.53x/11.29x with the prior
UCP pilot closed; subroutine/conditional cases retain their accepted dispatch
improvements and remaining gaps. No blanket parity claim or repeated campaign.

Finite controls are measured through the existing [public resource lanes](resources.txt)
([reported medians](resource-summary.json)): 650 timed samples, minimum45.728ms.
On `(a+)(b+)`, Core IsMatch is 1.166us unlimited versus 1.311us with all four finite
limits and a live token; Python Search is 1.024us versus 1.177us. Both add120B.
Core Count is .771->.875us; Python Count1.047->1.203us. These are real policy costs,
not free controls or identical cross-profile result contracts. PCRE2's finite
lane changes MatchLimit; it does not claim to measure all four Core controls.

The separate [fixed-baseline public cost check](cumulative-cost.json) completed
in109.361s against exact original `a40bda03` DLLs. Same typed public-call IL runs
in isolated contexts;26 equal-result cases, warm1.5s/min1000, independent60ms
calibration, five alternating samples>=57.817ms. Results are checked after timing.
[Preflight](cumulative-preflight.json), [summary](cumulative-summary.json),
[host](cumulative-host.cs.txt), [lanes](cumulative-lanes.cs.txt).

The genuine `(ab[0-9]d)` mapped-template workload recovers its historical debt:
text4.178->2.065us(-50.58%,1032->400B), native4.318->2.136us(-50.52%,1280->400B),
string4.103->2.122us(-48.29%,976->416B), destination-52.17%(1224->344B),
no-match-59.23%(328->40B). Other covered median regressions are <=5.1%; preserve
individual ranges/outliers and the small controlled-metadata allocation increases.
Core finite/live identifier Count improves20%, live Boolean71%; Python possessive
miss89%/lookbehind45%; PCRE2 Unicode enumeration31%. The unchanged BCL control
is-.16%. No confirmation, unweighted average or universal performance pass.
New funded APIs have no original counterpart; their feature receipts remain
separate. Linux source performance does not claim identical Windows package bytes.

Source CI41270306614 passed Windows/Linux/both targets and disabled dynamic code.
Imported snapshots/generated tables pass their affected source tests on both
runtimes. Root prose now gives the current date/scope; generated rows are unchanged
reporter output, sorted by decreasing producer/predecoded ratio.

Additional immutable archives under `artifacts/experiments/delivery-dev2-20261101/`:

- `full-refresh-8d554527.tar.gz`: source, binaries/PDBs, full refresh and cumulative
  driver/raw evidence, SHA256
  `71AE1D9DC5A9BCDD38FCA2F6D1117A47D70551418D9FAE77E540D7609A268501`.
- `linux-consumers.tar.gz`: exact package inputs, copied fixtures, logs, TRX and
  identity observations, SHA256
  `7DC0BD7DF8D610E6318E93E7EE52A1A063B240906F26FC3713BCF86CB8E3293F`.
- `final-verification.zip`: snapshot-import checks and final template checks,
  SHA256 `C9CCFF098F2D110E242038EDCF07C005427561CF673FD98A27FEB6EC00A6050B`.

Optional PCRE2 reusable scalar projection remains explicitly unfinished in its
specification. POSIX is a separate post-performance milestone. Neither is silently
included in the current native/funded byte-range contract or package acceptance.
