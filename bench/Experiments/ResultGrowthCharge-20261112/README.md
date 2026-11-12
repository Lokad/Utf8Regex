# Zero-capacity result-charge guard — rejected

The five-line candidate skipped result-byte charging when an existing staging
buffer had spare capacity. Its per-item workspace/interruption checkpoint and
all actual preallocation/result-capacity requests stayed. Source establishes
2040 zero-byte requests among2048 appends (eight growths); this is a call-site
count, not an executed-code speedup. No new state, flags, control type or engine.

One **100.862s** public comparison, under the declared110-second cap, ten rows and
two input policies in ordinary-process ABBA.400 samples, minimum64.321ms,largest
retained batch72.432MiB. Normal eight-CPU startup then CPU2 affinity,.NET11.0.0,
defaultPGO,2s first-row/500ms later-row warmup,75ms target/20ms floor. Complete
retained results/checksums are consumed after timing. Explicit distinct Core
DLL/PDBs; identical Python/caller/reference, inputs and result checks verified.
Validate checks whole UTF8; AssumeValid trusts valid unchanged borrowed input.

**All20 target/control ranges overlap.** Many strings604.225→577.691us strict
(−4.39%),614.916→598.019 trusted(−2.75%). Bytes−5.76/−.18%,split−5.67/−9.88%,finite
strings−3.06/−4.68%,funded/deadline−4.39/−1.88%. Detailed cursor−3.55/+7.61%,generic
Core Count−.59/−4.14%,Core literal+9.96/−3.00%,.NET+.47/+.07%. Keep the uncertainty:
no settled useful gain or causal regression, no normalization/parity claim.
[All per-process medians/ranges/costs](cost-summary.csv). Literal split is affected
staging despite its historical`control/`row name; detailed cursor includes the
unchanged caller's List/ToArray. The .NET Count control is not output parity.

Allocations are unchanged:many242008B,one45168B,bytes242008B,split159896B,
finite246208B,funded246280B,detailed607144B,Count/literal/.NET0B. Shared modeled
peak357073B and program+parent held45425B remain; final0. This is not heap/RSS.
No confirmation, variant, Full refresh or package qualification. Restore and
close rather than retain the extra branch for an uncertain saving.

Windows/Linux candidate and restored Release:Core326/Python1238 per.NET10/11.
Existing exact cumulative-growth, boundary, denial, ownership, finite/result,
input-policy and original cancellation/deadline checks remain unchanged.
342Core PDB documents/side:only this helper and generated revision differ;
reuse the qualified baseline and matching unchanged document hashes instead of
repeating source/package qualification. Common Python is frozen. Core source
and matching live pairs are restored exactly; Windows reuses existing pairs,
Linux net11 reuses its frozen pair and only the missing net10 Core target rebuilds.
Existing matching test binaries are reused. Frozen measured candidate pairs stay
unchanged. SourceCI43171289113 is GREEN Windows/Linux, closed.

Raw local artifacts/experiments/result-growth-charge-20261112/ are canonical,
hash-verified offVM, all workers terminal0/keepers released:

- gate-evidence.tar.gz:D176F5FFF4ACCAC4C70D0E6FB34B20F276A003BE3D7C6DD08A1BD5FC345798D0
- public-evidence.tar.gz:D1ADA14EE252F0FE14EDEFA2BBE86DA30C807371D1CD3DB014FD76E79A6E4740
- windows-evidence.tar.gz:870CAED4552152CEF6F49874F9E7E0D8BBE961318BDA95B4C2BBDDADA058602C
- linux-restore-evidence.tar.gz:EDB4BE4AE079E99A9AA1EC0B01B1C2881AC55FD4FD6A6ABF526C25A9D823E1C3
- windows-restore-evidence.tar.gz:71B99455E18C1F01601873542FCED076D4685BE36EA2F1F0BF3CDBE128626B5E

Timed Linux Core baseline5ED67903CA9A5316D730F9F603BCA8913DDC98E293F0F4A4CEEA0AD6824B0464,
candidate505925D547A8F5C5B66E16C3A7F36702E6DA1F8464EC26F85A3576035C805E4C.
Common callerA6D4B71753B35FB3E11C4DBFEB1A856528E10F765998A6EE8A5B7DB27D0E78C4.
Full DLL/PDB/reference/process identities and source patch remain in raw evidence.
No published/staged bytes or historical generated tables change.
