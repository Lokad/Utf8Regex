# Greedy repeats before a required ASCII class

Parent `34bf9b712af0909c94a91eb2ec12ca160743445b`. Before is the accepted
PythonCountEight AFTER graph, without a producer rebuild. The actual Python
`verbose/fullmatch-hit` pattern/input/options are checked against the frozen
benchmark catalog. Untimed replay shows two whitespace rewind frames and six
journal entries,1056B public allocation and1120B modeled working capacity;
all loans refund. Value snapshots and on-demand stack allocation already exist.

Extend the existing no-rewind greedy-repeat proof to a mandatory positive,
unfolded ASCII set, directly or at the start of a nonempty simple repeat. At most
128 following scalars are checked against the current profile's actual predicate,
charging work before each check. If disjoint, reuse RunClassRepeat: every shorter
endpoint would point at a consumed scalar that the following class excludes.
Capture effects still execute. Nullable, overlapping and unsupported followers
retain backtracking; lazy-left, RTL and NonBacktracking behavior retain their routes.
No new executor, retained metadata, mode or unchecked access.

The [contract](../../../docs/SPEC.md) records intentional numerical changes:
avoided rewind frames/journals reduce working/depth requirements; bounded proof
work can increase. Repeat vectors, captures, output, original synchronous token/
deadline authority and admission before major allocation remain. Strict validation
still covers the complete subject. Actual Unsafe.SizeOf queries are unchanged.

Release focused checks pass on Windows/Linux and .NET10/11: Core1704, Python1066,
PCRE334 per runtime. New fixtures cover direct/repeated/nullable/overlapping/folded
followers, nested captures, references/assertions, scalar bounds, retained snapshots,
funded closure/refunds, working/result denial and interruption in the proof/run.
Core's128-member proof observes original cancellation/deadlines. The first test
build had an incorrect namespace import; two tiny Python interruption witnesses
did not reach a checkpoint. Both fixture issues were corrected before measurement,
with no production adjustment; failed logs remain in the Windows archive.

ONE **88.679-second** separate-Default-process ABBA: identical caller, adapter,
PCRE2 and benchmark DLL/PDBs; only Core changes. Normal eight-CPU startup thenCPU2,
.NET11RC3, five samples per lane/process,160 owned observations, minimum owned
batch49.1275ms. Typed results are consumed after timing. No tuning, confirmation,
control normalization, package qualification or complete refresh.

| Public Python operation | Before us | After us | Change | Before / after B/op |
| --- | ---: | ---: | ---: | ---: |
| Three-literal finite/live Count control | 7.785 | 7.136 | -8.33% | 112 / 112 |
| Actual verbose FullMatch | 0.668 | 0.607 | -9.18% | 1056 / 64 |
| Actual verbose FullMatch, finite/live | 0.705 | 0.648 | -8.07% | 1168 / 176 |
| Actual verbose FullMatch, deadline/live | 0.900 | 0.792 | -12.00% | 1216 / 224 |
| Unicode whitespace FullMatch, finite/live | 0.777 | 0.703 | -9.52% | 1168 / 176 |
| Overlapping sets FullMatch, finite/live | 0.959 | 1.061 | +10.63% | 1168 / 1168 |
| Captured whitespace FullMatch, finite/live | 0.853 | 0.820 | -3.88% | 1824 / 832 |
| 4KiB whitespace FullMatch, finite/live | 160.174 | 30.771 | -80.79% | 182104 / 176 |

Both after-process medians beat both before medians on admitted cases. The
unchanged Count control's A1 process is slower than A0/B0/B1; retain its pooled
8.33% movement and spreads without credit or subtraction. Short/captured changes
remain observations with that uncertainty. The overlapping-set regression is a
public cost, approximately0.102us with unchanged allocation, and occurs in both
after processes. FullOpts helper code grows3163->3875B (+712B).

Accept the documented local cost for the substantial long-run gain (~5.2x,
181928B saved/call) and992B lower allocation on ordinary/finite/deadline/Unicode/
captured target calls. This follows the user's trade-off policy; the completed
checked-access10% allowance is not a universal gate. No broader profile speedup,
CPython verdict, new catalog Status or compounded improvement is claimed. BCL
lanes return equal full-match ranges on overlapping semantics, with predecoded and
decode-then variants; their resource policies differ. [Raw durations, graphs and
process observations](cost-summary.json) remain authoritative.

Immutable canonical artifacts/experiments/adjacent-set-repeat-20261105, verified
off the VM:

- evidence.tar.gz: `ACB191EA78DED35E10203C1279E75EA65EE53F91CC62837EDBF24DEFC0B76BE1`.
- windows-evidence.zip: `0102E37A8513DDEEDCE7F598CCB6266AFF432B0F88E06C6A9BE77CC1D98CEFD5`.
- Declaration: `27F788EFAE2D39F25BA15FF188C76D7CB711DB1A3A6974478E43151537DC9E4E`.
- Source patch: `D9834CDB62944C0161A24B261396CFB3BF55699EA1A50E7DDA464F3251DB12B8`.

Previous graphs/raw, published/staged bytes and dev36 packages are unchanged.
Future packages need a new common version and the existing delivery-boundary
checks; this isolated source experiment does not replace downstream acceptance.
