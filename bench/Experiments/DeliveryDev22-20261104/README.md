# Dev.22 delivery boundary

Common **0.5.0-dev.22** freezes the accepted
[Python boundary-interior proof](../PythonBoundaryInterior-20261104/README.md) and
[Core literal-family replacement](../LiteralFamilyReplacement-20261104/README.md).
All three packages target .NET 10/11 with exact adapter-to-Core pins. Packages are
staged, **not published**; earlier staged/published artifacts remain immutable.
Frozen source is `60491532fe8af8094887a3243f1b076d89bd8d0c`.
Freeze CI `41771019190` is green on Windows and Linux.

Local regressions are acceptable for relatively major gains. The completed
checked-access campaign's 10% allowance is not a universal release ceiling.
Historical fixed-original observations and rejected experiments stay unchanged.

| Accepted change | Public comparison and costs |
| --- | --- |
| Python boundary-interior proof | Split about38%, Count47% and sparse Split81% faster, unchanged allocations. All-space miss +5.55% (~7.73us), empty +6.73% (~0.028us). |
| Core replacement cursor reuse | Actual ordinary/compiled/native replacements about56% faster, unchanged allocations. Finite/deadline paths about+1.8/+1.9%. No-hit median +49.72% (~6.85us) has large spreads and remains an unresolved local cost. |

These are separate bounded comparisons with unchanged controls. Their individual
receipts retain raw samples, allocations and uncertainty; neither later catalog
movement nor independent trials establish a cumulative causal percentage. No new
runner, state, API or mode was introduced. Strict whole-subject UTF-8, scalars,
captures, retained aliases/results, original interruption/deadlines and useful
resource limits remain. Controlled/declined replacement paths retain their authority.

Windows package qualification passed in **263.426376s**, using two library-root
builds and one pack/profile. Eight detached fixtures pass **108,064 checks on each
platform**. Six target assets and464 original compiler documents, including
generated inputs, retain source/PDB/DLL/feed/cache/lock evidence. Linux consumes
exactly those Windows-produced bytes without repacking; six assets/24 restores
pass. The Full separately builds the frozen Linux source graph; its timings do
not claim exact Windows-package identity.

Original copied Awk19/Lython118 checks per target, public native/funded probes and
zero final root loans pass on both platforms. The original1,565-file audit changes
only3 exact pins. Compatibility is distinct from adoption or POSIX acceptance.

The sole [Full refresh](full-refresh.json) passed in **2568.1743125s (42m48.17s)**:
Full/Complete/terminal0, exactly83 expected names/order, all Passed, below3600s.
Its outer timeout covers one Release build, both-runtime correctness (**108,104
source checks**), disabled-dynamic checks, complete .NET11 catalogs/paired
comparators, lifecycle/scaling, resources and final verifiers. No restart.

All seven generated files were hash-imported; only the root introduction's
duration/receipt link was then edited. Matching-binary snapshot guards pass3/1/1
checks per runtime, six hosts/ten checks without rebuilding. Four generated blocks,
six descending tables and178 ratio rows were checked.

[Current rankings](ratio-summary.json): Core115 wins/2 tie candidates/55 gaps/
6 unqualified; Python37 ManagedFaster/37 CpythonFaster/1 equivalent/11 inconclusive/
4 unqualified; native PCRE2 74 ManagedFaster/1 equivalent/13 NativeFaster/
13 inconclusive/25 excluded. Core's unqualified rows cover Twain, River window
and symbol Count in both modes: minimum samples15.56/19.48/17.67ms are below20ms.
Python's ASCII-option, multiline, offset-literal and Unicode-category cases have
removable comparator harness floors above10%. Keep these observations unqualified;
no additional refresh was run to replace them.

Chinese Count remains Core's largest gap,7.26/6.64/5.70x; imports3.56x and
email/Holmes about3.38/3.34x. Compiled replacement is1.55x (ordinary0.36x), about
28.3us/13648B. Python early literal remains5.40x with required complete validation;
Unicode Split3.58x, reluctant Search3.40x, nullable FindAll3.12x/replacement2.93x
remain gaps. Boundary Split is2.133x [2.127,2.146],143.05us/40016B; boundary Count
is0.712x [0.711,0.714],797.22us/0B. These Full observations retain their identity
separately from earlier selective measurements. PCRE2 subroutine/date hit/miss
remain4.40/4.30/4.15x. Closed Chinese-fusion and stopped Unicode-class pilots stay closed.

Resource costs are scoped to each profile's same public operation/policy. The
reporter's IsMatch uses Core.IsMatch, Python.Search(...).Success (a value result)
and PCRE2.IsMatch. Core/Python finite lanes cap work/depth/working/result; PCRE2's
lane sets MatchLimit. Captured9B unlimited -> finite/live medians are Core
1.077 ->1.094us, Python0.957 ->0.974us and PCRE2 1.128 ->1.082us, with5744 ->5856B.
These observations do not establish universal overhead, identical Boolean costs
or total-heap bounds. Raw evidence retains all operations, policies and samples.

Completed fixtures and recent duplicate graph/archive copies were hash-verified
off-VM and compared before retirement. An initial RAM preflight stopped before
launching; the sole admitted Full began with8195MiB available. No build-server
shutdown, unrelated kill, protected cleanup retry or new qualification framework.
Broader PCRE2 completion/projection and post-performance-release POSIX remain separate.

Immutable evidence under artifacts/experiments/delivery-dev22-20261104/:

- windows-evidence.zip: `5F2DD4CEDF15455E9D545E5D702A045A79631E5BABCC82938FB0E1A801284BF6`.
- windows-restore-inputs.zip: `39D9D7FCCABB7A7EAF01358788443EC6EAA2CBD550C96DF75AD773730A268A5B`.
- linux-evidence.tar.gz: `AD7CF9EC432E319F978525039FD8897CCB55872D3E4329BF36C6CC5EB20542A4`.
- full-evidence.tar.gz: `3D178176337ADDCBA0655A58A9C8F328383CDE06BD33011103C51164B2F9AE56`.
- post-refresh-guards.zip: `371506A7B1C718A96D27558F021B08001442CFC482055C15D18A7BEE339B4FBA`.
