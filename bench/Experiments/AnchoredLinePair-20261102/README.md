# Deferred search for anchored starts

**Rejected and fully reverted.** Checked search for LF followed by the required first byte nearly
halves actual imports Count, but two representative public misses exceed the
10% allowance. No runtime, fixture, contract, package or catalog change survives.

The prototype changed only the existing beginning-anchor seeker for an immediate
mandatory unfolded ASCII literal in multiline mode. It searched adjacent LF/first
bytes through checked spans of at most2048 bytes, overlapping one byte between
blocks. The same VM verified survivors; unsupported prefixes/options kept their
seeker. No retained metadata, allocation, engine, mode or unchecked access was added.
This concerns scanning cost, unlike the closed longer-prefix experiment that
could not remove an actual VM attempt.

One quiet-VM .NET11 comparison took **100.062s**, under120s, with16 public
Count/Match cases and minimum timed lane **28.764ms**. It reused frozen `ea01c3cd`
Full-refresh binaries before; data-only parent `ffbef55a` has identical producer
sources. Only Core was rebuilt with the patch. Benchmark/PythonRe/PCRE2 caller
DLL/PDB bytes match. Metadata revisions remain explicit in the raw identities.
Normal8-CPU startup then CPU2, managed2s/min512 warmup, BCL250ms/min128,
independent60ms calibration, five alternating samples and complete result batches
consumed after timing follow the existing comparison protocol. Compilation is
excluded from matching timers; predecoded/decode-then Regex are diagnostic lanes.

| Public operation | Before us | Prototype us | Change |
| --- | ---: | ---: | ---: |
| Actual imports Count, ordinary |31.646|16.176|-48.88%|
| Actual imports Count, compiled |31.543|16.898|-46.43%|
| Actual imports, finite/live token |36.924|19.215|-47.96%|
| Actual imports, deadline/live token |63.815|20.596|-67.73%|
| Dense LF miss |81.677|2.020|-97.53%|
| Dense false-prefix miss |91.965|103.567|**+12.62%**|
| Sparse anchored miss |0.942|1.261|**+33.81%**|
| Dense actual hits |97.970|99.430|+1.49%|

Allocation medians match for all16 cases. Dense false-prefix before/after spreads
are1.005/1.006; sparse-miss spreads1.018/1.003. These support rejection without
confirmation. The unchanged literal control also moves-15.86%; its algorithm was
unchanged, so that observation is not claimed as an isolated producer gain.
All results and other controls remain in [the summary](summary.json).
**No confirmation, variant, selected catalog refresh or package qualification.**

Focused Release Core 445 / PythonRe 545 / PCRE2 474 per runtime pass on .NET 10/11.
Boundary/offset/capture, every ASCII first literal, LF/CRLF/Unicode and malformed
tails, unsupported options, work/working admission and original cancellation/
deadline checks cover production entry points and the production seeker directly.
Initial fixtures incorrectly assumed a public route's work/storage minima and
the diagnostic hook cadence; those failures and corrected fixtures are preserved.
VM preflight initially lacked its AppContext-relative corpus; staging was repaired
before timing. The runtime prototype stayed unchanged throughout.

Local and VM source are restored; the experimental fixture is removed and
affected graphs rebuilt without repeating passed suites. The qualified dev.12
candidate, its42m04.30s refresh and all staged bytes remain unchanged. A broader
sparse/dense candidate design needs separate evidence; do not retune this trial.

Raw inputs, before/after DLL/PDBs, driver, all observations and restoration log:
`artifacts/experiments/anchored-line-pair-20261102-deferred/evidence.tar.gz`, SHA256
`234255597B52E50298E665EE3B30646DEECF393BF69DF5F44364003AB4948ECC`.
`correctness-source.zip`, SHA256
`FA3B53EB5FCB342E33D771C0B70433A6F12CA18CF22D5A08B9935DA2555FB66C`.
An initial archive returned a directory-metadata warning from writing inside its
input; it is retained separately, and the final archive writes outside that input.
