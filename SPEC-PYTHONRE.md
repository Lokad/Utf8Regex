# SPEC-PYTHONRE

`Lokad.Utf8Regex.PythonRe` is an optional sidecar package that exposes Python-style regular expression behavior over UTF-8 inputs.

## Intent

The package exists to model Python `re` semantics as a separate profile from base `Utf8Regex`, whose matching unit is one Unicode scalar over strict UTF-8. The Python profile retains its own grammar, case rules, numbering and global empty-match progression; .NET is a comparator only where behavior overlaps.

Like the base `Utf8Regex` library, this profile is culture-invariant by construction. Python `re.LOCALE` is intentionally out of scope. Python `re` bytes-pattern semantics are also intentionally out of scope in the current profile.

## Architecture

The implementation is parser-first:

1. parse Python-style source into a PythonRe AST
2. validate Python-specific semantic constraints
3. render owned scalar atoms/ranges for `Utf8Regex`, including newline scopes and Python-specific case expansions
4. reuse admitted core scalar programs even where its default route chooses a native plan
5. retain the separate managed-compatible rendering only for remaining managed callers during migration; remove their execution and compensation code under PLAN.md R04/R05. The semantic authority remains the pinned CPython 3.13.2 `str`-pattern oracle

The native rendering consumes complete scalars. Supplementary class members and ranges use numeric scalar endpoints, rather than separate surrogate classes. Python's separate `\uD800`/`\uDC00` escapes denote separate surrogate atoms and cannot combine into a scalar; they cannot match strict UTF-8. A raw supplementary character or `\U00010000` escape denotes one scalar. A surrogate-only class contains no valid scalar, and its negation admits valid scalars. Captures retain primary byte ranges and derived UTF-16 coordinates. This describes native admission, not completion of every managed caller or resource gate.

## Current scope

Supported public operations:

* `IsMatch(...)`
* `Search(...)`
* `Match(...)`
* `FullMatch(...)`
* `Count(...)`
* `FindAll(...)`
  This is a structural helper that returns full-match records, not the CPython `re.findall(...)` result-shaping surface.
* `FindAllToStrings(...)`
  This is the dedicated CPython-shaped `re.findall(...)` string result surface:
  full-match strings with no captures, group strings with one capture, and tuple-shaped string rows with multiple captures.
* `FindAllToUtf8(...)`
  This is the host-oriented UTF-8 equivalent of the CPython-shaped `findall` result surface.
* `SearchToString(...)`
* `MatchToString(...)`
* `FullMatchToString(...)`
  These are host-friendly scalar helpers that return the matched string or `null`.
* `SearchDetailed(...)`
* `MatchDetailed(...)`
* `FullMatchDetailed(...)`
* `SearchDetailedData(...)`
* `MatchDetailedData(...)`
* `FullMatchDetailedData(...)`
* `FindIterDetailed(...)`
  These expose non-`ref struct` detailed match snapshots suitable for runtime hosts.
* `Replace(...)`
* `ReplaceToString(...)`
* `SubnToString(...)`
* `Subn(...)`
  This returns UTF-8 bytes plus the replacement count for host runtimes that want to stay bytes-first.
* `ReplaceToString<TState>(...)`
* `SubnToString<TState>(...)`
* `Subn<TState>(...)`
  These support evaluator-driven replacement over detailed match snapshots.
* `Replace<TState>(...)` with `Utf8PythonUtf8MatchEvaluator<TState>`
* `Subn<TState>(...)` with `Utf8PythonUtf8MatchEvaluator<TState>`
  These support evaluator-driven replacement while keeping replacement values in UTF-8 bytes end to end.
* `SplitToStrings(...)`
* `SplitDetailed(...)`
  This exposes the CPython split output shape with capture-vs-segment metadata for runtime hosts.

Supported feature families in the current profile:

