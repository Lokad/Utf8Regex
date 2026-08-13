# PCRE2 corpus porting guide

The local PCRE2 semantic corpus is self-contained. It does not depend on an
upstream checkout during build or test. `oracle-profile.json` pins the exact
upstream release and normalization profile used when importing cases.

## Source of truth

Use the official PCRE2 10.47 test corpus first:

* `https://github.com/PCRE2Project/pcre2/tree/pcre2-10.47/testdata`

Supplement with upstream docs only when a case needs interpretation:

* `https://github.com/PCRE2Project/pcre2/tree/pcre2-10.47/doc`
* `https://github.com/PCRE2Project/pcre2/blob/pcre2-10.47/README`
* `https://github.com/PCRE2Project/pcre2/blob/pcre2-10.47/RunTest`

## Porting rules

* preserve upstream intent rather than rewriting the scenario into a different local shape
* keep each local case small and normalized to the `Utf8Pcre2Regex` API surface
* port cases into checked-in local JSON corpus files; do not execute upstream files directly from the test run
* when one upstream scenario maps to multiple local API operations, split it into multiple local cases with distinct ids
* keep `Pattern` textual when possible; use `PatternBytesBase64` with
  `PatternEncoding` set to `Utf8BytesBase64` only when the pattern is not valid
  UTF-8
* malformed byte patterns are compiler inputs; they never enable matching an
  invalid UTF-8 subject

## Provenance format

Every local case must set `Source` with a stable provenance string.

Preferred format:

* `pcre2:testdata/testinput2:case-184`
* `pcre2:testdata/testinput15:lines-220-241`
* `pcre2:testdata/testinput3:pattern-branch-reset-duplicate-name`

If a case is derived from the spec instead of directly ported, say so explicitly:

* `spec-pcre2:nonmonotone-global-iteration`

## Status usage

Use statuses deliberately:

* `Active`
  The case is expected to pass and should run in normal CI.
* `UnsupportedYet`
  The case is in planned scope but not implemented yet.
* `OutOfScopeBySpec`
  The case is intentionally excluded by `SPEC-PCRE2.md`.
* `KnownFailure`
  The case should be in scope, currently fails, and must remain visible.

Do not use status as a vague parking lot. The status should answer whether the case belongs to the product and whether it should currently pass.

## First batches to port

Port in topical batches rather than file order:

1. compile errors and option handling
2. single-match capture slots and name table semantics
3. duplicate names and branch reset
4. partial matching
5. global iteration and zero-length restart behavior
6. replacement syntax
7. `MARK`, `\\K`, and `\\C`

## Review standard

For each imported batch, reviewers should be able to answer:

* where did this case come from upstream?
* why is the local normalization correct for `Utf8Pcre2Regex`?
* is the status correct for the current spec and implementation plan?
