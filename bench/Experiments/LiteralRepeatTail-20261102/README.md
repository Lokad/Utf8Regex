# Fixed literal tails in value FullMatch

**Accepted.** Extend the existing capture-free positive greedy literal-period
loop to a fixed literal tail. FullMatch fixes the endpoint, so subtracting the
tail width determines one possible repetition count, including overlapping tails.
Owned scalar instructions supply UTF-8 period/tail bytes; the optional Core
rendering and its required-needle copy are no longer needed for this proof.
No retained plan, heap buffer, new engine or shared RunSegment change was added.

The existing <=64-instruction gate leaves at most60 literal scalars, encoded with
checked Rune/span operations into fixed240-byte stack scratch. This is bounded
incidental helper storage, like the existing fixed-stack candidate proof, rather
than a major-capacity loan. Existing one-byte working/result successes remain.
The original invocation starts before proof or declined cold preparation. Whole
strict UTF-8 validation precedes comparisons; period blocks remain <=64 periods
and<=2048 bytes, the tail<=240 bytes, with original interruption/publication checks.
Enclosing numerical loans still receive the zero-byte admission checkpoint.
Capture/fold/lazy/branch/class/assertion/RTL/NB exclusions retain their verifier.
Fixed-byte periods are nonempty and scalar-aligned; no backtracking or capture
choice is discarded. Result metadata never references stack scratch.

One quiet-VM .NET11 RC3 **22-case public comparison took103.345s**, under120s,
minimum sample **52.224ms**. Fixed original `a40bda03`, parent `35ff6ac4` and parent
plus the two-file PythonRe patch load separately; Core DLL/PDB bytes match exactly.
The previous18 cases remain, plus Unicode/overlapping tails and controlled suffix
calls. Existing protocol: normal startup then CPU2, warm500ms/min128 per lane,
independent60ms calibration, five alternating samples>=20ms, complete public
outputs consumed after timing, allocations and equal-output compiled Regex
predecoded/decode-then diagnostics. **No confirmation or variant.**

| PythonRe public FullMatch | Before us | After us | Change | Allocated B before/after |
| --- | ---: | ---: | ---: | ---: |
| Suffixed `(?:ab)+c` |36.531|3.108|-91.49%|43752 / 0|
| Unicode period/tail |54.266|19.111|-64.78%|43752 / 0|
| Overlapping `(?:ab){1,1000000}ab` |38.736|3.117|-91.95%|45848 / 0|
| Finite limits/live-token suffix |43.043|3.356|-92.20%|43864 / 112|
| Deadline/live-token suffix |43.690|3.562|-91.85%|43912 / 160|
| Existing complete literal-repeat control |16.303|16.092|-1.30%|0 / 0|
| Existing folded control |80.962|80.506|-0.56%|85848 / 85848|
| Existing captured control |98.338|94.958|-3.44%|79616 / 79616|

Every allocation median is unchanged or lower; largest adverse current median
is branched FullMatch+2.87%. All22 simultaneous original ratios fit10% here
(largest nullable atomic Search+7.41%), but this focused result does not replace
the complete cumulative gate. Prior adverse observations remain preserved.

Focused40/runtime and complete **4617/runtime** PythonRe Release cases pass on
.NET10/11. New checks cover overlapping/Unicode/NUL tails, offsets and poisoned
outside-span storage, same-byte-width wrong periods, 240-byte proof/code boundary
and larger fallback, partial Rune/malformed prefix/tail, exact work cutoffs,
comparison cancellation/deadlines and final publication, refused cold preparation,
funded close/refund/retained values. The cold-decline fixture now uses a genuine
class body rather than preserving the former lack of tail support. No production
change followed timing. SPEC documents lower work/depth/major-byte requirements.

Actual optimized public-route code and source are retained, not used as throughput
proof: FullMatch helper code3466->1989 bytes; proof code668->886 bytes, its local
frame472->24 bytes. The after helper separately allocates fixed240 stack bytes.
Warm-probe call counts are diagnostics only. Existing native/profile comparators
remain outside shipped execution. No package/catalog/platform qualification yet.

Raw driver/protocol, all three producer DLL/PDB sets and hashes, patch, every
sample/codegen/log: `artifacts/experiments/literal-repeat-tail-20261102/evidence.tar.gz`,
SHA256 `4BC2C7D9B12167A71BE28679251D8FC4F87E4D68D7138C141EDCE79B6D919FDC`.
`correctness-source.zip`: SHA256
`BDFB33A64E7564EF5205414969F14678E59E1AA98BA500C8A375810388285E58`.
