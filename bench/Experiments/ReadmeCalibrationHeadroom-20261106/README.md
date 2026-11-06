# Core benchmark calibration headroom

**Accepted.** Raise the existing six-lane runner's target from50ms to75ms and
preserve its relative confirmation window (.8..1.5), single confirmation and
20ms qualification floor. No retry loop or new qualification mechanism. Shipped
libraries, APIs, semantics, resource controls and package bytes are unchanged.

Dev41 has six unqualified ordinary/compiled rows: Twain, river window and symbol
Count. Their allocating .NET lanes recorded15–18ms despite calibration. One
**47.457930294s** bounded comparison exercises those three workloads and English
Count, with the existing equal-result CLI and five samples per lane. Source
parent `13d97dd7`; the old runner's project sources match frozen dev41 exactly.
Only the benchmark DLL/PDB changes. All six shipped DLL/PDBs and every other
copied dependency stay identical; normal runtime startup and existing benchmark
affinity are preserved. No confirmation, tuned variant or public table refresh.

| Workload | Before minimum ms | After minimum ms | Runner before s | Runner after s |
| --- | ---: | ---: | ---: | ---: |
| Twain |24.408|25.758|4.958|5.879|
| River window |18.410|29.531|5.309|6.707|
| Symbol |16.265|34.913|5.465|6.528|
| English control |37.523|60.788|4.982|5.973|

River and symbol reproduce below-floor batches; all120 candidate samples exceed
25.7575ms. Historical Twain failures do not reproduce in this pilot. This supports
more headroom, not a guarantee against future short samples. Per-workload wall
costs rise18.56–26.32%; English rises19.89%. Results and allocations match across
all lanes. [All raw-duration summaries, iterations, phases and identities](cost-summary.json).
The separate unchanged `--measure-case` control retains result513; its11–18ms
single samples are descriptive and do not qualify throughput. Changes in the
reported operation medians are harness observations, not an engine speedup.

The nominal extra sampling budget is178*6*5*25ms=133.5seconds. The prior full run
is43m35s; a future coherent Full must still prove the unchanged3600s target and
original RAM/disk admission. Do not infer a new Full-clock result or change the
six catalog exclusions from this dirty-source pilot. Package/platform matrices
and generated figures wait for that boundary.

Release benchmark builds pass Windows.NET10/11 and Linux.NET11. Existing snapshot
guards pass both targets/both OS: Core3/Python1/PCRE1 per target, matching binaries
reused. All eight frozen dev41 Full DLL/PDBs remain byte-identical. Benchmark
before `B7F07B3FEEB743FDDD8E24B3EE0C8CC04884614B409718A41F41EF8C8A3C7DE6`,
after `7CBDD84F85EB017DD4B1DF00F1A7B0D7E7E56BCCCDF4646B850A9E5C67E6C28A`.
Peak affected-corpus RSS379484–384684KiB; no memory-reduction claim.

Off-VM `artifacts/experiments/readme-calibration-headroom-20261106/evidence.tar.gz`:
`EC4597B54A4C84A0E7F3EF2AE2437448102673075858F575EC03B58C40A55884`.
It retains exact graphs, source patch/declaration, scripts, every sample, phase,
process resource observation and Linux guards. Windows `windows-evidence.zip`:
`EA6B8CE74BBC0B8E8D2BD2C58E37736B58CE85C0CB2121C14DCABF4D30BA72D5`.
Published/staged packages and previous snapshots/raw remain immutable.
