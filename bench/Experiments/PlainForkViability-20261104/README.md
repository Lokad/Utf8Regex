# Omit disproved plain-alternative frames

**Accepted.** The scalar runner reuses its existing bounded capture-marker/literal
proof before acquiring a plain forward Fork frame. A required exact ASCII literal
can disprove the first branch or the deferred branch at the current input position.
Viable alternatives retain their order, captures, rollback and ordinary execution.
Balancing, assertion/atomic frames and RTL keep their existing behavior; opaque
branches retain frames unless the other branch is disproved.

No opcode, dispatcher, prepared metadata, pool, owner, engine, mode, accounting
framework or unchecked access is added. The Fork instruction retains its normal
work charge; proof inspection is bounded at 32 instructions per branch. Required
state still needs depth/major-buffer admission before acquisition. Full-subject
strict validation, scalar boundaries, retained cursor/results and original-token
cancellation/absolute deadlines remain. Avoided instructions/frames can reduce
numeric work, depth and working requirements; the three profile contracts and
release notes document this version-dependent change.

Untimed inspection of the current imports corpus confirms 46429 bytes, 19 public
Count matches, 43 instructions, four repeat ids and 1952 allocated bytes per call.
Its optional captured `shared` branch starts with `s`, against a required quote;
the path alternative starts with a dot, against a nullable class repeat. All 19
actual paths begin with underscore. This identifies unnecessary frames; it is not
an executed branch census or timing evidence. Original pattern/corpus hashes and
the probe are preserved in the raw archives.

One **78.6667817-second**, 15-case public A/B compares committed `e53ec824` Core
against three-file overlay `915B87F9`, built on test/receipt-only `99cff1d9` with an
identical source tree. All six frozen `ba457088` companion DLL/PDB files remain
identical; only Core changes. [All observations](cost-summary.json) retain exact
binary identities, every allocation, sample spread and comparator diagnostic.

| Public operation | Before us | After us | Change | B/op before -> after |
| --- | ---: | ---: | ---: | ---: |
| Actual imports Count |12.82|11.47|-10.54%|1952 ->80|
| Actual imports compiled Count |12.88|11.51|-10.65%|1952 ->80|
| Actual imports finite/live |15.28|13.95|-8.70%|2064 ->192|
| Actual imports deadline/live |16.95|15.50|-8.57%|2112 ->240|
| Actual imports detailed Match |2.86|2.38|-16.83%|4848 ->2976|
| Dense disjoint branches Count |537.59|567.31|+5.53%|3552 ->3552|
| Same finite/live |664.39|657.77|-1.00%|3664 ->3664|
| Overlapping first literals |593.40|656.70|+10.67%|3552 ->3552|
| Folded first branch |449.92|447.92|-0.44%|3552 ->3552|
| Nullable branch |350.35|344.76|-1.60%|3552 ->3552|
| RTL imports control |9506.89|10114.93|+6.40%|56248 ->56248|
| Supplementary first branch |403.31|433.45|+7.47%|3552 ->3552|
| Python disjoint branches Count |602.93|613.54|+1.76%|5744 ->5744|
| Native Count control |2.46|2.45|-0.58%|0 ->0|
| PCRE2 Count control |143.18|141.04|-1.49%|0 ->0|

Imports Count saves 1872 B/op, **95.90%** of its former allocation; the existing
counter vectors remain. Required conservative working capacity also falls: the
new public test admits both ordinary and live-token imports Count under 256 bytes,
with work/depth/result caps still enabled. Dense programs that retain other frames
or journals do not automatically save a chunk. The local overlapping/supplementary/
Python costs are accepted for the actual imports gains and simpler live state.
This follows the user's trade-off policy, not a universal 10% gate.

Imports before spreads reach 1.095 versus 1.041 after. Detailed Match is noisier,
1.551 before and 1.106 after, so its time reduction is less certain; its allocation
saving is unchanged across samples. Other spreads stay below 1.046. Preserve RTL
movement without inventing a cause, and assign no causal gain to unchanged controls.
There is no confirmation or tuning variant: the allocation/operating-cost decision
is sufficiently clear. These results do not update catalog parity Status.

Normal eight-CPU startup precedes CPU2 affinity. Managed warmup is at least 1.5s/
128 calls, BCL diagnostics 250ms/64 calls, independent calibration targets 60ms,
then five alternating samples. All **150 owned samples exceed 54.329ms**. Typed
public results are consumed after timing. Same-options predecoded/decode-then BCL
lanes agree on the supplied overlapping subjects; no CPython qualification is
claimed. Strict validation remains inside every public operation.

Windows/Linux .NET 10/11 each pass focused Core 1230, PythonRe 790 and PCRE2 165
checks. New tests cover branch ordering, capture histories/aliases, backreferences,
nullable/overlapping/Unicode alternatives, assertion/atomic/balancing/RTL/NB routes,
strict malformed suffixes, original cancellation and tight finite working limits.
Existing tests cover funded admission, deadlines and retained cursor lifetimes.

The first PCRE2 run exposed a stale expectation that `cat|horse` needs workspace.
It now succeeds with a one-byte heap cap and is explicitly tested as workspace-free;
existing nonlinear-repeat/capture/assertion cases retain real heap denial. Two
initial edits mistakenly changed the duplicate general Count data row, causing
further failures. Those logs are retained; the early explanation about `cat|car`
factoring was unverified and withdrawn. The final diff only moves the correct
heap row, preserving the program-kind inventory. Core/Python checks were reused;
only PCRE2 was rebuilt/retested. Runtime source did not change during these repairs.

Immutable archives under `artifacts/experiments/ascii-fork-viability-20261104/`:

- `evidence.tar.gz`: `C7278AD370BBFF200CC57E0E9EA806D80A6455947D6CA436C60767FF9936E73B`.
- `windows-correctness.zip`: `CA35A49FF4128C7924D0F08AB2BB4CED1E978FA191952B61110767FD485268E5`.

Raw evidence includes declaration, probe, source overlays, driver, frozen graphs,
failures and passing checks, all samples and terminal status. Published/staged
packages and dev.28 Full tables remain immutable. No package/platform/Full matrix
or completed checked-access campaign was repeated for this small change.
