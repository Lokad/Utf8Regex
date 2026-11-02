# FullMatch for proved literal repeats

PythonRe's Unicode FullMatch workload `(?:Шерлок )+` traversed the scalar VM's
repeat frames, journals and coordinate mapping for every period. Its complete
13-byte literal was already retained and admitted by Core's required prefilter.
The value FullMatch path now borrows that needle after a bounded complete-root
proof over existing instructions. No stored plan, new buffer, engine, mode or
unchecked operation is added.

Eligibility requires a capture-free positive greedy repeat whose complete body is
a nonempty unfolded literal. At most 64 instructions are inspected; every literal
scalar, repeat target and terminal instruction must agree with the borrowed
needle. Unsupported, captured, folded, branched and lazy shapes retain complete
VM verification. Existing exact-literal routes remain first. This targets value
FullMatch; detailed and text operations retain their implementations.

One invocation starts before the proof and any declined cold preparation. Strict
validation still covers the entire original subject, including invalid prefixes
and tails on immediate misses. Working admission observes the existing parent
account. Comparison checkpoints cover at most 64 periods and 2,048 bytes; final
publication checks the original token and deadline. Coordinate and suffix floor
semantics, borrowed-result validity and funded close remain. Avoiding lazy end-
constrained preparation, VM scratch and coordinate maps deliberately lowers
route-dependent construction, work, depth and byte requirements. No major
execution or result buffer is acquired by the eligible value operation. The
resource policy still covers modeled major capacity, not incidental objects/RSS.

One quiet-VM .NET 11 RC3/CPU2 comparison took **64.282 seconds**, 17 public cases,
four equal-result lanes and identical Core DLL bytes. Both producer sets come
from clean `cedb253b`, with only the archived two-file runtime patch applied to
the candidate. Warmup is 500ms/minimum 128 calls per lane, independent calibrated
60ms batches and five alternating samples. Every sample is at least 47.743ms.
Setup/compilation is excluded; full UTF-16 range outputs are checked after timing.
Offset/byte/capture correctness is checked separately. Diagnostic compiled Regex
lanes use overlapping scalar-literal semantics, both predecoded and decode-then;
official CPython qualification is separate. Normal runtime startup precedes
in-process affinity. No external taskset, confirmation, variant or repeat campaign.

| Public FullMatch | Before us | After us | Change | Bytes before / after |
| --- | ---: | ---: | ---: | ---: |
| Unicode 1,024-period hit |89.283|14.018|-84.30%|43752 / 0|
| Unicode same-length miss |109.585|14.046|-87.18%|43752 / 0|
| ASCII 4,096-period hit |143.757|8.882|-93.82%|169992 / 0|
| Mixed BMP/astral period hit |74.596|17.752|-76.20%|43752 / 0|
| Unicode hit after scalar prefix |90.612|14.111|-84.43%|43752 / 0|
| Valid incomplete-period miss |107.485|11.796|-89.02%|43752 / 0|
| Finite/live Unicode hit |100.827|14.596|-85.52%|43872 / 120|
| Finite/live Unicode miss |121.656|14.552|-88.04%|43872 / 120|
| Deadline/live Unicode hit |100.852|14.861|-85.26%|43920 / 168|

**Promote.** Largest adverse median is the branched FullMatch control, **+9.48%**,
within the 10% allowance; captured/lazy/folded/suffixed controls and all original
observations remain in `summary.json`. The unchanged Search control is +0.002%.
Control allocation medians are unchanged. Literal control movement is not claimed
as a causal improvement. This comparison is against its parent; the complete
candidate must still pass the fixed-original `a40bda03` cumulative delivery gate.
No package, full-catalog or platform qualification was repeated.

Focused Release checks pass on .NET 10/11, including all valid scalar floors,
unaligned backing storage, BMP/astral/mixed periods and bounds, strict malformed
prefix/tail rejection, no cold preparation under denied construction, work denial,
comparison cancellation/deadlines, final-publication token order, declined-proof
cold authority and funded close/retained values. Complete PythonRe passes all
4,571 tests on .NET 11. .NET 10 passes 4,570 and hits one existing replacement-cache
recovery test's real 200ms deadline under load. Its expiry witness already used a
diagnostic clock; recovery now uses a deterministic outer clock too. The affected
21-test class passes both runtimes after that test-only fix. No product timeout
was relaxed or suite silently retried. Initial diagnostics-API test build errors
and all failure logs are preserved.

Raw inputs, both DLL/PDB sets, driver/binaries, protocol, patch and all samples:
`artifacts/experiments/python-literal-repeat-fullmatch-20261102/`.

- `evidence.tar.gz`: SHA256 `639D52308F677A8F42A75B64AB565DF7A61725B1F08518019C593F45BDDEDA8B`.
- Runtime patch: SHA256 `23B52311F2328CC8614877142AB314B1566963A284A3E725C366BDEEC51DAD6E`.
- `correctness-source.zip`: SHA256 `FFE333A421BD006E65B2004DF184AE4CC10334F60442BB49FA3105AEC4718DF7`.

Existing staged dev.9 and published packages remain immutable. Official selective
qualification and generated snapshot updates follow from a clean committed source;
platform/package/full-refresh work stays at the next coherent delivery boundary.
