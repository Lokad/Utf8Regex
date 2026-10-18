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
    # Sparse prefix guidance must preserve lexical profile matching rather than
    # treating the translated native plan as the complete matcher.
    (r"mark(?P<x>k)(?i:(?P=x))", "xxmarkkK yy markkK"),
    (r"mark(?P<x>Σ)(?i:(?P=x))", "markΣς markΣσ"),
    (r"mark(?P<x>İ)(?i:(?P=x))", "markİi markİİ"),
    (r"mark(?P<x>K)(?ai:(?P=x))", "markKK markKK"),
    (r"(?i:k)mark(?P<x>a)", "Kmarka Kmarka kmarka"),
    (r"(?ai:k)mark(?P<x>a)", "Kmarka Kmarka kmarka"),
    (r"mark(?i:[A-Z])(?P<x>.)", "markİ😀 markıé markſx markKx"),
    (r"(?P<x>😀)?(?(x)mark|other)", "other xx 😀mark mark"),
    (r"(?P<x>😀){0}(?(x)mark|other)", "mark other"),
    (r"(?P<x>)(?(x)mark|other)", "other mark"),
    (r"(?<=(?P<x>😀))mark(?P=x)", "😀mark😀 x mark😀"),
    (r"(?<=(?i:k))mark(?P<x>.)", "Kmark😀 Kmarké"),
    (r"mark(?=(?P<x>😀))(?P=x)", "mark😀 marké"),
    (r"mark(?:(?P<x>a)b|(?P<y>a))", "marka markab"),
    (r"mark(?P<x>a)?(?(x)b|c)", "markc markab markb"),
    (r"mark|", "mark x"),
    (r"(?:mark)?(?P<x>)", "mark x"),
    (r"mark(?P<x>)|mark(?P<y>ab)", "markab mark"),
    (r"(?P<x>éé)|(?P<y>é)", "ééé xé"),
    (r"(?P<x>😀é)|(?P<y>😀)", "😀é😀 x😀"),
    (r"mark(?P<x>𐐀)(?i:(?P=x))", "mark𐐀𐐨 mark𐐀𐐀"),
    (r"mark(?P<x>𐐀)(?ai:(?P=x))", "mark𐐀𐐨 mark𐐀𐐀"),
    # Required empty iterations must not stop Python's optional repeat phase.
    # Capture state can enable a consuming branch only after the minimum.
    (r"(?:(?(2)(a)|()))+", "aa"),
    (r"(?:(?(2)(a)|()))+?", "aa"),
    (r"(?:(?(2)(a)|()))++", "aa"),
    (r"(?:(?(3)(?(2)(a)|())|())){2,4}", "aa"),
    (r"(?:(?(3)(?(2)(a)|())|())){2,4}?", "aa"),
    (r"(?:(?(3)(?(2)(a)|())|())){2,4}+", "aa"),
    (r"(?:(?(2)(😀)|()))+", "😀😀"),
    (r"(?:(?(3)(?(2)(😀)|())|())){2,4}", "😀😀"),
    (r"(?:(?(2)(a)|()))*", "aa"),
    (r"(?:(?(2)(a)|()))*\1", "aa"),
    (r"(?:(?(3)(?(2)(a)|())|())){2}", "aa"),
    (r"(a?)*?a\1", "aa"),
    (r"((a?)|(b))+", "b"),
    # Fixed lookbehind width counts scalars, even when alternatives differ in
    # both UTF-16 code units and UTF-8 bytes. Include captured references and
    # repeated capture completion across the mixed-width alternatives.
    (r"(?<=(?P<x>😀|a))b", "😀b ab"),
    (r"(?P<x>😀|a)(?<=(?P=x))b", "😀b ab"),
    (r"(?<=(?P<x>😀|a){2})b", "😀ab a😀b"),
    # Ignored comments retain leading flag placement, quantifier attachment and
    # escaped-token boundaries; captures still close on the successful path.
    (r"(?#lead)(?i)(?P<x>😀a)", "😀A 😀a"),
    ("(?x) #lead\n (?i)(?P<x>😀a)", "😀A 😀a"),
    (r"(a)(?#between){2}", "aa a"),
    (r"(?#escaped\)closer)(?i)(?P<x>😀a)", "😀A 😀a"),
    ("(?x)(?P<x>a)#continued\\\nb", "a ab"),
    # Bracket control escapes are scalar literals, not word-boundary atoms or
    # their spelling letters; include range endpoints and supplementary offsets.
    (r"😀(?P<x>[\n]+)", "x😀\n\n 😀n"),
    (r"(?P<x>[\b-\r]+)", "x\b\t\n\v\f\r ybfnrtv"),
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
