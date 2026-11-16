# Python value coordinates count already-valid text

Accepted: move the existing long result-ledger sizing loop into CountValidUtf16,
and reuse it at five Python input-range coordinate call sites. Required byte/
UTF16 endpoints stay exact; matching still uses Unicode scalars. This uses the
existing checked 32-byte masks and scalar-aligned 4096-byte work/interruption
blocks. Ranges at most4096 bytes keep their BCL counter. Bounded pattern counts,
general Core decoder/fallbacks, PCRE2 and coordinate-prefix indexing are unchanged.
No API, mode, descriptor, cached flag, decoder or allocation change.

**Intentional regression: large ASCII native FullMatch rises7.897→39.405us,
+31.508us / +398.97% (about5x), with separated ranges.** Retain it under the
user-authorized trade-off for the 92–96% Unicode savings, around1ms on the large
native cases. Do not claim ASCII parity, uniform improvement or compose earlier
percentages. No universal10% gate applies. Stop this trial without tuning.

Windows and SAME-byte Linux per.NET10/11: affected Core847 and full PythonRe5231
pass; focused native/scalar coordinates and original interruption74/target,
including borrowed offsets, all widths, vector/block cuts and retained borrowed
values. Windows Core integration source guard passes. Software-intrinsics-disabled
public preflight passes. An initial deadline test selected an ineligible finite-
timeout DotAll route; corrected to the existing exact-literal FullMatch route,
without changing selectors or producer binaries. No public contract change.

One111.942s ordinary-process ABBA /180s cap,0 confirmations. Twelve public operations,
normal8CPU startup thenCPU2,1s warmup,75ms calibration plus one calibration check,
five150ms-target samples/process/row, all>=20ms. Native engagement is asserted;
funded scalar calls keep live token/deadline, finite work/depth/working/result
policy and parent refunds. Ref-struct values return fixed endpoint summaries;
complete borrowed byte ranges are verified after timing. All allocation medians
agree. Unchanged short/miss/string/coordinate/Core/.NET sample ranges overlap.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| native-full-unicode | 1067.081 | 42.488 | -96.02% | separated |
| native-full-ascii | 7.897 | 39.405 | +398.97% | separated |
| native-offset-unicode-prefix | 1049.042 | 42.258 | -95.97% | separated |
| scalar-full-unicode | 133.336 | 7.126 | -94.66% | separated |
| funded-scalar-full-unicode | 133.642 | 8.316 | -93.78% | separated |
| scalar-search-prefix | 137.620 | 11.025 | -91.99% | separated |
| short-value-control | 0.267 | 0.272 | +2.01% | overlapping |
| miss-control | 1.433 | 1.436 | +0.21% | overlapping |
| ledger-string-control | 544.011 | 539.881 | -0.76% | overlapping |
| coordinate-control | 15.709 | 15.457 | -1.60% | overlapping |
| unchanged-core-count | 12.247 | 12.206 | -0.33% | overlapping |
| unchanged-dotnet-count | 22.581 | 22.004 | -2.56% | overlapping |

CoreBD0AD520→2BAAF4AC, Python977FA3BC→0220DAB3; actual393 compiler documents,
DLL/PDB/member/input and common caller/.NET identities verify. The before graph
is immutable dev.102. All684 feed assets rehash unchanged; no package, Full or
canonical/native-ratio refresh. Source qualification follows separately.

The first external-tmpfs setup failed a SourceOnly repository-snapshot lookup
before timing (other functional tests pass). Its raw is preserved. Correcting the
fixture location to repository-relative tmpfs uses the identical producer/caller/
test archive, with no rebuild and one actual comparison. Original handles are
terminal1/0 as recorded; both raw archives are verified offVM. Artifacts:
artifacts/experiments/python-value-unit-sizing-20261116/ and
artifacts/experiments/python-value-unit-sizing-measure-20261116/.

- Initial setup SHA256: 8D45D391F943ED40CB434AAAFCFF2E09C0B6D521FC86F3BBD1230BA48066A3E4.
- Public comparison: 8071FBBB08CD45CF49F03D4541E6F037DE4EEDC7925005479D2E45C1ADAE9A17.
- Validation: BCECE0DB6FEB1E9EDA7AE68194101CD2DD55CD155F88924860057C9DF6D4F777.

Source db90f728 CI43814881958 passes Windows/Linux, saved/closed. Owned VM
scratch is retired after accepted hashes and zero references; the failed external
tmpfs fixture root was already absent. Dev.102 remains immutable.
