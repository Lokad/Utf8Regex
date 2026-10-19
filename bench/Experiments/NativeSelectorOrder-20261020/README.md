# Skip irrelevant ASCII scans in the owned-program selector

Parent89135899,113-dev. Actual immutable112 Core is the baseline.64-case public
Detailed preflight and private selector observations agree before/after on both
Windows and Linux. Release code shows a real full-input scan, not an optimized-away
source operation: Windows selector599->581B, Linux583->563B. Literal-kind/boundary
metadata now precedes the same ASCII predicate, skipping it when irrelevant.

Only one conjunction is reordered. Matching/validation/search/SIMD, all scalar /
RTL/NB/capture/Kelvin fences, allocation, resource admission/numeric work and
original token/deadline remain unchanged. No helper/state/matcher/classifier,
skip-validation mode, unchecked access or public contract is added. The full
objective stays open; native Detailed's wider validation reuse remains separate.

Affected Release Core6326, Python148 and PCRE84 pass, zero failures/skips. Existing
public/strict-validation/capture/control tests cover the unchanged truth table;
no implementation-order/existence test is added. All tested Core binaries match
the measured candidate.112 and earlier packages/snapshots remain immutable;
no complete package/platform matrix is repeated for this bounded source edit.

## One public pair and its sole confirmation

Quiet VM Xeon8573C CPU2/.NET10.0.12, FullOpts/R2R-off. Same fixed driver and
Core-DLL-only swap. Public Detailed/Value/Count, ordinary/compiled,32B plus4KiB /
128KiB inputs; finite Detailed bypasses the changed fence, and .NET decode /
predecoded lanes are unchanged controls. A tiny nonliteral structural neighbor
also enters the shared selector and is retained as a neighboring-case control,
not asserted to have an unchanged instruction path.120ms per-lane warmup,80ms
calibration, five alternating sample orders;60s child/140s batch caps.

The initial pair has tiny Detailed+12.27%/+10.85%, so the sole predeclared
candidate-first confirmation uses identical source/driver/runtime/protocol.
It does not reproduce that tiny regression: ordinary/compiled-0.83%/-0.84%.
Keep both observations; no fixed tiny-case gain/equivalence guarantee is claimed.
All480 samples across four phases exceed72.47ms; decoded .NET rows can be noisy
(confirmation maxspread1.441x). No additional confirmation, tuning or rescue.

Confirmation public medians, ordinary/compiled:

| Operation | Before -> after | Allocation |
| --- | --- | --- |
| Detailed32B |278.37->276.05 /279.28->276.92ns|0B|
| Value32B |172.98->178.20 /171.64->176.98ns|0B|
| Count32B |139.36->110.80 /138.77->110.84ns|0B|
| Detailed4KiB |849.80->598.50 /845.30->580.67ns|0B|
| Detailed128KiB hit |22.145->13.528 /22.368->13.641us|0B|
| Detailed128KiB miss |19.750->11.140 /19.863->11.101us|0B|
| Value128KiB |13.332->9.094 /13.269->9.039us|0B|
| Count128KiB |14.062->9.821 /14.149->9.806us|0B|
| Finite Detailed control |4.380->4.320 /4.385->4.365us|304B|
| Nonliteral tiny Detailed neighbor |425.92->444.36 /390.47->404.86ns|0B|

Large benefits repeat; finite controls within1.4%, .NET confirmation controls
within1.7%, tiny structural neighbors+3.7..4.3%. No basic metering is removed.
Initial tiny Detailed includes occasional fractional allocation noise; all such
confirmation samples are0B, and the source changes no acquisition. Accept the
proved removal of irrelevant work with these scoped results, not a universal
overhead/whole-engine/default-PGO/immutable112/Status claim. No more timing.

## Exact inputs and reproduction

BaselineCore63FC0831103780EB12057D293FA5C53ABD0B25DB98EFCC622F1FD1F4D2CD9FA2.
CandidateCore91993F5EF4BFFFEEC322B5C91FEEB8262EF79E433663FC07061BC9E11D44DF07.
DriverB9E9F299558B4992F661A7D5214BBC970E00C3B8803FD1B383CC80C5380AA251.
[Every raw sample and both summaries](measurement.json). Downloaded archives,
SHA256 verified:
D8D63DFD89A05FEF598DF9C06BF25B7F82C880F8A1AE52895DA4F6046C88658E /
E172CA3052654D478CF43B4E3BEBCAD290830FAB490910911A0A4CB4D1C9EAF8.
Both quiet gates CPU2=0%, other observed dotnet tick deltas0. All processes exit0.
Full private codegen/source/test receipts:artifacts/native-selector-order-20261020/.

Standalone source uses only the selected Core DLL; no library rebuild happens
when building the driver. Set CoreLibraryPath to an absolute DLL path, then run
the built driver with `--preflight <new.jsonl>` for untimed evidence or one new
output JSONL path for measurement. FullOpts experiments used
DOTNET_TieredCompilation=0 and DOTNET_ReadyToRun=0. Driver output uses CreateNew
to preserve receipts. These files are a frozen experiment, not a new qualification
framework or a reason to reopen the closed comparison.
