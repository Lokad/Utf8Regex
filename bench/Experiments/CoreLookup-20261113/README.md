# Checked lookup for Core UTF-8 validity

Accepted design: measured prototype `d41b80ad`, consolidated source `117cbeb5`.
The validity-only path uses checked admitted span windows with AVX2/AVX-512
nibble lookups. Metadata consumers keep the coordinate decoder; short tails and
invalid inputs reuse it for complete scalar checks and exact first-error offsets.
Non-x86 hardware uses the checked BCL validator, with the same error authority.

Removed the density selector, sampled two/three-byte preference, adaptive drain,
unused validity descriptors, private drain-stop tests and stale mode diagnostics.
Existing adversarial/boundary assertions now exercise the production validators.
Pattern, replacement and completed byte-output checks share that validity entry.
A discovered metadata defect is fixed: Kelvin signs inside consecutive three-byte
runs now set the folding flag. Public mixed-text operations have regression tests.

Core strict defaults, Python/POSIX valid-text contracts, native PCRE UTF windows/
bypass, byte coordinates, original cancellation/deadline and bounded work/storage
remain. No public API or resource-policy change, unchecked read, native dependency
or profile framework. MIT source attribution is included in the packed license.

## One public comparison

Quiet VM, .NET 11.0.0. Baseline uses the exact Full83 Core pair (`7b8c22d0`),
reused after proving Core source equivalence at `40bd096c`. The caller is identical;
changing producer hashes differ and both DLL/PDB/source checks pass. Each Count
receives the same bytes/options and returns the independently verified same count.
Strict whole-input checking is timed. AssumeValid Count is an unchanged matching
control, not a different profile or a new .NET/native parity figure.

| Strict public Count | Before µs | Prototype µs | Observed change | Allocated B, both |
|---|---:|---:|---:|---:|
| chinese | 92.653 | 67.721 | -26.9% | 0 |
| email | 983.665 | 933.823 | -5.1% | 0 |
| ascii | 94.841 | 96.180 | +1.4% | 0 |
| constrained | 12.073 | 4.352 | -64.0% | 0 |
| four-byte | 32.230 | 2.563 | -92.0% | 0 |
| chinese-finite-live | 129.008 | 108.157 | -16.2% | 112 |

Chinese and constrained/four-byte public improvements are clear in this run.
Email improves less; unchanged controls move +0.75% to +4.38%, and one after email
control sample is high. ASCII +1.41% accompanies its unchanged control +1.87%.
Retain all ranges and movement; do not normalize them into exact causal gains.
Finite/live Chinese uses work10M/depth4096/working16MiB/result16MiB and a live
token. All allocations are unchanged. Nine samples per lane, 1000ms warmup,
250ms targets, actual samples >=241ms; 120s cap per ordinary producer process.
Only 64 integer results are retained and consumed after each timed batch.

No confirmation or additional timing. Final consolidation removes superseded
branches and changes the short tail/error decoder; it is correctness/codegen
checked, **not separately timed**. Exact values above belong to the prototype.
The next coherent catalog refresh measures the complete candidate; these values
do not replace frozen README ratios or compose with other focused changes.

## Validation and delivery

Focused Windows checks pass on .NET10/11 across all four profiles, including
strict/default versus trusted behavior, PCRE windows, malformed/truncated widths,
borrowed offsets/tails, first errors, finite admission, output and interruption.
Linux final-source checks pass1591 Core/219 PCRE2 per target; direct256/512-bit
paths are checked on the VM. Disabled-intrinsics fallback passes52 Windows checks.
Public Release disassembly observes three vector byte shuffles at both widths;
checked range-failure paths remain. Managed-member inventory permits actual
Unsafe.SizeOf only. [Acceptance](acceptance.json) records exact identities/counts.

Raw archives live in `artifacts/experiments/core-lookup-20261113/`; VM archives
were hash-verified offVM before releasing both SSH keepers. All524 staged package/
symbol files are unchanged. Dev83 excludes these later source changes. No package
or Full refresh was repeated; future packages require a fresh common version.
