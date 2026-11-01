# Common-prefix-only Python hint: rejected

Source `476ccd99` has the same runtime as frozen dev.7 (`2ff58324`). Its optional
Core hint for `item-([0-9]+)` contains ten necessary alternatives, `item-0` through
`item-9`. The [actual-program census](../DeliveryDev7-20261101/findall-programs.json)
records the unchanged twelve-instruction scalar verifier and the changed hint
eligibility versus the fixed original `a40bda03`.

This pilot reused the existing prepared SingleBucketPrefix bytes as a necessary
start, using checked span searches in overlapping, bounded 2,048-byte windows.
The scalar VM still verified captures and the complete pattern. No new storage,
API, mode or framework. Complete native results and other families stayed intact.

One 95.731-second public A/B, sixteen workloads/four equal-result lanes, on the
dedicated .NET11RC3 VM/CPU2. Both lanes load **identical frozen Core bytes**;
PythonRe is frozen source versus that source plus the archived patch. Typed public
calls, one-second/minimum1,000-call actual-loop warmup, independent calibration,
five alternating samples of at least20ms, results consumed after timing. Both
predecoded and decode-then BCL projections cover overlapping semantics.

| FindAllToStrings workload | Before us | After us | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Dense captured values |758.167|563.279|−25.71%|112944/112944|
| Finite limits/live token |968.478|690.380|−28.71%|117152/117152|
| Deadline/live token |1458.238|832.137|−42.94%|113112/113112|
| Mixed Unicode |407.295|305.207|−25.06%|55576/55576|
| Dense false common prefixes |85.777|106.601|+24.28%|2320/2320|
| All-false common prefixes |170.619|210.256|+23.23%|64/2256|
| False prefixes, finite/live |101.277|112.267|+10.85%|2592/2592|

[All medians, allocations, spreads and paired changes](summary.json). The
false-prefix regressions are stable, exceeding the10% allowance. Dropping family
confirmation sends those positions into the VM, including scratch acquisition on
the all-false miss. Rejected and fully reverted, including pilot-only tests. No
confirmation or variants; parent gains do not establish fixed-original acceptance.
The other dev.7 cumulative failures remain open. No packages, catalog snapshots
or benchmark Status changed.

Candidate Release PythonRe4530 and focused Core372 checks pass per runtime on
.NET10/11. Coverage includes boundary/offset/tail/false-prefix/ordered capture
behavior, malformed tails, retained aliases, every work cutoff, caller loans,
original-token cancellation and deadlines. After restoration,221 affected
PythonRe checks pass per runtime; matching binaries were rebuilt.

Immutable artifacts under `artifacts/experiments/python-family-prefix-20261101-deferred/`:

- `evidence.tar.gz`: SHA256 `35B17C5138CB690C0256B5C6860DC76A3CCF31EAC74135748A95B2890F7CA07C`.
- `correctness.zip`: SHA256 `46ED5FA99ACD53D3C1CA79CBC3B5883DB35F0A522B315C36BFA1776E68FBA382`.

Raw evidence includes measured binaries/PDBs, identities, driver, patches,
predeclaration, preflight and sample durations. Tar reported its enclosing
directory's timestamp changing while creating the excluded archive; the complete
archive is readable and its copied SHA256 matches the VM. It has not been rewritten.
