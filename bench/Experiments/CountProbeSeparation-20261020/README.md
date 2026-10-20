# Keep alternative literal Count probes in benchmark tooling

Four alternative Count probes had only benchmark callers but crossed the facade,
runtime base, inspection dispatcher and literal runtime in the shipped library.
Move their eligibility and dispatch into the existing benchmark `Utf8MatchInspection`.
Delete 21 shipped forwarding/dispatch methods, including the unused no-count
helper: 157 deleted lines, three visibility changes, 154 net shipped lines removed.

Production's factory creates literal wrappers exactly for ExactLiteral/LiteralFamily;
the benchmark probes use that existing prepared engine kind as the same gate.
Their original per-kernel eligibility remains. The three production counting
kernels retain identical bodies and real matching consumers; only private-to-internal
visibility changes for friend access. Prepared search already exposes its live
counter. No new runtime accessor, execution mode, classifier or accounting framework.

Public matching, scalar/UTF-8 semantics, captures, interruption, resource charges,
construction admission, result/cursor lifetimes and allocation algorithms are
unchanged. The removed inspection surface is internal. Remaining match probes
and their live kernels are outside this bounded Count-only deletion.

Focused Release validation: 1,457 Core checks plus eight strengthened probe
cases; 121 PythonRe and 188 PCRE2 checks, no skips. These exercise public literal
Count/Match/IsMatch, offsets, finite policies, cancellation/publication, scalar
boundaries and malformed tails. Tooling regressions verify positive eligibility,
unsupported-route decline, borrowed views, complete scalar tails and agreement
with public Count in ordinary/compiled/RTL modes. The benchmark dependency closure
build and `--inspect-pattern 汉字 CultureInvariant` also pass.

All five affected build/test/benchmark outputs load the same Core DLL; private
validation and body hashes are in `artifacts/count-probe-separation-20261020/validation.json`.
No live method body changes, so no new performance comparison or package
qualification is appropriate. The separate [rejected byte-pair experiment](../BytePairSearch-20261020/README.md)
retains its sole public comparison. Immutable scalar.115 and earlier artifacts
remain unchanged. Full R06/R07/R08/R09/R10 is still open.
