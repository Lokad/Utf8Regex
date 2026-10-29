# Caller-owned shared working admission for PythonRe cursors

**Accepted, source 3f81f037; timeout follow-up 887341bf.** A four-argument cursor
factory accepts IUtf8MemoryAllowance directly. The existing numeric storage ledger
reserves modeled major capacity before acquisition, including growth overlap;
EOF/failure/disposal refund exclusive storage synchronously. No alternate account
or uncapped retry follows refusal. Utf8MemoryAdmissionException reports the refused
additional amount. Numeric work/depth/working/result controls remain authoritative.

This first public path funds cursor working/result-staging storage until
publication. It does **not** fund compilation, retained programs/caches,
caller-retained results, borrowed subjects or incidental owners. No traffic quota
or default working cap is implied. Full aggregate hosting ownership remains open.
The host implements a fast thread-safe non-reentrant allowance; failed/throwing
reservation leaves it unchanged, and Release must not throw/observe cancellation.

[Public A/B](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt), [preflight](preflight.txt): exact c96846b7 and
3f81f037 DLL/PDBs, quiet VM CPU2, .NET11 RC3, 500ms warmup, independent calibration,
three alternating samples. **29.00s**, minimum batch **37.55ms**. No confirmation.
Each cursor operation includes creation/validation, every advance, all owned
groups' ranges/text lengths, EOF/disposal. All producer/predecoded/decode-then-.NET
checksums agree on overlapping semantics. No catalog/parity Status refresh.

The additional allowance uses a caller-side atomic counter. A typed emitted
adapter crosses isolated assembly contexts without boxing/reflection per buffer;
it is benchmark tooling only. Every operation asserts zero outstanding caller
funding after cursor disposal. Compilation/provider setup is outside timing.

| Public cursor operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Add shared admission, ASCII | 29.75 | 37.03 | 10560 / 10688 |
| Add shared admission, Unicode/captures | 48.04 | 54.81 | 15104 / 15232 |
| Add shared admission, already finite/live-token | 42.38 | 45.81 | 10672 / 10688 |

These are **additional admission authority costs**, not equal-authority matcher
regressions: **+24.5%/+14.1%** from unlimited calls, **+8.1%** with existing finite
controls. The allowance adds no implicit cumulative output cap. No metering or
authority was removed to hide these costs.

Existing API controls cost at most **+2.7%** (finite cursor); live-token cursor
+1.8%, native miss +0.5%, Python Count +0.7%. Negative ASCII/Unicode/Core Count
variation is not a gain. The new allowance reference grows fixed invocation/cursor
metadata: controls allocate **8-16 B/op more**, while their payload allocation
behavior is unchanged. No allocation-equivalence or global <=10% claim.

[Release checks](verification.txt): complete PythonRe 4089/runtime, final shared/
API ten/runtime, Core 381/runtime and PCRE2 32/runtime. Shared public paths prove
500,000 discarded ranges under a single 16 KiB allowance, concurrent independent
users of one expression/account, preservation of other host loans, partial refusal
refunds, post-grant cancellation, numeric precedence, owned map cleanup and retained
results. Fixture compilation and stale API snapshot failures are preserved.

Follow-up 887341bf omits timeout Input with shared admission, preventing a large
diagnostic decode outside its funded scope. Thirty-two shared/persistent/API checks
per runtime pass, including deterministic expiry and synchronous refunds. This
cold failure-path correction is after the timed binary; success-path source is
unchanged and no timing was repeated for provenance. Final cumulative/package
qualification still requires final release bytes.

Immutable archives under artifacts/experiments/python-shared-working-20261029/:

- parity-python-shared-working-3f81f037.tar.gz: exact baseline/candidate DLL/PDBs,
  source bundle, driver/build/preflight/raw results. SHA256
  `EF8507FEB23E12AB25192A5FD964998DFFC8CD7E47B1F2AD914301C3665D723B`.
- correctness-logs.zip: SHA256
  `0CF5C0514FCAE2E77A2DEE9C5A60611261DEE1C53BB92B15D6358D8952F70426`.

Archives streamed to the workspace and passed a complete tar inventory read.
No package/platform/catalog matrix was repeated; published/staged bytes stay intact.
