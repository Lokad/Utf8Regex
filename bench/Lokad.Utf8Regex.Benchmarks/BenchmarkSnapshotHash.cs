using System.Security.Cryptography;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

// Snapshot files travel across autocrlf checkouts, so raw-byte hashes differ
// per platform (CRLF worktrees vs LF worktrees). Hash the LF-normalized text
// instead: writers store these values and tests recompute them, and every
// checkout agrees. Machine-bound identities (processor, runtime versions,
// native fingerprints) stay untouched: paired-qualification evidence is
// single-machine by design.
internal static class BenchmarkSnapshotHash
{
    internal static string HashNormalizedFile(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}

