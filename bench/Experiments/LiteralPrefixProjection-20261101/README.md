# Project an ASCII literal from its prefix

`MatchExactAsciiLiteral` only needs bytes before the hit to derive its UTF-16
start. Bound its existing ASCII check to that prefix. Full-subject validation,
search, prefix decoding, allocation and invocation controls remain unchanged.
No API or accounting contract changes; staged packages are untouched.

One [public comparison](comparison.json), **86.160s**, quiet Ubuntu VM/.NET11RC3,
CPU2. Frozen8d source before, one archived patch after; same typed public-call
IL in isolated contexts. Thirteen cases/four lanes, warm1s/min64, independent60ms
calibration, five alternating samples, minimum45.087ms. Complete outputs checked
after timing. No confirmation. [Preflight](preflight.json), [summary](summary.json),
[host](host.cs.txt), [lanes](lanes.cs.txt).

| Public Match | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Early | 2.613 | 1.203 | -53.95% |
| Early compiled | 2.609 | 1.439 | -44.82% |
| Middle | 3.005 | 2.280 | -24.13% |
| Late | 3.623 | 3.581 | -1.14% |
| RTL early | 2.844 | 1.947 | -31.54% |
| RTL late | 2.600 | 2.621 | +0.81% |
| Unicode before | .668 | .667 | -.17% |
| Unicode after | 5.866 | 4.664 | -20.48% |

Allocations are unchanged: ordinary0B, finite/live120B, deadline/live168B.
Finite/live moves +5.97% (all-pairs range5.27..7.17%); deadline/live-10.04%.
No-match-.66%. The unchanged Count control moves-7.28%, and contrasting Python
Search-17.39%; these observations remain explicit and are not attributed to
the changed projection without path evidence. They do not explain the much
larger affected Core gains. No measured regression exceeds10%.

Predecoded/decode-then-.NET comparators return equal byte and UTF-16 ranges;
their byte coordinates include prefix encoding. Untimed [FullOpts disassembly](coverage-jit.txt)
from public-only preflight proves the changed method is exercised (260/264B);
it does not attribute default-tier performance. No diagnostic replay is timed.

Focused Release checks pass on net10/net11: Core12871, PythonRe408, PCRE2871
per runtime. Added public tests cover scalar/NUL prefixes and tails, RTL,
compiled mode, full malformed-tail rejection and original-token pre-cancellation.
Existing finite/deadline/work-cutoff, alias, Unicode and offset tests remain.

This is a newly found redundant projection scan. It does not reopen the closed
validator-selection or checked-access campaigns. The fresh8d catalogs and dev.2
qualification remain historical evidence; no unchanged package/full refresh was
repeated. Python's65542B early-Search validation cost is a separate observation.

Immutable `artifacts/experiments/literal-prefix-projection-20261101/`:

- `evidence.tar.gz`: exact DLL/PDBs, measured driver, patch, preflight/codegen and
  raw observations, SHA256 `E64E724BF95F041188A052E932C29C8DA6CB4B5E73823714E229815FD5224FAC`.
- `correctness-source.zip`: source, driver and focused logs,
  SHA256 `02DBE8FA46899393E83DF2C3D088FB84373E9064F017BAC6FCCA67901A4169CA`.
