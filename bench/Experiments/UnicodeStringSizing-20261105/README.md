# Reuse bounded character counting for validated Unicode strings

**Accepted for controlled Unicode output.** Python scalar string shaping validated
interior substrings again solely to size returned strings. Whole-subject validation
already proves their scalar ranges. Extract the existing checked GetCharCount loop
from Core's output helper and reuse it. Keep ASCII/full-subject/map sizing,
string.Create decoding, result/working admission and retained-output lifetimes.
No matcher, selector, buffer, cache, execution mode or unchecked API is added.

**Intentional accounting change:** interior sizing uses the existing scalar-aligned
4096-byte output blocks instead of repeating validation's2048-byte blocks and
initial/terminal charges. It retains the same invocation/input-work authority,
original token and deadline checks. Whole-subject strict validation stays unchanged;
the operation's existing maximum4096-byte decode interruption quantum remains.
String payload is still admitted before allocation. This reduces counted work;
work limits continue to bound implementation-defined work rather than exact scans.
The [Python contract](../../../docs/SPEC-PYTHONRE.md) records this change.

One **113.539s**, separate-Default-process ABBA comparison uses frozen dev32Full
7f152e64 before graphs and parent0eb13405 plus the archived source patch. Core and
Python DLL/PDBs change; all four PCRE/benchmark companion files and the typed caller
remain identical. Each process loads one Core and one Python producer. Normal
8CPU startup precedes CPU2 affinity; producer1.5s/min16 and BCL250ms/min4 warmup,
independent60ms calibration, five alternating samples/process. All160 producer
samples exceed **42.9003ms**. Returned arrays/strings/ints are retained during
timing; complete checksums are consumed afterward. Ten observations per version
are pooled per row, with the even median averaging its two middle values.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual Unicode Split |123.391|123.406|+0.01%|19512/19512|
| Same finite/live |160.812|148.258|−7.81%|23712/23712|
| Same deadline/live |223.248|202.273|−9.40%|23760/23760|
| Captured Unicode FindAll |217.695|217.919|+0.10%|25248/25248|
| Long astral Split |1752.953|1706.892|−2.63%|49272/49272|
| ASCII Split control |69.985|70.661|+0.97%|43088/43088|
| Core string-output neighbor |13.610|13.410|−1.47%|4664/4664|
| Core Count control |2.149|2.151|+0.10%|0/0|

The actual3200B input repeats東京，大阪,京都，128times and returns385strings.
Finite/live and deadline/live improvements have tight process/sample spreads;
retain the controls' small movement without causal credit or normalization.
Unlimited Split and captured FindAll are effectively unchanged. Accept for the
simpler validated-range invariant and useful controlled public savings. This does
not close the3.58x unlimited CPython gap or assign a new catalog Status. BCL
predecoded/decode-then lanes are equal-result diagnostics; finite resources have
no BCL equivalent. No phase subtraction, confirmation or source tuning follows.
[All costs, allocations, graph hashes and process observations](cost-summary.json).

Windows/Linux focused Release checks pass on each.NET10/11: Core323/Python811/
PCRE72. Ten new cases cover scalar-aligned interior ranges, byte offsets, BMP/astral,
tails around4096bytes, captures, strict malformed full tails despite a
split limit, finite funding and original-token/deadline interruption during sizing
before string admission. Existing empty/full-range, result/working/work cutoff, shaping and retained
lifetime checks remain. No result/execution forwarding test helper is introduced.

A pre-timing source guard stopped on Git's8-versus9-digit hash abbreviation in
patch headers. No measured worker had started. Canonical full-index headers then
proved identical source bytes; original patch/run, metadata correction and graphs
are retained. Declaration8D28DE6A is unchanged; canonical patch21FE22BA records the
same source as initialFF761613. Tests/graphs were reused, with no extra build or
performance trial. Earlier ShortResultDecode/known-end/detailed projection work
remains closed; the string decoder is retained.

Immutable artifacts/experiments/unicode-string-sizing-20261105:

- evidence.tar.gz: `200F07DAB80283E6E8D3FB54651FD96075E22C490A4EEE6ACD7B1491D965F802`.
- windows-correctness.zip: `A1ADF084E3A120E6AE0E034ABDCD15AEBF9F29A03F23902132ECF0FD5EAE7D64`.

All six staged dev32 package/symbol hashes remain unchanged. No package, Full,
platform matrix or unlimited catalog refresh is repeated for this small source
change. The [dev32 delivery receipt](../DeliveryDev32-20261105/README.md) keeps its
frozen source, classifications and43m07.82s Full identity. New source awaits the
next coherent delivery boundary, alongside the remaining measured gaps.
