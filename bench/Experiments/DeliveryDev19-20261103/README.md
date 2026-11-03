# Dev.19 delivery boundary

Common **0.5.0-dev.19** freezes the accepted keyword filter, bounded multiline
candidate search, PythonRe IPv4 reuse and positive-boundary candidate skip.
Core/PythonRe/PCRE2 retain exact adapter pins and .NET 10/11. Packages are not
published automatically. Dev.18 and earlier staged/published bytes stay immutable.

| Decision | Evidence and remaining scope |
| --- | --- |
| Keyword filter accepted | [One public comparison](../EmittedPrefixPolicyReview-20261103/README.md): corpus Count about3.7x faster, matching allocation unchanged; dense hits+14%, miss scans+6–7us. |
| Line-start search accepted | [One public comparison](../AnchoredLinePolicyReview-20261103/README.md): ordinary/compiled Count about2x faster, finite/deadline gains; false-prefix+5.8%, sparse+0.16us. |
| Python IPv4 accepted | [Public comparison](../PythonIpv4PolicyReview-20261103/README.md): repeated IsMatch about15x faster/0B; construction confirmation+10.61% and retained Core root. CPython catalog observation remains Unqualified due short paired samples. |
| Positive-boundary skip accepted | [Public comparison and one confirmation](../PythonBoundaryPolicyReview-20261103/README.md): date miss about20%, dense word interiors46% lower; Unicode+9.6% and boundary Count+7%. Identical Core control's23–25% difference remains an unresolved attribution limitation. |
| Chinese literal/alternation deferred | [Validation](../ChineseValidationCost-20261031/README.md) and [search](../ChineseAlternateCost-20261031/README.md) attribution: existing validity-only and checked sequence/packed search routes; earlier cheap substitutions do not help. A combined SIMD validation/search design needs separate work. Strict whole-input validation remains. |
| Python early literal deferred | Full65542B input must be validated even for an early match; CPython's input is predecoded. No permission to skip validation, and no demonstrated hard performance lower bound. |
| PCRE2 native date gaps and optional scalar projection | Separate profile queue; not a blocker for Core/PythonRe performance delivery. POSIX remains its own later optional package milestone. |

The user accepts local regressions for relatively major gains. The 10% allowance
belongs to the completed checked-access campaign. The
[old217-case fixed-original results](../DeliveryDev17-20261103/README.md) and their
`Accepted=false` remain unchanged and historical; they are not a current-source
cumulative claim or a universal release ceiling. Focused public comparisons above
and the new boundary's catalog/resource checks serve different purposes.

Qualification is pending: one clean-source Windows package qualification, exact
same-byte Linux detached fixtures, original copied Awk/Lython and native/funded
producer probes on both targets/platforms, followed by one current-source Full
refresh with the existing83-step inventory and3600s cap. Preserve source/PDB/feed/
cache/lock identities and final snapshot guards. No additional qualification
framework, per-pilot matrix or unchanged217-case rerun.
