# Direct Core grammar input: internal native-compilation prerequisite

Producer **bcff91d178929f9152940865c60ae818e9bcba08**; frozen baseline
**27e28db0b13570b94a163770599549a28ccc15e7**. Core's existing authoritative
grammar now accepts a borrowed UTF-8 span directly. This does **not** deliver a
public native Compile API or complete H3 pattern/metadata ownership.

The existing construction input value now supports ASCII syntax peeks and direct
scalar reads. Native patterns undergo strict, interrupted full-pattern validation
before parser storage acquisition. Parser/context state borrows that input only
for the call; the resolved tree uses the same admitted storage, grammar and scalar
lowerer. Text input retains unpaired-surrogate behavior and UTF-16 diagnostics;
internal native diagnostics explicitly use byte offsets. Only selected lexemes
(names/properties) are decoded, while the existing literal IR still stages text.
There is no second grammar, full-pattern decoding pass or unchecked access.

Public native compilation remains blocked by retained pattern/metadata ownership
and string-dependent preparation. No claim that the byte overload requirement is
closed, and no published/staged package was modified.

Release checks on both .NET 10/11:

- Existing text/parser/construction/managed/API selection: **2491/runtime**.
- Initial native grammar cases: **186/runtime**, comparing actual scalar matching
  and captures against existing public text expressions, including caller-buffer
  reuse. Additional cases cover Unicode names/ranges/subtraction, escaped surrogate
  pairs, NUL, combining marks, verbose mode, sparse/shared groups, conditionals,
  ignore-case, RTL and NonBacktracking.
- Full Core after those cases: **40353/runtime**.
- Final deadline/admission additions and affected guards: **236/runtime**.
- Affected PythonRe **289/runtime**, PCRE2 **270/runtime**.

Native malformed-input checks reject before parser allocation; cancellation,
deadline and work limits interrupt validation before a malformed tail. Byte syntax
diagnostics agree with text error kinds and source-coordinate conversion. A large
native comment uses the same modeled storage peak as a small one. Initial compile
errors (a scalar cast and test exception-property name) and passing logs remain in
the immutable archive. [Verification](verification.txt).

One [public construction A/B](comparison-first.json), **25.76s**, then its single
predeclared [noise confirmation](confirmation.json), **36.59s**, both under 120s
caps. The initial scalar samples drifted from 86us to 52us while tiering settled;
its +50.4% median cannot settle the cost decision. Confirmation warmup grew from
750ms to 2s/lane; it retains the same three construction patterns and an unchanged
literal Count control. Each lane calibrates independently toward 80ms, with three
alternating samples. Minimum actual samples **46.21ms / 49.97ms**. Environment:
Ubuntu 24.04.4, Xeon Platinum 8573C, CPU mask 4, .NET 11 RC3.

| Public text compilation, confirmation | Before (us) | After (us) | Change | After funded + close (us) | Before/after bytes | Funded bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Literal | 48.588 | 47.722 | -1.78% | 51.666 | 40480 | 40592 |
| Scalar captures/lookbehind | 83.916 | 84.722 | +0.96% | 100.495 | 55256 | 55368 |
| Unicode captures/lookbehind | 100.742 | 100.643 | -0.10% | 109.767 | 56896 | 57008 |

Confirmation literal Count control **0.423 -> 0.419us**, zero allocations. Initial
scalar Count control **98.934 -> 91.244us**, unchanged 3872 B/op. Funded matching
lanes retain their feature costs; these do not measure grammar execution. Public
compile/close uses the existing explicit compilation policy and checks refunds;
result/checksum consumption follows timing. Funded compilation adds the existing
112 B, not a new parser allocation. No isolated-parser timing or public native
speedup is reported. [Initial driver](Compare-first.cs.txt),
[confirmation driver](Compare-confirmation.cs.txt).

Samples still vary substantially (literal compilation 32.51–48.44us after, scalar
70.00–91.47us after), including shifts on unchanged controls. Accept this as bounded
cost evidence with unchanged allocations and near-equal paired medians, **not** a
precise equivalence/parity verdict. No further confirmation, tuning campaign,
catalog refresh or package matrix. Final cumulative/current-source qualification
remains at the coherent delivery boundary.

Immutable evidence under artifacts/experiments/native-core-pattern-20261030/:

- native-core-pattern-bcff91d1.tar.gz: exact before/after Core DLL/PDBs, source
  bundle, affected-project build, both drivers/preflights and raw samples. SHA256
  `438D351A5C091B281ABBD522FF94E23487A0F72430AFA669148C7FC05E69C403`.
- correctness-logs.zip SHA256
  `8C5388DB60427E46C3E7FAD52C0D7B0ED7BE66CFAA58C4FD77EB8BCDBDEA3E06`.

Initial VM fetch requested `master` from a HEAD-only source bundle and failed
before build/timing; fetching its actual HEAD succeeded. Existing archives and
packages remain immutable. Continue native retained pattern/preparation work;
actual downstream package acceptance remains unfinished.
