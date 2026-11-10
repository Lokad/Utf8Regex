# Focused trusted-input baseline

Frozen tool/producer source `9df5eb7cde15fd400b67560280c6a98842b61012`; production C#
is unchanged from dev66. One Linux .NET11 RC3 sequence takes **178.449s**, under
480s total/90s per command. Twelve existing cases, both policies, nine samples;
existing Core V3, paired CPython and POSIX protocols are reused. No Full, package
matrix, table rewrite or published/staged package change.

Times are complete public operations in microseconds, managed/reference. Core
references below are compiled predecoded .NET; Python uses long-lived CPython
3.13.2 with startup/compilation excluded. Validate checks the whole subject;
AssumeValid requires valid unchanged UTF-8. Different caller contracts remain
separate. Ordinary generated tables remain Validate.

| Operation | Validate managed/reference µs | AssumeValid managed/reference µs | Evidence |
|---|---:|---:|---|
| Core `industry/mariomka-email-count` | 1189.625/456.905 | 289.307/479.757 | V3 durations pass; per-operation sample ranges retained |
| Core `industry/leipzig-river-window-count` | 3302.963/1579.698 | 3937.335/1564.416 | V3 durations pass; per-operation sample ranges retained |
| Core `industry/leipzig-name-family-count` | 4056.353/3088.568 | 3516.509/3042.112 | V3 durations pass; per-operation sample ranges retained |
| PythonRe `findall/many-capture-strings` | 659.817/352.409 | 668.143/352.348 | CpythonFaster |
| PythonRe `findall/one-capture-strings` | 392.354/186.288 | 406.921/185.228 | CpythonFaster |
| PythonRe `split/captures` | 386.176/255.891 | 387.308/256.552 | CpythonFaster |

Core durations all exceed20ms; sample ranges are normalized by each lane's own
iteration count. V3 duration qualification is not a bootstrap confidence verdict.
Python string operations above remain CPython-faster under both policies. Byte
FindAll is distinct: CPython includes UTF-8 encoding of requested outputs; it is
not an isolated matching-engine comparison. Many-capture bytes are Inconclusive
under AssumeValid; Unicode captured bytes are ManagedFaster.

POSIX SKU remains about0.84µs/call under AssumeValid (4096 calls/batch); its
native comparison is Inconclusive because lane-order medians differ2.71%. Empty
captures remain NativeFaster. Finite/live/funded/deadline lanes, allocations and
workspace peaks are retained. Native UTF validation remains enabled, including
when the managed caller selects AssumeValid. The tiny Python trusted early-hit
control remains Unqualified (minimum2.518ms); this does not change the priority,
so no confirmation or calibration repair was spent.

## Next structural causes

1. Core river Count: trusted preparation is slower than default despite omitted
   validity checks. Trace all native structural helper consumers; Count/IsMatch
   need validity and ASCII eligibility, whereas mapped enumeration needs UTF-16
   totals. Remove discarded metadata without fabricating partial descriptions.
2. Python capture strings/split retain material absolute deficits. Audit lazy
   subject metadata, then the shared VM/cursor work and duplicated construction.
3. Core compiled name-family retains a smaller absolute gap. POSIX per-call/history
   work stays behind these larger costs. Email matching is not the first trusted
   input bottleneck; its default validity cost remains an explicit contract cost.

No source speedup is claimed by this baseline. Preserve uncertain lanes and use
a bounded producer comparison with unchanged controls for the next intervention.

Raw data, all samples/identities/result digests, DLL/PDB pairs and exact scripts:
`artifacts/experiments/performance-p0-20261110/evidence.tar.gz`, SHA256
`FF29F3E6D25EAAEFBF47F36C80F5CBB241427705547DF9D74717A48B190D26C4`.
The full derived summary is retained beside that archive, not as another tracked
benchmark catalog. Published/staged dev66 bytes remain immutable.
