# POSIX history-prefix proof foundation

No production change or performance claim. Six new cases extend the existing
independent scalar-extent oracle: ambiguous/reversed alternatives, nested and
nullable occurrences, astral input, byte floors and output-slot independence.

Two counterexamples constrain future pruning:

- `(aa|a)(aab)*` on `aaab`: a shorter first capture permits a longer whole.
- `((aab|aa|ba)*)([ab]*)` on `aaba`: a shorter closed descendant permits a longer
  higher-priority parent, even when the whole endpoint is already maximal.

Full Release POSIX suite passes **247 checks per target** on Windows and Linux,
.NET 10 and 11. Library source and immutable packages remain unchanged.
Patch/log archive: `artifacts/experiments/posix-history-prefix-20261111/oracle-checks.zip`,
SHA256 `5E6BE87EE4E486967F8CA9026CD3B895AF2D2BF789D1C64B3065C563F606F47B`.
