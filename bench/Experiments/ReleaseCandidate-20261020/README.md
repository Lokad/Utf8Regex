# Common 0.4.0-rc.1 release preparation

All three immutable packages are READY, not published or tagged. Producer
`2e97ac974e6e8c60aac95db1e4808cbda2ff9736` freezes documentation and shared version;
both adapters pin exactly Core `[0.4.0-rc.1]`. No production C# changed from
qualified scalar.119. The rejected metadata pilot remains absent; its semantic
regression tests are retained. No new performance measurement was made.

[Full qualification identities and scope](release.json), [terminal CI](ci.json),
[transfer archive identity](archive.json), [original consumer hashes](original-programs.json).
Local [handoff](../../../artifacts/utf8regex-0.4.0-rc.1-20261020/HANDOFF.md),
[consume](../../../artifacts/utf8regex-0.4.0-rc.1-20261020/CONSUMING.md),
[immutable feed](../../../artifacts/release-candidates/0.4.0-rc.1/feed).

One affected producer build (12.624 s), one nupkg/snupkg pack per package, no Linux
library build. Final DLLs pass complete isolated Release suites on both platforms:
Core 39,870; differential 1,504; PythonRe 3,881; PCRE2 5,911. No failed/skipped
TRX outcomes. Five existing duplicate-PCRE2-ID discovery warnings are recorded
in logs; duplicate inputs are not additional unique tests. Existing compiler/
analyzer warnings remain. There was no release-gate failure, correction or repack.

The unchanged original Awk/Lython producer public assertions and PCRE2 assertions
(including 676 recorded native property observations) pass on Windows/Linux.
A combined public consumer restores all three packages with one Core graph and
checks scalar counts, captures and original-token cancellation. Both platforms
also verify 121 CPython capture fixtures and 91 semantic digests. Fresh source-
mapped private caches, exact project/lock/feed/loaded SHA identities, public API,
PDB/SourceLink and symbols pass. Core/PCRE2 retain only permitted SizeOf references;
PythonRe has no unchecked API references. No native imports/BCL matcher/results.
Original scalar.119 and exact Core/PCRE94 artifacts retain their recorded hashes.

Public release notes, migration guide and concise package READMEs now describe
scalar/strict-UTF8 behavior, cancellation, usable finite controls and actual memory
scope. All three packages include exact finalized release/migration/host documents.
Chronological execution history is preserved in git and ignored PLAN.HISTORY.md.
No runtime/ownership/accounting framework or additional switches were introduced.

Defaults/static calls remain unlimited; modeled capacities do not contain total
heap/RSS, callback/getter/host retention or every preparation phase. Profile grammar
and operation limitations remain. PCRE2 email Count can exhaust a 50-million-work
policy. Existing performance receipts retain their own identities; no blanket RC1
speed or full flavor equivalence is claimed. Producer checks are not external
adoption. Broader original PLAN R01–R10 is paused after this completed release
preparation, at the user's request. Publication and downstream pins remain undone.
