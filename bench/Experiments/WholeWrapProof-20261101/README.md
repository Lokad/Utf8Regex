# Whole-wrapper proof order: closed without promotion

Frozen baseline `c6b716ee`; prototype reversed the existing whole-literal and
single whole-wrapper capture proofs in TryGetOwnedDetailedGroupCount. Both
return the same group count, but the literal proof opens construction scratch.
The prototype is completely reverted. No public contract or package changed.

One [public comparison](comparison.json), **89.600 seconds**, ten equal-result
cases/four producer and .NET comparator lanes, .NET11RC3 on the quiet Ubuntu VM,
CPU2. Warm1.5s/min64, independent60ms calibration, five alternating samples with
checksums after timing. [Driver](driver.cs.txt), [summary](summary.json).
Warm grouped operations move +0.03..0.87%; allocations are unchanged.

The original preflight proved native-template/scalar selection, but omitted the
inner capture-map decision. Follow-up [coverage](coverage-preflight.txt) proves
**GroupedMap=true** on every uncontrolled grouped case: these cases bypass the
changed proof. This does **not** qualify affected-path performance or establish
that the helper is free. It establishes no benefit for the concrete warm debt
being investigated. No confirmation or alternate proof variant was attempted.

Prototype Release checks passed on .NET10/11: Core1770 replacement/resource and
9281 capture/detailed/RTL/NonBacktracking cases, PythonRe284 and PCRE21123.
No new tests survive; existing public semantic/resource checks were reused.
Exact binaries, patch, build/test logs and diagnostic FullOpts code generation
are archived separately; the diagnostic frames are not default-tier attribution.
The subsequent direct-enumerator experiment is a distinct change on the original
baseline, not a continuation or qualification of this rejected prototype.

Immutable `artifacts/experiments/wrap-capture-proof-20261101/` archives:

- `evidence.tar.gz`: exact DLL/PDBs, measured driver DLL, patch, raw comparison,
  coverage and diagnostic output, SHA256
  `D47DA165E3D6B457EB8D5EAE9C921B67E85C941F8A320FE6116AAE303830C608`.
- `correctness-source.zip`: measured/reconstructed driver source, patch, tests,
  coverage, diagnostic output and baseline bundle, SHA256
  `D1E115752136DD89F3082626817CA536EF57AAB4FC555ED739C4A385C06DDD6E`.
