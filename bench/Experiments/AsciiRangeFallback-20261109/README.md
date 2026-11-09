# ASCII-range fallback follow-up

Source `82f71c28` plus a selector-hoist correction. Compare with the **fixed
original `3fd56b21`** Core/Python adapter/worker retained by
[N8](../AsciiRangeRepeat-20261109/README.md), not with its intermediate producer.
The candidate selects the checked range loop once, leaving the scalar loop
without an extra per-character range branch. Eligibility, scan limits, capture
effects, storage, work units and original interruption otherwise stay unchanged.

One **75.677-second** ordinary-process ABBA, capped at 180 seconds, .NET 11 RC3
quiet VM; same original worker and adapter, deliberately changed Core. Five
independently calibrated samples/process/row of at least 20 ms after 500 ms
warmup. Full Python outputs and zero loans checked outside timing. Core probes
time MatchDetailed plus returned coordinate fields; full text is checked before
sampling. No confirmation or further tuning trial.

| Public workload | Original (µs) | Candidate (µs) | Change |
|---|---:|---:|---:|
| Long contiguous capture plus coordinates | 13.81 | 6.98 | −49.5% |
| Same, finite/live | 17.28 | 7.28 | −57.9% |
| Holey capture plus coordinates | 14.14 | 15.47 | +9.4% |
| Folded capture plus coordinates | 24.87 | 25.53 | +2.7% |
| Python one-capture strings, unlimited | 473.76 | 701.36 | +48.0% |
| Python many-capture strings, unlimited | 827.80 | 823.36 | −0.5% |
| Python many-capture strings, finite/live | 1,195.39 | 1,147.00 | −4.0% |
| Unchanged Core literal Count | 0.89 | 0.88 | −1.8% |

**Withdraw the bulk-repeat path, including N8's intermediate promotion.** This
reduces its holey/folded penalties, but the original one-capture processes now
take 461.7/477.4 µs versus candidate 692.9/705.0. Earlier original processes were
bimodal, so this is an observed public cost rather than proof of a universal
48% regression. It is still a substantial adverse consumer observation; the
main many-capture gap barely moves while the long-run improvement is a different
workload. Prefer the original scalar loop for this profile-focused delivery.
This weighs absolute consumer cost and priority, not a universal 10% gate.

Allocations remain unchanged in all 19 lanes. Unicode tuples/strings move
+3.6%/+3.8%, captured Count −5.8%/−6.5%, finite range cursors −3.2%, and early
literal +0.3%. [Summary](summary.json) preserves per-process medians/sample ranges.
Do not compose N8/N9 gains, normalize by controls or infer new native ratios.

The smaller constant-time mask proof remains: it replaces 128 bit probes with
two checked word reads and a population/width identity, preserving exact interval
eligibility. It adds no state, switch or matching loop. No isolated public
speedup is claimed for that retained simplification. Independent mask-boundary,
hole, block/max and interruption cases remain useful for current production code.
The temporary scan-unit contract paragraph is removed because matching returns
to its original per-scalar path. Published/staged bytes and tables are unchanged.

Trial and restored source are checked in focused Release runs on Windows/Linux,
.NET 10/11: Core 514 and PythonRe 5,009 per target, including long-run original
cancellation/deadlines, parent loans and finite admission. No package/catalog
qualification or third timing. The original scalar-runner file is restored
exactly; subsequent changes need a distinct demonstrated cause.

Intermediate N8 CI `42656018288` failed on both platforms: the two ASCII cases
in `Utf8TerminalCaptureRepeatTests.OriginalInterruptionInsideTerminalRunReleasesScratch`
completed before their step hook fired, so neither interruption was triggered.
After restoring the scalar loop, all 34 tests in that fixture pass per target on
both platforms using the already rebuilt binaries. Tests were not weakened;
full CI follows the final rollback commit.

Raw trial evidence is copied off the VM at
`artifacts/experiments/ascii-range-fallback-20261109/evidence.tar.gz`, SHA-256
`281A5EBA584E8EFB10CD54B61051AB6565F749BA71CD056B26F75649F6B261CF`.
Restoration checks and final source/binary identities are retained alongside it.
`restoration-evidence.tar.gz` SHA-256:
`DE4D67D61D9467C15B4C28BC6DD5366690B20A2D837C9050BC5E6AA8B6619317`.
