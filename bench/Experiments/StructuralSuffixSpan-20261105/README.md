# Checked span search for native structural suffix Count

**Accepted.** The existing reverse-suffix counter now uses checked span `IndexOf`
for its exact literal search. Boundary tests, whitespace rewind, prefix
verification and non-overlapping advancement are unchanged. This replaces one
custom scalar candidate-search call while retaining SIMD through the BCL span API.
The now-unused prepared-search descriptor in each literal suffix is deleted;
suffix parts retain their existing byte array directly. Shared prepared-search
helpers with other consumers remain.

No new algorithm, selector, threshold, cache, owner or mode. Strict complete-subject
UTF-8/scalar semantics, captures, retained results, RTL/NonBacktracking and original
cancellation/deadlines remain. Controlled/finite invocation paths are unchanged.
Removing preparation and descriptor storage can lower construction work and cache
capacity requirements. Major admission still precedes allocation and uses actual
`Unsafe.SizeOf<T>()` strides; no manual size estimate is introduced.

The untimed route probe confirms both ordinary/compiled public calls already use
`native_structural_family_suffix`, with no emitted kernel. Dispatch is unchanged.
The real53352-byte ASCII subject has SHA256
`E25F45289E44A6DE119149C10B258E8416A8E24E4FF01D94ED4753FA5A9A351B`;
public/native/BCL Count all return0. Existing generic-prefix replays are slower
controls, not the public implementation. The selected-kernel attribution command
does not support this shape and exited before timing; its failure is preserved.

One **105.622-second** bounded public comparison uses parent e0816420 and the
frozen206691c4 graph. Baseline Core SHA256
`63A885F3AEDA71CF0D1FB6245640DB90A35ECC8D789682642954D7E4757F19E6`;
candidate `97453E596BFD6D1EF40945BF909F3BB192DB2ABF8F22D5286F5E09CE11A67E01`,
source overlay `2FA82765DBBD202E3BB829E170187307BBFDC2F3F38B195F3A869D4937BB9B52`.
One Core per ordinary Default-context process, A0/B0/B1/A1 order, identical typed
caller and six frozen companion DLL/PDB files. [All observations](cost-summary.json):

| Public Count | Before µs | After µs | Change | Before/after bytes |
| --- | ---: | ---: | ---: | ---: |
| Real modifier/type, ordinary |23.46|5.02|−78.59%|0 / 0|
| Real modifier/type, compiled |23.52|5.02|−78.65%|0 / 0|
| Same finite/live token |122.41|122.57|+0.13%|112 / 112|
| Same finite deadline/live token |195.80|196.84|+0.53%|160 / 160|
| Dense true suffixes |65.89|66.46|+0.87%|0 / 0|
| Dense false suffixes, one final match |132.81|61.59|−53.62%|0 / 0|
| ASCII literal control |2.64|2.66|+0.62%|0 / 0|
| Actual Ruff control |4534.84|4565.43|+0.67%|3584 / 3584|

Keep the dense-hit cost, about0.57µs, and all movement on unchanged finite/deadline
and control paths without causal credit. Results include3072 dense true matches,
one final false-prefix control match and20 Ruff matches. BCL predecoded and strict
decode-then lanes compare matching results/options; finite producer policies have
no BCL resource equivalent. These diagnostics do not assign a new catalog Status.
No confirmation or source retuning follows this comparison.

Normal eight-CPU startup thenCPU2; producer warmup1.5s/min16, BCL250ms/min4,
independent60ms calibration, five alternating samples/process with typed checksums
after timing. All160 producer samples exceed **58.1167ms**. Summaries pool ten
observations/version/case, averaging the two middle values for an even median.

Focused Release checks pass on Windows/Linux and.NET10/11: Core1038, Python176,
PCRE165 per runtime. Coverage includes structural suffixes, boundary and separator
overlap, borrowed/offset spans, malformed inputs, finite/funded/publication and
interruption/lifetime controls, plus construction/cache admission. Existing tests
exercise these production paths; no implementation-mirroring test is added.

Before any timing, a call-site audit found the per-suffix search descriptor had
become unused. The declaration was amended to include its deletion. Initial
two-line prototype source,874-check logs and graphs remain preserved; only changed
projects were rebuilt for the final candidate. The timed comparison measures the
complete simplification once against the original frozen baseline. Its eight-case
caller is unchanged; the final preflight reuses the original before graph.

Immutable artifacts/experiments/structural-suffix-span-20261105:

- evidence.tar.gz: `A4A01347AD4C112ACD5E288D171D6B0E0BCFD7C66E34385DB377FCE44CA0BFA6`.
- windows-evidence.zip: `3820F9768F118DBB2A20464ED131B543D621FC74CCE69B0E14D3E121D67356BE`.
- route-evidence.zip: `04581CC06052D60B4196DE6885CB7D4F93C8D7D89F6AB6612A69EC884B81E0EB`.

Raw evidence retains declarations, initial/final source and graphs, caller,
preflights, every sample, focused logs and source/companion checks. Published/
staged packages and previous refreshes remain immutable. Full/package matrices
stay at the next coherent delivery boundary.
