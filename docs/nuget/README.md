# Lokad.Utf8Regex packages

Managed, bounds-checked regex libraries for .NET 10 and .NET 11. Match UTF-8
directly, using Unicode scalars and primary byte coordinates. No native regex
binary or BCL matcher is used by the libraries.

| Package | Supported profile |
|---|---|
| Lokad.Utf8Regex | .NET-style grammar, captures/history, backreferences, assertions, RTL and a NonBacktracking subset |
| Lokad.Utf8Regex.PythonRe | Python re-style grammar, flags, capture numbering and result shaping |
| Lokad.Utf8Regex.Pcre2 | Selected PCRE2 standard-matcher grammar, substitution, capture slots and partial probes |
| Lokad.Utf8Regex.Posix | Invariant scalar POSIX ERE subset, leftmost-longest selection and hierarchical capture priority |

Adapters require the exact matching Core version. These profiles have documented
boundaries; they do not promise full .NET, Python, PCRE2 or locale-aware POSIX
equivalence. POSIX excludes lookarounds, backreferences and locale collation.
PCRE2 excludes DFA matching, invalid-UTF subject mode, callouts, split and full
capture history; partial probing has a limited supported scope.

POSIX is case-sensitive by default. `PosixCompileOptions.IgnoreCase` uses invariant
Unicode 16.0 simple-lowercase equivalence, retaining longest-match selection and
original byte offsets. This single-scalar relation has no normalization or
multi-scalar expansion: I/i pair, dotted/dotless I remain separate, and final
sigma/long s are not merged with ordinary sigma/s. Brackets accept the case
closure of their members, including fixed ASCII named classes; `Newline` composes
with the option. It does not provide locale-aware POSIX classification.

## Scalar matching and validation

```csharp
using Lokad.Utf8Regex;

var regex = new Utf8Regex(".");
int count = regex.Count("A😀"u8); // 2 scalars
var match = regex.Match("😀"u8); // Utf8CaptureRange: byte endpoints [0, 4)
```

All profiles require already-valid UTF-8 subjects and unchanged borrowed bytes.
Regex operations do not validate subjects or expose a validation policy. Check
external bytes where they become text, using `System.Text.Unicode.Utf8.IsValid`
when the host representation does not already guarantee validity. Malformed
subjects have unspecified matching results; managed bounds checking, cancellation
and enforceable resource limits remain. Pattern, template and assembled-output
checks stay independent. Live route eligibility, scalar matching and output sizing
may still inspect text. Match results expose byte endpoints only.

PCRE2's explicitly enabled `\C` byte atom and special `\K` ranges have separate
slice-validity rules; check result slice properties before decoding those results.

## Cancellation, limits and ownership

Synchronous cancellation observes the original token. One absolute deadline and
finite work/depth/working/result policies cover the documented operation stages.
Explicit compilation factories offer separate construction controls. Defaults are
unlimited.

Memory limits admit modeled major buffers, retained payloads and growth overlap,
not total heap/RSS, arbitrary callbacks/getters or host retention. Caller-owned
output destinations are not producer-owned result allocations. Work units and
capacity charges depend on the engine/version/route: recheck finite policies when
upgrading. A refusal is distinct from no match; it does not publish truncated
successful output or retry under a different allowance.

Keep borrowed input alive and unchanged. Follow each cursor's copy/alias contract
and dispose program/result/cursor owners on early termination. Temporary loans
refund synchronously. Owned snapshots keep their documented lifetime; continued
host retention after releasing a receipt is the host's responsibility.

## Upgrading and further documentation

Version 0.4 introduced scalar matching over strict UTF-8 in place of the older
UTF-16 matching behavior. Version 0.5 adds .NET 11 assets and POSIX, consolidates
byte-only results and requires caller-valid subjects across all profiles. Remove
`inputValidation` arguments and `InputValidation` reads when upgrading. The old
validation option and malformed-subject rejection guarantees are removed.

NuGet's project-website link leads to the repository specifications, migration
guide, profile examples and benchmark tables. Those tables retain their measured
source/runtime identities; they are not a speed guarantee for every package.
NativeAOT publishing and trimming remain unqualified.
