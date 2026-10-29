# Explicitly funded retained PythonRe range results

**Accepted, source ce857608; cleanup follow-up 46efb411.** MoveNextRetained returns one typed disposable
Utf8PythonRetainedRangeMatch receipt (Match, ChargedBytes, Dispose). It requires
the cursor's caller-owned working allowance. Ordinary result APIs and discovery
algorithms are unchanged; no generic output framework, ambient host scope or
overload matrix. Unfunded cursors reject retained use before discovery and remain
usable through ordinary methods.

Range/name arrays and the conservatively sized receipt are admitted before
acquisition. Shared immutable name text is counted again for an independent
lifetime, without copying or identity tracking. Publication transfers the existing
loan from invocation to receipt without external Release/TryReserve. The receipt
can outlive cursor/program disposal and host waits. Dispose refunds exactly once,
including concurrent aliases, with no finalizer. Previously copied immutable
range data stays valid; continued admission after explicitly disposing its receipt
is the caller's responsibility. Subject bytes remain borrowed.

Numeric result traffic includes receipt/shared-name acquisition and never refunds
on transfer/disposal. Retained live results can exhaust the shared allowance;
immediately released receipts support bounded-live streaming. This is not exact
heap/RSS admission. Text/bulk/substitution ownership transfer, scalar positions
and structured diagnostics remain open H1 work.

[Public comparison](comparison.json), [console](comparison.txt),
[preflight](preflight.txt), [driver](driver.cs.txt), [project](driver.csproj.txt):
exact 451abf14 versus ce857608 DLL/PDBs, quiet VM CPU2, .NET11 RC3. **21.96s**,
500ms per-lane warmup, independent calibration, three alternating samples,
minimum batch **28.73ms**, 120s outer cap. No confirmation or catalog refresh.

Each public operation creates/disposes a cursor, enumerates every result, copies
numeric capture ranges into common consumer arrays, and disposes each retained
receipt after consumption. All consumer shaping/disposal is timed; the outer
batch retains every numeric output until timing ends. Equality/checksums consume
every output after timing. Both .NET diagnostic lanes materialize all captures
and project UTF16 indices to UTF8 bytes inside timing; those coordinate conversion
costs are included, rather than a matcher-only comparison. Patterns overlap on
scalar/code-unit semantics. No engine parity or benchmark Status claim.

| Public range operation | Matches | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: | ---: |
| Existing unfunded ASCII ranges | 48 | 13.074 | 13.021 | 7520 / 7520 |
| Existing funded ASCII ranges | 48 | 15.121 | 15.248 | 7664 / 7664 |
| Existing funded Unicode/capture ranges | 32 | 20.096 | 20.142 | 10808 / 10808 |
| Add retained ASCII receipts | 48 | 14.880 | 18.023 | 7664 / 11120 |
| Add retained Unicode/capture receipts | 32 | 20.073 | 22.015 | 10808 / 13112 |
| Add retained named-capture receipts | 48 | 19.050 | 24.127 | 13312 / 16768 |
| Add retained finite/live-token receipts | 48 | 17.962 | 22.555 | 7664 / 11120 |

Existing calls remain <=**+0.9%**, allocations unchanged. Negative variation is
not a gain. Independently funded retained receipts add **21.1%** ASCII,
**9.7%** Unicode/captures, **26.7%** named and **25.6%** finite/live. Each receipt
adds **72 allocated bytes/match**; its modeled charge is deliberately more
conservative, and shared-name charges allocate no copied strings. This adds
retained-result authority absent from the baseline API; it is not an equal-
authority regression or a global <=10% promise. Final disposal balances every
account; no historical charges accumulate.

[Release checks](verification.txt): PythonRe 4145/runtime, affected Core
result/ledger/inventory 715/runtime, PCRE2 result/resource/lifetime 727/runtime,
.NET10/11. Eleven public tests prove independent lifetime after engine disposal,
async waits, concurrent receipt aliases, no external publication release,
live-result refusal, 500000 immediately released receipts under 16KiB remaining
shared capacity, explicit result-traffic denial despite prior disposal, post-grant
cancellation at array/receipt stages, timeout, mixed legacy advances and preserved
snapshot aliases. A 4096-character capture name cannot escape admission after
the program closes: its retained charge is conservative and independently paid.

CI 40970082730 on ce857608 passed Windows/Linux, both runtimes and disabled
dynamic code. Final lifetime review then found that a disposed receipt kept its
subject/metadata alive if the host retained it for ChargedBytes. A public weak-
reference test failed on both runtimes before 46efb411; disposal now clears its
local Match payload before refund, while copied matches remain valid. Match
reads must finish before disposal; concurrent Dispose remains idempotent. All 224
retained/range checks per runtime pass afterward. The GC witness proves payload
roots only; funding refunds never depend on GC.

Because this fixes real disposal work, one separate two-row comparison measures
only that change: [raw](cleanup-comparison.json), [console](cleanup-comparison.txt),
[driver](cleanup-driver.cs.txt), ce857608 versus 46efb411, **6.89s**, minimum batch
**26.02ms**, 60s outer cap. Same retained API before/after, plus unchanged legacy
funded ranges. Retained 32.953 -> 33.063us (**+0.34%**, 11120 B unchanged); control
27.587 -> 28.100us (+1.86%, 7664 B unchanged). Absolute timings belong to this
separate paired run and are not merged with the initial pilot. No confirmation or
repeat of the seven-row campaign. This correction removes a demonstrated root,
rather than adding accounting precision or a new lifetime mechanism.

Immutable archives in artifacts/experiments/python-retained-ranges-20261030/:

- python-retained-ranges-ce857608.tar.gz: exact producer DLL/PDBs, source bundle,
  driver/build/preflight/raw results. SHA256
  `53AB7372A598CCE77686ABAD689D48551AD0EC5C13B458D26F390D44AAD421FC`.
- correctness-logs.zip: SHA256
  `C7B527772D4EB92294F5E1B9C74006D9283A39691504290A5A4B2279A15BF23D`.
- python-retained-ranges-cleanup-46efb411.tar.gz: exact cleanup binaries,
  source bundle, driver/build/raw results. SHA256
  `4991135E81A02A298F363B75032A86EA7ECED7500CAF0E670E5E559B0EC2CAB8`.
- cleanup-correctness.zip: failed and passing root-retention witness/checks.
  SHA256 `E961D8D1F47651B473A52183622219BC91DF70A1FC39CD492A91BB3E1AAA034F`.

The archive was streamed to the workspace and fully inventoried. No unchanged
platform/package matrix was repeated. Published/staged assets remain immutable;
final cumulative performance, current sub-hour refresh and release/consumer gates
still require final candidate bytes.
