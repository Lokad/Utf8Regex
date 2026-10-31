# Chinese alternation Count attribution

Read-only diagnosis on clean matching `f42523ba` VM binaries, .NET 11 RC3,
CPU 2, one 120-second outer cap. Existing route/census/alternating phase commands;
1000 calls and five samples per timed lane. [Commands](diagnose.ps1),
[raw output](diagnosis.txt), [input identity](input-identity.txt).

The 813478-byte corpus is shared with the earlier single-literal case. This
five-literal alternation returns 207 matches. Its actual public Count route is
the existing packed-nibble SIMD family strategy, with strict validation first.
The diagnostic candidate replay reports 212 full verifications and five rejected
candidates. It is not a count of actual production branches.

| Existing phase / public operation | Median us/op | Range us/op |
| --- | ---: | ---: |
| Validity only | 148.562 | 105.648–235.701 |
| Ordinary selected kernel | 211.260 | 134.152–1168.607 |
| Compiled selected kernel | 211.977 | 149.930–235.211 |
| Validation then ordinary kernel | 364.631 | 294.459–449.199 |
| Validation then compiled kernel | 364.911 | 359.714–371.038 |
| Public ordinary Count | 364.409 | 315.729–401.814 |
| Public compiled Count | 364.935 | 244.329–370.147 |
| Predecoded ordinary Regex | 104.683 | 82.069–426.492 |
| Predecoded compiled Regex | 51.779 | 40.903–142.599 |

Search remains material here; the single-literal validation diagnosis does not
explain this case completely. Candidate rejection is already low. A worthwhile
search improvement must reduce scanning cost while preserving dense/folded
consumers, tail bounds and the existing verified-candidate semantics. No backend
sweep, replacement selector, BCL/native matcher or source prototype was added.

Ranges are wide, so these phase observations do not establish a stable effect
size, exact additive decomposition, public speedup or a new benchmark Status.
Keep the existing qualified 5.367x compiled snapshot and larger gap visible.
Defer a broader SIMD scan investigation while addressing the next 5.268x letter
Count gap: its already-owned primitive lacks the public bounded admission that
single-category Count already uses. Reusing that mechanism is the next bounded
investigation; it requires public cost and resource evidence before promotion.

No rebuild, test rerun, package change or catalog refresh was needed. Matching
source/DLL identities and clean-input status are in the raw output; the binaries
are also frozen by the preceding NonBacktracking quantum receipt.
