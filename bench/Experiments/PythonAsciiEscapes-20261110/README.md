# Lower Python ASCII escapes without rendering and reparsing

Accepted producer is baseline `581f9fa61c5ea5037dd9e1c48138f54d1084965f`
plus the archived patch. The scalar compiler recognizes the parser-owned ASCII
`\w/\W/\d/\D/\s/\S` fragments and emits its existing admitted 16-byte Set
mask directly. Negative escapes become one scalar-complement Set, removing
negative-lookahead/dot fragments. Positive masks and existing scoped flags remain.
ASCII whitespace excludes U+001C–U+001F; complements include valid non-ASCII
scalars. No new matcher, instruction, cache, policy switch or unchecked read.
Remaining bracket/predefined unions keep their existing preparation.

One **86.875s** Linux .NET11 RC3 ordinary-process ABBA, capped180s,15 public
lanes/five samples/process: **300 samples >=45.909ms**. Construction and
construction+first Count are separate; complete warm captured-string results
cover both input policies. Consume/dispose after timing. Common Core/caller,
distinct Python DLL/PDBs,8 processors visible before CPU2 affinity; default
runtime settings. No confirmation or tuned variants.

| Public operation | Before µs | After µs | Change | Bytes/op before → after |
|---|---:|---:|---:|---:|
| construct/ascii-captures | 166.216 | 210.938 | +26.91%, overlapping | 80184 → 72360 |
| construct-first-count/ascii-captures | 192.408 | 190.133 | -1.18%, overlapping | 80456 → 72632 |
| construct/ascii-complements | 162.514 | 128.522 | -20.92% | 139256 → 100984 |
| construct-first-count/ascii-complements | 162.494 | 131.036 | -19.36% | 140072 → 101272 |
| construct/six-ascii-classes | 192.702 | 156.073 | -19.01% | 159824 → 109776 |
| construct-first-count/six-ascii-classes | 205.444 | 158.474 | -22.86% | 160656 → 110080 |
| construct/scoped-fold-unicode | 85.329 | 79.063 | -7.34%, overlapping | 77528 → 73368 |
| construct-first-count/scoped-fold-unicode | 86.594 | 80.722 | -6.78%, overlapping | 78856 → 74696 |
| construct/byte-pattern | 99.823 | 92.765 | -7.07%, overlapping | 80224 → 72400 |
| control/construct-unicode | 63.319 | 63.109 | -0.33%, overlapping | 57920 → 57920 |
| warm/ascii-captured-strings/Validate | 777.327 | 828.737 | +6.61%, overlapping | 242008 → 242008 |
| control/warm-unicode-captures/Validate | 870.563 | 865.659 | -0.56%, overlapping | 242008 → 242008 |
| warm/ascii-captured-strings/AssumeValid | 756.078 | 672.393 | -11.07%, overlapping | 242008 → 242008 |
| control/warm-unicode-captures/AssumeValid | 838.950 | 750.483 | -10.55%, overlapping | 242008 → 242008 |
| control/core-construction | 55.971 | 59.786 | +6.82%, overlapping | 49344 → 49344 |

Accept the simpler lowering, consistent allocation savings and disjoint19–23%
construction/first-use gains for complement-heavy families. Complements allocate
about27% less; the six-class family about31% less. First-use allocation also
benefits from the simpler scalar program. Positive ASCII capture/scoped families
have identical instruction/mask hashes; negative-containing families change.

Two-capture construction remains bimodal: before process medians109.725/222.707µs,
after314.542/107.333µs. Its adverse pooled median+26.91% has overlapping ranges
96.8–279.5 versus100.2–354.8µs. First Count and warm captured strings also overlap.
Preserve these costs/uncertainty; no general constructor/warm-matching/native-parity
claim. Core construction control+6.82% and trusted Unicode warm control−10.55%
also overlap. No confirmation is needed to decide the clear allocation/complexity
reduction and complement-family gains.

Release PythonRe **5081 checks per target** pass on Windows and Linux, including
all ASCII bytes and six wider scalar controls for each escape under both input
policies, string/byte patterns, scoped ignorecase/restoration, scalar complements,
capture coordinates, globals/cursors and construction byte-boundary refunds.
Core terminal/disjoint172 per target pass on both OSes, including corrected
interruption injection after block accounting. Existing resource/cancellation/
lifetime tests remain unchanged. Work/depth totals intentionally decrease on the
simpler route; major admission and interruption guarantees remain. SPEC-PYTHONRE
records the accounting interpretation. No API or package changes.

Two local caller preflights failed before timing: Python rejects lowercase\z
(use\Z), and a Python-only(?a:) scope is not a valid Core control. Both caller
mistakes were corrected before freezing the comparison. No measured sample was
removed or repeated.

Raw graphs/PDBs, caller, protocol, patch, shapes and observations:
`artifacts/experiments/python-ascii-escapes-20261110/evidence.tar.gz`, SHA256
`46A3BC084BEEE4DD99B3CE330954F27C52074F6DEDDD7B7DCDC2EF57593AEF13`.
The full derived summary is beside the archive. Common accepted Core DLL
`F65EB5D4D176E34691D7711DD8C95116411FA4A736841611F81CB4D257EB6F63`
retains d1b0f50a metadata plus the accepted16bd8f69 interval patch. Python before
`D32C5FB851AC8AEFB3814F062DC37EBD370E346D686EAA2FD7717F6372D86CB3`,
after `E2986B90DE9C9C931769F6780DD926E76BDD9B48B0C38DF00D7524600AB31574`;
full PDB identities are in the summary/archive. Linux checks archive SHA256
`407D5C26EEE637B2F0C40B4D1770D37EC142F27EF1BBBB225520B1B2B1F1601E`.
Both archives are copied offVM/hash verified. Timing is closed. CI is a separate
source gate; no full refresh, package repeat, publication or altered immutable
artifact. Continue construction/preparation and remaining history work.
