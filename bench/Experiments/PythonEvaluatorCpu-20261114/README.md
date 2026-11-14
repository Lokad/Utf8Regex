# Python callback replacement CPU attribution

One15-second public ReplaceToString capture on current Python `8ec2f992` and
Core `e8b20aa5`, .NET11 GA. Pattern `([a-z]+)`,1536 valid unchanged UTF-8 bytes,
384 prepared-constant callbacks; complete2304-character output checked after
32-call batches. Only the private caller built. Frozen DLL/PDB pairs match the
prior accepted staging, with every343/50 compiler document checked against saved
source bytes. Normal8CPU startup then existing placement. [Samples](cpu-summary.json).

17238 whole-process cpu-clock:u samples at997Hz over17.111s, including setup,
warmup, checks and runtime. SnapshotDetailed18.17%, MoveNext9.40%, class repeat8.93%,
SubstituteScalar8.85%, result AllocateArray6.80%, capture root5.56%, DecodeUtf85.05%,
candidate search4.93%. CoreCLR unresolved8.95% is NOT a GC attribution. Inlined
cost belongs to its containing method; these are not exact phase durations.

This points to owned snapshot/projection and matching orchestration rather than
justifying a new UTF-16 output writer. Source inspection found empty name metadata
still passes through result admission/allocation/copy helpers, and captures equal
to the whole match repeat its coordinate projection even though their text is
already shared. Investigate omitting that redundant work while keeping every
mutable group slot/name array, LastIndex and owned lifetime. The profile does not
justify a callback API change, capture compiler or decoder sweep.

Canonical managed/CPython timed callbacks already return prepared constants;
rich callback verification is untimed. No fairness defect or new CPython ratio
claimed. No throughput/allocation delta, optimization gain, producer rebuild,
package/Full/canonical refresh or confirmation. Raw verified offVM before keeper
release at `artifacts/experiments/python-evaluator-cpu-20261114/evidence.tar.gz`,
SHA256 `434231F3158D95FA7BDACF1DDD3516E15533DDABCA922F9B6D09751670DC8A01`. Worker and keeper terminal0.
