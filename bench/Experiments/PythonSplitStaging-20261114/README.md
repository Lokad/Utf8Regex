# Captured splitting reuses staging interruption checks

Accepted source `cdfc889b39ef5625f35b99c1fbc6864009bee765`, against frozen Python
`45e68632` / unchanged Core dev93. SplitScalar no longer checks immediately after
each captured value's AddResult: that shared helper already checks the SAME
original invocation before staging, including absent captures. Decoding, capacity
admission, next discovery and final publication keep their checks. No new state,
API, cache, owner, execution mode or accounting framework. Numerical work falls
by one step per captured value. [All rows and producer identities](cost-summary.json).

One **80.062s** ordinary-process ABBA, ten public rows, five150ms-target samples
per row/process,50ms calibration floor/20ms sample floor,150s cap. Normal8CPU
startup then existing placement, .NET11.0.0/default tiering/PGO. Complete strings
and detailed capture markers materialize during timing and are all checked
afterward, including null absent groups. Corpus11776B,2048 captured separators,
4097 returned values. The independent overlapping-semantics BCL match oracle
constructs expected Python split shapes before timing; native parity is not measured.

| Complete public operation | Before us | After us | Observed change |
|---|---:|---:|---:|
| Captured split strings |262.198|250.492|-4.46%, overlapping|
| Finite limits/live token |415.363|381.206|-8.22%, separated|
| Funded/deadline/live token |872.054|828.757|-4.96%, overlapping|
| Detailed split |385.386|395.310|+2.58%, overlapping|
| Unicode/absent captures |209.793|204.468|-2.54%, overlapping|
| Limited split |3.160|2.952|-6.58%, separated|
| Capture-free control |244.794|259.292|+5.92%, overlapping|
| FindAll control |393.140|395.349|+0.56%, overlapping|
| Unchanged Core / .NET controls |12.152 /22.818|12.022 /22.756|-1.07% /-0.27%, overlapping|

Accept the demonstrated finite-cost reduction and simpler staging invariant;
ordinary effect sizes remain uncertain. Retain the adverse detailed/capture-free
observations: capture-free after-process medians274.277/241.827us versus
244.261/244.835us before. No normalization, confirmation, threshold/decoder/polling
variant, new CPython ratio, canonical table refresh or package/Full qualification.
All ten allocation medians remain identical; main274648B, finite278848B,
funded/deadline278920B. Funded root loans return to their held amount after every
call and zero after program disposal. This is not a total heap/RSS bound.

Windows.NET10/11 and Linux.NET11 each pass the full affected Python suite.
Four focused tests inject original cancellation or deadline expiration at every
observed captured-split checkpoint, for both string/detailed output, Unicode and
present/absent groups. They require no partial publication, original token/timeout
identity and validity of retained earlier results. Existing independent split
oracles, supported options, finite work/depth/working/result admission and cursor
lifetime checks remain. Only the Python adapter/tests built; matching Core pairs
and every compiler-document checksum verify. Frozen Python DLL/PDBs differ;
Core, common caller, reference and inputs are identical across producers.

The initial private caller mistakenly used IDisposable syntax for Core Utf8Regex;
compilation failed before preflight/timing. Only the common caller was corrected;
exact already-built producers reused. Failed raw preserved, no earlier samples.
Raw verified offVM before keeper release; all workers/collectors terminal:

- Completed `artifacts/experiments/python-split-staging-measure-20261114/evidence.tar.gz`,
  SHA256 `37BB76013236E69606F424B2C74BCC944B8DC7253A584931BC08CC9FC1BC1ADA`.
- Setup-only failure `artifacts/experiments/python-split-staging-20261114/evidence.tar.gz`,
  SHA256 `6C318A5444EF09780AE43E81C12B74E0DAF884C2A28F46A8A69209199A72C8F8`.

Qualified dev93 assets and presentation remain immutable. This bounded trial is
closed; remaining capture/search/output gaps require a distinct demonstrated cost.
