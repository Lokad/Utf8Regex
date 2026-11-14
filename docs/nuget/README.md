# Lokad.Utf8Regex packages

Managed, bounds-checked regex libraries for .NET 10 and .NET 11. Match UTF-8
directly, using Unicode scalars and primary byte coordinates. No native regex
binary or BCL matcher is used by the libraries.

| Package | Supported profile |
|---|---|
| Lokad.Utf8Regex | .NET-style grammar, captures/history, backreferences, assertions, RTL and a NonBacktracking subset |
| Lokad.Utf8Regex.PythonRe | Python re-style grammar, flags, capture numbering and result shaping |
| Lokad.Utf8Regex.Pcre2 | Selected PCRE2 standard-matcher grammar, substitution, capture slots and partial probes |
| Lokad.Utf8Regex.Posix | Case-sensitive POSIX ERE subset, leftmost-longest selection and hierarchical capture priority |

Adapters require the exact matching Core version. These profiles have documented
boundaries; they do not promise full .NET, Python, PCRE2 or locale-aware POSIX
equivalence. POSIX excludes lookarounds, backreferences and locale collation.
PCRE2 excludes DFA matching, invalid-UTF subject mode, callouts, split and full
capture history; partial probing has a limited supported scope.

## Scalar matching and validation

```csharp
using Lokad.Utf8Regex;

var regex = new Utf8Regex(".");
int count = regex.Count("A😀"u8); // 2 scalars
var match = regex.Match("😀"u8); // 4 bytes, 2 derived UTF-16 units
```

Core's `Utf8InputValidation.Validate` is the default: it checks the entire subject,
including unsearched prefixes/suffixes, at most once per operation or cursor
lifetime. Select `inputValidation: Utf8InputValidation.AssumeValid` once at engine
construction or through a factory only when the host guarantees valid UTF-8 and
unchanged borrowed bytes. Read-only `InputValidation` reports that choice.
Supported Core matching, counting, replacement, splitting and cursor operations inherit
it. Pattern/replacement validation keeps its separate profile rules. There is no
per-call validation override. Required coordinates and result shaping may still
scan input.

PythonRe models Python `re` over `str`; PythonRe and POSIX take already-valid,
unchanged UTF-8 text without regex-operation validity passes or validation options.
Validate external bytes at the application's text boundary. PCRE2 retains its
UTF-mode checking and explicit trusted-input bypass. Pattern/replacement checks
remain separate in every profile.

PCRE2's explicitly enabled `\C` byte atom and special `\K` ranges have separate
slice-validity rules; check result slice properties before decoding those results.

## Cancellation, limits and ownership

Unknown short Python string lengths can use a conservative UTF-8-byte-derived
capacity before decoding; finite Unicode working/result budgets can deny earlier.

Synchronous cancellation observes the original token. Absolute deadlines and
finite work/depth/working/result policies cover the documented operation stages.
Explicit compilation factories offer separate construction controls.
Defaults and static cached conveniences are unlimited.

Memory limits admit modeled major buffers/capacities, not total heap/RSS,
callbacks, getters, arbitrary host retention or every helper allocation. Work
units depend on the engine/version/route. Caller-owned output destinations are
not producer-owned result allocations. An omitted validation pass can reduce
required work without disabling matching admission or interruption.

Keep borrowed input alive and unchanged. Follow each cursor's alias contract and
dispose deterministic program/result/cursor owners on early termination. Retained
results preserve their documented lifetime; the host accounts for its own storage.

## Upgrading and further documentation

Version 0.4 introduced scalar matching over strict UTF-8 in place of the older
UTF-16 matching behavior. Version 0.5 adds .NET 11 assets, POSIX and Core's optional
caller-validity policy while preserving Core's default strict rejection. PythonRe/
POSIX require existing valid text; PCRE2 follows its UTF checking contract.

Complete Unicode word-run Count can now avoid scalar execution workspace.
Numerical admission requirements may decrease with the execution route; ASCII
word counting can cost more time. Recheck finite policies when upgrading.

NuGet's project-website link leads to the repository specifications, migration
guide, profile examples and benchmark tables. Those tables retain their measured
source/runtime identities; they are not a speed guarantee for every package.
NativeAOT publishing and trimming remain unqualified.
