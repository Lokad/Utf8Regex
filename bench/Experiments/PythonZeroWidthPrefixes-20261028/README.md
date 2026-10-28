# Reuse PythonRe candidate filtering through zero-width prefixes

3e691f73 extends the existing first-character interval filter. At most 64 readonly
instruction visits and a fixed 16-entry pending stack must establish that every
zero-width branch converges on the same consuming atom. The proof recognizes the
existing lookahead trailers; the original VM still executes every assertion and
decides each surviving match. No parser, matcher, public API or execution mode
was added.

After complete validation proves an ASCII subject, the profile's fixed digit,
word and space predicates have exact ASCII projections. Arbitrary tables,
multiple/negated members, folds and nonconvergent prefixes decline. Mixed inputs
retain Unicode class verification; ordinary positive ASCII-only masks still
filter mixed subjects. The [initial route](initial-route.txt) shows why the date
case previously attempted the VM at every scalar start.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt)
and [project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2, exact 4a8037bb
baseline versus 3e691f73. Ten public workloads, equal-result preflight, four lanes,
500 ms warmup and three alternating approximately 50 ms samples. One comparison
took **29.06s**, with a 120s cap. No A/B replay; a40bda03 remains the fixed
cumulative release baseline. The .NET diagnostic lanes overlap these subjects'
semantics and do not set PythonRe's public Status.

| Public operation | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Date IsMatch miss | 29.657 | 11.174 | 3568 / 3568 |
| Date IsMatch hit | 32.713 | 12.229 | 3568 / 3568 |
| Finite limits and live token | 39.190 | 14.427 | 64968 / 21936 |
| Dense ASCII candidates | 3305.369 | 1889.833 | 3568 / 3568 |
| 32 KiB without a possible first character | 16362.275 | 1.241 | 3568 / 0 |
| Mixed-Unicode scalar control | 2004.500 | 1954.604 | 3568 / 3568 |
| Existing mask/capture control | 0.780 | 0.748 | 3872 / 3872 |
| Exact literal control | 0.194 | 0.197 | 0 / 0 |
| Date Match control | 0.712 | 0.748 | 3568 / 3568 |
| Date FullMatch control | 0.725 | 0.742 | 3568 / 3568 |

Unchanged/control variation is -2.5%, -4.1%, +1.2%, +5.1% and +2.3%. Do not call
the small negative differences improvements. Candidate rejection can avoid VM
workspace; it preserves modeled admission rather than a fixed denial threshold.
Validation, original token/deadline, cumulative work and result ownership remain.

[Full PythonRe checks](python-both.txt) passed on both runtimes, followed by
[focused added checks](boundary-both.txt). Public operations at every scalar
offset are compared with the direct VM, independently of the new filter. Coverage
includes non-BMP digits, Python word boundaries around combining marks, captures,
retained aliases, malformed tails, work denial and original interruption/loans.
Durable hook witnesses now use enough input to cross 64 steps even with
2,048-byte candidate windows; exception, concurrency and lifetime assertions stay.
Failed intermediate tests are preserved in `correctness-attempts.zip` below.

The [first raw refresh](python-date-refresh.txt) warmed only 100 ms and measured
about 79 us, while its [paired qualification](python-date-qualification.txt)
measured about 11 us. 51605746 increases the existing retained-result warmup time
to one second, retaining its bounded call threshold and actual warmup fields.
One [confirmation refresh](python-date-confirmed-refresh.txt) is preserved;
independent raw medians remain distinct from qualified paired results. The nominal
full-catalog addition is about four minutes before batch overshoot and call-cap
savings. The <=60-minute complete refresh must be measured at the release boundary.

The [matching final paired run](python-date-confirmed-qualification.txt) qualifies
**13.14x CPython**, interval **13.09–13.27**, previously 37.57x. Managed median
11.396 us, CPython 0.865 us, managed allocation 3568 B/op, CPU3. The verdict remains
**CPython faster**. [Generated-page verification](python-date-confirmed-markdown.txt)
passed. Only common/date-miss changed; other cases, lifecycle/scaling, catalog and
corpus metadata are unchanged. No further measurement or package matrix was run.

Immutable evidence under `artifacts/experiments/python-zero-width-prefixes-20261028/`:

- `parity-python-date-3e691f73.tar.gz`: exact baseline/candidate DLLs and PDBs,
  driver, comparison and first refresh/qualification. SHA256
  `4081B880008E48983269965CC71C20759A94D0C968FE27382AF8CE06E52D5280`.
- `parity-python-date-confirmed-51605746.tar.gz`: final benchmark binaries/PDBs,
  raw confirmation, paired qualification and generated data/page. SHA256
  `0ECB0276A7ACB0E4E1B404E28285A20124F4735BFF4AD00D78D836BE37D37E03`.
- `correctness-attempts.zip`: initial setup/cursor-witness failures and route-build
  log. The implementation and bounded comparison are accepted; this pilot is closed.

CI 40735991657 passed Linux; Windows/net11 failed the existing ordinary literal-
family allocation witness (5056 B, allowed 0–512 B). The allocation source is
unconfirmed. Its 64-call warmup and separate measured loop are replaced by one
NoInlining batch warmed for one second at the measured call site. The allocation
threshold is unchanged, and every call now also verifies the expected miss.
[Focused both-runtime checks](core-allocation-fixture.txt) pass. Raw failed CI is
preserved beside the archives as `ci-40735991657-failed.txt`. No production pool
or matcher change, CI retry, benchmark replay or package qualification was used.
