# Immutable dev.74 delivery checkpoint

Qualified all four `0.5.0-dev.74` packages with exact Core dependencies for
.NET10/.NET11, frozen at 7e6f00e6810b4bd3b92c4313c2ac81f50ba0ed6c.
SDK 11.0.100 and .NET 11.0.0 remain the performance baseline. Packages and symbols
are staged under artifacts/nuget; nothing was published or adopted downstream.
[Complete acceptance](acceptance.json), [Full receipt](full-refresh.json).

This batch combines the POSIX parent/whole boundary proofs with the smaller Core
dispatcher. The POSIX changes resolve demonstrated finite-work refusals for
captured Unicode/negated/dot histories. Core preserves exact backreference work
while reducing repeated VM entry clearing. Focused causal gains and accepted
costs remain in their original receipts:

- [Parent alphabet proof](../PosixParentAlphabet-20261111/README.md).
- [Parent boundary simplification](../PosixParentBoundary-20261112/README.md).
- [Whole endpoint simplification](../PosixWholeBoundary-20261112/README.md).
- [Cold backreference extraction](../ColdBackreference-20261112/README.md).

Retain folded-reference +6–8%, trusted RTL +4.43%, and earlier-shorter POSIX proof
costs from those comparisons. No composition of their gains or general native
parity/polynomial guarantee is claimed.

Windows package qualification passes 112088 checks across 10 detached fixtures.
Linux accepts the **same Windows package/symbol bytes**, without repacking or
rebuilding producers, with the same 112088 checks and 10 fixtures. Original copied
Awk 19/Lython 118 checks and public producer probes pass per target/OS, final root
loans 0. The 1565-file original source audit permits only three exact version pins.
These checks are compatibility evidence, separate from adoption or POSIX consumer
acceptance. Source/PDB/DLL/feed/cache/lock identities, 28 Linux restore audits and
14 TRX files verify. Windows qualification takes 301.872s.

One full VM refresh passes **2831.007s /47m11s**, all 85 steps, under 3600s. It covers
the Release build, both-runtime correctness, disabled dynamic code, all .NET 11
catalogs, lifecycle/scaling, paired comparators, finite/resource and Verify checks.
Core/Python/PCRE2/POSIX snapshot guards pass 9 checks per target/platform, reusing
matching binaries. Eight summaries/generated READMEs are imported from this run;
the authored root refresh date/duration/receipt link is updated separately.
Source CI 43145671739 is green on Windows/Linux.

The refresh still records material gaps. Core compiled email 2.528x, Chinese 3.390x,
river 1.413x; Python many strings 2.220x, one 2.332x and captured Split 1.536x. The full
run does not establish a generic Python-output gain from the focused changes.
POSIX SKU is now qualified native-faster at 3.811x, 2623.434µs per 4096-call batch;
retain 22.345B/batch pool-fill allocation. PCRE2 retains 12 native-faster rows,
15 inconclusive and 25 excluded. Core has 119 wins/51 gaps/8 tie candidates; Python
47 managed-faster/29 CPython-faster/7 inconclusive/2 equivalent/5 floor-unqualified.
POSIX retains 8 history exclusions and 9 policy measurements. Historical Full
observations are not causal A/B comparisons; retain uncertainty and exclusions.

All 444 older canonical package/symbol files remain byte-identical, with 8 new files
added: 452 total. All new feed/canonical hashes match. An initial private Windows
wrapper failed after successful qualification/consumers because transfer state
was not initialized. Existing bundle contents/hashes were verified and publication
state completed; no package build, repack or tests were repeated. The first Linux
staging preflight stopped before Linux work; its corrected replacement used the
same one Full run and the existing verified bundle.

Canonical offVM artifacts/experiments/delivery-dev74-20261112:

- Full:6808886E4045BE4B257C5AA76E4526E856A212E86F23010BCC7AEAB7951429D0
- Linux acceptance:4DB2723A28FAE39701824F34F1D0DD938377B94B70BE540C4054B80E9F5D8713

Archives are hash-verified, all delivery workers are terminal and keepers released.
This is a delivery checkpoint. Shared warm Python/Core and general POSIX history
work remain open; do not repeat an unchanged package/Full matrix for small changes.
