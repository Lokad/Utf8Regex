# Literal-root construction simplification: deferred

**Rejected and reverted.** A 4 KiB upfront root allowance removed the literal
runtime's mirrored construction record, factory transfer and unused backend
field. The prototype passed semantic/resource checks, but repeated public Count
penalties exceed the predeclared 10% gate. No further timing, tuning or package
qualification follows. Production keeps its existing admitted descriptor; no
accounting or ownership contract change is shipped.

The original record has 15 fields: actual `Unsafe.SizeOf` is 2,488 and its modeled
object charge is 2,528 bytes on the observed local .NET 10.0.13 layout. The
prototype reserved 4 KiB before root/child acquisition, kept major child admission
and work/depth bounds, and added a final original-authority checkpoint. Matching
kernel bodies stayed unchanged. A naive 2 KiB replacement would be too small.
These are layout/implementation facts, not a performance cause or heap guarantee.

## Bounded public comparison

One quiet-VM .NET 10.0.12 FullOpts/R2R-off pair, then its sole identical
candidate-first confirmation. Xeon 8573C CPU 2; both quiet gates 0% busy / other
observed dotnet CPU ticks 0. Same frozen host, DLLs, inputs, commands and counts;
no VM build/restore/pack. Each pair runs three existing constructor commands
(eight instances, five samples), two public Count commands and one public
PrefixMatchLoop control. Caps: 45 seconds per command, 180 seconds per batch.
Completed in 79.731 / 79.525 seconds, every command exit 0.

Public literal workload: `literal/sherlock-en`, 899,232 UTF-8 bytes, 513 matches.
Microseconds, ordinary / compiled where the command supplies both:

| Operation | Initial before → after | Confirmation before → after |
| --- | --- | --- |
| Public constructor | 59.413 → 62.312 / 62.875 → 57.275 | 60.612 → 58.362 / 61.000 → 58.150 |
| Public first Count, fresh instance | 89.338 → 101.513 / 90.812 → 103.050 | 93.388 → 106.000 / 87.625 → 104.075 |
| Warm public Count | 91.940 → 89.010 | 89.215 → 103.085 |

First Count increases 13.5–18.8% across both observations. Confirmation warm Count
increases 15.5%. Its unchanged predecoded .NET Count control falls 4.7%; the
unchanged identifier-loop controls move +0.6% ordinary / −0.1% compiled.
The initial warm Count decrease does not establish equivalence. Constructors
allocate 57,520 bytes unchanged; first Count allocates zero. Faster private
runtime-construction phases (roughly 31–45% in confirmation) do not qualify
public matching costs. The precise cause of the regression remains unproved.

The method-token-family preflight selects **StructuralLinearAutomaton**, so it is
another unchanged control, not affected literal-family performance coverage.
Its confirmation constructors cost +4.9–6.1%; identifier constructors +4.5–5.4%.
Warm ordinary Count has one short average per process (20 calls); constructor
rows report five-sample medians/ranges. Preserve those limits: no universal
steady-state ceiling, default-PGO gain, runtime11 result or CPython Status claim.
The family and identifier controls retain decode/predecoded comparison lanes;
the literal Count command supplies the predecoded .NET comparator only.

## Correctness and provenance

Prototype Release checks passed: 186 construction/layout checks, then 988 public
and construction checks including final cancellation/publication; PythonRe 250,
PCRE2 301, no skips. They cover cutoffs, child admission, failed-scope loans,
original authority, captures/scalars/RTL/NB and native public operations. All four
tested Core copies equal the measured candidate. The production proposal and its
new accounting assertions are reverted together; no failing test is disabled.

Source parent `c6dade89`; frozen host
`1294A402001B52F89AF054489D6E26A3ABDE6A263FB21EB21105236A865D9A28`.
Before Core `8EA6A50F778841EE0A2C1ECAB11201A1839F4DBF25ADDC68EAEF37E7A47185E8`;
candidate `AE57907F66F948B268557B8C218473D735D1B4D50F27493EE26DAA8D2C005367`.
The overlay runs outside a Git checkout: CLI `SourceCommit=<unknown>` and false
dirty flags are not a clean-source claim. Frozen hashes and the [patch](candidate.patch)
bind the comparison. Corpus/dependency symlinks are read-only inputs to the new
stage; existing staged packages/hosts are not overwritten.

[Initial summaries/logs](measurement.json), [sole confirmation](confirmation.json),
[identities, archive hashes, gates and decision](receipt.json). Full private
receipts remain under `artifacts/literal-root-construction-20261020`.
Earlier experiments, checked52, scalar.115/older immutable bytes and snapshots
remain unchanged. Current source CI `39353359533` is terminal green on both
platforms; the reverted prototype was never pushed or packaged.
