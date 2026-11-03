# Copy one bounded output piece directly

Accepted source `d54d9cf2`. `Utf8InvocationOutput.Copy` now uses one checked
`Span.CopyTo` for at most4096 bytes. That operation already provides overlapping
memmove semantics, so these copies need no direction calculation or block loop.
Larger copies retain the original direction-aware bounded loop. Empty copies
still charge/check once; nonempty copies still charge/check before and after.
No new mode, allocation, resource policy, unchecked access or public contract.

One **102.349s**, twelve-case public A/B against frozen `5fefe08a` graph binaries;
only Core rebuilt. Quiet VM, .NET11RC3, normal startup then CPU2,1.5s/min64 warmup,
independent calibration and five alternating samples, minimum49.525ms. Complete
UTF8 outputs are checked after timing. The diagnostic BCL lanes materialize equal
UTF8 output, including encoding/copy for destinations; they differ from catalog
string-output comparators and do not claim equal finite-resource policies.

[All observations](summary.json) show ordinary/compiled actual replacement about
2% faster, native-byte replacement2.51%, destination1.47%, finite/live2.67% and
deadline/live5.82%. Allocations are unchanged in every case. Mixed Unicode,
overlapping alternatives and dense adjacent replacements improve1.5-2.3%.
Unchanged Count moves+0.07%. Primary replacement spreads are at most1.019 except
native-before1.048. Large-copy and no-hit controls remain bimodal, about1.5-1.95;
their median movements are observations, not demonstrated source gains.
The consistent target lanes and stable Count control need no confirmation.

Focused Release checks pass on both .NET10/11: Core1446, PythonRe421, PCRE2476.
Overlap tests cover both directions/self, empty/one-byte/SIMD-size and4095/4096/
4097/8192/20000-byte boundaries with exact unchanged work charges. Existing
pre-cancellation/deadline, resource admission, original loans and public
replacement tests remain. No package/full-platform replay; dev.19 bytes and the
frozen43m08s Full receipt remain unchanged. No cumulative fixed-original claim.

Immutable evidence under `artifacts/experiments/small-output-copy-20261103/`:

- `evidence.tar.gz`: `A6B906510FDC964B6731683774B6583AC1D0C7371BBA42622BA9250C68237FB7`.
- `correctness-source.zip`: `0AAED38CAD0B3108BC2CC5DA4836900FA718566BA3F269CCE9E99568DC29B481`.
Clean-source selective publication changes only the two `common/replace-words`
ordinary/compiled records. Compiled ratio moves3.416x ->3.399x, ordinary0.740x;
both13648B. The new absolute catalog medians (101us Core,29.8us compiled .NET)
are higher than the earlier point (65.8us/19.3us) and the causal A/B. That point
movement is not attributed to this source change; the separate matched A/B is
the effect-size evidence. No extra timing campaign. Minimum selected sample
27.815ms; parity verification, six descending tables/178 ratios and reused Core
snapshot guards3/runtime pass. Other rows retain their recorded identities.

- `selective.tar.gz`: `F307D87FD6138181D8AF6B518879E9AFCBABBFFC5D85DEE3ABC8A16B02D4E982`.