# Unicode literal Count tail bounds

Source fix `13ff4c5f`, predecessor/package `0e24cece`. Public ordinary and compiled
Count threw ArgumentOutOfRangeException after a complete match when a later
partial leading-scalar anchor occurred too close to the subject end. Reproduction:
pattern `夏洛克·福尔摩斯`, subject that full literal +32 ASCII x +`夏洛克`.

The existing anchor IndexOf span now ends at the last complete-needle start plus
the anchor width. This preserves its algorithm, allocations and full-subject
validation while ensuring every candidate is a safe full-needle slice. There is
no policy, cancellation, scalar or coordinate contract change.

Four public regression theories fail before the correction. Focused Release checks
pass on both targets: Core450, PythonRe93 and PCRE2162. Tests cover complete scalar
prefixes, supplementary endings, borrowed-view offsets/canaries, no-match tails,
None/Compiled/NB/RTL and finite/live controls. Original malformed-input and
cancellation guards remain in those focused suites. See the retained test logs.

One VM comparison is closed in **12.567s**, inside its 30s cap, after the full
refresh. Warm1.5s/min1000calls, independent60ms calibration, five alternating
samples; counts consumed after timing, minimum sample67.550ms. No confirmation.

| Public Count | Before us | After us | Change | Allocations |
| --- | ---: | ---: | ---: | ---: |
| Actual Chinese catalog, ordinary | 132.324 | 132.462 | +0.10% | 0 B |
| Actual Chinese catalog, compiled | 170.543 | 162.935 | -4.46% | 0 B |
| Unchanged literal control | 0.648 | 0.618 | -4.64% | 0 B |

Ordinary samples are stable within1.6%; compiled samples share~35% spread, so
the negative median is not a demonstrated compiled speedup/equivalence claim.
No public regression is established. The correctness fix is retained. Corrected
throwing inputs have no comparable successful timing baseline. See
[raw cost](cost-result.json) and [Linux preflight](preflight-linux.json).
CI `41221555011` is green on Windows/Linux/both targets, including disabled
dynamic code. Preserve dev.1 packages; no repack/catalog repeat for this fix.
