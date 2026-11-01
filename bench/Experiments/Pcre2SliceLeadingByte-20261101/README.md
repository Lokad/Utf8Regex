# Use the existing first-byte proof before Boolean slice attempts

PCRE2 Boolean slice search reuses the compiler-required ASCII byte, including one
following a capture assertion. Impossible starts are scanned under at most2048-byte
work quanta; every scan checks the original token and deadline, including unlimited
calls. Actual candidates use the existing matcher and capture workspace. A current
candidate avoids the scan, preserving the ordinary dense-candidate path.

Nullable, contiguous-origin and compile-anchored programs decline this hint;
non-None match options keep their existing owned route. Other public operations
pass no hint. No compiled metadata, buffer, execution engine, selector/framework,
compatibility mode or unchecked access was added. The cursor holds one existing
proof value. Work/admission costs can decrease by skipping impossible attempts,
with basic metering retained; [SPEC](../../../docs/SPEC-PCRE2.md) documents that.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json) took
91.058s on the quiet Ubuntu VM, .NET11RC3,CPU mask4. Frozen a198 binaries before
(parent7a changes data/receipt only), parent plus archived four-file adapter patch
after. Core code unchanged; its assembly was rebuilt with parent metadata.
Fourteen cases, four equal-result lanes, warm1s/min64, independent60ms calibration,
five alternating samples, minimum59.714ms. Complete results checked after timing.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Assertion-capture default |851.417|1.130|753x faster|
| Live token |925.959|1.406|659x faster|
| Finite/live |948.302|1.476|643x faster|
| Deadline/live |940.252|1.733|543x faster|
| Missing first byte |847.383|0.297|-99.96%|
| Dense hit |1.047|1.023|-2.28%|
| Dense false starts, initial |853.963|948.797|+11.11%|
| Failed candidates |632.907|240.611|-61.98%|
| Unicode prefix |201.614|9.797|-95.14%|
| Caseless control |3316.169|3308.316|-0.24%|
| Unicode-leading control |372.347|399.150|+7.20%|
| Nullable control |0.835|0.809|-3.11%|
| Anchored control |0.976|0.963|-1.33%|
| Literal control |0.247|0.252|+2.10%|

Allocations are unchanged except the miss,5744 to64bytes. Keep all
[pairs/spreads](summary.json), including the initial adverse dense observation.
It straddled10% across pairs(8.7..13.3%) and exceeded the gate by less than its
observed spread, so the sole selected [confirmation](confirmation.json) used
identical producer/lane DLLs and5s/min64 warmup, only dense false starts and the
unchanged literal control. It took45.538s under60s; independent60ms calibration,
five alternating samples. Dense847.055 to866.982us,+2.35%,allpairs0.7..5.3%; literal
control+.75%. [Confirmation details](confirmation-summary.json) retain both results.
This supports warm throughput within10%; it does not erase the1s observation or
establish identical costs through every warmup phase. No further comparison/variant.

[Release checks](verification.json) pass on both runtimes: full PCRE2 suite6462
per runtime, then final30 new fixture checks. They cover actual first-scan
interruption (slot-only peak, no VM workspace), bounded step costs even unlimited,
refusal, EOF publication, original token, borrowed13-byte loans, scalar offsets,
strict malformed tails, captures and ineligible nullable/anchored/contiguous/
caseless programs. No new package/platform/fullcatalog qualification.

- [Raw harnesses, patch, binaries and measurements](../../../artifacts/experiments/pcre2-slice-leading-byte-20261101/evidence.tar.gz):
  `0F660100B9B5A13DEA120A64F71EF6C6DA8255A576D4E61CEAD1B7E9C0C78184`.
- [Correctness and source](../../../artifacts/experiments/pcre2-slice-leading-byte-20261101/correctness-source.zip):
  `9E0D41C43222D73210409D2EB41A1504A4972983E551206EB983C3DD7981C3EE`.

Immutable dev.3 packages and its full refresh remain that source's qualified
boundary. This new source needs only its affected-row refresh now; future cumulative
and package gates remain at the next coherent delivery boundary.