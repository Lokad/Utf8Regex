# SPEC-PYTHONRE

`Lokad.Utf8Regex.PythonRe` is an optional sidecar package that exposes Python-style regular expression behavior over UTF-8 inputs.

## Intent

The package exists to model Python `re` semantics as a separate profile from base `Utf8Regex`, whose matching unit is one Unicode scalar over strict UTF-8. The Python profile retains its own grammar, case rules, numbering and global empty-match progression; .NET is a comparator only where behavior overlaps.

Like the base `Utf8Regex` library, this profile is culture-invariant by construction. Python `re.LOCALE` is intentionally out of scope. Python `re` bytes-pattern semantics are also intentionally out of scope in the current profile.

## Architecture

The implementation is parser-first:

1. parse Python-style source into a PythonRe AST
2. validate Python-specific semantic constraints
3. resolve lexical case rules and capture identities in the Python AST, then lower it to the shared scalar VM
4. render exact scalar atoms/ranges for retained native `Utf8Regex` controls; reuse the owned class decoder for profile atoms
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
* admitted scalar programs use owned CPython 3.13.2 / Unicode 15.1.0 case data.
  Unicode literals and classes include simple-lower inverses and the extra
  literal partners. Backreferences compare the captured/input scalars using
  simple lowercase only; they do not apply the literal-only partners. ASCII
  scopes fold ASCII letters only, and references follow the flags at the
  reference rather than the capture. Supplementary case pairs consume one
  scalar on each side even when their byte widths differ. The former Turkic,
  scoped-ASCII, Unicode-under-ASCII and complex ASCII-reference regressions
  now execute and pass. See the [CPython compiler](https://github.com/python/cpython/blob/v3.13.2/Lib/re/_compiler.py)
  for the profile distinction; the shipped code owns its implementation and
  derived data. `python tools/generate-pythonre-scalar-case-data.py --check`
  verifies the pinned data with CPython 3.13.2; no Python runtime is shipped.
* native direct matching, detailed discovery and Count now classify supplementary
  word/digit members and scalar-adjacent boundaries through the owned core. The
  existing CPython category spellings remain `[\p{L}\p{N}_]` for `\w`,
  `[\s\x1c-\x1f]` for `\s`, and decimal digits for `\d`. Their complements
  and bracketed forms retain the whole scalar domain. Admitted scalar programs
  now serve captured/global iteration, empty-capable result shaping, substitution
  and split with Python's same-position nonempty retry. Capture snapshots preserve
  Python numbering and normalized scalar byte ranges, including lookbehind.
  One-shot search/match/fullmatch and detailed data likewise use the profile
  scalar program, with byte starts and the original assertion context. FullMatch
  adds an end assertion before discovery so alternatives can backtrack to a
  complete result. Unserved managed callers and their compensation remain
  R04/R05 debt; category-version reconciliation and finite/result/output
  resource bounds remain separate gates.
* CPython 3.13.2 searches a supplementary scalar with a scoped-ASCII complement
  (`(?a:\W)`, `(?a:\D)` and bracket forms) and misses, while its own match/fullmatch
  find the whole scalar. Search, Match, and FullMatch here stay mutually consistent
  and follow the match/fullmatch oracle.

Unsupported or incomplete cases should fail explicitly rather than silently approximating Python semantics.

## Cancellation and resource contract

Every execution operation has an explicit `CancellationToken` twin that throws `OperationCanceledException` carrying an already-canceled token before matching begins, ahead of input validation. `FindIterDetailed` materializes its results. Admitted scalar global operations retain one invocation across whole-subject validation, discovery, capture mapping, bounded copying/decoding and callback boundaries. Tokens interrupt work synchronously; cancellation from an evaluator is observed immediately after it returns. The operation deadline persists across matches and callbacks instead of restarting for each discovery call. Library code does not interrupt arbitrary user callback code. Remaining managed routes still require the full R06 migration.

Timeouts pass through with `RegexMatchTimeoutException` identity. PythonRe supports
finite `Utf8ResourcePolicy.MaxWorkSteps`. One counter covers full-input validation,
candidate/backtracking work, required coordinate mapping, bounded result copying,
decoding, callback-return encoding and publication. The explicit match cursor
keeps that counter across moves; each separate operation starts a new invocation.
Exhaustion throws `Utf8ResourceLimitExceededException` with `ExecutionWorkSteps`,
`Limit` and `Requested`. Counters saturate at `ulong.MaxValue`; a denied request
beyond that value reports `Requested = ulong.MaxValue` without wrapping.

A step is a bounded engine/checkpoint unit, not one byte or scalar. Work counts
may change between releases; validation/copy loops operate in bounded blocks.
Finite calls use the metered scalar route rather than uncontrolled native/core
shortcuts. The limit does not bound wall time, retained output bytes or process
memory. Pattern compilation, lazy full-match compilation and replacement-plan
preparation retain separate construction backstops; they are outside execution
work counts. Arbitrary evaluator execution and later standalone result getters
are outside this cap; the library charges encoding/copying the evaluator's return.
Cancellation and the absolute invocation deadline remain independently enforced.

Finite depth, working-byte and result-byte policies still throw
`NotSupportedException` before pattern compilation. Zero budgets remain invalid.
The core still rejects every finite public invocation budget. PythonRe's work cap
does not qualify the unfinished common memory, retained-result or default-policy
gates.

`CreateMatchCursor(ReadOnlyMemory<byte>)` is an incremental, explicitly owned
alternative to the materialized `FindIterDetailed` array. Its offset/token overloads
retain a single invocation across advances, including host waits in the original
absolute deadline. The memory subject must stay unchanged and its manager alive
until EOF or disposal. Early abandonment requires `Dispose`; EOF/failure clean up
synchronously, without a finalizer. Reference aliases share position and lifetime;
concurrent/reentrant access is rejected. `Current` is valid only after a successful
advance, while independently owned snapshots remain valid after future advances
or disposal. Existing span/value API lifetimes are unchanged. Depth, working-byte and
retained-result controls and last-capture completion metadata remain unfinished.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
```
