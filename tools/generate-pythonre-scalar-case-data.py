#!/usr/bin/env python3
"""Derive owned scalar case and identifier data from a pinned CPython oracle.

Use --check for verification. --write regenerates data after an explicit
Unicode/profile version decision; run it only with repository builds/tests/
benchmarks terminal. This tool and CPython are not shipped dependencies.
It copies no compiler or matching engine implementation.
"""
import argparse
import sys
import unicodedata
import _sre
from collections import defaultdict
from pathlib import Path
from re._casefix import _EXTRA_CASES

parser = argparse.ArgumentParser(description=__doc__)
mode = parser.add_mutually_exclusive_group(required=True)
mode.add_argument('--check', action='store_true')
mode.add_argument('--write', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
py = root / 'src/Lokad.Utf8Regex.PythonRe'

# Data, independently sampled from the installed CPython regex oracle. No
# interpreter, copied compiler, or runtime engine is a shipped dependency.
assert sys.version_info[:3] == (3, 13, 2)
assert unicodedata.unidata_version == '15.1.0'
pairs = [(c, _sre.unicode_tolower(c)) for c in range(0x110000)
         if not 0xD800 <= c <= 0xDFFF and _sre.unicode_tolower(c) != c]
inverse = defaultdict(set)
for c, lo in pairs:
    inverse[lo].update((c, lo))
groups = {}
for lo in set(inverse) | set(_EXTRA_CASES):
    values = set(inverse.get(lo, (lo,)))
    for fix in _EXTRA_CASES.get(lo, ()):
        values.update(inverse.get(fix, (fix,)))
    groups[lo] = tuple(sorted(values))
flat, offsets, lengths, shared = [], [], [], {}
for lo in sorted(groups):
    g = groups[lo]
    if g not in shared:
        shared[g] = len(flat)
        flat.extend(g)
    offsets.append(shared[g])
    lengths.append(len(g))

def array(name, values):
    rows = ['        ' + ', '.join(f'0x{v:X}' for v in values[i:i+12]) + ','
            for i in range(0, len(values), 12)]
    return f'    private static readonly int[] {name} =\n    [\n' + '\n'.join(rows) + '\n    ];\n'

source = '''namespace Lokad.Utf8Regex.PythonRe;

// Owned, sparse scalar data derived from CPython 3.13.2 (_sre), Unicode 15.1.0.
// Regenerate/check with tools/generate-pythonre-scalar-case-data.py.
// Literal classes include CPython's extra partners; references use simple lower
// alone. These are scalar decisions, independent of host ICU/NLS and UTF-16.
internal static class PythonReScalarCaseData
{
    internal const string UnicodeVersion = "15.1.0";

    internal static int LowerUnicode(int scalar)
    {
        var index = Array.BinarySearch(LowerKeys, scalar);
        return index < 0 ? scalar : LowerValues[index];
    }

    internal static int LowerAscii(int scalar) => scalar is >= 'A' and <= 'Z' ? scalar + 32 : scalar;

    internal static ReadOnlySpan<int> LiteralPartners(int scalar)
    {
        var index = Array.BinarySearch(PartnerKeys, LowerUnicode(scalar));
        return index < 0 ? ReadOnlySpan<int>.Empty : Partners.AsSpan(PartnerOffsets[index], PartnerLengths[index]);
    }

'''
source += '\n'.join(array(n, v) for n, v in [
    ('LowerKeys', [c for c, _ in pairs]), ('LowerValues', [v for _, v in pairs]),
    ('PartnerKeys', sorted(groups)), ('PartnerOffsets', offsets),
    ('PartnerLengths', lengths), ('Partners', flat)]) + '}\n'
identifier_source = '''namespace Lokad.Utf8Regex.PythonRe;

// Owned identifier data from CPython 3.13.2 / Unicode 15.1.0 str.isidentifier.
// Regenerate/check with tools/generate-pythonre-scalar-case-data.py. These are
// sorted disjoint half-open scalar intervals, independent of host categories.
// Validation preserves the original group spelling; it does not normalize it.
internal static class PythonReIdentifierData
{
    internal static bool IsStart(int scalar) => scalar < 128
        ? scalar is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_'
        : Contains(StartRanges, scalar);

    internal static bool IsContinue(int scalar) => scalar < 128
        ? scalar is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_'
        : Contains(ContinueRanges, scalar);

    private static bool Contains(int[] ranges, int scalar)
    {
        var low = 0;
        var high = ranges.Length / 2;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (scalar < ranges[2 * middle]) high = middle;
            else if (scalar >= ranges[2 * middle + 1]) low = middle + 1;
            else return true;
        }
        return false;
    }

'''
for name, accepts in [('StartRanges', str.isidentifier),
                      ('ContinueRanges', lambda c: ('A' + c).isidentifier())]:
    endpoints = []
    start = None
    for scalar in range(128, 0x110001):
        hit = scalar < 0x110000 and not 0xD800 <= scalar <= 0xDFFF and accepts(chr(scalar))
        if hit and start is None:
            start = scalar
        elif not hit and start is not None:
            endpoints.extend((start, scalar))
            start = None
    identifier_source += array(name, endpoints) + '\n'
identifier_source += '}\n'

for filename, contents in [('PythonReScalarCaseData.cs', source),
                           ('PythonReIdentifierData.cs', identifier_source)]:
    destination = py / filename
    if args.check:
        if destination.read_text(encoding='utf-8-sig') != contents:
            raise SystemExit(f'Pinned {filename} differs; explicit version/semantic review required.')
        print(f'Pinned CPython 3.13.2 / Unicode 15.1.0 {filename} verified.')
    else:
        destination.write_text(contents, encoding='utf-8')
        print(f'Wrote {filename}; qualify public semantics before committing.')
