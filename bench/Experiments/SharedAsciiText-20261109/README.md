# Rejected short-ASCII result decoder

Investigated Core's shared result decoder for PythonRe capture-string findall
and Core/PCRE2 replacement text. For previously validated UTF-8, equal byte and
UTF-16 lengths prove ASCII. The candidate used `Encoding.ASCII.GetString` for one
nonempty block of at most 4096 bytes, preserving preallocation admission and the
existing two checkpoints. Longer, empty and non-ASCII paths stayed unchanged.

One .NET 11 RC3 quiet-VM ordinary-process ABBA completed in **40.776 seconds**
under a declared 180-second cap. The same immutable PythonRe/PCRE2 adapters and
worker DLLs ran throughout; only Core changed. Normal startup before affinity,
500 ms warmup, independent 75 ms calibration, five samples/process, 20 ms floor.
Complete outputs were verified after timing. This is a producer comparison,
not a new CPython/native ratio.

| Public operation | Before µs | Candidate µs | Change |
|---|---:|---:|---:|
| Python one-capture findall, unlimited | 476.73 | 2047.21 | +329.4% |
| Same, finite/live | 597.93 | 582.37 | -2.6% |
| Python many-capture findall, unlimited | 834.86 | 798.48 | -4.4% |
| Same, finite/live | 1295.14 | 1228.19 | -5.2% |

The single-capture regression appears in both after processes: medians
2010.01/2150.71 µs versus 477.94/458.29 µs before. Sample ranges are disjoint.
Allocations remain unchanged. Unicode strings, Core/PCRE2 replacement and boolean/
Count controls have small overlapping movements. The runtime/code-generation
cause is unestablished; do not dismiss the public regression as a kernel artifact
or infer a universal ASCII-decoder cost.

**Rejected and reverted**, including its trial-only tests. The small other gains
do not justify this regression. No confirmation or further tuning campaign.
Windows focused checks had passed on both targets (Python rendering/interruption/
resources/ownership, Core output admission/replacement and PCRE2 result/replacement),
but correctness alone does not qualify a performance change. No package changes.

[summary.json](summary.json) retains per-process medians, ranges and allocations.
Raw worker, patch, fixed binaries and samples:
`artifacts/experiments/shared-ascii-text-20261109/vm-evidence.tar.gz`, SHA-256
`E934B35F9012119644FE1B8472235B129B9AB073C0BD2D359F4F458B69C67F64`.
