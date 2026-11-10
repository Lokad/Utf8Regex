# Private capture-order output option — rejected

All production changes restored. A default-true private option let only string/
UTF8 FindAll omit no-frame latest-value closing journals. Detailed/public cursors,
frames, assertion/history, balancing and opening journals retained their behavior.
It required four-file flag plumbing and a changed internal Core call signature.
Pcre2 source was unchanged but rebuilt against each matching Core ABI. No public
input-validation option/semantics changed; no cache/backend or unchecked API.

One134.576s quiet VM.NET11 RC3 ordinary-process ABBA per validation policy,
15 public lanes,600 samples >=30.444ms. Explicit distinct Core/Python DLL+PDB
pairs, identical caller and input hashes,8CPU startup then CPU2 affinity. Every
result checked after timing.75ms calibration/one confirmation,180s cap. One
worker's1GiB memory admission covers bounded retained outputs/runtime headroom.
A missing worker import was fixed before any timing; matching producers were reused.

| Public operation | Validate before -> after us | AssumeValid before -> after us |
|---|---:|---:|
| many-capture strings |723.367 ->1037.376 (+43.41%)|733.260 ->702.634 (-4.18%)|
| many-capture UTF8 |676.770 ->637.286 (-5.83%)|668.528 ->662.325 (-.93%)|
| finite/live strings |888.143 ->882.899 (-.59%)|906.987 ->888.785 (-2.01%)|
| detailed-order control |929.433 ->1213.041 (+30.51%)|1237.651 ->1117.881 (-9.68%)|
| one-capture split control |138.010 ->159.212 (+15.36%)|137.082 ->149.323 (+8.93%)|
| twelve captures |121.695 ->112.882 (-7.24%)|118.358 ->94.460 (-20.19%)|

Every listed range overlaps; the full summary retains substantial process/
sample movement. These are inconclusive effects, not demonstrated regressions
or public gains. Unsafe.SizeOf reports368B state before/after. Many-output
allocation242008->241800B (-208B) and twelve-group53776->53416B (-360B) are real,
but insufficient to justify the new option without clear operation-level value.
Funded public-cursor control peak417B and parent17/refund0 stay unchanged; that
control does not establish the private FindAll path's modeled peak. No second
measurement is needed for this complexity/value rejection. No timed variant,
threshold sweep, catalog/package matrix or staged-package mutation.

Candidate full Python5086/target and focused Core285/target pass. Strengthened
existing completion tests check strings/UTF8 and retained detailed LastIndex under
both policies. Restored Windows/Linux516 focused tests/target pass. Only those
independent guards remain. Source/no-frame audit next investigates coalescing
obsolete close markers while retaining the latest one, with no output option;
assertion/history, opening/balance journals and live frames must retain undo.

All caller/protocol/patches/producer graphs/results are copied offVM/hash verified:
artifacts/experiments/python-value-capture-order-20261111/evidence.tar.gz SHA256
340C96006E9C218AF3FCDE245A715D8E14E4E502381424BD0E7CB0C3F95D0A11;
restored Linux54BD1BEB40B41828B69F69F5B02B0016954480325EABFD4FA38C4FE255E7F31D.
Complete summary.json is alongside. All workers closed; immutable dev.67 remains
accepted. No performance-parity or hard lower-bound claim follows this rejection.
