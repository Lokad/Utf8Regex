# PCRE2 exact literal-family Count omits unused descriptions

Accepted producer `99270ebc`, compared with `2bdb3839` on .NET11 GA. Delete one
eager subject-description call. The existing analyzer admits nonempty exact,
case-sensitive alternatives and exact Core portfolios; prepared Count consumes
bytes without UTF-16 totals. The search/count algorithm, native UTF checking,
nonzero scalar starts and finite/options fallbacks remain unchanged. No API,
allocation, ownership mechanism or accounting policy is added.

One bounded public Count comparison used default native UTF checking, identical
case-sensitive literal alternatives/options and scalar counts in both lanes.
PCRE.NET reused its prepared match buffer. Compilation/input construction were
excluded; outputs were consumed after timing. Separate ordinary processes staged
matching producer DLL/PDB pairs and checked their source documents; caller and
native control identities stayed unchanged. Nine samples per lane/case exceeded
107ms, with a120-second cap per process. Retained int samples were bounded to4MiB;
transient allocation traffic was measured rather than treated as retained data.

| Public Count workload | Before µs | After µs | After/before, lower better | B/op before/after |
|---|---:|---:|---:|---:|
| ASCII miss | 37.458 | 37.861 | 1.011 | 0 /0 |
| Unicode hits | 75.006 | 26.967 | 0.360 | 0 /0 |
| Unicode miss | 323.477 | 72.645 | 0.225 | 0 /0 |

Unchanged native controls moved+1.23%,+0.25% and−0.29%, respectively. Different
allowed CPUs still limit exact causal attribution; these observations support
removing the unused scan without a confirmation or general parity claim.

The existing five-portfolio theory proves the production counter needs no
description over a borrowed view at a nonzero scalar start. Default/trusted
checking and anchored options retain their expected Counts. Focused Release
route/input/count/limit checks pass both runtimes, including finite limits and
original cancellation/deadlines. No replacement executor or test-only forwarding
implementation was introduced.

Raw samples, caller, identities and checks are in
`artifacts/experiments/pcre-literal-preparation-20261113/`. The immutable
[dev81 packages and complete catalogs](../DeliveryDev81-20261113/README.md)
precede this source change and remain unchanged. No package or full-refresh
qualification was repeated for this small follow-through.
