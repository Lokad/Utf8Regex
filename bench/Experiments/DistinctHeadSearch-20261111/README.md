# Share direct distinct-head literal matching

Accepted for the governed-path gain and shared production loop, with the
uppercase/lowercase costs below. SingleLiteralBuckets proves one literal per first
byte. Both IndexOf and fused position/length search now use its direct comparison;
the old position-only implementation is replaced. Generic multi-literal buckets,
literal identity resolution and backward search stay as before. No strategy,
rank/gate, field, allocation, resource charge or public contract changes.

One 105.854-second .NET 11 quiet-VM ordinary-process ABBA covers 12 public lanes
under Validate/AssumeValid: 480 samples >=73.702 ms, 75 ms calibration target with
one calibration confirmation, 140-second outer cap. Distinct Core DLL/PDB pairs;
identical PythonRe/Pcre2/caller assets and unchanged counter eligibility. Eight
processors visible at startup, then CPU2 affinity. At least 1 GiB actual available
RAM for one bounded worker; checksums/results are consumed after timing.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| compiled name-family Count | -14.37%, overlap | -1.96%, disjoint |
| uppercase corpus/pattern | -8.15%, overlap | +2.07%, disjoint |
| lowercase corpus/pattern | -7.86%, overlap | +2.04%, disjoint |
| finite/live name Count | -18.14%, overlap | +8.13%, overlap |
| funded/live name Count | -20.49%, overlap | +0.63%, overlap |
| five-literal corpus | -9.81%, overlap | +2.19%, overlap |
| dense four-name hits | -17.23%, overlap | -0.31%, overlap |
| rejected prefixes | -1.94%, disjoint | -3.19%, disjoint |

Python Count +0.37% strict is disjoint, -7.46% trusted overlaps; Pcre2 -0.01%
strict overlaps and -1.69% trusted is disjoint. Unchanged .NET control +1.15/+0.17%
and Core literal -4.99/-1.55% overlap. Keep those adverse/uncertain observations;
the initial large default gains are not established public speedups.

One bounded 39.975-second four-lane confirmation reuses the exact producers and
a shared filtered caller: 160 samples >=51.665 ms, 50-second cap.

| Public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| compiled name Count |3832.957 ->3876.915 (+1.15%)|3255.316 ->3247.267 (-0.25%)|
| uppercase Count |43356.975 ->43583.375 (+0.52%)|42278.200 ->43126.850 (+2.01%)|
| finite/live name Count |3681.055 ->3239.137 (-12.01%)|3089.854 ->3115.093 (+0.82%)|
| unchanged predecoded .NET |3102.210 ->3080.721 (-0.69%)|3027.246 ->3093.006 (+2.17%)|

Only strict finite/live improvement and trusted uppercase cost are disjoint in
the confirmation. Accept the ~442 us governed-operation saving and consolidated
loop under the authorized local-cost trade-off; record the ~2% uppercase/lowercase
Count cost. No general ordinary-name speedup or fresh native-parity ratio is
claimed. Allocations stay 0 ordinary /112 finite-live; funded parent 17 is held
and final loan balance is zero. All source fields/preparation are unchanged.
No third run or source tuning follows this trial.

Windows/Linux focused Release pass per .NET 10/11: Core 721, PythonRe 162,
Pcre2 89. Independent seeded raw-byte matching, public Count/IsMatch/Match/
enumeration, rejected prefixes/partial tails/UTF-8 byte ranges, construction/
execution admission, original cancellation and cursor controls are covered.
The new public test initially used IDisposable on Core's ordinary facade; that
fixture compile error was corrected before timing. No package/catalog matrix or
mutation of immutable dev.67 artifacts.

Evidence copied off VM/hash verified in
`artifacts/experiments/distinct-head-search-20261111/`:

- `evidence.tar.gz`: SHA256 22A5DD522960F1DEC924B4977696F4C9983ADE13DB01E8CF618C2A4B961FDDD0.
- `confirmation.tar.gz`: SHA256 FB9703BE95B0488C6DD2368B1623B67B2FC421F3BCCDB4156CF1430498FD8225.
- `linux-checks.tar.gz`: SHA256 4299B481108FD7F50923DC9792F254A6AA636E1DB73543BCCBE9FBF5C59EA65F.

Full summaries retain all medians/ranges, caller/input/producer identities and
allocation samples. Baseline a2a60a44 plus the archived one-file Core patch.
The [CPU attribution](../CoreNameCurrent-20261111/README.md) predates this change;
its shares must not be presented as a new after-change profile.
