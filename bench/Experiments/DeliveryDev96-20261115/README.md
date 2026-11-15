# dev.96 capture and input preparation delivery

Frozen source `4d9234147bcd36ffc047dc842266714c2e5713ed`, all four exact-Core-pinned packages/symbols,
.NET 10/11, staged in `artifacts/nuget/`. No publication/adoption.
[Acceptance](acceptance.json), [Full receipt](full-refresh.json).

One actual completed **Full refresh: 47m20s**, all 85 stages pass under one hour:
one build, both-runtime tests, disabled dynamic code, all .NET 11 catalogs,
lifecycle/scaling, paired comparators, finite policies and final verification.
No extra validation-policy matrix or per-change package qualification.

A read-only release audit confirms byte-template substitution preserves PCRE2's
ignored prefix, including replacement-only and destination output. Document its
distinction from independent callback/text UTF-8 checks and extend the existing
input-window theory; both-target focused checks pass with frozen producers intact.
The native PCRE2 enumerator validates its first match then uses NO_UTF_CHECK;
POSIX already uses trusted UTF input and documented ordered-NFA limitations.

The remaining preparation audit finds no subject validity or whole-input coordinate
pass in Python string findall or POSIX existence/count. Requested Python/POSIX
coordinate outputs count only the required ranges/prefixes and reuse collection
origins. PCRE2 PrepareInput borrows bytes after its single native-window check;
trusted input skips the check without computing whole-input coordinates. Its one
remaining explicit subject-ASCII probe guards non-positive-ASCII class-run
eligibility; positive ASCII ranges bypass that probe. Removing it without another
proof would change Unicode matching, not merely remove redundant preparation.

Includes [shared bounded capture scan](../TerminalAsciiRepeatEntry-20261115/README.md),
[folded-state cleanup](../FoldedPreparationInvariants-20261115/README.md) and
[validity-prefix reuse](../ValidityPrefixReuse-20261115/README.md), after qualified
dev95. Independent public comparisons preserve +2.02% overlapping finite-capture
cost, ~1KiB capture code growth and unstable first-row observations. The validity
change deliberately retains finite late Unicode +0.940us/+21.85% and finite email
+31.963us/+2.69% alongside -27.52% late-Unicode/-17.41% finite mixed gains.
These are public-operation trade-offs, not kernel estimates or a universal speedup.
Do not multiply private improvements into final canonical native ratios.

Remove 49 net lines of verified dead folded fallback/recomputation and four net
lines of obsolete private-shape test inventory. Add 28 net source lines sharing
the existing capture scan and five for ASCII-prefix validity reuse. No new mode,
framework, allocation owner or public API. Original metering/admission, scalar
semantics, captures/history, alias lifetimes, cancellation/deadlines and managed
safety remain. No numerical resource-contract change in this batch.

Windows and SAME-byte Linux pass ten detached fixtures/112748 checks,
original copied Awk 19 / Lython 118 per target and native/funded probes/root loans 0.
Source/API/PDB/DLL/feed/cache/lock identities pass; only three pins among 1,565
original inputs change. Linux 14 TRXs / 28 restore audits. All 620 older assets remain
byte-identical; eight new files total 628. Source CI green. Four offline writers and
both-runtime snapshot guards pass using matching binaries, without repacking.
Linux-built performance and exact Windows package acceptance are separate evidence.

Core defaults to whole-input Validate and supports immutable AssumeValid.
PythonRe models Python re over str; PythonRe/POSIX require valid unchanged UTF-8
without subject validation passes/options. PCRE2 keeps native UTF-window checking
and trusted bypass; independent pattern/template/raw-output checks remain.
Canonical comparisons align guarantees/options/complete output. CPython workers
exclude startup/compile. POSIX's private PCRE.NET 1.5/PCRE2 10.47 UTF+NO_UTF_CHECK,
Singleline/DollarEndOnly ordered NFA covers verified overlap, not POSIX selection/
history semantics. Unqualified/incompatible rows retain their exclusions.

Capacity preparation retired three redundant completed dev95 transfers after
exact offVM hashes and live-reference checks; canonical evidence/shared caches/
packages stay. The first shell setup exited 2 before any runner/build because of
CRLF; failure is archived, LF corrected and no prior worker/output verified before
the single actual Full. No completed run was restarted. All workers/keepers are
terminal; raw verified offVM before release in artifacts/experiments/delivery-dev96-20261115/.
Full `C900FE72732CE74F1E7DA67C24D3DB0B1C6149ADEA46F6E16A3D6565D8D36488`; Linux `46AD72013A4825BD38D87B77229D626268FA6AF61447C9E658CB19FB520041CE`.

Fresh canonical whole-operation costs against predecoded .NET are Chinese literal
3.74x/~70us, compiled Chinese alternatives 2.55x/~91us, email 1.86–2.03x/~909–919us,
Russian literal 1.75–1.94x/~169–170us and compiled folded English 1.70x/~129us.
River/symbol are 1.38x/1.34x at ~2.15/1.65ms. Python captured strings are
1.41–1.45x/~306–468us and callback output 1.25–1.26x/~332–340us against CPython.
There is no qualified slower PCRE2/POSIX single operation at least 100us; POSIX
SKU observations batch 4,096 calls. Tiny limited-output Python rows remain
inconclusive. Finite ratios describe same-producer operating cost, not native
parity. These final measurements do not attribute cross-run changes to one pilot.
Remaining capture/repeat progression, search and complete output cost need a
distinct live-cost proof before another implementation; required Core validity
and requested output cannot simply be excluded from their public contracts. The
[anchor audit](../FoldedAnchorFrequencyAudit-20261115/README.md) rules out a
primary-English selector change; replay is not execution timing. Broader profile
grammar remains separate. Public 0.5 needs its own final version/docs freeze.
