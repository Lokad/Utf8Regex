# Managed PCRE2 execution architecture

`Lokad.Utf8Regex.Pcre2` is a managed UTF-8 companion, not a mode of
`Utf8Regex` and not a native PCRE2 binding. Its semantic front end and runtime
remain removable with the companion project. The base library retains its
.NET 10 `Regex` contract.

## Directional flow

Compilation is one-way:

`Pcre2CompileRequest -> validation -> parser/semantic tree -> lowering -> Pcre2CompiledProgram`

Invocation is one-way:

`validated UTF-8 subject -> operation program -> runner/cursor -> public projection`

Replacement consumes matches from the same compiled operation program and
global cursor used by the other operations. It does not parse patterns, choose
a backend, or implement an independent matching loop. Partial probing has a
separate, curated program because PCRE2 partial matching is not an ordinary
successful match.

`Utf8Pcre2Regex` owns one immutable `Pcre2CompiledProgram` plus a bounded,
thread-safe replacement-plan cache. The facade validates public arguments,
creates an operation-local subject context, delegates once, and projects the
result. It does not classify complete pattern strings or select fixture-shaped
execution methods.

## Compiled-program ownership

The compiled program owns:

- the normalized compile request and immutable group/name metadata;
- the semantic syntax tree;
- one typed program for each public operation;
- the candidate-search program; and
- an optional partial-probe program derived from semantic nodes.

Direct operation programs are closed variants: no program, compatible
`Utf8Regex`, compatible BCL `Regex`, PCRE2 literal, PCRE2 character, or PCRE2
backtracking. `Pcre2ProgramInvariant` proves that any compatible backend is
owned by the same compiled program. Literal, character, and backtracking
programs generically cover the admitted syntax; no `Pcre2ExecutionKind`,
complete-pattern classifier, or legacy match/replacement router remains.

Delegation to `Utf8Regex` or BCL `Regex` is an optimization for the proven
common subset. The PCRE2 semantic compiler always owns PCRE2-only constructs,
and the corpus verifies that delegation does not change the selected PCRE2
profile.

## Invocation and global progress

Capture slots, undo journals, backtracking/call frames, resource counters,
timeout state, UTF-8 projection state, and global progress are invocation
local. Compiled regex instances retain none of those buffers and can be used
concurrently.

The backtracking runtime keeps consumed and reported ranges distinct. `\K`
changes the reported start but not the consumed range used for restart and
empty-match detection. Global cursors apply PCRE2's empty retry at the same
position, then advance by one valid UTF-8 scalar only when that retry fails.
Count, enumeration, `MatchMany`, and substitution share this progression
machinery, with an operation-specific disposition for non-monotone results and
reset-empty substitution.

`Pcre2ResourceBudget` meters candidate work, backtracking, depth, heap, and
managed timeout independently. A public zero limit retains its documented
engine-default or unlimited meaning. Stack-like transient state uses pooled
storage with deterministic return; result arrays and caller-requested output
remain ordinary owned allocations.

## Partial probing

`Probe` is deliberately smaller than normal matching. The compiler recognizes
the curated partial profile from character/backtracking semantic trees and
emits a typed `Pcre2PartialProbeProgram`. Exact semantic-shape keys may select
a specialized partial algorithm, but they are not complete source-pattern
keys, do not affect normal matching, and fall back to the ordinary non-partial
runner whenever the request does not ask for partial matching. Unsupported
partial shapes fail explicitly.

## Reuse from `Lokad.Utf8Regex`

The companion reuses only flavor-neutral mechanics:

- UTF-8 validation and operation-local byte/UTF-16 projection;
- prepared search kernels and byte-set carriers;
- transactional UTF-8 output writing;
- pooled stack storage;
- timeout polling; and
- adjacent-scalar access.

Every necessary core hook is tagged `PCRE2-INTEGRATION-POINT`. PCRE2 syntax,
options, compile errors, Unicode policy, capture rules, empty-match policy, and
replacement grammar remain in the companion. No generic flavor framework or
PCRE2 mode enters the core.

The only product friendship is the reviewed core-to-companion access needed
for these internal mechanics. Test and benchmark friendships expose internal
diagnostics only; no `InternalsVisibleTo` declaration appears in a project
file.

## Removability and dependency boundary

All PCRE2 production types and files live under
`src/Lokad.Utf8Regex.Pcre2/`. The package targets `net10.0`, depends only on
the BCL and `Lokad.Utf8Regex`, and contains no P/Invoke, native loader, RID
asset, external matcher, or generated native payload. Removing the companion
project and the explicitly tagged core hooks leaves the `.NET`-semantic
library intact.

Release evidence and the reviewed ownership, C#-guideline, package, corpus,
allocation, and scaling inventories are recorded in
[`QUALIFICATION.md`](QUALIFICATION.md).
