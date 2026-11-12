# Direct ASCII Set membership: inconclusive, restored

Source baseline `6dc05402dafb111dee77c06d008443478a6f9a4d`. The actual Python
captured-split program is CaptureStart, Set, CaptureEnd, Accept. One 12-line
prototype read its existing mask directly for unfolded forward ASCII bytes,
avoiding the Rune decoder/membership round trip. Other scalars, folding and RTL
retained their routes. Instruction work, admission, owners and allocations were
unchanged. No root specializer, field, helper, mode or unchecked access was added.

**Do not promote.** One 98.468s ordinary-process ABBA covered14 complete public
operations under Validate/AssumeValid: 560 samples, minimum 24.340ms, maximum 209.031MiB
allocated per retained batch, 140s cap. Normal 8-CPU startup then CPU2, .NET 11.0.0,
default tiering/PGO; distinct staged/loaded Core DLL/PDBs, common Python/caller/
.NET controls and complete results verify. Every observed range overlaps.

| Public operation | Validate change | AssumeValid change |
|---|---:|---:|
| Captured split strings | −66.94%, distorted baseline | −2.82% |
| Captured split ranges | −4.06% | −3.26% |
| Finite/live captured split | +5.29% | −2.97% |
| Deadline/live captured split | −0.98% | +3.04% |
| Funded deadline cursor and caller collection | +0.34% | +4.46% |
| Unicode captured split | −7.20% | +3.31% |
| Uncaptured split | +6.24% | +1.06% |
| Negated scalar capture FindAll | −6.11% | +0.85% |
| Folded capture control | −3.46% | −1.80% |
| Failed-branch captures | −4.43% | −2.96% |
| Repeated-capture control | −4.93% | +0.61% |
| Tiny captured search | +6.75% | −5.69% |
| Unchanged Core literal | +0.66% | +0.25% |
| Unchanged predecoded .NET split | −17.63% | −25.43% |

Strict primary before-process medians 3496.811/778.378us versus 696.182/717.332us
after do not support a stable 66.94% gain. Trusted 826.062/804.676 versus 742.167/
842.636us also interleave. Keep the adverse finite/cursor observations and the
large unchanged .NET movements; no normalization, CPython ratio or catalog Status.
String allocations remain 274648B, finite 278848B, deadline 278896B and funded cursor
197320B. Range-result observations vary by small incidental amounts; the raw
summary retains them. Modeled parent loans return to zero, including 17B held by
the caller; this is not an RSS measurement.

The sole five-row confirmation retained the same frozen producers and a new
common caller, longer first warmup, 35ms target and 32MiB batch cap under 50s.
It stopped during the first baseline process because the unchanged .NET split
fell below the 20ms sample floor. No completed paired claim comes from that run.
No third comparison, caller retuning or production variant follows. The entire
prototype and its two trial-only test files are removed.

Windows/Linux focused Release checks per .NET 10/.NET 11 pass: Core 4691, PythonRe 683,
PCRE2 874; software Core 7/PythonRe 2. New independent tests cover all 128 ASCII values,
BMP/astral scalars, complementary masks, RTL, capture offsets/aliases and every
work cutoff. Existing tests cover folding, rollback, NonBacktracking, input
policies, resource admission and original cancellation/deadlines. After restoration,
only Core is rebuilt; existing Core 83/PythonRe 63 checks pass per target/OS. No package/
Full cycle; all 468 canonical package/symbol files remain unchanged.

Setup corrections precede timing: the private caller initially used a value
match's nonexistent Groups property and was changed to the existing detailed API.
The first Linux source-transfer check stopped before builds on CRLF/LF bytes;
normalized source verifies. Windows tested identical C# with its original CRLF
checksum. The copied raw PDB verifier's descriptive dev76/fe baseline label is
stale: actual before hashes identify the accepted exact-pair graph (source 6dc).
Both 342-document source inventories and PE/PDB IDs verify; only the intended
production document/generated revision differ. Original logs and labels remain.

Immutable off-VM evidence under artifacts/experiments/ascii-vm-set-20261112/:

- Focused gate:69F9556838E73D9DE061C5EBE225CC2CEFCA57BD84C2FC48C8E8A4AD54F0C387.
- Public comparison:DABDD9CBE4E2A131729A7517AD81F8E1FC585DCAD81EBC9FCA96ACE2D9351839.
- Incomplete confirmation:4B4C3EFFD75968335FBAC02E4B4B127BEEBD0D4542178200F39567362870CEA3.
- Windows:E2B1CC5C94CBE9FE157CCE22C58E0ACCC231452052A54EFCA8579F9A68D61F76.
- Source preflight:62879A0881805482CBC981F79DFCAF85F248F14D69566D386454B7E3346C8A78.
- Linux restoration:EFC95DDEEC829A3FF928194EA2E4D5D71554A32F3C24733795C0E0E69954B2A7.

Raw observations, producer graphs, callers, source patch, independent tests and
verification remain there. All SSH keepers release only after off-VM verification.
This approach is closed without a distinct material cause; larger P0 gaps remain.
