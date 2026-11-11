# One-shot Python string sizing

Accepted with the strict large-astral cost below. Scalar SearchToString,
MatchToString and FullMatchToString size their returned range through the existing
facade decoder/result ledger. Native one-shot output shares the final decoder and
supplies its known character count. Batch GetText, shared VM/cursor state and
Core are unchanged. No flag, field, cache, public API or executor was added.
String admission precedes allocation; its reservation stays live through final
publication. Original interruption and resource/deadline failure mapping remain.

One 86.672-second quiet-VM .NET 11 ordinary-process ABBA covered ten public lanes
under Validate/AssumeValid: 400 samples >=33.958 ms, 75 ms calibration target,
one calibration confirmation, 120-second outer cap. Explicit distinct PythonRe
DLL/PDBs; identical caller/Core/Pcre2 assets. Eight processors visible at startup,
then CPU2 affinity. One bounded worker required 1 GiB actual available RAM;
results/checksums were consumed outside timing.

| Public MatchToString operation | AssumeValid before -> after us |
|---|---:|
| small result, 1 MiB ASCII suffix | 10.729 ->0.317 (-97.04%) |
| small result, 1 MiB BMP suffix | 205.058 ->0.274 (-99.87%) |
| small result, 1 MiB astral suffix | 1078.459 ->0.274 (-99.97%) |
| small result, finite/live ASCII | 14.182 ->0.438 (-96.91%) |

These ranges are disjoint, with unchanged allocation: 104 bytes ordinary and
216 finite/live. Under 128 work steps the trusted early operation changes from
refusal to success; default strict validation still refuses this budget. Public
tests also cover nonzero byte offsets and Unicode returned text. These compare
the same input policy before/after, not UTF-8/native parity or different contracts.

Dense trusted many-string +0.08%, one-string +1.23%, UTF-8 control +0.87% and
finite/live many-string +0.30% all overlap. Strict counterparts +5.09/+2.60/+3.07/
+4.19% overlap, as does unchanged Core control +4.48/-1.74%. Strict early-text
movements +3.84/+11.87/+0.70/+4.01% overlap. Tiny native Python FullMatchToString
+26.46/-22.34% (about +24/-25 ns) overlaps. Keep all observations; no normalization
or blanket throughput claim. Main-run allocations and 368-byte state size stay
unchanged. The worker's 17-byte parent hold/refund does not establish an external
funded one-shot peak; focused tests separately verify program-funded refunds.

One 39.042-second coverage supplement used the same frozen producer pairs and a
shared four-lane caller: 160 samples >=62.767 ms, 50-second cap. Scalar FullMatch
returns complete 64 KiB ASCII/BMP/astral subjects, plus an unchanged Core control.
Strict ASCII -1.39% and BMP +11.82% overlap, but strict astral
307.650 ->378.955 us (**+23.18%, +71.305 us**) is disjoint. Trusted full-output
+8.87/-1.13/-1.33% overlaps. Core control +0.51% overlaps strict and -11.30% is
disjoint trusted. Nominal output allocations stay the same; ASCII readings retain
small fractional 0–6-byte per-operation movements in the raw data.

The strict astral cost comes with an avoidable selected-range count when strict
validation already knows the complete character length. It is accepted here for
the substantial trusted early-output savings, under the authorized local-cost
trade-off. **Next bounded follow-up: forward existing description/count facts to
the scalar one-shot caller, without deriving new metadata, fields or modes.**
Batch fact reuse remains mandatory. No third timing or tuning in this trial.

Windows full PythonRe 5,095 checks per target pass; final 17 subject-fact checks
cover four subsequently added large-range/funded cases. Linux full 5,099 checks
per .NET 10/11 pass. Coverage includes strict malformed tails, valid policies,
scalar/block boundaries, exact result admission, original cancellation/deadlines,
retained strings and refund to the held program balance/17-byte parent hold.
The specification records the scoped sizing/work change. No package/catalog
refresh or changes to immutable dev.67 artifacts.

Evidence copied off VM and hash verified in
`artifacts/experiments/one-shot-text-20261111/`:

- `evidence.tar.gz`: SHA256 64396DC99419607BED5F5B1736D052D729EBA36098B3D2768C08DE9C614AEA3B.
- `coverage.tar.gz`: SHA256 4B01AE26FC19C31356689E1C59230F8AEF677E2B8A1DFA1C4D4EA5593F8A9066.
- `linux-checks.tar.gz`: SHA256 7C03AD7AECFF4299E9E3D9937A1AD68ACBA8BE50087FD90CBFA94403305F875A.

Summaries preserve process medians, ranges, identities and allocations. Baseline:
56de0556ff81dffb25d2d6d3b49ff126ce35c9f6 plus the archived two-file producer patch
for the candidate. The rejected broad per-range recipe remains absent.
