# Reuse owned text for identical capture ranges

Accepted source `4512d763572a51f33f02e54bd2bbcdebee6ca476`, frozen Python baseline
`cdfc889b` / unchanged Core dev93. FindAllToStrings decodes its first tuple capture
once and reuses that owned immutable string when another group has exactly the
same byte range. Independent mutable tuple arrays remain. No map, cache, cross-
match state, new executor, public API or unchecked access. The first range uses
two call-local coordinates; every reused/absent capture retains its interruption
check, and decoder/admission/publication authority remains. Matching, captures,
UTF8/scalar semantics and retained-result lifetimes are unchanged.

Shared text is acquired once within this owned result, reducing actual allocations
and covered result/working/work requirements. Admission still precedes every
acquisition. This is not exact heap/RSS accounting or a promise of stable numerical
thresholds. Independent retained owners keep their separate conservative charges.
[Complete observations and producer identities](cost-summary.json).

ONE **80.531s** public ordinary-process ABBA, ten rows/five150ms-target samples
per process,50ms calibration/20ms sample floor,150s cap. Default.NET11.0.0 tiers/PGO,
normal8CPU startup then existing placement. Complete tuple/string materialization
occurs during timing; all values and shapes are checked afterward. No confirmation,
new CPython/native ratio, canonical table refresh, package or Full qualification.

| Complete public string findall | Before us | After us | Observed change | Allocated bytes before → after |
|---|---:|---:|---:|---:|
| Two identical captures |137.117|112.619|-17.87%, separated|180520 →102696|
| Eight identical captures |181.724|90.150|-50.39%, separated|336216 →63832|
| Eight, finite/live token |250.758|117.769|-53.03%, separated|338344 →65960|
| Eight, funded/deadline/live |404.632|193.784|-52.11%, separated|338416 →66032|
| Unicode identical captures |105.809|90.223|-14.73%, separated|33064 →22824|
| Overlapping distinct ranges |138.056|139.086|+0.75%, overlapping|61736 →61736|
| Ordinary separated captures |382.436|394.361|+3.12%, overlapping|242008 →242008|
| Absent first capture |113.883|113.565|-0.28%, overlapping|49976 →49976|
| Unchanged Core / .NET controls |12.240 /23.153|12.221 /23.272|-0.16% /+0.51%, overlapping|0 →0|

Accept the clear intended-workload time/traffic reductions. Preserve the adverse
ordinary row: before-process medians370.128/395.232us, after395.601/393.121us.
No stable ordinary speedup or regression is established; no normalization or
variant/confirmation campaign follows. The separate dev93 canonical many-capture
CPython deficit remains. The test/caller matches selected overlapping semantics;
its BCL oracle is not an implementation dependency or complete Python oracle.

Windows.NET10/11 and Linux.NET11 each pass the affected Python suite. Existing
tuple cases now include exact Unicode ranges, overlapping distinct captures and
successful empty groups, in addition to absent-first groups. Tests preserve
independent mutable tuple slots and values after regex disposal. A public4096-byte
nested capture fits a12KiB covered result budget. Every observed checkpoint is
subject to original cancellation/deadline injection, including new equal-range
Unicode and absent-first programs, under finite/unlimited policies. Existing
work/depth/working/result denials, scalar boundaries and cursor lifetimes remain.
No identity assertion makes this sharing strategy a public guarantee.

Only the affected adapter/tests and private caller built; frozen Core stays
unchanged. Distinct Python DLL/PDBs, identical Core/caller/.NET controls and all
compiler-document checksums verify. Funded calls restore the held root amount;
disposal leaves zero loans. Published/staged packages remain unchanged.
Raw verified offVM before keeper release:
`artifacts/experiments/python-tuple-text-reuse-20261114/evidence.tar.gz`, SHA256
`199E36CCAB70BF0B9E457FC51C0F721A3B470C63CD71ACE1CE2388E6F07FF89B`.
All worker/collector handles terminal0. This bounded trial is closed.
