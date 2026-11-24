# PCRE2 implementation

## Compilation and ownership

`Utf8Pcre2Regex` owns a compiled program and a bounded replacement-plan
cache. Compilation admits pattern storage and work under the construction
policy. The PCRE2 grammar validator runs before any shared scalar preparation.
Supported literal patterns compile directly. Other patterns lower to the
PCRE2 backtracking program. Construction derives candidate search, capture
metadata and an optional program for the supported partial probes.
[Compile and program selection](../src/Lokad.Utf8Regex.Pcre2/Pcre2ExecutionArchitecture.cs)

The compiled program selects a typed route for each operation. Compatible
operations can share a scalar program or an exact literal family. A family
that uses Core owns its Core instance directly. A shared scalar route owns
its instruction payload and the PCRE2 fallback it uses for unsupported
option combinations. Only partial probing can retain the original syntax
tree. Preparation admits bounded work and major storage before allocation.
[Construction policy](SPEC-PCRE2.md#interruption-and-resource-controls)

## Invocation and results

An invocation admits the requested byte start on the caller-valid, unchanged
UTF-8 subject. The runner sees the original bytes, including any prefix that
assertions can inspect. Candidate search can rule out starts or reject a
miss. The selected matcher verifies acceptance and fills capture slots as
needed. Probe and replacement use the profile's own rules for partial matches,
MARK and substitution. [Input contract](SPEC-PCRE2.md#subject-input-contract) ·
[Matching and reporting](SPEC-PCRE2.md#operations-and-results)

Global operations advance from the consumed range. They retry an empty
match at the same byte position under anchored, nonempty options before
advancing by a scalar. This preserves PCRE2 progression when `\K` changes
the reported start. [Progression rules](SPEC-PCRE2.md#reported-ranges-and-global-progress)

The original invocation owns work, depth, working-byte and result-byte
admission and observes cancellation and the deadline. A search or Count
can reuse admitted retry storage across candidates. Assertions and buffer
growth account for simultaneous capacity. Span enumerator copies share the
original limits and carry independent positions. Memory-cursor aliases share
position and disposal, and the cursor releases its exclusive workspace at
completion, failure or disposal. [Cursor and result lifetimes](SPEC-PCRE2.md#cursor-and-result-lifetimes)

## Dependency boundary

The adapter targets .NET 10 and 11 and pins the matching Core package. It
reuses Core's checked search, scalar execution, buffer admission, scratch
storage and UTF-8 output helpers. Required hooks in Core are marked
`PCRE2-INTEGRATION-POINT`. Grammar, options, errors, capture and global
progress rules remain in the adapter. Native PCRE2 serves as a test and
benchmark comparator. [Public scope](SPEC-PCRE2.md#isolation-and-scope)
