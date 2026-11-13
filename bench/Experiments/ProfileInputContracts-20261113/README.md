# Profile input contracts

Producer `67bd1fd8`, .NET 11 GA on the benchmark VM. Nine samples per selected
case; these observations do not replace a Full refresh or package qualification.
Raw samples, producer DLL/PDB hashes and unchanged comparator identities are in
`artifacts/experiments/profile-input-contracts-20261113/`.

PythonRe and POSIX consume existing valid, unchanged UTF-8 text. Their development
validation option/property was removed. PCRE2 defaults to its native UTF checking
window, including lookbehind and `\A`/word-boundary context; immutable AssumeValid
is its trusted subject bypass. Core retains its existing policy. Pattern/template
checks, managed bounds, original interruption and resource admission remain.

Each reference below uses equivalent input guarantees, options and requested
outputs. Python uses the existing long-lived CPython `str` worker. POSIX uses
**ordered PCRE2 NFA**, UTF mode with NO_UTF_CHECK, DotAll/DollarEndOnly and ASCII
named classes. It is not a POSIX oracle: rows with different selection/capture
histories remain excluded. PCRE2 uses native UTF checks in both lanes.

| Selected public workload | Managed | Reference | Managed/reference |
| --- | ---: | ---: | ---: |
| POSIX SKU captures, 4096 calls | 2620.237 µs | 652.252 µs | 4.017x |
| PCRE2 atomic reset, enumeration | 1.085 µs | 0.884 µs | 1.228x |
| PCRE2 atomic reset, Count | 0.309 µs | 0.308 µs | 1.005x |
| PCRE2 atomic reset, MatchMany | 0.721 µs | 0.885 µs | 0.814x |
| Python Unicode literal-repeat FullMatch | 3.108 µs | 19.139 µs | 0.162x |

Lower ratios are better. PCRE2 measured zero managed allocations. Python's
FullMatch measured zero allocations and ratio 0.162 [0.159, 0.165]. Its prior
producer, with the equivalent trusted-text guarantee, took 8.840 µs; the CPython
control moved from 19.799 to 19.139 µs. Different allowed CPUs and control movement
limit exact attribution. The bounded-pattern UTF-16 width calculation removes
actual work after successful literal-repeat verification.

POSIX managed time was effectively unchanged from 2617 µs. Its old native lane
still checked UTF, so the old native ratio is not an equivalent producer comparison.
Current finite/funded/deadline lanes cost 3554/4064/4418 µs per 4096 calls; those
controls retain their real overhead. Its unlimited allocation observation was
about 22 bytes per batch, versus roughly 3.1–3.3 MB for governed batches.

Small PCRE2 differences are not precise version regressions or gains: an initial
same-producer observation had MatchMany at 0.637 µs, versus 0.721 µs in preserved
paired raw data. One receipt confirmation repaired a collector filename-case
error; no further timing was run. Native substitution remains excluded where the
wrapper cannot provide equivalent UTF-8 output.

Correctness includes both-runtime profile suites, native-window/offset tests,
retained results and original-token/deadline/admission tests. CI's only initial
failure was a documentation isolation guard, fixed in `70bf564b`; CI43418211845
is green. Follow-through targets actual capture/search/output work, starting with
[Python string sizing and anchored search](../PythonStringSizing-20261113/README.md).
