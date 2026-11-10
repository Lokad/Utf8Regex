# PCRE2 complete ASCII class-run Count

Accepted after `df8858a0`. For a complete positive-minimum, unbounded greedy
character-class repeat over validated ASCII, count maximal member runs once
instead of retrying every short-run suffix. Reuse the existing prepared byte set
and runner, with checked span searches and bounded 4096-byte scan charges.
There is no new engine, buffer, cache, program metadata or public API.

Eligibility excludes caseless classes, compile anchoring/end anchoring/first-line,
nondefault match flags, CRLF-continuation skipping, diagnostic hooks and controlled
invocations. Nullable, bounded, lazy and non-ASCII subjects retain their runners.
CRLF settings alone do not necessarily imply continuation skipping: the eligible
class control still improves. UCP membership uses the existing prepared ASCII set.
Strict whole-input validation and scalar-floor checks precede dispatch. Candidate
and bounded scan charges remain; finite/funded/deadline policies use the existing
execution and admission. Captures, result lifetimes and working peaks are unchanged.

One valid **95.905s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
24 public lanes,480 samples, minimum71.324ms. Same Core/caller, one PCRE2 producer
per process,8 startup processors, affinity set after runtime startup. Counts and
zero outstanding loans are checked outside timing. No noise confirmation or
package/catalog/Full repeat; immutable dev62 packages and snapshots stay unchanged.

| Public Count/control | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Catalog word-class miss |236.354|24.697|−89.6%|
| Same, finite |1638.478|1662.870|+1.5%|
| Same, funded |1656.245|1664.589|+0.5%|
| Same, deadline |1834.690|1871.160|+2.0%|
| Long-word hits |247.754|37.946|−84.7%|
| Dense minimum-one runs |287.560|74.410|−74.1%|
| Negated class |166.270|37.240|−77.6%|
| One65536-byte run |383.080|6.550|−98.3%|
| Non-ASCII fallback |235.640|236.030|+0.2%|
| Caseless fallback |255.880|263.490|+3.0%|
| Compile-anchored fallback |0.361|0.521|+44.1%|
| Match-flags fallback |0.495|0.647|+30.7%|
| Unchanged Core Count |0.850|0.850|+0.1%|

Accept the large eligible gains with the measured approximately0.15–0.16µs
anchored/flagged penalties. Their process ranges are disjoint; do not dismiss
them as noise. All allocation counts and funded peaks are unchanged, including
320B for catalog miss and1504B for hits. [Summary](summary.json) retains every
lane, process median and range without normalization. No new native ratio is
claimed: the native comparator was not retimed.

The8208-byte catalog subject is copied from the existing common-search builder,
SHA-256 `5F34B28F92B157A6A110DBB76F66705EB03089A48651F3F2DE46727E01949C55`.
Actual-facade preflight proves the existing single-token repeat owner, minimum10,
unbounded maximum, greedy class and prepared set, with helper absent/present.
Before PCRE2 DLL `D8866386D9C64AD364DB22F9EA0C1D0680CC6A78E616A132705FB672EE94C270`;
after `06523AA76C6DE816BB10DA51402647655E2EE182467E153FED4799A71CDF0D33`.
Common Core `A459B397B644D413DDD35A2370BAABC08D18D190889D52B704C027B02D247232`.

All6647 PCRE2 Release checks pass per target/platform on Windows/Linux, .NET10/11.
Twelve new public cases cover block boundaries, tails, offsets, NUL, negation,
UCP/non-ASCII, lazy/bounded/caseless/nullable alternatives, anchoring and CRLF.
Existing refusal, original-token interruption, deadline and refund checks remain.
Initial fixture mistakes (nullable EOF and unsupported inline newline spelling)
and a preflight reflection-flags error were corrected before accepted timing;
their failed logs are retained, not counted as passing evidence.

An initial timed attempt is **invalid**: both slots loaded the candidate producer.
Its apparent differences are not change evidence. Corrected staging explicitly
copies frozen DLL/PDB pairs and rejects equal producer hashes before timing;
both preflights verify actual loaded identities and helper engagement. The valid
run above is the sole usable before/after comparison, not a noise confirmation.

Raw protocol, graphs, preflights, checks and samples are verified off VM:
`artifacts/experiments/pcre-class-run-count-20261110/verified-evidence.tar.gz`,
SHA-256 `BC76DF7307A0895946FCE37B78B7D08B900078F4022ACE54328FD5C5868F96E3`.
The invalid attempt is preserved separately as `evidence.tar.gz`,
SHA-256 `F743502B6BC92C8321D8B55E8386985A7CA5D4056E91B2BC93CD0CF17973827E`.
Windows checks/preparation failures are alongside the archives. Source CI42732742433
passes Windows/Linux for1df925f7055f37bc4992818a0aa7daa4f71435e1.
