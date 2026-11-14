# Current folded-search CPU and anchor audit

One diagnostic on frozen Core `8b9b0a69`, .NET11GA: public invariant-ignore-case
`Count`, `John Watson`,899232 valid UTF8 bytes,11 results. AssumeValid trusts
unchanged text. Actual caller checks every integer result after each64-call
batch.1.5s warmup,15s capture workload,30s outer cap; existing sampler/decoder.
Only a private diagnostic caller is built, with exact producer DLL/PDB/source
and managed-member checks. Producer/caller image hashes survive collection.

17093 whole-process cpu-clock:u samples at997Hz span17.109s, including startup,
warmup, checks, JIT and runtime work.16478 map to managed code;615 are native/
unresolved, zero ambiguous non-stub mappings. 16196 samples (94.75%)
land in optimized Utf8LiteralCompiledEngineRuntime.Count, including inlined
search. Out-of-line Horspool has222 samples (1.30%); this does not assign all
Count samples to Horspool or establish exact instruction/phase costs.

The separate read-only prepared-anchor audit on the frozen VM finds:

| Literal | Preferred position / byte | Existing vector gate |
|---|---|---|
| John Watson |4 / space|Declines|
| hello world |5 / space|Declines|
| Sherlock Holmes |7 / k|Admits|
| alpha-beta |3 / h|Admits|
| abcdefghi |6 / g|Admits|

Source confirms the vector helper requires both anchors to be letters. Its
common0x20 fold mask cannot prove exact punctuation equality (space/NUL and
@/backtick are different bytes). This supplies a distinct source hypothesis:
keep the existing correlated algorithm and selected anchors, using a case mask
per anchor:0x20 for ASCII letters,0 for other ASCII bytes. Wider anchors retain
their current route. No new ranking, threshold, kernel framework or descriptor.
Boundary/raw-byte/false-alias tests and one bounded public A/B must decide this
candidate. No performance gain is claimed from this attribution.

CPU archive SHA256 `FE07F4A9DFFF6B275FB5E2CE232D4D122EC04C9976093D2C628C329E153A233E`, verified offVM before keeper38447 terminal0.
Raw under `artifacts/experiments/core-folded-search-cpu-20261114/`;
[acceptance](acceptance.json) preserves source/images, sample ranges and anchor
values. The first launch stopped on CRLF before any build or sampling; LF
transport was corrected once. No repeated capture, producer rebuild, package or
Full qualification. Accepted dispatch comparisons remain closed.
