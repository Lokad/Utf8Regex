# Dev.13: fixed-original cost gate rejected

Source **b8099e59**, common **0.5.0-dev.13**, is **not qualified**. No packages were
created. Windows/Linux package qualification, original-consumer execution and a
Full benchmark refresh were not started. The prior immutable dev.12 candidate
and its evidence remain unchanged.

The freeze includes the accepted Python boundary-only retry improvement. Core
and PCRE2 runtime source are unchanged from qualified ea01c3cd. Exact adapter pins,
both targets, owned scalar semantics and resource/lifetime contracts remain.
Source CI41505175693 passed on Windows and Linux; that is separate from this
rejected operating-cost result.

The complete gate measured **172 entries once**: original147 plus the unchanged
19 current controls, adding six representative boundary Split/Count/finite/
deadline/capture/nonboundary controls. Original archive333FF4E5 was reverified;
all six DLL/PDB baseline assets were freshly extracted and matched against the
prior qualified baseline. Existing driver definitions changed only source/version
expectations. Named inventories, outputs, allocations and every lane's duration
were checked.

The one bounded batch took **790s wall time**, **787.984s** summed part times.
All parts finished; minimum sample **29.350ms**. **171 entries fit the10% allowance.**
The remaining public operation failed:

| PythonRe Search | Original a40bda03 | Dev.13 | Change |
| --- | ---: | ---: | ---: |
| Pattern `needle`, subject `xxneedlezz` |77.357ns|85.786ns|**+10.895%**|

All five paired changes are +10.81..11.35%; before/after spreads are1.0077/1.0051.
Allocations remain zero. This is a stable failure, so **no noise confirmation**
was spent. No successful section or stable failure was replayed. Other171 entries
have maximum adverse7.539%; all six added boundary entries improve at least74.86%
versus the fixed original. These positives do not override the rejected gate.

[Full assessment](cumulative-acceptance.json), [verification](verification.json).
Independent predecoded and decode-then comparators, finite limits, live tokens,
deadlines and complete output consumption retain the existing protocols. Int
result batch caps in the added section bound retained values; string batches keep
the existing conservative payload cap.

Original copied consumer archiveCBD35E99 was also verified. All1,565 files were
compared after staging: only three project pins changed dev.8->dev.13. Package,
capture, consumer, restore-audit and Full scripts were prepared but not executed.
This preparation is not consumer acceptance.

Next investigate dispatch cost before another freeze. [Read-only IL inspection](policy-inspection.json)
of the exact frozen assets shows Python's four-nullable-test finite-policy getter
is97 IL bytes; Core already snapshots the same immutable decision (7 IL bytes).
This identifies an investigation candidate, not its share of public runtime or a
demonstrated speedup. Original limits, interruption and admission must remain.

Immutable archives under `artifacts/experiments/delivery-dev13-20261102/`:

- `cumulative.tar.gz`: source, drivers, exact DLL/PDBs, corpus inputs, preflights,
  all raw timings and assessment. SHA256
  `F0ECEE607AA6782944DE5C73D1012B47FB8B9CB95D379BE9958A18BD058DDB6B`.
- `local-preparation.zip`: local preparation scripts, pin audit, source/CI/cost
  assessment inputs; hash in verification.json. No package qualification claim.

The initial tar command wrote inside its input directory and failed the GNUtar
self-change check. That failed attempt is preserved on the VM. The corrected
archive was written outside the input directory; measurements were not rerun.
