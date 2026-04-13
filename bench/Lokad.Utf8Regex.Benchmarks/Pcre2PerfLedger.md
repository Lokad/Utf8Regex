# PCRE2 Perf Ledger

Iterations: 16384

RequestedIterations: 50

Samples: 3

| Case | Kind | Plan | IsMatch us | RawCount us | PublicCount us | NativeMaterialize us | RawEnumerate us | PublicEnumerate us | MatchMany us | ReplacementOnly us | PublicReplace us |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| pcre2/branch-reset-nested | BranchResetNested | `IsMatch=Utf8RegexEquivalent, Count=None, Enumerate=None, Match=Utf8RegexEquivalent, Replace=None` | - | 0.228 | 0.217 | 0.215 | 2.327 | 2.890 | 0.257 | 0.173 | 0.411 |
| pcre2/duplicate-names | DuplicateNamesFooBar | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | - | 0.254 | 0.210 | 0.279 | 2.484 | 2.985 | 0.279 | 0.183 | 0.289 |
| pcre2/same-start-global | ManagedRegex | `IsMatch=Utf8Regex, Count=None, Enumerate=None, Match=None, Replace=None` | - | 0.174 | 0.148 | 0.214 | 2.710 | 2.928 | 0.238 | 0.207 | 0.331 |
| pcre2/kreset-repeat | KResetRepeatAb | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | - | 0.247 | 0.191 | 0.181 | 2.395 | 3.185 | 0.290 | 0.118 | 0.244 |
| pcre2/kreset-captured-repeat | KResetCapturedRepeatAb | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | - | 0.176 | 0.156 | 0.191 | 2.451 | 2.851 | 0.229 | 0.113 | 0.252 |
| pcre2/kreset-atomic-alt | KResetAtomicAltAb | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | - | 0.175 | 0.151 | 0.183 | 2.306 | 2.774 | 0.233 | 0.122 | 0.245 |
| pcre2/branch-reset-followup | BranchResetSameNameFollowup | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | - | 0.204 | 0.182 | 0.212 | 2.201 | 2.855 | 0.248 | 0.157 | 0.251 |
| pcre2/conditional-negative-lookahead | ConditionalNegativeLookahead | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | 0.045 | - | - | - | - | - | - | - | - |
| pcre2/conditional-accept-negative-lookahead | ConditionalAcceptNegativeLookahead | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | 0.046 | - | - | - | - | - | - | - | - |
| pcre2/backslash-c-literal | BackslashCLiteral | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | 0.070 | - | - | - | - | - | - | - | - |
| pcre2/recursive-optional | RecursiveOptional | `IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None` | 0.155 | - | - | - | - | - | - | - | - |
