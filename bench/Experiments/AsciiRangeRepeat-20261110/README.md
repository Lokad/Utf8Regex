# Checked span searches for no-rewind ASCII interval repeats

Reuse the existing constant-size contiguous-ASCII-set proof in RunClassRepeat.
For an already proved no-rewind repeat with minimum0/1 and no maximum, bounded
IndexOfAnyExceptInRange replaces scalar mask iteration. Reuse the existing
<=2048-byte dot/complement scan loop. No new prepared field, allocation, owner,
mode or executor. Other set shapes, bounded repeats, rewind choices, captures
inside repeats, RTL and NonBacktracking keep their existing rules.

**Accounting revision:** charge basic work/check original interruption before
each <=2048-byte block and at completion, even for unlimited calls. This retains
bounded input progress; exact work totals are route/version dependent. Memory,
depth, result admission and shared loans are unchanged. ASCII endpoints remain
scalar boundaries; a positive interval stops before a wider scalar. Core/Python
specifications document this coarser input quantum. A public trusted4KiB capture
under256 work steps changes from work refusal to an exact4096-byte/UTF16 match,
confirming that the changed helper is used. No metering is disabled for speed.

One quiet Linux .NET11 RC3 ordinary-process ABBA per input policy: **157.972s**,
14 public lanes,560 samples>=65.431ms. Common caller/Python/Pcre2 DLL/PDBs;
distinct Core. Eight CPUs visible before CPU2 affinity; normal runtime settings.
Stored strings, UTF8 tuples, every collected cursor capture and value coordinates
are checked after timing. Cursor rows include caller collection-array allocation;
they are not an isolated producer/iterator kernel. Parent starts at17 bytes,
returns there after each funded operation and releases to0; peak417 stays equal.

| Public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Long captured MatchDetailedData |71.214 → 13.201 (-81.46%)|71.734 → 14.360 (-79.98%)|
| Captured Count |397.656 → 377.206 (-5.14%)|406.259 → 383.709 (-5.55%)|
| Short captured Match |0.470 → 0.461 (-2.03%)|0.481 → 0.461 (-4.11%)|

These ranges are disjoint. Long fields are8192 ASCII letters and8192 digits,
with complete whole/capture strings requested. This is useful public output cost,
not a kernel estimate. Significant allocation baselines stay unchanged:242008 B
many-string/UTF8 tuples,94344 B one-capture strings,272 B captured Count/short
value,66000 B long detailed output,230136 B cursor collection. Occasional small
runtime traffic in the long row remains in the raw samples. PCRE2/literal controls
stay0 B; no native-parity result is inferred from producer comparisons.

Many-string FindAll moves -3.01% strict/-15.40% trusted; bytes -0.71%/-16.77%,
all overlapping/bimodal. Finite/live strings -5.52%/-4.47%, funded cursor array
-4.67%/-11.77%, deadline strings -1.75%/-2.56% also overlap. No stable FindAll
or governed throughput gain claim. Keep adverse controls: trusted one-capture
strings+4.43%; complement/Unicode+2.92%/+5.27%; folded Unicode+3.23%/+2.41%; all
overlap. Unchanged PCRE2+1.49%/+0.80% and strict literal-0.74%/-0.93% overlap.
Accept long-field and Count improvements without normalizing control movement.
No confirmation/tuned variant; capture-heavy parity remains active.

Windows Release per.NET10/11: affected Core565, full Python5071 and affected
PCRE2 workspace/resource/capture144 pass, including zero-allocation guards.
Linux Core565/Python340 per target pass. Tests cover scalar/vector/block offsets,
every work cutoff and parent refund, interruption inside the production loop,
malformed whole-input rejection, folded/partial-category/rewind/lazy/atomic cases,
retained results/cursor aliases and original public resource policies.

Baseline source d1b0f50a plus archived patch. Frozen Core/Pcre2 baseline metadata
is0165e5cb with matching production C#; common Python is the accepted anchored-
start build (8a37ec05 plus its patch), not the older benchmark-directory Python.
Graphs/PDBs, source patch, protocols/caller and every sample are verified offVM:
`artifacts/experiments/ascii-range-repeat-20261110/evidence.tar.gz`, SHA256
`3BD358AE7742E8E5A2A97DD7B407F994D86A68DDEA57E455E5D06DDA36DCA915`.
Linux evidence: `linux-checks.tar.gz`, SHA256
`49E447C0F8DE919EA934D93A4012F62E6FA9DD1ADD78CAFD977444C6A4A8D693`.
Complete derived summary and Windows logs accompany these archives. No package/
Full refresh, ordinary benchmark-table rewrite or immutable-artifact change.

CI42887365988 exposed a stale interruption fixture on Windows/Linux and both
targets: 2 KiB ASCII now completes before the diagnostic hook at step64. The
fixture uses256 KiB ASCII and verifies hook entry inside RunClassRepeat; the
original-token/deadline and borrowed-loan assertions remain. All172 terminal/
disjoint-repeat Release tests pass per target. This correction changes no
production code, benchmark graph or timing claim.
