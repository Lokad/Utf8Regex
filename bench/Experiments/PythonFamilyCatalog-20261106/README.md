# PythonRe family Count catalog investigation

The dev.38 and dev.39 catalogs report 722.79 -> 1004.05us for public
`Count("cat|dog|bird")` over the same 86016-byte ASCII subject. Both allocate 0B;
their nine paired sample ranges do not overlap and neither collects. Retain that
observation in [dev.39](../DeliveryDev39-20261106/README.md).

Untimed inspection verifies the exact archived Linux catalog DLL identities and,
independently, the Windows package assets. Both return 12288 and select
`CountPreparedLiteral -> CountForResult -> TryCountNativeLiteralMatches ->
TryFindSmallExactFamilyCandidate`. The three needle lengths are 3/3/4; the recent
17–32-byte tier change does not apply. All five resolved IL bodies/local types
match. One raw-IL difference resolves to the same metadata member.

On the pinned VM, all five FullOpts bodies also match except 28 process-address
lines: 345/331/526/1062/2264 bytes respectively. This run disables tiering and is
not a public performance result. Separate default-tiering warm runs retain the
Tier0/OSR/Tier1 output: four final method sizes match, while CountForResult is
7312/7319 bytes. Neither observation establishes the catalog's cause.

The catalog qualifies cases sequentially in one process; preceding cases can
affect PGO history. One predeclared **17.524-second** separate Default-process
ABBA tests reproducibility with identical fresh-process training, an unchanged
control and a finite-limit lane. It uses the exact frozen Full graphs, default
tiering/PGO, eight-CPU startup then CPU2 affinity, 60 samples (minimum 58.10ms),
equal results and post-timing checksum consumption. No producer rebuild.

| Public operation | dev.38 | dev.39 | Change | Allocations, both |
| --- | ---: | ---: | ---: | ---: |
| Exact Count control | 2.473us | 2.468us | -0.20% | 0B |
| Family Count | 513.043us | 511.388us | -0.32% | 0B |
| Family Count, finite limits | 609.299us | 594.051us | -2.50% | 112B |

[Process observations](cost-summary.json) retain overlapping unlimited ranges,
finite-lane movement and the control. The 38.9% catalog difference does not
reproduce here. These results do not replace the catalog, prove harmless noise,
identify a causal source change or establish a general improvement. No
confirmation, normalization, catalog refresh, source change or package
qualification follows. Close this investigation unless new evidence changes it;
return to the largest actionable qualified gaps.

Immutable `artifacts/experiments/python-family-catalog-20261106/` evidence:

- `inspection.zip`: `B032A16F78F638F0253229221A715EE7EB59C7EC24CA602A70A81BCB1620C697`.
- `raw-evidence.tar.gz`: `692F436A4F7D7A9F95E57ECF5668DE04253ACB2B745037952C33C462AECF5190` (2711508 bytes, verified off VM). Contains scripts/source, declarations, frozen graphs, code generation, public samples and environment.

Published/staged packages, symbols and existing raw measurements remain unchanged.
