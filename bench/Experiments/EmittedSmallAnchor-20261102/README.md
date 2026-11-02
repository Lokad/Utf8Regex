# Small emitted anchor search: closed trial

The checked BCL small-set search did not improve the primary keyword-declaration
workload. It slowed public Count by 7.08% ordinary and 5.84% compiled. Although
within the 10% allowance, it does not close the target gap. The implementation and
its trial fixture were reverted; there was no timing confirmation or second variant.

Current frozen Core f48327e6 serves the 53,352-byte ASCII code corpus through
`NativeStructuralFamilyEmitUpperWordIdentifier`. Its anchor at offset three is
`osu`: 5,878 raw byte candidates produce 73 matches. The earlier deep diagnostic's
90 complete-literal candidates describe a different helper. Its sub-20-ms phase
samples establish neither a public bottleneck nor a speedup. Actual SizeOf values
(2,720-byte search plan, 408-byte prepared searcher) do not prove hot copies in this
emitted route.

The single change reused the existing owned anchor bytes with span IndexOfAny for
sets of at most three bytes, omitting their additional PreparedByteSet allocation.
Larger sets retained their existing prepared SIMD implementation. Candidate order,
verification, strict validation, token/deadline authority and resource paths were
unchanged. Before/after Release FullOpts disassembly is retained; the proposed path
has BCL length dispatch, while the original uses PreparedByteSetState.Find.

| Public operation | Before us | Trial us | Change |
| --- | ---: | ---: | ---: |
| Corpus Count, ordinary | 73.46 | 78.66 | +7.08% |
| Corpus Count, compiled | 74.25 | 78.58 | +5.84% |
| Dense-hit Count | 37.49 | 33.63 | -10.30% |
| Sparse-miss Count | 3.51 | 1.82 | -48.01% |
| Two-anchor corpus Count | 50.75 | 49.06 | -3.33% |
| Larger-anchor control Count | 14.06 | 14.22 | +1.15% |
| Finite/live corpus Count | 48.57 | 46.63 | -4.01% |
| Live-token corpus Count | 45.75 | 46.76 | +2.20% |
| Deadline/live corpus Count | 56.35 | 56.70 | +0.63% |
| Dense-miss IsMatch | 390.03 | 409.28 | +4.93% |

The sole quiet-VM run took 59.409 seconds: 16 public Count/IsMatch workloads, four
isolated equal-result lanes, 500-ms warmup, independent calibration and five
alternating samples per lane. Minimum sample duration was 28.0134 ms. Allocation
medians were unchanged for every operation. These are parent comparisons, not the
fixed-a40 cumulative gate or a refreshed catalog status. The 73-match ordinary BCL
comparator differs from the compiled comparator; preserve that distinction.

Focused Release checks pass on .NET 10/11: public Count/IsMatch/Match/cursors,
production emitted searches over unaligned storage and every candidate origin,
boundaries/tails, larger sets, construction admission, finite limits and cursor
interruption. Adjacent construction/resource/deadline checks also pass. Initial
fixture mistakes (semantic Compiled flag and anchor-origin translation) were
corrected before timing. Reflection-probe failures and the stale local build are
not current-source evidence. No production defect was established by those errors.

Raw driver, input, producer DLL/PDB pairs, patch, full samples, phase output and
code generation are sealed in
`artifacts/experiments/emitted-small-anchor-20261102/evidence-complete.tar.gz`
(SHA256 `84F9793FAAEE98BCB36211E41AB30B651AC05B647BFBC3FB1E319D1C1A35D9BE`).
The correctness/source archive is `correctness-source.zip`
(SHA256 `38734358EFED8280E471B84F5FEE582357E84A47F8C7299EEB99341CCC94BFD3`).
Earlier archive attempts are preserved; final evidence uses distinct task-directory
prefixes. This did not rerun measurements. Source was byte-compared and restored on
both hosts, then affected projects rebuilt. No package, profile/catalog matrix or
published/staged artifact changed.

Next investigate the actual route choice: this same public corpus already runs in
roughly 46–56 us with live/finite/deadline authority, versus 73–74 us through the
unlimited emitted path. That is an observation across distinct routes, not proof
that switching all workloads is faster. Preserve dense-hit and native-path controls
before choosing a further change. Do not reopen this small-set substitution trial.

The raw driver's narrative Scope retained stale27078/407 labels from its template.
The measured producer identity/SHA fields identify frozenf483 and9f3+patch, as
recorded above and in summary.json; those fields and archived binaries are the
authoritative identities. Original raw bytes remain untouched.
