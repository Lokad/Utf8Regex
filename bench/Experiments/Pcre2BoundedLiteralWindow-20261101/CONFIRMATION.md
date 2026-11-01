# Sole selected confirmation

Initial fifteen-case comparison completed79.5437s, minimum53.8993ms, all results
equal and allocations unchanged. Date hit/miss -61.29%/-57.45%, finite hit/miss
+4.27%/+4.61%, deadline hit/miss+6.29%/+7.90%. UCP control+10.001231% with paired
observations+8.45..12.77%, straddling the allowance. Initial observations remain.

Use the same exact engine/Core DLL/PDBs. The existing scratch driver is filtered
to date hit/miss, finite hit/miss, deadline miss, UCP and literal control; only
the warmup is extended to3seconds/min1000calls. Same independent60ms calibration,
five alternating samples and full result/allocations consumption. One120s cap.
This assesses warm throughput; do not claim unchanged warmup-phase costs.
No other confirmation, optimization variant, source change, package/full refresh.
If a public penalty above10% persists, defer and revert the runtime prototype.
