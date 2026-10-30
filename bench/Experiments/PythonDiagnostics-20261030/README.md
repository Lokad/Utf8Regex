# PythonRe positioned diagnostics

Producer **0128afc2b927c073e0d8293cbf41026332d8b610**, baseline
**4cab6712ffedcfb25f24840f07ba9995385345be**. PythonRe-only source change;
no package/catalog/platform requalification at this feature boundary.

`PythonRePatternException` now exposes immutable ErrorSource and ErrorKind.
Source distinguishes pattern (including inline flags), replacement and supplied
compile options. Kind distinguishes syntax/reference/width errors, invalid
options and intentionally unsupported features. Existing constructors default to
pattern/syntax. Existing types/messages and error precedence are preserved; no
wrapper, registry, retained source text, source index or diagnostic framework.
Known positions in five template throw sites replace -1 with the existing UTF-16
Position contract. Deferred width/global-option errors may remain unpositioned.
Missing named templates retain IndexOutOfRangeException and no position.
See the [profile contract](../../../docs/SPEC-PYTHONRE.md).

Eleven public template failures against an empty subject were independently
checked against official CPython 3.13.2, using an emoji prefix and converting
its scalar positions to the promised UTF-16 units. [Before](current.jsonl),
[after](current-after.jsonl), [oracle](cpython.jsonl), [version](cpython-version.txt),
[Python probe](oracle.py.txt) and [managed probe](probe.cs.txt) retain the evidence.
The seven missing-position cases now agree with those expectations; positioned
group-name errors and the unpositioned missing-name distinction are unchanged.

Release checks: **99 focused checks/runtime** and **4198 full PythonRe/runtime**,
including API snapshot/source guards. New tests cover public string/UTF-8
constructors and Compile, inline/supplied flag origin and precedence, unsupported
named Unicode escapes, mixed BMP/supplementary template offsets, empty/no-match
validation, large invalid funded preparation, valid cache coexistence, original
cancellation and synchronous refunds. The initial test expected inline Locale to
reach final unsupported-option validation; the parser actually rejects it earlier
as an invalid inline option. Its classification now follows that real throw site.
[Verification](verification.txt) summarizes passing runs; failed/passing logs are
archived. Core/PCRE2 source is unchanged, so their matrices were not repeated.

One [public before/after](comparison.json) took **13.99s**, minimum sample
**87.60ms**, under a 120s outer cap. No confirmation or additional campaign.
.NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4, Xeon Platinum 8573C, CPU mask 4.
Independent 750ms warmup and 80ms calibration target per lane, three alternating
samples. Separate assembly contexts hold the exact producer/baseline binaries.
All strings/exceptions stay alive through timing; equality/metadata/checksums
are checked afterward. All funded accounts balance after disposal.

| Public SubnToString workload | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Warm cached-template control | 1.043 | 1.099 | +5.37% | 2504 / 2504 |
| Numeric template churn, 64 plans | 1.874 | 1.887 | +0.71% | 3704 / 3704 |
| Funded numeric template churn | 2.675 | 2.714 | +1.49% | 4112 / 4112 |
| Invalid template, empty subject | 11.892 | 11.897 | +0.04% | 4176 / 4184 |
| Funded invalid template | 13.713 | 13.797 | +0.62% | 4584 / 4592 |

The exception fields add eight bytes per thrown pattern error. Successful calls
have unchanged allocations. The warm control's observed +5.37% is retained in
the evidence; unchanged allocations do not establish timing equivalence.
These are complete public preparation/failure costs, not isolated-kernel timing,
catalog Status, a comparator parity claim or a revision of the completed
checked-access campaign. The final cumulative fixed-baseline assessment remains
required. [Driver](driver.cs.txt), [project](driver.csproj.txt),
[console](comparison.txt) and [preflight](preflight.txt) are retained.

Immutable local archives under artifacts/experiments/python-diagnostics-20261030/:

- python-diagnostics-0128afc2.tar.gz:
  `75DA724CD1D5FBDA381536BD7DF8CD828C9465C14FECD90F45E95FDEC1B11A9E`.
- correctness-logs.zip:
  `D6929A084790807253AA7E7448CA822EEDAA761AC2ECD210AE74ACA205DE135D`.

The archive preserves source bundle, both DLL/PDB pairs, exact driver/binary,
raw samples/build/preflight and oracle output. Existing published/staged bytes
are unchanged. Scalar result coordinates, host acceptance and final release
performance/package/consumer gates remain open.
