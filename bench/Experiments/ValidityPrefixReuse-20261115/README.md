# Reuse the ASCII prefix in validity-only preparation

Accepted source `b7afb0f4`; actual frozen Core baseline `fb6a9ebe`. Validity-only
checks use the first non-ASCII byte to retain the prefix's validity proof and run
the existing UTF-8 checker only on the suffix. Metadata validation already uses
this approach. Replace the ASCII boolean primitive with a bounded span range
search; matching and lookup/BCL UTF-8 recognition algorithms remain. Five net
source lines; no selector, mode, ownership or new public API.
[Acceptance](acceptance.json), [all observations](cost-summary.json).

Whole-input rejection, byte error offsets, returned ASCII classification and
independent pattern/output checks remain. Invalid suffix errors add the skipped
prefix length; the scalar decoder remains the first-error authority. Unchanged
invocation entry/<=2KiB block/completion checks preserve original work admission,
cancellation and deadlines. AssumeValid still omits subject validity checks.
The raw runner's generic algorithm-unchanged flag does not describe the changed
ASCII primitive/preparation range; acceptance records those changes explicitly.

ONE **78.854s** ordinary-process ABBA, ten public Count workloads, five
150ms-target samples per process, 20ms floor, 150s cap, no confirmation. Default
.NET 11.0.0 tiers/PGO, normal eight-processor startup before fixed allowed CPU2.
Identical valid unchanged bytes/options/complete counts; Validate unless explicitly
trusted, finite rows use live original tokens. One producer per process; actual
DLL/PDB/compiler input checksums and managed-member inventory verified. No
CPython/native parity comparison or canonical table update.

| Public Count | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| Actual email | 891.106 | 908.049 | +1.90% | overlapping, baseline movement |
| Finite/live email | 1189.161 | 1221.124 | +2.69% | separated |
| ASCII-heavy Unicode | 14.096 | 14.168 | +0.51% | overlapping |
| Finite/live ASCII-heavy Unicode | 14.838 | 12.255 | -17.41% | separated |
| Late Unicode | 9.702 | 7.032 | -27.52% | separated |
| Finite/live late Unicode | 4.303 | 5.244 | +21.85% | separated |
| Chinese literal | 69.239 | 69.924 | +0.99% | overlapping |
| Whole-ASCII contrast | 388.137 | 384.033 | -1.06% | overlapping |
| Unchanged trusted email | 271.455 | 268.173 | -1.21% | overlapping |
| Unchanged predecoded .NET email | 447.732 | 445.492 | -0.50% | overlapping |

Accept the -27.52% late-Unicode and -17.41% finite mixed gains while deliberately
retaining finite late-Unicode +0.940us/+21.85% and finite email +31.963us/+2.69%.
Local costs for larger relative gains follow the authorized policy; the completed
checked-access campaign's 10% gate is not a universal delivery gate. Ordinary
email's baseline process medians are 1122.55/886.90us; its pooled observation
establishes no stable regression/gain. No normalization or confirmation campaign.
Allocations remain zero or 112B for finite calls. Whole-ASCII uses the changed
classifier and is a contrast, not an unchanged control.

Public inputs include the actual 6.8MB/92-match email corpus, 261,504-byte mixed
ASCII/Unicode, 131,083-byte late Unicode, Chinese and whole ASCII. Required strict
validation remains part of the timed operation. Prefix removal saves different
work under whole-input and finite block admission; do not attribute all changes
to one isolated kernel or claim every input improves.

Separate FullOpts preflight emits a 194B standalone after helper; there is no
standalone before listing. This is not a total code-size comparison or evidence
that inlining explains the costs. No additional codegen/tuning variants follow.

Focused Release passes on Windows net10/net11 and Linux net11, each Core1007,
PythonRe892, PCRE2 396, POSIX36. Existing valid/malformed prefix cases now also
check the returned ASCII flag. Independent all-byte-pair/vector-edge, scalar
width/offset/tail, strict suffix, original-token/deadline and resource tests remain.
Forced-no-intrinsics public preflight and 106 validation cases pass, including
absolute errors. Build only Core and the changed Core test host; reuse adapters
and other matching hosts. No validation-policy benchmark matrix.

No package/Full refresh, publication/adoption or staged/published byte changes.
Qualified dev95 remains immutable. Raw/baseline/Windows hashes are in acceptance
and artifacts/experiments/validity-prefix-reuse-20261115/; verified offVM before
keeper release. Original worker/collector terminal0. This bounded trial is closed;
no new decoder, density/fusion/width, polling or alternate-classifier campaign.
