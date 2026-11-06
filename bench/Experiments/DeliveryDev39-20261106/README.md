# Common 0.5.0-dev.39 delivery checkpoint

Frozen source `07b10eabfb99770f76bd9b8766a20cb10480cb2c`; Core, PythonRe and PCRE2
retain .NET10/11 and exact adapter-to-Core pins. Publication and original downstream
adoption remain separate. Historical staged packages, symbols and raw stay immutable.

This batch combines two measured source improvements without new state, API,
allocation, selector framework or unchecked access. Strict whole-subject UTF-8,
scalar boundaries, captures, RTL/NonBacktracking, aliases and original synchronous
cancellation/deadlines retain their contracts. Useful admission and bounded work
remain; the literal-window change can lower numerical work requirements.

[Signed continuation classification](../ContinuationSigned-20261106/README.md)
uses one signed vector comparison in place of mask-plus-equality at existing
256/512 widths. ONE91.422s public ABBA observes Chinese Count-2.03%/-1.36%, family
-1.97%, samealloc; control+.10%, deadline+1.08% and uncertain Russian/finite ranges
are retained. Both after medians beat both before on Chinese targets. Method
code remains1414B. No broad profile gain or compounded estimate is claimed.

[Complete literal windows](../BalancedLiteralWindow-20261106/README.md) reuse the
existing checked span-search tier for17-32 exact bytes before extending their
admitted windows. Total scan span INCLUDING lookahead remains<=2048 with basic
pre-scan and terminal metering. The old cutoff-only proposal stays rejected.
ONE101.370s ABBA observes actual finite Chinese1164.40->406.30us (-65.11%), deadline
2067.78->415.23us (-79.92%), mixed-input value Match1.20->.24us (-79.76%), samealloc.
Dense-first442.72->1.21us; dense-last+4.90%/.06us, reverse+4.12/+3.12% and control
-1.06% remain. Accept those costs for the substantial affected public gains; no
normalization, tuning, extra confirmation or universal10% release veto.

Source CIs42145971192 and42150213615 are terminal GREEN Windows/Linux. These
isolated comparisons are not additive or new catalog Status; exact graphs,
process spreads and compatibility checks remain in their individual receipts.

Windows qualification took 298.47 seconds, with two root builds and one pack per
profile. Detached fixtures, original Awk/Lython consumers and the public
native/funded probes pass. Loaded package assets, compiler documents and restore
identities match; original source differs only at the three exact package pins.
All 204 prior staged assets are unchanged. Six new outputs bring the total to
210. No publication or original downstream adoption is claimed.

One **2617.52-second (43m37s) Full** passes the unchanged 3600-second budget.
The [manifest](full-refresh.json) records all 83 expected steps, completion and
terminal zero: one build, both-runtime source correctness and disabled-dynamic
checks, .NET 11 catalogs, paired comparators, lifecycle/scaling, resource checks
and final verifiers. Build took 70.44s, source tests 283.53s, Core 435.18s,
Python raw 484.05s/paired 450.07s, PCRE scaling 81.54s and resources 101.25s.
Original 8 GiB available-RAM/1 GiB repository admission passed immediately at
8752244 KiB. The runtime saw eight CPUs at startup before affinity to CPU2;
.NET 11 RC3 and official CPython 3.13.2 remain the comparators. No server shutdown.
Package qualification time is separate from the complete benchmark refresh.

[Fresh qualified rankings](ratio-summary.json) preserve distinct baselines:

- Core: 120 wins, 54 gaps, four unqualified rows. Chinese literal Count is
  4.54x ordinary/3.60x compiled versus predecoded Regex; compiled Chinese family
  2.86x, imports 2.66x and word Match 2.62x. The two email lanes now qualify;
  river-window/symbol exclusions remain. No floor waiver or favorable rerun.
- PythonRe: 39 managed wins, 41 CPython wins, one equivalent, five inconclusive,
  four unqualified. Early literal 2.88x, named-reference miss 2.76x, Unicode Split
  2.63x, date miss 2.56x and verbose 2.39x. Alternation/category misses now qualify;
  greedy miss becomes unqualified. Preserve every lane's harness-floor decision.
- PCRE2: 78 managed wins, 12 native wins, one equivalent, ten inconclusive and
  25 excluded rows. Unsupported and result-incompatible native comparisons keep
  their reasons; PCRE.NET remains private benchmark tooling.

[Movement between catalogs](catalog-movements.json) is **not a causal A/B**.
Retain Python family Count 722.79 -> 1004.05us (+38.91%, +281.26us, both 0B),
still ManagedFaster; CPython moves 2433.72 -> 2454.76us. Its ASCII `cat|dog|bird`
needles have lengths 3/3/4, outside the changed 17–32-byte search tier. No source
cause is established. Trace its actual route/protocol next, before proposing a
bounded investigation; do not rerun catalogs to improve this row.

Other adverse observations include bounded-repeat/context +2.73%/~641us ordinary
and +2.10%/~497us compiled, AWS full +2.23%/~233us ordinary, Python template
replacement +2.89%/~188us and Unicode capture FindAll +4.92%/~26us. Allocations
stay unchanged. AWS quick improves 22.80% compiled/13.09% ordinary; this does not
establish that a source fix caused the movement. The earlier AWS investigation
remains closed without a demonstrated cause. Isolated accepted source gains and
their local costs remain in the two receipts above; no additive estimate,
normalization or universal 10% release veto is inferred from refreshed rows.

Linux consumes the exact Windows packages without producer rebuilding or
repacking. Detached fixtures, original Awk19/Lython118 per target and public
native/funded probes pass, with final root loans zero. Six assets, 24 restore
audits and all 12 detached/original TRX hashes and counts match. Admission passed
immediately at 8671812 KiB. Windows and Linux each cover eight detached fixtures;
the source Full also includes repository guards omitted from package fixtures.

Seven generated files were imported once. Four README blocks, six descending
ratio tables and all 178 rows match the generated data. Six snapshot hosts pass
with matching binaries and SkipBuild. All eight Full DLL/PDB identities survive
Linux qualification and the guards. Data CI is checked separately after this
receipt commit; publication, downstream adoption and POSIX remain separate.

Immutable archives under `artifacts/experiments/delivery-dev39-20261106/`:

- `windows-evidence.zip`: `2D4B631FB0995E4D3586A43D59CDEE15425D1390FA4536DC413CE6924EC2D34E`.
- `windows-restore-inputs.zip`: `DE290E09C922C236F920622009988DE9455758C97AF20DB96E285146305C8B93`.
- `windows-compilation-inputs.zip`: `43994B8A9259FD397F577D1FD2F8CC2F0C4D514F30D6789BD0C07612FDBF1F46`.
- `full-evidence.tar.gz`: `0D5CD91A2955B43494215D5E8988E93675C3B4FDE56FD526970387B6F0F4F8BC` (34333868 bytes; verified off VM).
- `linux-evidence.tar.gz`: `6B07F92D1536C55D2BBA08AADD5490670C266B8C150D2F46CEC23D2F86FE2020` (808441886 bytes; verified off VM).
- `post-refresh-guards.zip`: `B8F91259B534940BA987AB2582FB3F7E1A3833A9E27ACF2C7A3297D5316D1A74`.

Fresh dev39 Linux fixtures (2176031011 bytes) and their export duplicate
(808441886 bytes) were retired once after off-VM SHA verification, exact archive
comparison and fixed resolved-path checks. All eight Full graph identities still
match. Canonical packages, symbols and raw evidence remain immutable.
