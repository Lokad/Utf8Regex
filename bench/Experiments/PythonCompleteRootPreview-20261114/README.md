# Complete capture roots omit the delimiter preview

Accepted source `45e686327dc1b43fa0f0faecbf5819f55bb7578d`, frozen dev93 baseline.
Python's existing first-candidate gate predates the complete no-rewind capture-root
proof. Such roots now reuse their metered verifier directly for that candidate,
omitting the redundant64-byte delimiter preview and long-prefix window rewind.
Failure resumes existing window discovery. Unproved roots retain the preview.
No new root shapes, flags, executor, buffer, owner, unchecked access or API.
Captures/closing order, scalar endpoints, original cancellation/deadlines and
admission remain. Actual route-dependent work can fall. [All observations](cost-summary.json).

The initial150ms-target ABBA stopped on a long-run sample below20ms; its completed
baseline process and failed after log are preserved, not qualified. One confirmation
uses the SAME producers and a new common caller with100ms calibration/300ms target.
An initial confirmation setup assertion compared the current candidate with baseline
hashes and failed before building/timing; only the private assertion was corrected.
No producer rebuild or extra timing comparison followed that setup failure.

The completed confirmation takes **117.393s** under240s: ten public rows, four
ordinary processes in ABBA order, five samples each. Normal8CPU startup before
existing placement, .NET11.0.0/default tiering/PGO. Every returned string/tuple
is checked after timing; no validation-policy matrix or native parity claim.
Frozen distinct Python DLL/PDBs, identical Core/caller/.NET controls and every
compiler-document checksum verify. No package/Full refresh or further confirmation.

| Complete public operation | Before us | After us | Observed change |
|---|---:|---:|---:|
| Long65KiB first capture |477.494|21.436|-95.51%, separated ranges|
| Many-capture strings |441.950|441.087|-0.20%, overlapping|
| Funded/deadline many strings |1131.511|1126.272|-0.46%, overlapping|
| Unicode failed candidates |65.490|66.482|+1.51%, overlapping|
| Repeated failed candidates |130.923|131.757|+0.64%, overlapping|
| Excluded lazy root |186.001|188.264|+1.22%, overlapping|
| Missing delimiter |1.408|1.603|+13.83%, overlapping|
| Unchanged Core / .NET controls |12.234 /22.861|12.231 /22.823|-0.03% /-0.17%, overlapping|

Ordinary capture parity remains unresolved. One-capture -7.71% also overlaps;
its route is unchanged and this is not a causal speedup claim. Missing-delimiter
before medians1.015/1.545us and after1.596/1.825us show process uncertainty; retain
the adverse observations without normalization or another tuning campaign.
Nine rows retain identical allocation medians. The long-output batch reports
131615.934→131493.245B/op with fractional/process-dependent observations; requested
string payloads and allocation algorithms are unchanged, with no fixed allocation
reduction claimed. Do not multiply this gain into canonical dev93 ratios.

Windows.NET10/11 and Linux.NET11 each pass the complete Python checks. Added
public cases cover63/64/65,2047/2048/2049 and65536-byte first runs, failed prefixes,
actual supplementary text, original byte offsets, missing delimiters and retained
cursor results. Existing tests cover finite work/depth/working/result refusal,
original token/deadline interruption, capture order, aliases and supported options.
Only affected Python adapter/tests built; unchanged Core images verify.

Raw is verified offVM before keeper release; every worker/collector is terminal.
Canonical artifacts/experiments/:

- `python-complete-root-preview-measure-20261114/evidence.tar.gz`, SHA256
  `5F4D7622C90AA55F375690BC8643A22DF0AF865B3AB9CE001E9BF70E4DEE01AF`.
- Initial sample-floor failure: `python-complete-root-preview-20261114/evidence.tar.gz`,
  `0EDF156DCD98C2EDD00D477D76CA7665DA8AA08F7A7E613278FA0744971C343E`.
  Collector skipped diagnostic FIFOs when extracting regular files; original
  archive bytes remain unchanged and verified.
- Setup-only assertion failure: `python-complete-root-preview-confirm-20261114/evidence.tar.gz`,
  `07B6FAABE5A865D61D218B6779A86F09FA68B203538B44B1BBCC760E433D4ED0`.

This bounded trial is closed. Preserve immutable dev93 packages/presentation;
continue demonstrated material capture/search/output costs without reopening
unproved-root first-candidate, decoder, journal or unchecked-access campaigns.
