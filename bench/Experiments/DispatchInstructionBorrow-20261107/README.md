# Scalar dispatch instruction borrowing: not retained

**Rejected; production restored.** The trial borrowed the immutable instruction
by readonly reference in RunSegment and rebound that reference in its adjacent
ASCII literal loop. It changed two local bindings, with no new state, helper,
mode, public API, allocation or numerical work policy. Construction freezes the
instruction array; no execution path mutates it.

Matching Linux Release FullOpts on source0f06dc60 shows the original16+4byte
temporary copy at both load sites. The trial removes those copies but grows the
method10554->11286code bytes. This identifies a code-generation trade-off; it
does not establish a public improvement.

ONE [public comparison](cost-summary.json), **93.364836154s**, separate Default
ABBA processes,18workloads/360samples/min65.9471ms. Core365EC927 ->B767CBCE;
PythonEED2DFAF,PCRED2494007 and callerF0D6C4D0 bytes/PDBs remain fixed. The Python
companion includes the accepted scoped invocation-transfer change. Normal
eight-CPU startup thenCPU2, default tiering/PGO,750ms/min32warmup, independent75ms
targets and five samples; one calibration confirmation precedes sampling.
Results are checked after timing. No tuning or noise rerun followed.

| Public operation | Before us | Trial us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual mail Count |3653.10|3648.41|-0.13%|1544|
| Same finite/live |5036.08|5121.93|+1.70%|1656|
| Same deadline/live |5674.29|5761.58|+1.54%|1704|
| Dense class union Count |132.70|128.90|-2.87%|0|
| Long captured class Count |52.56|53.51|+1.80%|64|
| Partial-letter Count |148.03|145.96|-1.40%|0|
| Folded category Count |155.37|156.94|+1.01%|0|
| Anchored FullMatch control |0.29454|0.30717|+4.29%|0|
| PCRE2 literal control |0.30476|0.31584|+3.64%|0|

Mail has no useful gain. Dense/partial gains and controlled-mail/capture/folded
costs have overlapping overall ranges. Anchored FullMatch+4.29%/+12.630ns and
PCRE2 literal+3.64%/+11.085ns are persistent/disjoint adverse observations.
All18allocation medians are unchanged. Preserve the unchanged-control movements
without normalization or an invented cause.

Unicode literal rows show apparent5-9% median gains, predominantly in after-b;
after-a resembles the baselines and ranges overlap. These native/validation
routes bypass the changed dispatch, so their movement does not establish its
benefit. [All process observations](process-observations.json) remain intact.
The net public evidence does not justify promotion. This is not a universal10%
rejection or proof that readonly references generally regress execution.

Prototype and restored Release checks pass on Windows/Linux, each .NET10/11:
Core1649,PythonRe38,PCRE2=46. They cover shared scalar dispatch, classes,
captures, lookarounds, RTL/NonBacktracking and finite/original controls. No test
mirrors the two local bindings. [Restoration](restoration.json) retains byte
identity and the launch correction: one Linux restoration wrapper omitted its
scratch-path environment variable; redirection failed before tests ran. Only the
environment was corrected, with no producer, input or timing change.

Canonical off-VM archive:
`artifacts/experiments/dispatch-instruction-borrow-20261107/evidence.tar.gz`,
`9C9D1DECCF4C31DAE1FD3BBB150D27F4147AA23AD73C15B1295798CA3A4FA76D`,4867535B.
The patch, both graphs/PDBs, unchanged caller, declaration, code generation,
prototype tests and every sample remain immutable. Restoration evidence is
archived separately. Frozen Full49 graphs and all264staged assets remain intact.
The accepted instruction/invocation parameter changes remain; no package/Full
qualification or NuGet publication was repeated. This trial is closed absent a
distinct demonstrated cause.
