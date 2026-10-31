# Fixed-baseline cumulative cost checkpoint

**Closed**, fixed `a40bda03 -> 0e24cece`, .NET 11 RC3 on the quiet Ubuntu/Xeon
VM. The 21-case initial run took **88.153s**; its sole selected seven-case
confirmation took **49.997s**. Minimum timed samples were 35.788ms and 68.765ms.
All public outputs agree; allocation observations are repeatable. There is no
blanket cumulative <=10% pass. Several wall-time lanes remain inconclusive after
the allowed confirmation; no further run of these unchanged sources is planned.

See [predeclared scope](PREDECLARED.md), [initial raw results](cost-result.json),
[summary](summary.json), [confirmation](confirmation.json) and
[confirmation summary](confirmation-summary.json). The confirmation selects the
two Chinese catalog lanes, finite/live Core NonBacktracking, Python/PCRE2 Count
and Core/BCL controls. It reverses warm/initial sample order, warms2s and takes
nine independently100ms-calibrated samples. The original 21 workloads and frozen
DLLs are unchanged; no new workload was added to the confirmation.

Selected stable initial observations (complete operation, setup excluded):

| Public operation | Before us | After us | Change | Before / after B |
| --- | ---: | ---: | ---: | ---: |
| Core live-token ASCII IsMatch | 0.895 | 0.245 | -72.7% | 1792 / 120 |
| Core identifier Count, finite/live | 7.912 | 6.348 | -19.8% | 112 / 120 |
| Core identifier Count, unlimited | 0.669 | 0.711 | +6.3% | 0 / 0 |
| Core grouped text Replace, unlimited | 3.584 | 3.587 | +0.1% | 6504 / 6512 |
| Core grouped native Replace, unlimited | 3.871 | 3.695 | -4.6% | 6744 / 6512 |
| Core grouped Unicode Replace | 333.158 | 324.390 | -2.6% | 73920 / 73928 |
| Python captured FindAll, finite | 49.932 | 49.472 | -0.9% | 18720 / 18728 |
| Python possessive Search miss | 1.627 | 1.650 | +1.4% | 3552 / 3552 |
| Python lookbehind Search | 1.365 | 0.799 | -41.5% | 1872 / 0 |
| Python grouped Subn | 3.146 | 3.022 | -3.9% | 6056 / 6056 |

The unchanged Core literal confirmation is -0.9% with1.02/1.05 max/min spreads.
Chinese ordinary/compiled confirmation medians are +1.9%/+0.5%, but spreads reach
1.50x/1.43x. Finite/live NonBacktracking medians change +16.3% with~1.72x spreads
in both versions. PCRE2 Unicode Count medians change +45.8% with1.75x/1.81x spreads;
both versions switch between roughly6.5us and11us. Those unstable medians do not
establish equivalence or a stable regression magnitude. The BCL control also
shows~1.27x spread. Keep these rows inconclusive rather than selecting favorable
samples or treating the median ratio as a proved loss. The initial noisy PCRE2
enumeration/replacement observations are not promoted to gains either.

The matrix's `(a+)` replacements exercise a different route from the earlier
`(ab[0-9]d)` warm-template debt. It does **not** discharge the +11–18% debt in
[NativeCoreTemplates](../NativeCoreTemplates-20261030/README.md). New funded APIs
have no historical funded baseline and retain their separate feature-cost
receipts. Catalog rankings, final-source package acceptance and final operating
cost remain separate release work.

[Immutable raw/driver/DLL archive](../../../artifacts/experiments/native-hosting-delivery-20261031/public-costs.tar.gz),
SHA256 `8FA336C142B03696E58EEE37B04FB1458CAB7E7684A7C6E5B6E268BDFE77F0CF`.
The same archive includes the separate [tail correction comparison](../UnicodeLiteralTail-20261031/README.md).
