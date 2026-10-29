# Caller-funded PythonRe compilation and program lifetime

**Accepted, source 7deb8230.** New text/strict-UTF8 Compile overloads ending in
IUtf8MemoryAllowance reuse construction admission/rollback, then retain one
conservative completed-program charge. This is not peak construction, historical
traffic, a private cache maximum or exact heap size. Failed/canceled compilation
refunds its reservations. Null MaxBytes adds no hidden byte cap on these overloads;
explicit numeric bytes and existing instruction/work/depth backstops remain.

Utf8PythonRegex now implements IDisposable. Funded disposal closes new matching
uses and defers its refund until existing operations/cursors finish. It never
waits, cancels callers, retains construction authority or uses GC reclamation.
Unfunded disposal is a no-op; owned results survive and range results preserve
their borrowed-subject lifetime. One typed program-use owner guards every public
operation family, including native literals, independent Count/FindAll lanes,
replacement preparation, callbacks, publication and durable cursors.

This scope funds construction/program storage. Shared cursor working admission
is a separate explicit parameter and can use the same account. Replacement-plan
preparation/retention and caller-owned output remain unfinished aggregate-hosting
requirements. No total-memory, complete hosting-contract or release qualification
claim is made here. Native algorithms, scalar semantics and resource controls stay.

[Main comparison](comparison.json), [console](comparison.txt),
[focused confirmation](confirmation.json), [console](confirmation.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt), [preflight](preflight.txt):
3f81f037 versus 7deb8230, quiet VM CPU2, .NET11 RC3, exact producer DLL/PDBs.
The main comparison took **38.87s**, with 500ms per-lane warmup, independent
calibration and three alternating samples. Matching batches were >=**39.55ms**.
Construction's first row had visibly unstable startup samples and undersized
comparator batches; its initial +182% median is not steady-throughput evidence.
One prebounded confirmation used 2s warmup and a 100ms calibration target on
construction plus unchanged matching controls: **67.99s**, all batches >=**119.99ms**.
Both runs had a 120s outer cap. No further confirmation/campaign.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Ordinary eager literal Compile + dispose, confirmation | 42.70 | 42.90 | 51320 / 51336 |
| Ordinary eager class/capture Compile + dispose, confirmation | 77.30 | 77.71 | 83040 / 83056 |
| Add funded eager class/capture Compile + dispose, main | 79.81 | 83.22 | 83040 / 83128 |
| Add funded scalar Count, main | 14.45 | 15.40 | 1680 / 1704 |
| Add funded Unicode/capture Count, main | 26.47 | 24.46 | 5744 / 5768 |
| Add funded finite/live-token Count, main | 16.02 | 16.85 | 1800 / 1824 |
| Add funded native literal miss Count, main | 0.323 | 0.370 | 0 / 24 |

Existing matching controls were <=+4.4% in the main run; repeated controls were
<=+0.3% in confirmation, with matching allocations unchanged. Negative variation
is not a speedup. Ordinary construction adds 16 B; funded construction adds 88 B
against the old unfunded call. Funded matching adds 24 B/call for an explicit
in-flight use: roughly +6.6% scalar, +5.2% already finite/live, and **+14.5% / 47ns**
on the tiny native miss. These are added lifetime-authority costs, not identical-
authority regressions or a global <=10% guarantee. No native route was disabled
or metering removed to conceal them.

The four lanes include public predecoded and decode-then-.NET Count comparators;
all matching counts agree on overlapping semantics. Python named groups use their
equivalent dialect spelling. Construction comparators are diagnostic different
grammar compilers, outside matching parity. Factories are emitted typed bridges;
reflection/setup is outside timing. Funded compilation/disposal asserts balance
per operation; warmed programs release their entire account at final disposal.
No catalog row or benchmark Status was refreshed.

[Release checks](verification.txt): PythonRe 4104/runtime, affected Core 1377 and
PCRE2 179/runtime, .NET10/11. Tests cover changing refusal stages, malformed UTF8,
post-grant cancellation, independent/concurrent programs, unrelated host loans,
async cursor waits/aliases, callback disposal, retained results and closed public
scalar/native families. Prior CI 40954991992 passed the Core guard correction but
had one Windows PCRE2 test-thread join timeout; that fixture passes locally in
the affected checks. Preserve the failure log rather than relabeling it.

Immutable archives in artifacts/experiments/python-funded-program-20261029/:

- python-funded-program-7deb8230.tar.gz: exact producer DLL/PDBs, source bundle,
  driver/preflight/build logs, raw measurements and confirmation. SHA256
  `2D89C98E8EED10AAC06C158C230E7DBCB1FCA0BD0813EC30BD7995912C41CA8D`.
- correctness-logs.zip: SHA256
  `B12231B9983FF4CA2D5EE4CAEA302E101D3EFC09CBAC3FFE3153F1D85EBB0D8E`.

The archive was streamed directly to the workspace and fully inventoried. The
initial driver's reflection-policy type mismatch failed preflight before timing;
that log is retained. The archived final driver adds only the confirmation mode
to the main-run driver. No package/platform matrix was repeated; published and
staged assets remain immutable. Final cumulative performance and delivery checks
still require the actual release bytes.
