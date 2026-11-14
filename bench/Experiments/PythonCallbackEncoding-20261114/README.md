# Bounded Python callback encoding

Accepted Core `1b1187a7`, baseline Core `f9fc88ad`, unchanged Python `8af3a569`.
Core's output writer encodes a nonempty callback of at most2048 UTF16 units
directly when three bytes/unit fit its already-admitted room. It omits only the
redundant sizing pass; exact sizing/growth and long/empty fragments keep their
existing path. Only PythonRe production consumes this writer method. No new
buffer, owner, policy, mode, unchecked operation or decoder. [Evidence](acceptance.json).

The byte bound covers isolated-surrogate repair. Checks remain before callback
encoding, around bounded encoding, and at final operation publication. Numerical
work falls one sizing-block step on eligible fragments; buffer/result admission,
growth overlap, independent output validity and retained lifetimes stay unchanged.

One **75.490s** ten-row separate-process ABBA, five150ms-target samples
per process/row, all>=20ms,150s cap, **zero confirmations**. Frozen Core DLL/PDBs
differ; Python/caller/.NET are identical. Verbatim compiler bytes were frozen before
mutation and every PDB document verifies. Both lanes use caller-valid unchanged
UTF8, equal options and complete Subn output/count. Prepared callbacks avoid user
allocation noise; requested owned detailed callback data is still produced in
timing. Funded lane includes finite work/depth/working/result, original live token,
timeout and seeded-loan refunds. Actual Unicode/repair cases are explicitly encoded.

| Complete public operation | Before us | After us | Change | Allocated B/op |
|---|---:|---:|---:|---:|
| subn-string-128 | 86.630 | 84.033 | -3.00% | 58832→58832 |
| subn-string-512 | 323.951 | 304.354 | -6.05% | 235520→235520 |
| funded-deadline-subn | 1203.043 | 1135.485 | -5.62% | 235704→235704 |
| unicode-string-callback | 98.100 | 93.294 | -4.90% | 58064→58064 |
| short-repaired-surrogate | 119.236 | 97.964 | -17.84% | 137656→94984 |
| block-edge-2048 | 11.152 | 10.827 | -2.91% | 114592→114592 |
| long-fallback-2049 | 14.122 | 13.896 | -1.60% | 114720→114720 |
| unchanged-byte-callback | 75.525 | 76.334 | +1.07% | 56528→56528 |
| unchanged-core-literal-path | 12.199 | 12.072 | -1.05% | 0→0 |
| unchanged-dotnet-count | 22.791 | 22.655 | -0.60% | 0→0 |

The512-repetition text, funded, Unicode, repair and2048-unit rows have separated
observed ranges. The128-repetition text row, long fallback and all unchanged
controls overlap. Keep byte-callback+1.07%, Core-1.05%,.NET-0.60% without gain/
regression claims. Existing affinity selection chooses each process's least-
contended allowed processor; placements/process medians remain in evidence.
No normalization, formal-confidence or new CPython ratio claim. All allocations
are unchanged except repair: removing the sizing call also avoids its BCL fallback
allocations. No package/canonical/Full refresh or multiplication of prior gains.

Release Core61 focused writer/output/capacity cases and Python5199 pass on
Windows.NET10/11 and Linux.NET11. Cases cover initial capacity/offset/tails,
BMP/supplementary/isolated surrogates,2048/2049-unit boundaries, split pairs,
every short-path work cutoff preserving unpublished logical output, no-admission
growth, working/result limits/refunds and original callback cancellation before
short and large text. No build/preflight/timing retry. All workers/keepers terminal0.

Raw verified offVM before keeper release:
`artifacts/experiments/python-callback-encoding-20261114/evidence.tar.gz`, SHA256 `680CD24B247D88082EC5D8671550B09D7242354A7C227C3F3886A592954435DC`.
Windows evidence is alongside it. All596 staged assets unchanged; dev92 excludes
this later source. [Specification](../../../docs/SPEC-PYTHONRE.md) records the lower
modeled work. CLOSED: no broad sizing/decoder/polling rewrite is implied.
