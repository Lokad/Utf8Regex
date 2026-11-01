# Checked ASCII token decode: deferred, fully reverted

At committed40c0659d/enginede2f5e67, investigated an explicit checked ASCII
branch in the existing CharacterRunner.TryDecode. Its offset guard, non-ASCII
Rune decoder/status handling, all numeric charges and interruption checkpoints
were unchanged. No new framework, mode, selector, allocation or unchecked API.
No production change or prototype-only test remains from this experiment.

Read-only literal/date-hot probes each consumed two million correct results.
Date-hot baseline Tier1 class/literal arms still call Rune.DecodeFromUtf8; the
candidate handles ASCII inline and retains decoder calls for non-ASCII. Token
code1287->1469B, standalone decode281->105B. Codegen only proves those operations
exist; it does not establish their share of public cost or a speedup.

Four new public checks cover all128ASCII and Unicode width/surrogate-gap
boundaries through subroutine-forced literal/class/dot tokens, nonzero origins,
EOF, retained captures and finite/unlimited policies. Focused265/full6490PCRE2
Release checks pass per runtime on .NET10/11. Initial fixture compilation used
coordinates on MatchContext instead of its existing Value; corrected before
test execution, failed log preserved. After full restoration261focused checks
pass again per runtime.

The [sole public comparison](comparison.json) completed63.804s,17workloads,
minimum37.173ms samples. Warm1.5s/min1000, independent60ms calibration, five
alternating samples, full result consumption. All allocations unchanged.

Datehit-0.91%, miss+.55%, densemiss-1.47%, denseCount+.29%, finitehit/miss
+5.74/+2.85%, deadlinehit/miss+2.73/+3.88%, no-digit miss+8.91%, nonwordCount
+2.18%, Unicode-heavy+7.54%, subroutine+1.93%, boundedcontrol-7.42%, literal
control+3.98%. [All observations](summary.json) are retained. Costs fit the10%
allowance, but useful changed-route benefit is insufficient to justify the
additional branch. No noise confirmation justified, none ran; no variants.

Immutable archives under
`artifacts/experiments/pcre2-ascii-token-decode-20261101-deferred/`:

- `evidence.tar.gz`: exact before/after DLL/PDBs, source/patch, public driver,
  both codegen probes, builds/raw measurements and original read-only attribution.
  SHA256 `321600A771E7A569DEA3975F70A3A155C03F96EC06405EE4A768557B72539C66`.
- `correctness.zip`: exact Windows candidate source/tests, matching both-runtime
  libraries/PDBs/test binaries and focused/full/failed-fixture logs.
  SHA256 `07CC7F6943566E93B0010F649C2B23469212E4ADFB99ECAE614D81CE02D23D5D`.

No benchmark snapshot/package changes. Earlier accepted instruction/request/token
borrows and ASCII boundary reuse remain. Move to the coherent cumulative and
delivery gate rather than continuing token-codegen variants. Optional PCRE2
scalar projection and POSIX remain separately unfinished.
