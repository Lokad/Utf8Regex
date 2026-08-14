# Prepared search-kernel complexity

This file is the complexity contract for the flavor-neutral prepared search
kernels. Here, `n` is the searched byte length, `m` is one literal length,
`L` is the sum of prepared literal lengths, `k` is the number of literals, and
`c` is the number of candidates returned to a verifier. Preparation is paid
once per compiled regex; scan state is retained by operation cursors.

| Owner | Preparation | Stateful scan envelope | Retention rule |
|---|---:|---:|---|
| `PreparedByteSearch` | `O(k)` | `O(n)` | `SearchValues<byte>` is retained only for sets larger than three bytes. |
| `PreparedSubstringSearch` | `O(m + alphabet)` | `O(n * m)` worst case; vector/Boyer-Moore tiers are sublinear on favorable input | Shift tables and selected comparison offsets are prepared once. |
| `PreparedQuotedAsciiRunSearch` | `O(1)` | `O(n)` | No candidate revisits an earlier opening quote. |
| `PreparedAsciiIgnoreCaseLiteralSetSearch` | `O(L + alphabet)` | `O(n * max(m))` worst case | Folded buckets and `SearchValues<byte>` instances are retained. |
| `PreparedSmallAsciiLiteralFamilySearch` | `O(L + alphabet)` | `O(n * k * max(m))` worst case | Anchor filters and pair/triple dispatch are prepared once; rejected candidates advance by at least one byte. |
| `PreparedLiteralSetSearch` | `O(L + alphabet)` | `O(n * k * max(m))` worst case | Packed, unique-anchor, and prefix-discriminator states are mutually exclusive prepared strategies. |
| `PreparedMultiLiteralSearch` packed/prefilter tiers | `O(L + k * alphabet)` | `O(n * k * max(m))` worst case | Every prefilter advances monotonically; selected offsets, masks, and buckets are retained. |
| `PreparedMultiLiteralSearch` automaton tier | `O(L * alphabet)` | `O(n + matches)` | The automaton state is carried in `PreparedMultiLiteralScanState`; it never restarts after a rejected match. |
| `PreparedMultiLiteralSearch` trie tier | `O(L)` | `O(n * max(m))` worst case | Trie nodes are immutable after preparation. |
| `PreparedSearcher` | selected-owner cost | selected-owner cost | It is a typed dispatcher and does not add another scan. |
| `PreparedWindowSearch` | two selected-owner costs | `O(leading work + trailing work + c)` | Leading and trailing scan states are both retained. A dense leading stream with an absent trailing literal scans the trailing suffix once, not once per leading candidate. |
| `Utf8LiteralEquality` | `O(1)` | `O(m)` per requested comparison | Bounds checks precede every comparison; no input scan is hidden here. |
| `AsciiSearch` preparation helpers | `O(L + k * alphabet)` | none | These helpers only construct retained search data. |
| `Utf8SearchKernel` scalar/vector primitives | `O(1)` | `O(n)` | Each primitive is a single monotone pass over its supplied span. |

Compositions may multiply candidate confirmation by the confirmed literal
length, but no prepared kernel restarts a losing source at byte zero. Candidate
portfolio merging is accounted for separately by `Utf8CandidatePortfolioCursor`:
each source advances monotonically and total merge work is linear in the sum
of source advances plus yielded candidates.

Any new retained kernel must add a row here and an adversarial test or warm
benchmark covering its declared worst-case input shape.
