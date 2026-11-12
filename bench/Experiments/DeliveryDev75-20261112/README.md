# Immutable dev.75 delivery checkpoint

Core, PythonRe, Pcre2 and Posix **0.5.0-dev.75**, both .NET10/.NET11, exact Core pins.
Frozen source: 25dff59829199a38144216251f964894f57eb4bd. SDK11.0.100/.NET11.0.0 performance baseline.
Packages and symbols: artifacts/nuget/; qualification: artifacts/package-candidates/0.5.0-dev.75/.
No publication, consumer checkout changes or downstream adoption.

## Accepted scope

Windows qualification: 112132 checks/10 detached fixtures in 313.170s.
Linux accepts the SAME Windows package bytes: 112132 checks/10 fixtures; no producer rebuild/repack.
Original copied Awk19/Lython118/public API probes pass per framework/OS; root loans return0.
1565 original files differ only at3 exact producer pins. Feed/cache/lock, DLL/PDB/source and restore/TRX identities verify.
One Full: 2819.261s/85 passed steps under3600s, including one Release build, both-runtime tests,
disabled dynamic code, all .NET11 catalogs, paired comparators, lifecycle/scaling/resource checks and verifiers.
Existing9 snapshot guards pass per framework/OS using matching binaries.452 historical plus8 new canonical assets match.
Source CI43180112035 is green on Windows/Linux. [Acceptance](acceptance.json), [Full receipt](full-refresh.json).

## Current operating costs

The table compares complete Full observations against GA dev.74. It is not a causal A/B, a combination of focused gains,
or a claim of complete parity. Both input policies remain separate contracts; ordinary catalog rows use Validate.

| Profile/case | GA74 managed us / ratio | GA75 managed us / ratio | Current status |
|---|---:|---:|---|
| Core/dotnet-performance-compiled/industry/leipzig-name-family-count | 1597.938 / 0.535 | 1633.419 / 0.534 | Win |
| Core/dotnet-performance-compiled/industry/leipzig-river-window-count | 2158.657 / 1.413 | 2162.694 / 1.405 | Gap |
| Core/dotnet-performance-compiled/industry/leipzig-symbol-count | 1866.729 / 1.500 | 1878.107 / 1.478 | Gap |
| Core/dotnet-performance-compiled/industry/mariomka-email-count | 1170.249 / 2.528 | 1177.581 / 2.603 | Gap |
| Core/dotnet-performance-compiled/industry/mariomka-uri-count | 1443.330 / 1.285 | 1447.715 / 1.278 | Gap |
| Core/dotnet-performance-compiled/literal/sherlock-zh | 81.314 / 3.390 | 82.773 / 3.396 | Gap |
| PythonRe/Count, FindAll, and FindIter/findall/full-strings | 472.119 / 1.284 | 467.849 / 1.266 | CpythonFaster |
| PythonRe/Count, FindAll, and FindIter/findall/many-capture-strings | 774.094 / 2.220 | 770.902 / 2.206 | CpythonFaster |
| PythonRe/Count, FindAll, and FindIter/findall/named-capture-strings | 143.642 / 1.520 | 136.588 / 1.465 | CpythonFaster |
| PythonRe/Count, FindAll, and FindIter/findall/one-capture-strings | 497.491 / 2.332 | 480.411 / 2.242 | CpythonFaster |
| PythonRe/Split/split/captures | 391.416 / 1.536 | 385.099 / 1.508 | CpythonFaster |
| Posix/AdapterControl/capture/sku007 | 2623.434 / 3.811 | 2683.279 / 3.893 | NativeFaster |
| Posix/FiniteDeadlineLive/capture/sku007 | 4837.887 / 1.844 | 4692.300 / 1.749 | Measured |
| Posix/FiniteLive/capture/sku007 | 3768.967 / 1.437 | 3628.790 / 1.352 | Measured |
| Posix/FundedFiniteLive/capture/sku007 | 4229.494 / 1.612 | 4157.526 / 1.549 | Measured |

Observed increases include Pcre URI22217.2->22585.8us, Core ruff4404.6->4580.4us and SKU2623.4->2683.3us;
these separate runs establish current costs, not causal regressions. Acceptance retains changed labels and the20 largest observed increases.
Retain all adverse/inconclusive observations and native result exclusions. Six tiny Python rows remain below the removable-harness-floor criterion;
do not retime them merely for status. Full lifecycle/scaling and finite/token/deadline/funded costs remain in raw evidence.
POSIX native history exclusions mean different results, not performance timeouts. Its SKU batch is4096 public calls.
Resource-section ratios compare governed calls with the plain managed call, not with native throughput.

## Accepted source batch and remaining work

[Shared VM failure construction](../SharedVmFailureExit-20261112/README.md) reduces hot frame/entry work with its explicit cursor/local costs.
[Reachable whole ceilings](../PosixReachableBoundary-20261112/README.md) and
[parent ceilings](../PosixParentReachability-20261112/README.md) enable additional finite-work captures; earlier-shorter costs remain explicit.
The zero-capacity charge guard and literal-successor trials remain rejected/restored. The
[general dominance model](../PosixDominanceModel-20261112/README.md) establishes a conservative rule, not a shipped frontier or speedup.
Arbitrary root-alternative history competition and material Core/Python gaps remain open. Basic metering, scalar semantics, input policy,
original interruption, resource admission and retained lifetimes remain. This checkpoint is not performance completion.

Full raw SHA256: AFE8DED44B3267FC6B88650BE5A04BBF0C62AD8A1E7710EAFAB19D24E478956C. Linux raw SHA256: 16FA7F71ED0A1C5CBC086C0845F48591C37F616A18CE94449DFF5F157D7F0B7F.
Canonical local archives: artifacts/experiments/delivery-dev75-20261112/. All archives verify offVM before keeper release.
No source/producer mutation during timing, extra Full campaign or package qualification after metadata-only edits.

Next: bounded admitted-state/history representation feasibility or a distinct demonstrated Core/Python cause. No tiny trial replay or new framework.
