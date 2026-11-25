# PCRE2 implementation

## Compilation and ownership

`Utf8Pcre2Regex` owns a compiled program and a bounded replacement-plan
cache. Compilation admits pattern storage and work under the construction
policy. The PCRE2 grammar validator runs before any shared scalar preparation.
Supported literal patterns compile directly. Other patterns lower to the
PCRE2 backtracking program. Construction derives candidate search, capture
metadata and an optional program for the supported partial probes.
[Compile and program selection](../src/Lokad.Utf8Regex.Pcre2/Pcre2ExecutionArchitecture.cs)

The compiled program selects a typed executor for each operation. A literal
family that uses Core owns its Core instance directly. A shared scalar executor
owns the lowered instructions and the managed PCRE2 program used for match
options that require it. Partial probing can retain the original syntax tree.
[Construction policy](SPEC-PCRE2.md#interruption-and-resource-controls)

## Invocation and results

Candidate search passes byte starts to the selected matcher, which retains
the original subject context for assertions and fills the requested outputs.
The adapter advances global operations from the consumed range and applies
the profile's empty-match retry. [Matching and reporting](SPEC-PCRE2.md#operations-and-results) ·
[Progression rules](SPEC-PCRE2.md#reported-ranges-and-global-progress)

Search and Count can reuse retry storage across candidates under the original
invocation. Assertions and buffer growth account for simultaneous capacity.
A memory cursor owns that workspace across advances and releases it at
completion, failure or disposal. [Interruption and limits](SPEC-PCRE2.md#interruption-and-resource-controls) ·
[Cursor and result lifetimes](SPEC-PCRE2.md#cursor-and-result-lifetimes)

## Dependency boundary

The adapter targets .NET 10 and 11 and pins the matching Core package. It
reuses Core's checked search, scalar execution, buffer admission, scratch
storage and UTF-8 output helpers. Required hooks in Core are marked
`PCRE2-INTEGRATION-POINT`. Grammar, options, errors, capture and global
progress rules remain in the adapter. Native PCRE2 serves as a test and
benchmark comparator. [Public scope](SPEC-PCRE2.md#isolation-and-scope)