* literals and alternation
* character classes and standard escapes
* greedy, reluctant, and possessive quantifiers
* capturing and named capturing groups
* named and numeric backreferences
* lookahead and fixed-width lookbehind
* atomic groups
* leading and scoped inline flags for `i`, `m`, `s`, `x`, `u` on `str` patterns
* Python replacement references `\\1` and `\\g<name>`
* Python escape parsing for `\x..`, `\u....`, `\U........`, and octal escapes on `str` patterns
* ASCII-mode semantics for `\w`, `\W`, `\d`, `\D`, `\s`, `\S`, `\b`, `\B`

## Offset contract

All `startOffsetInBytes` parameters follow CPython `pos` semantics against the pinned 3.13.2 oracle: the match must start at or after `pos`, while `^` still anchors at the real string start (or after a newline under `MULTILINE`), lookbehind and `\b`/`\B` keep their full prefix context, and lookahead and `$`/`\Z` keep their full suffix context. `FullMatch` additionally requires the match to cover the whole `[pos, end)` input. No `endpos` parameter exists; the input end always plays that role. Value, detailed, string, and bytes entry points agree on these rules.


## Deliberate boundaries

This profile is still incomplete. In particular:

* bytes-pattern semantics are intentionally unsupported in this profile; PythonRe only targets `str` regex semantics over UTF-8 inputs
* `LOCALE` semantics are intentionally unsupported; this profile is culture-invariant by construction
* `\N{...}` named Unicode escapes are intentionally unsupported; a faithful resolver would require embedding a large Unicode-name table, which is out of scope under the package-size constraint
* the execution classifier for `Utf8Regex` is conservative by design
* case-insensitive backreference comparison follows BCL invariant folding, which
  agrees with CPython `TOLOWER` comparison everywhere except U+0130
  participation: `(i)\1` against `i\u0130` and `(\u0130)\1` against
  `\u0130i` do not match here while CPython matches. All other BMP backreference
  shapes compare identically; astral scalars agree completely
* scoped `(?a:...)` regions under a case-insensitive Unicode pattern use backend
  folding and cannot exclude non-ASCII fold partners: `(?a:k)` matches U+212A
  here while CPython rejects it. Top-level ASCII runs the backend
  case-sensitively with fully explicit expansions and is exact, except
  backreferences over multi-character groups, which compare exactly instead of
  ASCII-folding (single-ASCII-character groups name both variants explicitly)
* Unicode-scoped backreferences under top-level ASCII compare exactly instead of
  folding: `(?u:(i)\1)` against `iI` misses here while CPython matches
* native direct matching, detailed discovery and Count now classify supplementary
  word/digit members and scalar-adjacent boundaries through the owned core. The
  existing CPython category spellings remain `[\p{L}\p{N}_]` for `\w`,
  `[\s\x1c-\x1f]` for `\s`, and decimal digits for `\d`. Their complements
  and bracketed forms retain the whole scalar domain. Global captured iteration,
  empty-capable result shaping, substitution and split still have managed callers;
  their supplementary categories, case comparisons and output costs require
  separate qualification and owned replacements. A native success does not prove
  those remaining operations. Unicode-data version reconciliation remains a gate.
* CPython 3.13.2 searches a supplementary scalar with a scoped-ASCII complement
  (`(?a:\W)`, `(?a:\D)` and bracket forms) and misses, while its own match/fullmatch
  find the whole scalar. Search, Match, and FullMatch here stay mutually consistent
  and follow the match/fullmatch oracle.

Unsupported or incomplete cases should fail explicitly rather than silently approximating Python semantics.

## Cancellation and resource contract

Same entry contract as the core library: every execution operation has an explicit `CancellationToken` twin that throws `OperationCanceledException` carrying an already-canceled token before matching begins, ahead of input validation. Lazy `FindIterDetailed` observes the token at creation. Evaluator-driven substitution additionally observes the token on every match iteration across owned and managed paths, so canceling from inside the callback aborts deterministically.

Timeouts pass through with `RegexMatchTimeoutException` identity. PythonRe defines no work, depth, or memory budgets; defaults are unlimited everywhere. The shared `Utf8ResourcePolicy` is accepted like the core library (unlimited only; finite budgets are rejected loudly).

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
```
