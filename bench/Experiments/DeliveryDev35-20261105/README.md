# Common 0.5.0-dev.35 delivery checkpoint

Frozen source `bc820332e517b2ea4c10422457a6ee3912501a53`; all three packages have
.NET10/11 assets and exact adapter-to-Core pins. Packages/symbols are staged in
artifacts/nuget. Publication and downstream adoption remain separate.

The batch reuses the existing scalar-repeat proof immediately before atomic
commit and reduces existing fixed capture-trail chunks128->32. No new runner,
storage mechanism, mode or unchecked access. Strict complete-subject UTF-8/scalars,
captures/history, RTL/NonBacktracking, retained aliases, original cancellation/
deadlines and useful resource admission/refunds remain. Smaller initial loans and
fewer futile choices intentionally change numerical route costs; deep capture
runs acquire more chunks/headers. [Resource contract](../../../docs/SPEC.md).

[Atomic commit](../AtomicRepeatCommit-20261105/README.md) independently measures
actual possessive Search-31.48%, atomic hit/miss-29.22/-28.33%, captured-23.92%;
1056->592B and captured3248->2784B. [Capture trail](../SmallCaptureTrail-20261105/README.md)
measures captured Search-16.63%, two-capture-13.76%, finite/deadline-9.82/-9.53%,
actual FindAll-2.68%, each saving1536B. Deep2048 capture-dependent LTR/RTL Count
costs+1.24/+0.86% and+9264B each, retained. Each had ONE bounded separate-Default-
process ABBA with unchanged caller/companions, typed results consumed after timing,
no normalization/confirmation/tuning. Do not compound these isolated percentages.

Windows qualification takes355.5112364s: two root builds/one pack per profile,
eight detached fixtures/109324checks/sixassets pass. Linux consumes those exact
Windows bytes without repacking: eightfixtures/109324checks, sixassets/24restore/
feed/cache/lock/PDB/source audits pass. Original Awk19/Lython118 per platform/runtime
and public native/funded probes pass, final root loans0. The1565 copied consumer
files differ only at three exact pins;464Windows compiler documents/generated
inputs and12original Windows restore audits match. Six staged35 asset hashes and
all prior33/34 packages remain unchanged. Source CI42062090590 passes Windows/Linux.

One **2636.8849538s =43m56.88s Full** passes the original3600s outer cap. The
[manifest](full-refresh.json) records Full/Complete/terminal0 and all83expected
steps in order. One Release build, eight sourcefixtures/109364checks across both
targets, disabled dynamic code on both, complete.NET11 catalogs/paired comparators,
lifecycle/scaling/resources and final verifiers pass. Build67.43s/tests290.54s,
Core433.40s, Python semantic4.78s/raw487.99s/pairs473.46s/lifecycle3.09s/scaling18.26s,
PCRE raw/all pairs/scaling81.45s, resources101.89s. Readiness0/8654520KiB passes the
unchanged8GiB available-RAM/1GiB repo guards. Normal eight-CPU startup thenCPU2,
pinned11RC3 and official CPython3.13.2. Package/consumer clocks are separate.

[Fresh rankings and qualification limits](ratio-summary.json):

- Core115wins/3ties/54gaps/6unqualified. Largest qualified ratios remain Chinese
  ordinary literal4.65x and compiled alternation3.95x. Leipzig Twain, symbol and
  river-window rows are unqualified in both modes. The new river-window failure
  is one decode-then-compiled-.NET batch18.22ms below the existing20ms V3 floor;
  owned batches exceed53ms. Retain the existing whole-six-lane verdict, raw timings
  and older exclusions; no floor waiver or favorable rerun.
- Python37Managed/41CPython/2equivalent/7inconclusive/3unqualified. The table below
  compares dev34 and dev35 Full observations. This is catalog movement, not an
  isolated causal A/B or a compounded improvement. Strong CPython ratios are
  distinct from Core's predecoded.NET ratios.
- PCRE77Managed/11Native/1equivalent/12inconclusive/25excluded. Date4.37x, subroutine
  4.22x and date miss3.98x remain. Broader PCRE completion stays independent of
  Core/Python delivery; native PCRE.NET remains benchmark-only.

| Public Python case | Before / after us | Before / after CPython ratio | Before / after B/op |
| --- | ---: | ---: | ---: |
| atomic/search-hit | 0.720 / 0.554 | 2.21x / 1.72x | 1056 / 592 |
| atomic/search-miss | 0.731 / 0.553 | 2.79x / 2.13x | 1056 / 592 |
| possessive/search-hit | 0.756 / 0.568 | 2.45x / 1.84x | 1056 / 592 |
| reluctant/search-hit | 0.610 / 0.638 | 1.72x / 1.82x | 64 / 64 |
| findall/one-capture-strings | 491.536 / 487.743 | 2.30x / 2.28x | 96536 / 95000 |
| split/unicode | 131.724 / 128.576 | 2.94x / 2.92x | 19512 / 19512 |
| verbose/fullmatch-hit | 0.757 / 0.758 | 2.55x / 2.62x | 1056 / 1056 |
| backreference/named-fullmatch-miss | 0.715 / 0.552 | 3.60x / 2.84x | 2256 / 720 |
| common/date-miss | 2.280 / 2.255 | 2.63x / 2.61x | 80 / 80 |

Reluctant's4.61% adverse time movement and unchanged64B are retained; its source
cause is unproved. Verbose time is nearly unchanged while its comparator ratio
rises. Named-backreference miss moves from Inconclusive to CpythonFaster; the older
3.60x estimate was not a qualified verdict. No blanket10% release gate, control
normalization or selective refresh is used to hide these outcomes.

The seven measured files are imported once; four generated blocks/six descending
tables/178ratio rows verify. Only the root introduction's duration/receipt link
changes beyond measured bytes. Six matching snapshot hosts/ten checks pass with
SkipBuild. Linux qualification temporarily restores only the seven exported,
hash-checked generated files for clean source, then restores their exact measured
bytes. Eight measured Full DLL/PDBs remain unchanged through qualification/guards;
no producer rebuild/repack or repeated package qualification.

Immutable canonical artifacts/experiments/delivery-dev35-20261105, verified offVM:

- full-evidence.tar.gz: `D2124DC972D7D6428299CAEFE67783D130C37EF977B9D07477E90C0E6D62643C`.
- linux-evidence.tar.gz: `0767CB59C6B27B761458562573A0A5D921411A7F5B49519DC6E86F49E83B6455`.
- windows-evidence.zip: `8B6A173B84D4C0C363662467F6BDDBB1FC95AD0F967A407ED10A33D6A5C9F749`.
- windows-restore-inputs.zip: `263B9174D755E8222E96248D877DD5A91033B639D26E7022AC612C54B614E6BC`.
- windows-compilation-inputs.zip: `0A4D98A78F290873FDADED8E48E7D3DEFA4C30559DF5EE1C36CF55F6BE9C769A`.
- post-refresh-guards.zip: `F910EC2C6F1D632B5C06C1763B45BD19A72830DC3504FE7119718C6B376E2817`.

Original sourceCBD35E99, repinned4E3032C5 and transfer088C7132 remain sealed. Fresh35
Linux fixture scratch2175311936B and duplicate archive808288045B are retired ONCE
after offVM SHA verification and exact tar comparison; never retry those targets.
Historical protected targets are untouched.
The complete source/data receipt does not authorize NuGet publication or replace
original downstream adoption/POSIX acceptance.
