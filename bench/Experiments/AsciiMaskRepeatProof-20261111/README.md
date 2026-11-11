# Close the two-mask repeat-proof trial

**Rejected; production and specification restored to `64424a77`.** The17-line
prototype replaced per-member proof work for an unfolded Set and positive ASCII
Set follower with two checked64-bit intersections. It added no retained state,
executor or public policy. Independent public mask/capture/cutoff tests remain.

The primary public coverage was mistaken. `([a-z]+)([0-9]+)` opens its second
capture between the repeats; the existing proof skips only capture endings.
It therefore never reaches the changed intersection branch. An untimed loaded-
pair audit confirms the emitted CaptureEnd/CaptureStart sequence and identical
minimum public FindAllToStrings work1223 for32 tuples before/after. The eligible
`(([a-z]+)[0-9]+)` changes1104→848 work, establishing the branch exists, without
establishing a measured public benefit worth promoting it. No second timing or
mask-tuning variant follows this trial.

One **128.332s** quiet .NET11 RC3 VM ordinary-process ABBA per input policy:
14 public rows,560 samples, minimum67.562ms,75ms calibration target,150s cap.
Common adapters/caller, distinct explicit Core DLL/PDBs, eight startup CPUs then
CPU2. All staged/loaded hashes, input identities, results and checksums verify
independently. Observed maximum sample allocation106.035MiB; actual available RAM
and capacity are recorded. Legacy descriptor hash fields were corrected to match
the already-correct archived patch map; no producer/caller/timing flag changed.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| Adjacent-class strings |+0.47%|+1.45%|
| Adjacent-class UTF8 output |+0.68%|+1.13%|
| Adjacent-class Count |+1.66%|+0.98%|
| Strings, finite/live |+0.83%|+1.68%|
| Complete funded range cursor |−2.70%|−2.86%|
| Strings, deadline/live |−0.35%|+1.79%|
| Long detailed match |−0.21%|−1.77%|
| Complement/Unicode strings |−0.50%|+3.16%|
| Original hyphenated strings control |−3.59%|−4.93%|

All ranges overlap. Preserve adverse observations without claiming a gain or
causal regression. Allocations are unchanged in every row, including243064B
adjacent strings,247264B finite,231304B cursor,247312B deadline and429288B long
detailed output. Funded peak1537B includes parent17; outstanding loans return0.
Fold/overlap/one-capture/Core/.NET controls and per-process ranges remain raw.
No new catalog Status, native/CPython ratio, package or Full refresh.

Candidate Release Windows/Linux pass per.NET10/11: Core250, fullPython5119,
Pcre591; Linux noHW adds149 Core checks per target. A reused fixture initially
assumed the original fork's workspace-free public route applied to new captured-
class cases; preserve that old assertion and check real working refusal for the
new cases. A test-edit encoding error was reversed using explicit UTF8. These
repairs preceded timing and changed no production semantics.

Canonical verified offVM evidence under
artifacts/experiments/ascii-mask-repeat-proof-20261111:

- `evidence.tar.gz`: `62EC852180CAD27762EA76A2E5BEC752AB3D2794F569CE820635378752126075`.
- `windows-evidence.zip`: `E3D055083936B43CA2AF9393487563F2E2DAC51138259AC96DC16464D9A3BAB6`.

Summaries and the independent untimed shape/work audit accompany those bytes.
Runner20195 completed successfully. Restore checks and exact baseline pair
comparison close this trial before further implementation. Published/staged
packages and previous evidence remain immutable. The capture-opening boundary
identified here is a separate structural investigation, not a mask speedup claim.

Restoration COMPLETE: Windows/Linux Core250/Python86 checks pass per target;
Linux Core DLL/PDB exactly match the frozen baseline. Restore worker60314
terminated0. Canonical `restored-linux.tar.gz` SHA256
`5CC9782D8F059AD7273F8AD6BC065F03CFBE87FFB228AC9A4C0EB4BDAE5037BF`;
`restored-windows.zip` SHA256 `C357BFF6F784C12568601CDA35B0EB627442563AA2F245B1A155C28C107AC7F9`.
