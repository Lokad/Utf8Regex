# Common 0.5.0-dev.43 delivery

Core, PythonRe and PCRE2 target .NET10/11 with exact common Core pins. The immutable
candidate is in `artifacts/nuget/`; **not published**. It delivers the accepted
[negative-pair Count rewind](../NegatedPairRewind-20261106/README.md) and
[benchmark calibration headroom](../ReadmeCalibrationHeadroom-20261106/README.md).
Scalar UTF-8 semantics, captures/lifetimes, cancellation/deadlines and useful
resource controls remain. This is qualified dependency delivery, not parity.

Package source `26fc4bee169401a63354e5a66ec90620db7a5104`; full-refresh source
`f7e13648950a6109949a8a16c4b3a8a0cac75f0d`. The earlier Windows42 attempt failed a
concurrency test's ten-second thread-state poll. Preserve its failure; an isolated
run and one diagnostic do not prove a cache defect or the precise scheduling cause.
The existing serialized collection now isolates that test class, with every
assertion retained. Full Python checks pass on both runtimes; producer assets stay
unchanged during that test-only build. Fresh43 preserves all failed42 bytes.

[Source bridge](source-bridge.json): only package-version metadata and the test
collection differ; all producer/benchmark C# is identical. Each evidence set keeps
its actual source and DLL/PDB identities. Windows43 packages were also measured
on Linux below; no claim of byte equality or another full refresh.

Windows qualification:362.7933064s, two selected producer builds, one pack/profile;
eight detached fixtures109750checks. Linux consumes those exact bytes: eight
fixtures109750checks, sixassets/24restore audits and12TRX verified, no repack.
Original Awk19/Lython118 per runtime and public native/funded probes pass on both
platforms, with root loans0. Only three pins in1565 original files change; all464
Windows compiler documents, including generated inputs, remain bound to symbols.
All222 prior staged assets are unchanged; six new packages/symbols give228 total.
Source CI is green on Windows/Linux; data CI is recorded separately after push.

ONE [full refresh](full-refresh.json), **2764.8607085s (46m04.86s)**, exact83 passed
steps/terminal0. Original8GiB RAM/1GiB repo/3600s guards pass immediately. Build65.61s,
correctness+disabled dynamic, complete .NET11 RC3 catalogs, official CPython3.13.2,
private native PCRE comparator, lifecycle/scaling/resources and verifiers included.
Core phase526.10s versus dev41's433.62s; full wall time rises149.34s. This cost
buys duration headroom: all178 Core rows qualify (121wins/1tie/56gaps).
Python40managed/37CPython/2equivalent/6inconclusive/5floor exclusions; PCRE79managed/
12native/1equivalent/9inconclusive/25excluded. [Reasons](python-unqualified-details.json)
and [ratios](ratio-summary.json) remain, including Chinese4.56x and email2.64x gaps.

[Catalog movement](catalog-movements.json) is not an isolated source comparison.
Retain AWS-full ordinary8968.24->10650.76us/+18.76%, and Pythonclass-run278.24->
309.83us/+11.36%, unchanged allocations. No normalization or favorable resampling.
Seven generated files are hash-imported once; root README retains four blocks,
six descending ratio tables/178rows. Six matching snapshot hosts/10checks pass
without rebuilding the measured graph. All eight Full DLL/PDBs are preserved.

ONE **73.977135909s** exact-package public Default-process ABBA cost control:
12workloads/240samples, minimum58.4739ms, fixed caller and benchmark. All results
and allocations agree. [Every cost, process observation and identity](package-cost.json).
Windows43 versus Linux42: native12.48->12.47us, imports10.28->10.30us, Core Unicode
Count2.94->3.02us, Python3.06->3.11us, PCRE12.55->13.03us. Finite/live7.16->8.36us
(+16.71%,112B), deadline7.59->8.78us (+15.54%,160B); both after-process medians
exceed both before. Retain those costs without causal source attribution. The
large accepted Count gains remain; no universal10% release gate is reinstated.
AWS8486.94->8527.10us/+0.47% has wide process movement; class-run297.30->284.47us
ranges overlap. Neither observation replaces the catalog or proves harmless noise.
No confirmation, tuned variant, new Status or repeated qualification follows.

Canonical raw under `artifacts/experiments/delivery-dev43-20261106/`:

- Full `BD4373CC8C599034AFBCA71A29F377A7746B497C612ED01B270DB44DAD561FEE`.
- Linux `7A5974B404161312CB1697765BE162C0E151F2FD25CDC8AF07FE51037BF6A3C5`.
- Windows `C81841A872FFC5E9D909B2089701F33B89726052EB7055DEDF21928C90F38367`.
- Restore inputs `922A1FC19230B35025CD853C79E203EE2A7227EEA1601BB70471025A18F61937`.
- Package costs `F73ACE11563592967B98DEF5BC6023A8FFD7CD3B706FC7367B2C61CBDB8A4B79`.
- Guards `CE51145E6FAD2D6F981BC01F562CA7CC3CB54212B1050E3FCD2F9730C0371078`.
- Test-recovery evidence `B6DEB5172C4A20FAA04448119AC7020D0D37B3D70F0D8A46191B742D502BBD83`.

The failed Windows42 archive remains separate in the sibling delivery-dev42 raw
folder (`92F3F0325104CFCAD4396E15881AED1C8BE19B6C51861CF294C781D4CF4D96FE`).
Published/staged bytes and prior raw evidence remain immutable. Broader PCRE2,
optional POSIX and remaining performance gaps keep their independent plan items.
