# Managed PCRE2 execution architecture

The PCRE2 companion is a UTF-8-native managed implementation. Its public
surface is `Utf8Pcre2Regex`; compiler, planner, runner, global iteration, and
replacement details remain internal and removable with the companion project.

## Directional flow

Compilation follows one direction:

`Pcre2CompileRequest -> Pcre2Compiler -> Pcre2CompiledProgram`

Execution follows another:

`Pcre2CompiledProgram + invocation input -> Pcre2Runner / Pcre2GlobalOperationDriver -> public result`

`Pcre2CompiledProgram` is immutable after construction and may be shared by
concurrent callers. Captures, backtracking checkpoints, iteration cursors,
timeout state, and resource counters belong to `Pcre2InvocationState`; an
invocation state must never be retained by a compiled program or public result.

The current implementation keeps the pre-compiler pattern implementations in
`Utf8Pcre2Regex` behind `Pcre2FullVerificationProgram`. This is an explicit
legacy-runner adapter. `BootstrapMigration.Shipped.txt` owns its deletion: each
vertical feature slice replaces legacy verification with a generic compiled
program, then removes the corresponding `Pcre2ExecutionKind` entry and
complete-pattern classifier arm.

## Program ownership

The compiled program owns:

* normalized compile settings and immutable metadata;
* a candidate-search program selected for the compiled pattern;
* a full-verification program;
* an operation plan made of typed backend variants; and
* only backend programs that are used by an operation or are required to
  preserve detailed verification semantics.

There are no nullable backend fields. Empty, UTF-8, and managed slots are
closed variants, while each direct operation program carries its non-null
payload. Candidate search and full verification are separate even when the
bootstrap temporarily selects the same underlying program.

Replacement-plan memoization is deliberately outside the immutable program in
`Pcre2ReplacementComponent`. The cache is thread-safe and stores compile-owned
replacement plans only; match output remains invocation-owned.

## Allocation and complexity contract

Compile-owned storage may scale with pattern length, capture count, and the
number of compiled candidate atoms. Invocation-owned storage may scale with
capture count, live backtracking checkpoints, and explicit public output.
Buffers that grow with transient backtracking or output should be pooled once
the corresponding generic runtime slice owns them.

`Pcre2ResourceBudget` keeps independent candidate, backtracking, depth, and
heap counters. A zero public limit retains the documented engine-default or
unlimited meaning; it is not translated into an immediate failure. Candidate
search does not consume the backtracking budget. Full verification does not
charge failed bytes a second time as candidate work. L3 and later slices wire
these counters to the generic instruction runner.

Global iteration owns its search offset and previous reported range. It must
apply the PCRE2 empty-match retry rule and make forward progress without
rescanning an already rejected prefix. Replacement consumes the same global
driver and must not implement another match loop.

## Reuse from Lokad.Utf8Regex

The companion reuses only flavor-neutral core facilities:

* UTF-8 validation and byte/UTF-16 boundary projection;
* immutable `Utf8Regex` programs for syntax that has already been proven
  semantically equivalent; and
* UTF-8 candidate-search machinery when a PCRE2 verifier still makes the final
  semantic decision.

The existing friend-assembly declaration is marked
`PCRE2-INTEGRATION-POINT`. PCRE2 syntax nodes, flags, diagnostics, limits, and
backtracking semantics remain in `Lokad.Utf8Regex.Pcre2`; they do not enter the
.NET-compatible core semantic front end.
