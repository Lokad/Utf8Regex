# Current captured-string CPU disposition

One15-second public FindAllToStrings capture, current Core source afc9354d and
unchanged qualified PythonRe dev.100. Frozen Core4F8E1F6D/Python830674FB DLL/PDB
pairs and all393 actual compiler documents/member inventories verify. Only the
private caller builds on Windows; no producer or VM caller build. Normal8CPU
startup thenCPU2. Valid unchanged UTF8, Python re over str; pattern
`([a-z]+)-([0-9]+)`,18432 input bytes,2048 tuples/4096 strings, every value checked
after each64-call batch. Canonical capture fixture/options, complete output.

17209 cpu-clock:u samples at997Hz over17.178s include startup/warmup/runtime and
consumption. Zero ambiguous managed-map samples. Flat self attribution:

| Method/work | Sample share |
|---|---:|
| Complete capture-root execution | 17.08% |
| Selected-range DecodeUtf8 | 10.47% |
| MoveNextCore | 8.18% |
| SeekCandidate | 5.71% |
| FindAllScalarStrings | 5.00% |
| Result array allocation | 4.12% |
| ChargeResultBytes / TryChargeResultBytes | 2.28% / 2.25% |
| Unresolved CoreCLR | 25.14% |

RunClassRepeat is no longer a leading entry after the accepted root/scan work;
the older d7e28bd3 shares are historical. Inlined work belongs to its containing
method. Unresolved runtime samples are not a GC attribution. These are neither
phase timings, throughput/parity/speedup nor a causal optimization proof.

Source inspection finds selected-range decoding, required result admission and
capture closing-order storage, not another whole-subject validation/map pass.
GetText also decodes unmatched split/replacement ranges; the capture root's ASCII
proof cannot be applied globally. ASCII range bounds are already encoded in the
immutable instruction operand, so do not add another descriptor/cache or rerun
mask analysis. Preserve basic metering and original interruption.

One untimed FullOpts preflight of the same frozen producer emits2070 bytes for
TryRunTerminalCaptureRoot. It copies the20-byte readonly instruction into a
by-value GetUnfoldedAsciiRange call at both run entries. Investigate a readonly
reference for that leaf accessor, with focused correctness, Release codegen and
one bounded public comparison/unchanged controls; no algorithm/polling/decoder
change follows from this capture alone. FullOpts is not the sampled PGO body,
and this observation does not prove an exact share or public benefit.

Derived perf-inject annotation rejects the existing capture because it lacks
`perf record -k 1`; retain that limitation, no CPU recapture. Independent FullOpts
inspection remains usable. No package/Full/catalog or native qualification.
Raw verified offVM before keeper release; original handles terminal0. Owned VM
task retired after accepted hash/zero process/cwd/FD/map references.

Artifacts: artifacts/experiments/python-capture-current-cpu-20261116/.
SHA256 raw081C5D6BA1D1C8E8C14888C323EAF778537B2F521A6259E341C9B4BE1DB3FAA1;
derived53CFDDB6D066CADC599659D670C462F69B349F24294110FB347BBD5441F07525.
