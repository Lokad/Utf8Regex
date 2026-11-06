# Literal-family Match classification: rejected

Parent `fd8c6b3f0ae7a5fa217e07273acfc91424125e3c`. The prototype forwarded an
existing validation result into literal-family Match and reused it instead of
classifying the subject again in cursor construction. The existing constructor
already chooses byte-only projection on ASCII; this was a duplicate-scan proposal.
Both production changes and the three added test rows are **fully reverted**.

The route assessment was incomplete before measurement: the actual
`common/match-word` ASCII workload returns through
`Utf8LiteralCompiledEngineRuntime.ExecuteMatchWithoutValidation`, before the
changed cursor path. Prepared execution kind alone did not establish coverage.
Mixed Unicode reaches the changed path, but showed no useful improvement.
Do not treat movement on the actual ASCII or finite controls as a source gain.

One bounded Default-process ABBA comparison completed all four producer runs:
eight public workloads, 160 samples, minimum58.277ms, identical results and
allocations. Normal eight-CPU startup precedes CPU2 affinity. Each workload warms
for1s/min128 calls, calibrates independently toward60ms and takes five samples.
[All observations](summary.json) retain process medians, ranges and adverse costs.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual common Match | 0.20965 | 0.20793 | -0.82% | 0 |
| Actual common Match, compiled | 0.20887 | 0.20564 | -1.54% | 0 |
| Family ASCII miss | 0.36370 | 0.36308 | -0.17% | 0 |
| Family mixed Unicode | 3.78558 | 3.80267 | +0.45% | 0 |
| Unchanged finite/live Match | 0.44567 | 0.42302 | -5.08% | 112 |
| Unchanged deadline/live Match | 0.71999 | 0.72290 | +0.40% | 160 |
| Unchanged single-literal Match | 0.18701 | 0.18275 | -2.28% | 0 |
| Unchanged family Count | 9.38336 | 9.43084 | +0.51% | 0 |

The four producer workloads finished successfully in44.600s combined. The whole
command ended134 after47.210s because the following separate BCL diagnostic hit
the20ms sample floor. Preserve that failure: there is no completed new comparator
qualification or catalog Status. No confirmation, variant or rerun. An earlier
untimed driver error read byte coordinates on NoMatch; its failure is retained.

Release code inspection retains Tier0 and separate FullOpts output; the latter
disables tiering only for untimed inspection. The exact-family block changes an
unconditional ASCII call into a proof/length check with classification for omitted
validation. This shows the code change, not coverage or a public speedup.

Focused Windows Release tests pass per runtime (.NET10/11): Core295, PythonRe117,
PCRE2166. They cover whole-input validity, scalar projection, branch order,
captures, offsets, finite admission, interruption and cursor ownership. Candidate
production code was restored on both hosts; all eight frozen Full DLL/PDBs remain
unchanged. No Linux candidate matrix, package or Full refresh was repeated.

Immutable raw under `artifacts/experiments/family-match-proof-20261106/`:

- `evidence.tar.gz`: before/after Core DLL/PDBs, unchanged worker and benchmark,
  declaration, preflight failures/successes, code generation, raw timings, scripts,
  production patch and restore proof; SHA256
  `C6427BD2B29705B25E5317DC9A7FEF3705444E93CAC458E6A63AC5A57CA14751`.
- `windows-evidence.zip`: complete test logs, source/test patch, analyzer and
  summary; SHA256
  `2F123A7A66EFBC0AFA5759B50F7807289B84EE8BFBDD68094EC3CA2D4DD5B2F1`.

No release contract or published benchmark figure changes. The qualified common
dev.43 packages and their all-green source/data CI remain the delivery checkpoint.
