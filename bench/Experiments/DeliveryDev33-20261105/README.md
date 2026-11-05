# Common 0.5.0-dev.33 delivery checkpoint

Frozen source `6cf844681cb6c75da3064cf43e65e8b296f056fa`; all three staged packages
target .NET 10/11 with exact adapter-to-Core pins. Packages and symbols remain in
artifacts/nuget. Publication and downstream adoption are separate.

This batch executes assertion-free scalar roots directly through the existing
segment runner. Assertion, capture-history and terminal-repeat paths retain their
existing driver. No execution mode, accounting framework or unchecked access was
added. Complete-subject strict UTF-8/scalars, captures, RTL/NonBacktracking, retained
aliases, synchronous cancellation/deadlines and useful resource admission remain.

The [isolated public comparison](../RootSegmentDispatch-20261105/README.md) records
23.08% lower actual Unicode Split cost, 16.32% with finite/live limits, 16.61% with
a deadline and 10.44% for captured Unicode FindAll, with unchanged allocations.
One bounded separate-process comparison used an unchanged control; no tuning or
confirmation followed. Local costs remain documented trade-offs for substantial
gains. The completed checked-access allowance is not a universal release gate.

Windows package qualification took 428.320706s. Eight detached fixtures/109118
checks and all six assets passed. Linux consumed the exact Windows bytes, without
repacking: eight fixtures/109118 checks, six asset and 24 restore audits passed.
Original Awk 19/Lython 118 checks pass on each platform/runtime; public native and
funded probes finish with zero root loans. The 1565 copied source files differ
only at three exact package pins; 464 Windows compiler documents/generated inputs
and 12 original restore audits match. Six staged package/symbol hashes are unchanged.
Source CI42021623848 passes Windows/Linux.

One **2617.5185568s = 43m37.52s Full** passed under the original 3600s outer cap.
The [manifest](full-refresh.json) records Full/Complete/terminal zero and all 83
expected steps in order. One Release build, eight source fixtures/109158 checks
across both targets, disabled dynamic code on both, all .NET 11 catalogs, paired
comparators, lifecycle/scaling, resources and verifiers passed. Normal eight-CPU
startup precedes CPU2 affinity; pinned .NET 11 RC3 and official CPython 3.13.2.
Readiness was zero seconds with 9480860 KiB available under unchanged RAM/disk guards.

Raw Full evidence was verified off VM before seven generated files were imported.
Four generated blocks, six descending tables and 178 displayed ratios verify.
Only the root introduction's duration/receipt link changed afterward. The same
eight measured DLL/PDB hashes survive Linux package checks and snapshot guards:
six hosts/ten checks pass with SkipBuild. No repeat Full or package qualification.

[Fresh rankings](ratio-summary.json) are catalog observations, separate from the
isolated causal comparison. Core has 116 wins/two tie candidates/54 gaps/six
unqualified; Python has 38 managed/36 CPython wins/three equivalent/nine inconclusive/
four unqualified; PCRE2 has 75 managed/13 native wins/13 inconclusive/25 excluded.
Unicode Split is 123.18us/2.758x and captured FindAll 452.79us/2.107x. Chinese
literal 4.69x, compiled alternation 3.98x and PCRE2 date 4.32x remain gaps.
Historical exclusions and uncertainty remain in earlier receipts.

**Open operating-cost issue:** AWS quick Count moved from about 5.09ms in the
previous Full to 9.76ms ordinary/9.70ms compiled, with unchanged 1936B allocation.
The compiled ratio moved from 1.377x to 2.681x while BCL medians remained similar.
Calibration and sample costs differ, and ordinary sample spread is substantial.
This is not proof of a source cause or a Root dispatch regression. Inspect the
actual route and raw warmup/timing evidence before one bounded causal experiment;
do not hide the movement with a catalog rerun. Other remaining gaps stay in PLAN.md.

Immutable artifacts/experiments/delivery-dev33-20261105:

- windows-evidence.zip: `6DE09F5385AE37E468740CCEC42CDE162B6A3E09A8D2FCD1FD2573AFC5885E28`.
- windows-restore-inputs.zip: `43955BE6D5E1F62B814DE2F8872413299B87DAB0625C142E3ED5701C3A8A4937`.
- windows-compilation-inputs.zip: `CCBFCA42515AD72AF96FC9CADA40F02F4A8A6E15F68F886294EBBE5168E4EDB8`.
- linux-evidence.tar.gz: `4EE32133313299B6276B87DCC14A8C061B6F9CF8DADAE2970844D7EA03E26E95`.
- full-evidence.tar.gz: `D8EA9BD387BB19D743BB6287A3857BC1AA347DA2D3B8AF828CCABB1C02F4F866`.
- post-refresh-guards.zip: `0A1FFF5A7A7395B4240FD072F7CA6A19D3A992AD8135102AD6352BF39666732B`.

All prior package/symbol/raw bytes remain immutable. This checkpoint does not
authorize NuGet publication or complete the broader performance objective.
