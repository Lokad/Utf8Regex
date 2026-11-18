#!/usr/bin/env python3
"""Check or regenerate Core's BMP case table from Unicode 16.0.0.

Pass UnicodeData.txt from https://www.unicode.org/Public/16.0.0/ucd/.
The table groups scalars by simple lowercase. Dotted and dotless I are
handled separately by the matching culture policy.
"""

import argparse
import hashlib
import re
from collections import defaultdict
from pathlib import Path


def generate(data):
    lowercase = {}
    for line in data.decode("ascii").splitlines():
        fields = line.split(";")
        if fields[13]:
            lowercase[int(fields[0], 16)] = int(fields[13], 16)

    groups = defaultdict(list)
    for scalar in range(0x10000):
        if 0xD800 <= scalar <= 0xDFFF or scalar in (0x49, 0x69, 0x130, 0x131):
            continue
        groups[lowercase.get(scalar, scalar)].append(scalar)

    values = []
    index = [0xFFFF] * 0x10000
    for group in groups.values():
        if len(group) < 2:
            continue
        entry = len(group) << 13 | len(values)
        if len(group) > 7 or len(values) >= 0x2000 or entry == 0xFFFF:
            raise ValueError("Case group exceeds the packed BMP table format.")
        values.extend(group)
        for scalar in group:
            index[scalar] = entry
    return values, index


def replace_initializer(source, name, items, trailing_comma=""):
    rows = ["            " + ", ".join(items[i:i + 16]) for i in range(0, len(items), 16)]
    content = ",\n".join(rows) + trailing_comma
    pattern = rf"({name} =>\n    \[\n).*?(\n    \];)"
    updated, count = re.subn(pattern, lambda match: match[1] + content + match[2], source, flags=re.S)
    if count != 1:
        raise ValueError(f"Expected one initializer for {name}, found {count}.")
    return updated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("unicode_data", type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--write", action="store_true")
    args = parser.parse_args()

    data = args.unicode_data.read_bytes()
    if hashlib.sha256(data).hexdigest() != "ff58e5823bd095166564a006e47d111130813dcf8bf234ef79fa51a870edb48f":
        parser.error("UnicodeData.txt must be the original Unicode 16.0.0 file.")
    values, index = generate(data)
    root = Path(__file__).resolve().parents[1]
    path = root / "src/Lokad.Utf8Regex/Internal/Execution/Utf8SliceCaseEquivalences.cs"
    source = path.read_text(encoding="utf-8")
    updated = replace_initializer(source, "InvariantFiberValues", [f"'\\u{v:04X}'" for v in values])
    updated = replace_initializer(updated, "InvariantFiberIndex", [f"0x{v:04X}" for v in index], trailing_comma=",")
    if source != updated:
        if args.check:
            raise SystemExit("Core BMP case table differs from Unicode 16.0.0.")
        path.write_text(updated, encoding="utf-8")
    print(f"Core BMP case table verified ({len(values)} values, {len(index)} index entries).")


if __name__ == "__main__":
    main()
