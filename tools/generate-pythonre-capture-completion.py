#!/usr/bin/env python3
"""Generate/check complete scalar capture outcomes with the local CPython oracle.

Only test data is produced. CPython is not a shipped dependency. --check runs
with other CPython versions too; --write records the interpreter provenance.
"""
import argparse
import json
import re
import sys
from pathlib import Path

# Includes the pinned downstream nested/repeated/optional completion controls.
CASES = [
    ("((a))", "a"), ("((a)+)", "aaa"),
    ("(?P<outer>(?P<inner>a))", "a"), ("(?P<outer>(a))", "a"),
    ("((?P<inner>a))", "a"), ("(?:(a)|(b))+", "aba"),
    ("((a)|(b))+", "ab"), ("(?:(a)(b)?)+", "aba"),
    ("((a)(b)?)+", "aba"), ("(?P<outer>(?P<left>a)|(?P<right>b))+", "ab"),
    ("((?=(a))a)", "a"), ("(?<=(a))(b)", "ab"),
    ("((a|ab))b", "abb"), ("(?:(a)c|(b))", "ab"),
    ("(a)(b)?", "a"), ("(a)(b)?", "ab"), ("(a)?", ""),
    ("()", ""), ("((a)?)", ""), ("a", "a"),
    ("(?P<outer>(😀))", "😀"), ("(?P<outer>(😀é))", "x😀é"),
    ("(😀)(é)?", "😀"), ("(?P<a>a)|(?P<b>b)", "b"),
    ("(?P<a>a)?(b)", "b"), ("(?P<a>a)+a", "aaa"),
    ("(?P<a>a)+?a", "aaa"), ("(a)+?b", "aaab"),
    ("((a)*)b", "aaab"), ("((a)*)b", "b"),
    ("(a)|(b)", "c"), ("(a)?", "b"),
    ("(?=(?P<future>a))", "a"), ("(a)(?=(b))", "ab"),
    ("(?!(a)b)(a)", "a"), ("(?!(a))()", "b"),
    ("(?:(?=(a))ab|(a))", "a"), ("(?:(a)b|(?P<ok>a))", "a"),
    ("(?<=(a)(b))", "ab"), ("(?<=(a)(b))c", "abc"),
    ("(?<=((a)|(b)){2})", "ab"), ("(?<=(?:(a)|(b)){2})", "ab"),
    ("(?<=(.){2})", "😀é"), ("(?<=(?P<last>.){2})", "😀éx"),
    ("(?<=((😀)(é)))", "😀é"),
    ("(?<=((a)(?=(b))))b", "ab"),
    ("(?<=((?<=a)(b)))c", "abc"),
    ("(?<=(a)(?=(b)))b", "ab"),
    ("(?<!((a)(b)))c", "xc"), ("(?<!((a)(b)))c", "abc"),
    ("(a)(?<=((a)))", "a"), (r"(ab)(?<=\1)", "ab"),
    (r"(😀)(?<=\1)(é)", "😀é"),
    (r"(?P<word>😀)(?<=((?P=word)))(é)", "😀é"),
    ("(?<=(a|b)(c|d))", "bd"),
    ("(?<=(a(?=(b))|c(?=(d))))", "ab"),
    ("(?<=(a?){0})()", "x"),
    ("(?<=(?P<x>)){0}()", ""),
    ("(?:(?P<x>😀)|(?P<y>é))+", "😀é😀"),
    (r"((a)|(b))+\2", "aba"), (r"(?P<x>a)(?P=x)(b)?", "aa"),
    ("(a)?(?(1)b|(c))", "c"), ("(a)?(?(1)(b)|(c))", "ab"),
    ("(?>(a|ab))(b)", "ab"), ("(?:(?>(a))b|(a))", "a"),
    ("(?P<x>a)++", "aaa"), ("(?P<empty>)(?P<maybe>a)?", "a"),
    ("(a*?)", "aa"), ("((a)?)*", "a"),
]

def snapshot(match, text):
    if match is None:
        return {"success": False, "lastIndex": None, "lastGroup": None, "groups": []}
    groups = []
    for number in range(match.re.groups + 1):
        start, end = match.span(number)
        groups.append({"success": start >= 0, "text": match[number] or "",
            "startByte": len(text[:start].encode()) if start >= 0 else 0,
            "endByte": len(text[:end].encode()) if end >= 0 else 0,
            "startUtf16": len(text[:start].encode('utf-16-le')) // 2 if start >= 0 else 0,
            "endUtf16": len(text[:end].encode('utf-16-le')) // 2 if end >= 0 else 0})
    return {"success": True, "lastIndex": match.lastindex,
        "lastGroup": match.lastgroup, "groups": groups}

def cases():
    result = []
    for number, (pattern, text) in enumerate(CASES):
        regex = re.compile(pattern)
        result.append({"id": number, "pattern": pattern, "input": text,
            "single": [snapshot(method(text), text) for method in
                (regex.search, regex.match, regex.fullmatch)],
            "global": [snapshot(match, text) for match in regex.finditer(text)]})
    return result

parser = argparse.ArgumentParser(description=__doc__)
mode = parser.add_mutually_exclusive_group(required=True)
mode.add_argument('--write', action='store_true')
mode.add_argument('--check', action='store_true')
args = parser.parse_args()
target = Path(__file__).resolve().parents[1] / 'tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/capture-completion.json'
rows = cases()
if args.write:
    target.write_text(json.dumps({"oracle": sys.version, "cases": rows},
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
else:
    assert json.loads(target.read_text(encoding='utf-8'))['cases'] == rows
print(f'CPython {sys.version.split()[0]}: {len(rows)} complete capture fixtures verified.')
