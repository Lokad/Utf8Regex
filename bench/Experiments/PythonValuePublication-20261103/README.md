# Check native value publication before constructing the value

Native PythonRe value operations derived their coordinates, constructed the
stack-only match, then kept it live across the final checkpoint. The method now
derives the same coordinates, performs that same checkpoint and constructs/returns
the pure value. Misses still perform one final checkpoint and return default.
No route, work unit, allocation, capture/coordinate, lifetime, admission,
cancellation or deadline contract changes. No helper, field, mode or framework.

Read-only warmed original/current code generation first ruled out lost inlining
of the literal-search/discovery chain. The changed FullOpts materializer shrinks
561 -> 533 code bytes, with a 104-byte frame unchanged. These diagnostics are not
used as a public performance claim.

One quiet-VM .NET 11 RC3 comparison completed **22 public cases in 97.756s**,
under 120s, minimum sample **43.987ms**. Fixed original `a40bda03`, parent
`1fc38f37` and its Python patch; parent/candidate Core DLL/PDB bytes are identical.
Twenty predeclared existing controls and explicit Python Match hit/miss retain
normal eight-CPU startup before CPU2 affinity, 500ms/minimum128 warmup,
independent 60ms calibration and five alternating >=20ms batches. Complete
range/count/predicate-pair results are consumed after timing, with allocations.
Compatible predecoded/decode-then compiled .NET diagnostics remain separate.

| Public operation | Change against parent |
| --- | ---: |
| Search hit | -0.68% |
| Search miss | -1.60% |
| Python Match hit / miss | -5.47% / -4.00% |
| FullMatch 17 / 256 bytes | -3.43% / -2.58% |
| Unicode Search | +2.60% |
| Boundary Search control | **+3.82%** |

**Accept:** modest actual Search improvement, larger Match gains, all parent
controls within 10%, allocations unchanged. No confirmation, tuning variant or
extra minimum-gain floor. [All results](summary.json) retain every row/sample
and fixed-original ratio. The favorable original Search ratio in this pilot
does not erase dev.16's stable +14.64% failure or establish delivery acceptance.

Existing focused Release checks pass **253 per runtime** on .NET 10/11: native
values/required starts, Unicode offsets/malformed tails, final original-token and
deadline authority, work/result/working controls, retained aliases and funding.
No mirrored tests or full matrix were added. No package or generated table changed.

Immutable raw evidence under `artifacts/experiments/python-value-publication-20261103/`:

- `evidence.tar.gz`, SHA256
  `9F577C8DC8620933B04B219FDBDACFF38DE07B129F323D351040C9011AE7DFCF`:
  frozen parent source, exact original/before/after assets, patch, driver/project,
  selection, preflight, codegen, all timings/allocations, scripts and exits.
- `correctness-source.zip`, SHA256
  `C21090D1550E38C4A8C8231C7FEB5C6332F16A86FE07CB141AFFD177A37D7E3C`:
  changed source and all focused checks.

Read-only warmed inspection is separately preserved under
`artifacts/experiments/python-search-hit-inspection-20261103/read-only-inspection.zip`,
SHA256 `88325768A787ADEFF830331CB75A5B5F6F11FEF080DEF98325B03097D5B66BBA`.
It contains exact asset hashes, public warm/checksum source and all JIT output,
with no throughput measurement or causal code-size attribution.
