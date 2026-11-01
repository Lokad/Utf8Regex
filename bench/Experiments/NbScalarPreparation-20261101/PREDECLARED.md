# Bounded scalar preparation

One <=120s comparison against frozen570c runtime/Core
09D6CB2B17106DF51E687D991EFEA6B2FEB7F8DCC0C569AEB11D089C4E0EA10C.
Current7e production code is identical; it only refreshed data/receipt. Candidate
is7e plus the archived one-file patch. Reuse those exact baseline binaries.

Already validated UTF8 scalar starts are counted in<=64byte blocks using a
checked ASCII-prefix search then checked byte tests. Exact offsets use existing
scalar decoding under<=64scalar admission groups. No new validator/metadata
cache/API/fields/buffers/SIMDkernel, modes or unchecked access. All existing
per-atom direct charges and row checks stay; numeric prep work changes by exactly
2*scalars-ceil(bytes/64)-ceil(scalars/64). Document the version-cost implications.
Array admission, storage/loans, strict full validation, scalar semantics and aliases
remain. Each group checks originaltoken/deadline directly; publication checks stay.

Fourteen public Count/IsMatch cases, quiet VM/.NET11RC3/CPUmask4: canonical LF
region46429B (SHA507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2)
ordinary/compiled/live/finite-live/deadline-live; Unicode/wide/nullable/denseASCII/
dense supplementary/category Count; category single-searchcontrol and unchanged
nonNB/literal Count. Four equal-result lanes including predecoded/decode-then-
.NET. Warm1s/min64, independent60ms calibration, fivealternating samples>=20ms,
complete outputs consumed after timing. Setup excluded, full validation included.
At most one selected noise confirmation, thenstop. Promote usefulprimary gains
with no demonstrated>10% adversecontrol, unchanged results/allocations and all
resource bounds; revert poor tradeoffs, no variantcampaign.

Release tests net10/net11 cover resource/constructor/capture/cursor semantics,
64/65byte/scalar boundaries (including split supplementary/continuation sequences),
and size/offset cancellation/deadline checkpoints with synchronousrefunds.
371 complete before/after probes compare all state/search fields except the
explicit exact preparation-work delta. No package/fullcatalog/unchecked campaign.
