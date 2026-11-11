# Current AWSquick and capture-string comparisons

One selective refresh on clean accepted source6f46f988, .NET11 RC3 quiet VM,
**Validate**. Existing catalog commands refresh AWSquick and Python's one-/many-
capture string operations; other rows retain their historical measured sources.
No producer A/B percentages are composed into these comparator ratios.

| Public operation | Managed µs | Current predecoded comparator µs | Ratio | Previous recorded ratio |
|---|---:|---:|---:|---:|
| AWSquick Count, ordinary |3778.765|9550.860 (.NET)|0.396×|0.677×|
| AWSquick Count, compiled |3805.295|3654.590 (compiled .NET)|1.041×|1.814×|
| Python many-capture strings |649.661|343.797 (CPython)|1.889×|2.348×|
| Python one-capture strings |428.963|217.061 (CPython)|1.979×|2.347×|

Lower is better. Compiled AWSquick remains a Gap at the existing2% classification
threshold. Python remains CPythonFaster: paired95% intervals1.878–1.917 and
1.976–1.988, order effects.988/1.004. Capture-output allocations remain242008B and
94344B. These are full requested string results, not isolated matching phases.
CPython3.13.2 runs in its existing long-lived worker with compilation/startup
excluded; preflight checks full semantic/consumption digests. Core comparisons
retain separate ordinary/compiled and decode-inclusive .NET lanes. The observed
decode-inclusive controls changed substantially too; their movement is not
attributed to the producer or used to normalize a speedup.

One affected benchmark-project net11 Release build, reused for all commands.
Combined measurements/verifiers were capped at180s after the120s build cap;
all commands completed successfully. Existing Core V3 has9 samples/lane with
minimum75.112ms. Python raw diagnostic rows use5 samples, paired qualification
uses9 samples with minimum49.701ms. Loaded DLL/PDB source checks require matching
clean producers; runtime affinity is applied after normal startup. No new timing
driver or qualification framework, confirmation, full refresh or package matrix.

Raw neighbor checks prove exactly two Core rows (one case in two sections) and
two Python cases changed. Every other row, lifecycle and scaling record is
identical to its accepted snapshot. Generated README/summaries come from existing
writers; source identities, descending ratio ordering and existing uncertainty
are preserved. Focused Release snapshot tests pass on Windows/Linux and both
.NET10/11: Core2/Python1 per target, reusing matching-source binaries.
Current-row parity and Python Markdown verifiers pass. The full Python freshness
verifier is reserved for a coherent full run; the other88 rows are intentionally
historical and are not relabeled as current-source measurements.

The first export failed after all measurements/verifiers and neighbor checks
succeeded: its copied shell script named a nonexistent compact Parity.json.
Export-only recovery preserved completed results and copied the actual Core/
Python summaries. No measurements were repeated. Original/recovery scripts and
all logs remain in the raw archive.

Hash-verified off-VM evidence is retained under
`artifacts/experiments/current-gap-refresh-20261111/`:

- Raw snapshots, explicit producer graph/PDBs, samples, protocol, exports and logs:
  F90C7228139E47B16ECA2903EA5E57007E1E83629EB0FE173C65F9D4BF79021D.
- Windows/Linux snapshot checks:
  0685AA8B6E0708AC1F9C665240C1703707038B571ADC9A2B93C6A87144F16839.

Published/staged package bytes and the frozen dev.68 full qualification remain
immutable. This selective source refresh does not qualify new package bytes or
complete the remaining strict-validation, warm-capture and POSIX history work.
