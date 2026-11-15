# Folded POSIX run selection and multiline rewind fix

Accepted: separate the existing greedy-selection proof from eligibility for its
ASCII byte shortcut. Positive ASCII-origin literals/classes use invariant
lowercase-fiber keys to prove disjointness. Folded partners retain ordinary
scalar execution and trace projection, preserving original byte widths and
hierarchical captures. No new executor, owner, map, public switch or unchecked
access. The existing byte shortcut remains ASCII-only. Required overlaps,
repeated public captures, non-ASCII-origin classes and nullable anchored sequences
retain general selection. Construction reuses two stack masks, with bounded work
charged before the26-letter closure walk; program masks remain immutable.

The audit also found an existing correctness defect: with `Newline`,
`^([a-z]+)([[:space:]]+)$` on `a \nb` missed the valid `a ` match. The direct
run consumed LF and could not rewind to the preceding `$`. An independent public
fixture fails before the fix. The exact staged dev.97 DLL reproduces the miss;
the changed producer returns whole[0,2), groups[0,1)/[1,2). Decline the direct
proof when a multiline end anchor and LF-consuming run can require rewind.
This diagnostic witness is excluded from timing: incorrect/correct operations
are not an equivalent-result performance comparison. dev.97 bytes and original
records stay immutable; its additive KNOWN-ISSUES.md records the limitation.

Windows and SAME-byte Linux.NET10/11 each pass477 focused Release cases. Existing
independent corpus/executors cover every capture-span width, Unicode-width/floor
boundaries, bounded/nullable/overlapping runs, newline classes, Count/cursors,
retained aliases and resource refusal. Original-token/deadline injection also
reaches folded scalar execution. Untimed witnesses require changed public routes
to move from general capture selection to first acceptance while the ASCII
shortcut stays false. Case-sensitive SKU retains both its proof and byte route.

ONE122.195s ordinary-process ABBA,180s cap,0 confirmations. Same common Core and
runtime controls, normal8CPU startup then CPU2. Five150ms-target samples per
process/row,>=20ms floor. Valid unchanged UTF8, reused caller capture storage,
complete returned coordinate structs consumed after timing; compilation/input
preparation excluded. Finite/live retains its original token, five-second
deadline, work/depth/working/result policy and caller working allowance.

| Public operation | Before µs | After µs | Change | Before/after B/op |
|---|---:|---:|---:|---:|
| Folded disjoint captures, `KK007` | 3.792 | 0.606 | -84.01% | 1920 / 0 |
| Same, finite/live | 4.350 | 1.015 | -76.66% | 2984 / 808 |
| Capture after512 failed starts | 22.989 | 29.748 | **+29.40%** | 1920 / 0 |
| Disjoint Count,16 records | 5.664 | 4.787 | -15.49% | 0 / 0 |
| Disjoint capture cursor,2 matches | 5.482 | 1.177 | -78.53% | 2312 / 400 |

These five sample ranges separate. Keep the late-search regression: recording
the successful trace during ordinary scalar search also records failed starts,
where the former general discovery omitted tags. Accept this local trade-off for
the large short/cursor gains and allocation reduction; do not introduce another
search/replay mode or tune toward an invented universal10% gate.

All other public/unchanged control ranges overlap and allocations agree. Retain
case-sensitive SKU+5.24%, nullable+1.29%, Count+2.49%, folded Count+1.26% and
cursor+0.69% observations. Common Core-0.60%,.NET+1.19% and native-0.16% also
overlap. No broad speedup is inferred. The private native unambiguous comparator
stays~0.340us; this one observed ratio falls from~11.2 to~1.8. Ordered ambiguous
captures and different folding relations still prevent general POSIX parity.
Native workspace remains outside CLR allocation counts.

Before implements frozen0e10d86b; after is a234ac0d plus the patch. DLL/PDB pairs,
343 Core/12 POSIX compiler documents and checked managed-member inventories
verify. No VM producer build. SHA256s:

- Before POSIX `26A505E44686FB05BE98B5BC5C2FED7ACDFA8D7A791040F981B4E793A6ED518C`.
- After POSIX `1C9968773FC5D1573787F8E5E73AB01728B8CFE17499DA78961B6A7F732A9181`.
- Common Core `FF79676EE691198C507DE2FA8CEEEDF07B05B9A207153BCF9CE038C33D2AC1EA`.
- Raw `artifacts/experiments/posix-folded-greedy-proof-20261115/evidence.tar.gz`:
  `402D533E510F7D9AA7B261039E62F665B282B8778FE60B4E17C512031E09EBB2`.

Original worker/collector handles are terminal0; raw was hash-verified offVM
before keeper release. No package or Full qualification for this bounded source
change. A later immutable POSIX candidate must include the demonstrated bug fix;
the recent dev.97 Full remains historical evidence, not a measurement of this
new proof. Other profiles are unchanged.
