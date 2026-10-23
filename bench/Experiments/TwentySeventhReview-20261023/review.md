# Twenty-seventh review, 2026-10-23

Checkout `53a76711`; reused Release Core/PythonRe/PCRE2 producer `3dea7dec`.
Read-only product/PLAN review. All witnesses are in this ignored directory.
No package, benchmark campaign, full-suite rerun or GitHub API request.

## 1. PCRE2 CRLF search progression differs from the reference; a test preserves it

With `Newline=Crlf`, `.` and `\N` can find the LF inside CRLF after a failed
attempt at CR. On subject CR LF, managed `.` produces byte range 1/1 while
native PCRE2 10.47 finds no match. On CR LF x, managed enumeration yields
1/1 then 2/1; native yields only 2/1. `(.)` is affected too,
and `.x` can match LF x where native finds no match.

`newline-witness.json` records seven differences in 16 small enumeration cases,
with AnyCrlf controls agreeing. Twelve further first-match
controls identify the boundary of the difference:

- Explicit start offset 1 can match LF in both engines, including Anchored.
- Anchored start 0 fails in both engines.
- The pattern `.|\n` agrees in both engines at both starting offsets.

The native source explicitly implements a CRLF bump-along rule when the pattern
does not explicitly match CR/LF (`external/pcre2/src/pcre2_match.c:8053`). This
is search progression, not UTF-8 decoding or a blanket rule making every LF
following CR unmatchable. The owned candidate advance at
`Pcre2CharacterCompiler.cs:1655` advances one scalar. The existing test
`Pcre2CharacterCompilerTests.cs:137` expressly expects the differing LF match;
it therefore cannot catch this profile discrepancy.

Recommendation: resolve the scoped CRLF search contract and replace that
unqualified expectation with reference-backed positive/negative cases. If the
PCRE2 profile keeps a deliberate distinction, document and qualify it explicitly.
Do not blindly change the dot predicate: preserve explicit LF starts and literal
newline alternatives. Keep scalar advancement, validation and original resource
authority elsewhere. This is a PCRE2 profile issue, not evidence against the
Core/PythonRe scalar contract or a reason to reopen their closed campaigns.

## 2. Benchmark cleanup can select unrelated dotnet processes

`bench.ps1:27-31` unconditionally selects Windows benchmark workers before a
normal BDN run by testing whether their entire command line contains both the
worker filename pattern and benchmark output-root text. It does not establish
that the launched DLL belongs to that directory, nor require a directory
boundary after the root text.

The production pipeline, executed with mocked discovery/stopping only, selected
three incorrect cases: a sibling directory sharing the root prefix, an outside
worker mentioning the root as an output argument, and another application
mentioning a worker DLL as data. One valid worker and two unrelated controls
behaved as expected. See `bench-ownership-witness.json` and the AST witness.
No processes were launched or stopped.

The test-host recovery fix did not change this separate benchmark path. A
dotnet.exe executable path is SDK-wide, so it cannot itself prove ownership.
Prefer removing unneeded unconditional global discovery, or verify the actual
launched DLL operand with directory-boundary containment and leave ambiguous
processes running. Preserve owned-child wall-time cleanup. No general process
management framework or shipped native dependency is needed.

## 3. Corpus support metadata and duplicated backlog gates are stale

Four batch11 detailed-match cases remain `OutOfScopeBySpec`, with backlog reason
`SpecRejectsDuplicateNameDetailedProjection`. Current public matching satisfies
their expected whole-match ranges/values, capture slots and name entries (see
`corpus-witness.json`). SPEC-PCRE2 and current branch-reset tests support these
features. The special-match corpus filter already executes these inactive rows;
this is stale classification, not a claim that these examples are untested.

The metadata is costly to reconcile because several parallel expectations pin
it in place:

- `Pcre2CorpusExecutionInventoryTests.cs:51` requires at least five executable
  cases to remain inactive. Completing their classification can fail the gate.
- `Pcre2CorpusCoverageComparisonTests.cs:35` hardcodes 39 backlog entries and
  their distribution; `Pcre2CorpusBacklogLedgerTests.cs:71` repeats those counts.
- `Utf8Pcre2CompletionLedgerTests.cs:51` pins another detailed summary (613
  active), although useful coverage-coherence assertions can stand separately.
- PORTING.md's dated inventory says 636/598 active; current files contain
  652/613 active, 25 out-of-scope and 14 unsupported rows. Keep historical
  qualification explicitly historical when updating maintenance guidance.

Recommendation: reconcile the four demonstrated cases, preserve IDs, expected
outcomes, provenance and actual exclusion reasons, and remove mandatory backlog
floors/repeated count snapshots. Keep coherence checks that catch missing cases
or unsupported execution. Do not replace them with another inventory framework
or infer wider profile completion from four passing examples.

## What to retain and what remains separate

No additional dormant matching engine was verified. Immutable scalar tables,
bounded pooling, original invocation authority, owned snapshots and shared
candidate helpers have real roles; low type-name counts alone do not justify
deleting extension containers. No new protection/accounting precision is proposed.
The recent grapheme/value/checker fixes stay delivered and their bounded timing
experiments stay closed.

Fresh final packages, isolated consumers and actual-package/cumulative operating
costs remain the release boundary already recorded in PLAN. These findings do
not constitute a fresh release or package qualification. Existing snapshots,
frozen RC1 bytes, TODO handoffs and product DLL/PDB identities remain unchanged.
