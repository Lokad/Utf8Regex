# PythonRe corpus porting guide

The local PythonRe semantic corpus is self-contained. The CPython checkout is reference material only.

## Source of truth

Prefer the CPython standard library sources first:

* `external/cpython/Lib/test/test_re.py`
* `external/cpython/Lib/re/_parser.py`
* `external/cpython/Lib/re/_compiler.py`

## Historical provenance limitation

The original CPython revision used to seed `ported-core.json` was not recorded
and cannot be recovered from this repository. Do not infer or claim a revision.
The benchmark snapshot therefore freezes the checked-in file itself, currently
seven vectors with SHA-256
`D443A4817B19A2156B70FDF90168D131823F27AF807B608744B49489BD82EAA5`, and
preserves each vector's local `Source` field. New CPython-derived vectors must
record the upstream commit in their source reference.

## Porting rules

* preserve upstream intent rather than rewriting the scenario into a different local shape
* keep each local case normalized to the `Utf8PythonRegex` API surface
* port cases into checked-in local JSON files; do not execute CPython tests directly from the .NET test run
* when one upstream scenario maps to multiple local API operations, split it into multiple local cases with distinct ids

## Provenance format

Preferred format:

* `cpython:Lib/test/test_re.py:test_fullmatch_atomic_grouping`
* `cpython:Lib/test/test_re.py:lines-2559-2584`
* `spec-pythonre:replacement-group-name`
