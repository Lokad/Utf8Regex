# Driver correction after aborted first attempt

The first attempt terminated before any completed row: the timed duration/result
guard fired. Its old one-million-call cap can prevent very fast routes reaching
the declared sample floor. No completed comparison/acceptance evidence exists
from that attempt; retain its original stdout/stderr/exit status and driver.

Only the disposable harness cap changes to16million calls (bounded64MiB result
batch), with explicit failure values. Engine/Core DLL/PDBs, workload definitions,
warmup, independent calibration target, five samples, order and gates are unchanged.
One corrected <=120s comparison, at most the originally allowed selected noise
confirmation. No retry after another harness failure without investigating its
actual reported values, and no engine variants. This is a calibration repair,
not a performance qualification result or a discarded adverse measurement.
