"""Measure PythonRe benchmark cases against the CPython stdlib re engine.

The legacy protocol accepts one JSON request. The streaming protocol keeps one
worker alive per case and exchanges newline-delimited commands. Interpreter
startup, JSON transport, pattern compilation, and result verification remain
outside timed batches. Keep this runner dependency-free: it is intended to
execute with an official, unmodified CPython installation.
"""

from __future__ import annotations

import base64
import gc
import hashlib
import json
import math
import os
import platform
import re
import statistics
import sys
import sysconfig
import time
from collections.abc import Callable
from typing import Any


PROTOCOL_VERSION = 1
STREAM_PROTOCOL_VERSION = 3
WARMUP_SECONDS = 0.1
MAX_WARMUP_CALLS = 65_536
STREAM_CALIBRATION_PILOT_NANOSECONDS = 5_000_000
STREAM_MAX_ITERATIONS = 10_000_000
SEMANTIC_DIGEST_OFFSET = 0xCBF2_9CE4_8422_2325
SEMANTIC_DIGEST_PRIME = 0x0000_0100_0000_01B3
SEMANTIC_DIGEST_MASK = 0xFFFF_FFFF_FFFF_FFFF
SEMANTIC_OPERATION_TAGS = {
    "IsMatch": 1,
    "Search": 2,
    "Match": 3,
    "FullMatch": 4,
    "SearchDetailed": 5,
    "Count": 6,
    "FindAllStrings": 7,
    "FindAllUtf8": 8,
    "FindIterDetailed": 9,
    "ReplaceString": 10,
    "ReplaceUtf8": 11,
    "SubnString": 12,
    "SubnUtf8": 13,
    "SubnEvaluatorString": 14,
    "SplitStrings": 15,
    "SubnEvaluatorUtf8": 16,
    "SearchFromOffset": 17,
    "CountFromOffset": 18,
    "FindAllStructural": 19,
    "ReplaceStringLimited": 20,
    "ReplaceEvaluatorString": 21,
    "SplitStringsLimited": 22,
    "SplitDetailed": 23,
    "FindAllStringsFromOffset": 24,
}


def to_int32(value: int) -> int:
    value &= 0xFFFF_FFFF
    return value if value < 0x8000_0000 else value - 0x1_0000_0000


def combine(seed: int, *values: int) -> int:
    for value in values:
        seed = to_int32(seed * 31 + value)
    return seed


def checksum_string(value: str) -> int:
    encoded = value.encode("utf-16-le", "surrogatepass")
    checksum = len(encoded) // 2
    for index in range(0, len(encoded), 2):
        checksum = combine(checksum, encoded[index] | (encoded[index + 1] << 8))
    return checksum


def checksum_bytes(value: bytes) -> int:
    checksum = len(value)
    for item in value:
        checksum = combine(checksum, item)
    return checksum


def digest_add(seed: int, *values: int) -> int:
    for value in values:
        seed = ((seed ^ (value & SEMANTIC_DIGEST_MASK)) * SEMANTIC_DIGEST_PRIME) & SEMANTIC_DIGEST_MASK
    return seed


def digest_string(seed: int, value: str) -> int:
    encoded = value.encode("utf-16-le", "surrogatepass")
    seed = digest_add(seed, 1, len(encoded) // 2)
    for index in range(0, len(encoded), 2):
        seed = digest_add(seed, encoded[index] | (encoded[index + 1] << 8))
    return seed


def digest_bytes(seed: int, value: bytes) -> int:
    seed = digest_add(seed, 2, len(value))
    for item in value:
        seed = digest_add(seed, item)
    return seed


def bound_trivial(value: int) -> int:
    return value


def build_utf16_offsets(value: str) -> tuple[int, ...]:
    offsets = [0]
    utf16_offset = 0
    for character in value:
        utf16_offset += 2 if ord(character) > 0xFFFF else 1
        offsets.append(utf16_offset)
    return tuple(offsets)


def build_utf8_offsets(value: str) -> tuple[int, ...]:
    offsets = [0]
    utf8_offset = 0
    for character in value:
        utf8_offset += len(character.encode("utf-8"))
        offsets.append(utf8_offset)
    return tuple(offsets)


def simple_match_checksum(match: re.Match[str] | None, utf16_offsets: tuple[int, ...]) -> int:
    if match is None:
        return 0
    return combine(
        1,
        utf16_offsets[match.start()],
        utf16_offsets[match.end()],
    )


DetailedGroup = tuple[bool, int, int, int, int, str]
DetailedMatch = tuple[DetailedGroup, ...] | None


def materialize_detailed(
    match: re.Match[str] | None,
    utf8_offsets: tuple[int, ...],
    utf16_offsets: tuple[int, ...],
) -> DetailedMatch:
    if match is None:
        return None

    groups: list[DetailedGroup] = []
    for group_index in range(match.re.groups + 1):
        start, end = match.span(group_index)
        if start < 0:
            groups.append((False, 0, 0, 0, 0, ""))
        else:
            groups.append(
                (
                    True,
                    utf8_offsets[start],
                    utf8_offsets[end],
                    utf16_offsets[start],
                    utf16_offsets[end],
                    match.group(group_index),
                )
            )
    return tuple(groups)


def detailed_checksum(match: DetailedMatch) -> int:
    if match is None:
        return 0

    checksum = 1
    for success, _, _, start_utf16, end_utf16, value in match:
        checksum = combine(
            checksum,
            1 if success else 0,
            start_utf16,
            end_utf16,
            checksum_string(value),
        )
    return checksum


def findall_checksum(values: list[Any], capture_count: int, encode_utf8: bool) -> int:
    shape = 0 if capture_count == 0 else 1 if capture_count == 1 else 2
    checksum = shape
    if shape != 2:
        for value in values:
            checksum = combine(
                checksum,
                checksum_bytes(value) if encode_utf8 else checksum_string(value),
            )
        return checksum

    for value_tuple in values:
        checksum = combine(checksum, len(value_tuple))
        for value in value_tuple:
            checksum = combine(
                checksum,
                checksum_bytes(value) if encode_utf8 else checksum_string(value),
            )
    return checksum


def digest_detailed(seed: int, match: DetailedMatch) -> int:
    if match is None:
        return digest_add(seed, 0)

    seed = digest_add(seed, 1, len(match))
    for success, start_bytes, end_bytes, start_utf16, end_utf16, value in match:
        seed = digest_add(
            seed,
            1 if success else 0,
            start_bytes,
            end_bytes,
            start_utf16,
            end_utf16,
        )
        seed = digest_string(seed, value)
    return seed


def digest_findall(seed: int, values: list[Any], capture_count: int, encode_utf8: bool) -> int:
    shape = 0 if capture_count == 0 else 1 if capture_count == 1 else 2
    seed = digest_add(seed, shape, len(values))
    if shape != 2:
        for value in values:
            seed = digest_bytes(seed, value) if encode_utf8 else digest_string(seed, value)
        return seed

    for value_tuple in values:
        seed = digest_add(seed, len(value_tuple))
        for value in value_tuple:
            seed = digest_bytes(seed, value) if encode_utf8 else digest_string(seed, value)
    return seed


def flags_from_options(options: int) -> re.RegexFlag:
    flags = re.NOFLAG
    if options & (1 << 0):
        flags |= re.IGNORECASE
    if options & (1 << 2):
        flags |= re.MULTILINE
    if options & (1 << 3):
        flags |= re.DOTALL
    if options & (1 << 4):
        flags |= re.VERBOSE
    if options & (1 << 5):
        flags |= re.ASCII
    if options & (1 << 6):
        flags |= re.UNICODE
    return flags


class CaseRunner:
    def __init__(self, request: dict[str, Any]) -> None:
        self.operation = request["Operation"]
        self.input_bytes = base64.b64decode(request["InputBase64"], validate=True)
        self.input_text = self.input_bytes.decode("utf-8", "strict")
        self.utf8_offsets = build_utf8_offsets(self.input_text)
        self.utf16_offsets = build_utf16_offsets(self.input_text)
        start_offset_in_bytes = request.get("StartOffsetInBytes", 0)
        if not isinstance(start_offset_in_bytes, int) or isinstance(start_offset_in_bytes, bool):
            raise ValueError("StartOffsetInBytes must be an integer.")
        try:
            self.start_offset = self.utf8_offsets.index(start_offset_in_bytes)
        except ValueError as error:
            raise ValueError("StartOffsetInBytes must be a UTF-8 scalar boundary.") from error
        self.start_offset_in_bytes = start_offset_in_bytes
        self.replacement = request["Replacement"]
        self.replacement_count = request.get("ReplacementCount", -1)
        self.max_split = request.get("MaxSplit", -1)
        if not isinstance(self.replacement_count, int) or isinstance(self.replacement_count, bool):
            raise ValueError("ReplacementCount must be an integer.")
        if not isinstance(self.max_split, int) or isinstance(self.max_split, bool):
            raise ValueError("MaxSplit must be an integer.")
        self.pattern = re.compile(request["Pattern"], flags_from_options(request["Options"]))
        self.byte_pattern: re.Pattern[bytes] | None = None
        if request.get("EnableByteControl", False):
            byte_flags = flags_from_options(request["Options"])
            if byte_flags & re.UNICODE:
                raise ValueError("The CPython bytes control does not support re.UNICODE.")
            self.byte_pattern = re.compile(request["Pattern"].encode("ascii", "strict"), byte_flags)
        self.callback_checksum = 0
        self.callback_digest = SEMANTIC_DIGEST_OFFSET
        self.evaluator = self.replace_callback

    def execute_predecoded(self) -> Any:
        return self.execute(self.input_text)

    def execute_decode_then_re(self) -> Any:
        return self.execute(self.input_bytes.decode("utf-8", "strict"))

    def execute(self, input_text: str) -> Any:
        operation = self.operation
        if operation == "IsMatch":
            return self.pattern.search(input_text) is not None
        if operation == "Search":
            return self.pattern.search(input_text)
        if operation == "SearchFromOffset":
            return self.pattern.search(input_text, self.start_offset)
        if operation == "Match":
            return self.pattern.match(input_text)
        if operation == "FullMatch":
            return self.pattern.fullmatch(input_text)
        if operation == "SearchDetailed":
            return materialize_detailed(
                self.pattern.search(input_text),
                self.utf8_offsets,
                self.utf16_offsets,
            )
        if operation == "Count":
            return sum(1 for _ in self.pattern.finditer(input_text))
        if operation == "CountFromOffset":
            return sum(1 for _ in self.pattern.finditer(input_text, self.start_offset))
        if operation == "FindAllStrings":
            return self.pattern.findall(input_text)
        if operation == "FindAllStringsFromOffset":
            return self.pattern.findall(input_text, self.start_offset)
        if operation == "FindAllUtf8":
            return self.encode_findall(self.pattern.findall(input_text))
        if operation == "FindIterDetailed":
            return [
                materialize_detailed(match, self.utf8_offsets, self.utf16_offsets)
                for match in self.pattern.finditer(input_text)
            ]
        if operation == "FindAllStructural":
            return self.materialize_structural(input_text)
        if operation == "ReplaceString":
            return self.pattern.sub(self.replacement, input_text)
        if operation == "ReplaceUtf8":
            return self.pattern.sub(self.replacement, input_text).encode("utf-8")
        if operation == "ReplaceStringLimited":
            return self.pattern.sub(self.replacement, input_text, count=self.replacement_count)
        if operation == "ReplaceEvaluatorString":
            return self.replace_evaluator(input_text)
        if operation == "SubnString":
            return self.pattern.subn(self.replacement, input_text)
        if operation == "SubnUtf8":
            result, count = self.pattern.subn(self.replacement, input_text)
            return result.encode("utf-8"), count
        if operation == "SubnEvaluatorString":
            return self.subn_evaluator(input_text, False)
        if operation == "SubnEvaluatorUtf8":
            return self.subn_evaluator(input_text, True)
        if operation == "SplitStrings":
            return self.pattern.split(input_text)
        if operation == "SplitStringsLimited":
            return self.pattern.split(input_text, maxsplit=self.max_split)
        if operation == "SplitDetailed":
            return self.materialize_split_detailed(input_text)
        raise ValueError(f"Unsupported PythonRe operation: {operation}")

    def execute_predecoded_batch(self, iterations: int) -> tuple[int, Any]:
        operation = self.operation
        input_text = self.input_text
        pattern = self.pattern
        result: Any = None
        started = time.perf_counter_ns()
        if operation == "IsMatch":
            for _ in range(iterations):
                result = pattern.search(input_text) is not None
        elif operation == "Search":
            for _ in range(iterations):
                result = pattern.search(input_text)
        elif operation == "SearchFromOffset":
            start_offset = self.start_offset
            for _ in range(iterations):
                result = pattern.search(input_text, start_offset)
        elif operation == "Match":
            for _ in range(iterations):
                result = pattern.match(input_text)
        elif operation == "FullMatch":
            for _ in range(iterations):
                result = pattern.fullmatch(input_text)
        elif operation == "SearchDetailed":
            for _ in range(iterations):
                result = materialize_detailed(
                    pattern.search(input_text),
                    self.utf8_offsets,
                    self.utf16_offsets,
                )
        elif operation == "Count":
            for _ in range(iterations):
                result = sum(1 for _ in pattern.finditer(input_text))
        elif operation == "CountFromOffset":
            start_offset = self.start_offset
            for _ in range(iterations):
                result = sum(1 for _ in pattern.finditer(input_text, start_offset))
        elif operation == "FindAllStrings":
            for _ in range(iterations):
                result = pattern.findall(input_text)
        elif operation == "FindAllStringsFromOffset":
            start_offset = self.start_offset
            for _ in range(iterations):
                result = pattern.findall(input_text, start_offset)
        elif operation == "FindAllUtf8":
            if pattern.groups <= 1:
                for _ in range(iterations):
                    result = [value.encode("utf-8") for value in pattern.findall(input_text)]
            else:
                for _ in range(iterations):
                    result = [
                        tuple(value.encode("utf-8") for value in item)
                        for item in pattern.findall(input_text)
                    ]
        elif operation == "FindIterDetailed":
            for _ in range(iterations):
                result = [
                    materialize_detailed(match, self.utf8_offsets, self.utf16_offsets)
                    for match in pattern.finditer(input_text)
                ]
        elif operation == "FindAllStructural":
            for _ in range(iterations):
                result = self.materialize_structural(input_text)
        elif operation == "ReplaceString":
            for _ in range(iterations):
                result = pattern.sub(self.replacement, input_text)
        elif operation == "ReplaceUtf8":
            for _ in range(iterations):
                result = pattern.sub(self.replacement, input_text).encode("utf-8")
        elif operation == "ReplaceStringLimited":
            replacement_count = self.replacement_count
            for _ in range(iterations):
                result = pattern.sub(self.replacement, input_text, count=replacement_count)
        elif operation == "ReplaceEvaluatorString":
            for _ in range(iterations):
                self.callback_checksum = 0
                self.callback_digest = SEMANTIC_DIGEST_OFFSET
                result_text = pattern.sub(self.evaluator, input_text)
                result = result_text, self.callback_checksum, self.callback_digest
        elif operation == "SubnString":
            for _ in range(iterations):
                result = pattern.subn(self.replacement, input_text)
        elif operation == "SubnUtf8":
            for _ in range(iterations):
                result_text, count = pattern.subn(self.replacement, input_text)
                result = result_text.encode("utf-8"), count
        elif operation == "SubnEvaluatorString":
            for _ in range(iterations):
                self.callback_checksum = 0
                self.callback_digest = SEMANTIC_DIGEST_OFFSET
                result_text, count = pattern.subn(self.evaluator, input_text)
                result = result_text, count, self.callback_checksum, self.callback_digest
        elif operation == "SubnEvaluatorUtf8":
            for _ in range(iterations):
                self.callback_checksum = 0
                self.callback_digest = SEMANTIC_DIGEST_OFFSET
                result_text, count = pattern.subn(self.evaluator, input_text)
                result = (
                    result_text.encode("utf-8"),
                    count,
                    self.callback_checksum,
                    self.callback_digest,
                )
        elif operation == "SplitStrings":
            for _ in range(iterations):
                result = pattern.split(input_text)
        elif operation == "SplitStringsLimited":
            max_split = self.max_split
            for _ in range(iterations):
                result = pattern.split(input_text, maxsplit=max_split)
        elif operation == "SplitDetailed":
            for _ in range(iterations):
                result = self.materialize_split_detailed(input_text)
        else:
            raise ValueError(f"Unsupported PythonRe operation: {operation}")
        return time.perf_counter_ns() - started, result

    def execute_predecoded_qualification_batch(self, iterations: int) -> tuple[int, Any, int]:
        operation = self.operation
        if operation not in {"Search", "SearchFromOffset", "Match", "FullMatch"}:
            elapsed, result = self.execute_predecoded_batch(iterations)
            return elapsed, result, 0

        input_text = self.input_text
        pattern = self.pattern
        utf8_offsets = self.utf8_offsets
        utf16_offsets = self.utf16_offsets
        consumption_checksum = 0
        result: re.Match[str] | None = None
        started = time.perf_counter_ns()
        if operation in {"Search", "SearchFromOffset"}:
            start_offset = self.start_offset
            for _ in range(iterations):
                result = (
                    pattern.search(input_text)
                    if operation == "Search"
                    else pattern.search(input_text, start_offset)
                )
                if result is None:
                    consumption_checksum += 1
                else:
                    start, end = result.span()
                    consumption_checksum += (
                        2
                        + utf8_offsets[start]
                        + utf8_offsets[end]
                        + utf16_offsets[start]
                        + utf16_offsets[end]
                    )
        elif operation == "Match":
            for _ in range(iterations):
                result = pattern.match(input_text)
                if result is None:
                    consumption_checksum += 1
                else:
                    start, end = result.span()
                    consumption_checksum += (
                        2
                        + utf8_offsets[start]
                        + utf8_offsets[end]
                        + utf16_offsets[start]
                        + utf16_offsets[end]
                    )
        else:
            for _ in range(iterations):
                result = pattern.fullmatch(input_text)
                if result is None:
                    consumption_checksum += 1
                else:
                    start, end = result.span()
                    consumption_checksum += (
                        2
                        + utf8_offsets[start]
                        + utf8_offsets[end]
                        + utf16_offsets[start]
                        + utf16_offsets[end]
                    )
        return time.perf_counter_ns() - started, result, consumption_checksum

    def execute_bytes_qualification_batch(self, iterations: int) -> tuple[int, Any, int]:
        pattern = self.byte_pattern
        if pattern is None:
            raise ValueError("This case has no eligible CPython bytes control.")

        operation = self.operation
        input_bytes = self.input_bytes
        consumption_checksum = 0
        result: re.Match[bytes] | bool | None = None
        started = time.perf_counter_ns()
        if operation == "IsMatch":
            for _ in range(iterations):
                result = pattern.search(input_bytes) is not None
        elif operation == "Search":
            for _ in range(iterations):
                result = pattern.search(input_bytes)
                consumption_checksum += self.byte_consumption_token(result)
        elif operation == "SearchFromOffset":
            start_offset_in_bytes = self.start_offset_in_bytes
            for _ in range(iterations):
                result = pattern.search(input_bytes, start_offset_in_bytes)
                consumption_checksum += self.byte_consumption_token(result)
        elif operation == "Match":
            for _ in range(iterations):
                result = pattern.match(input_bytes)
                consumption_checksum += self.byte_consumption_token(result)
        elif operation == "FullMatch":
            for _ in range(iterations):
                result = pattern.fullmatch(input_bytes)
                consumption_checksum += self.byte_consumption_token(result)
        else:
            raise ValueError(f"Unsupported CPython bytes-control operation: {operation}")
        return time.perf_counter_ns() - started, result, consumption_checksum

    def byte_checksum(self, result: Any) -> int:
        if self.operation == "IsMatch":
            return 1 if result else 0
        return simple_match_checksum(result, self.utf16_offsets)

    def byte_semantic_digest(self, result: Any) -> int:
        operation = self.operation
        digest = digest_add(SEMANTIC_DIGEST_OFFSET, SEMANTIC_OPERATION_TAGS[operation])
        if operation == "IsMatch":
            return digest_add(digest, 1 if result else 0)
        if result is None:
            return digest_add(digest, 0)
        start, end = result.span()
        digest = digest_add(digest, 1, start, end, start, end)
        return digest_string(digest, result.group().decode("ascii", "strict"))

    @staticmethod
    def byte_consumption_token(result: re.Match[bytes] | None) -> int:
        if result is None:
            return 1
        start, end = result.span()
        return 2 + start + end + start + end

    def consumption_token(self, result: Any) -> int:
        if self.operation not in {"Search", "SearchFromOffset", "Match", "FullMatch"}:
            return 0
        if result is None:
            return 1
        start, end = result.span()
        return (
            2
            + self.utf8_offsets[start]
            + self.utf8_offsets[end]
            + self.utf16_offsets[start]
            + self.utf16_offsets[end]
        )

    @staticmethod
    def execute_empty_batch(iterations: int) -> tuple[int, int]:
        result = 0
        started = time.perf_counter_ns()
        for iteration in range(iterations):
            result ^= iteration
        return time.perf_counter_ns() - started, result

    @staticmethod
    def execute_bound_trivial_batch(iterations: int) -> tuple[int, int]:
        result = 0
        invoke = bound_trivial
        started = time.perf_counter_ns()
        for iteration in range(iterations):
            result ^= invoke(iteration)
        return time.perf_counter_ns() - started, result

    def encode_findall(self, values: list[Any]) -> list[Any]:
        if self.pattern.groups <= 1:
            return [value.encode("utf-8") for value in values]
        return [tuple(value.encode("utf-8") for value in item) for item in values]

    def materialize_structural(self, input_text: str) -> list[tuple[bool, int, int, int, int, str]]:
        values: list[tuple[bool, int, int, int, int, str]] = []
        for match in self.pattern.finditer(input_text):
            start, end = match.span()
            values.append(
                (
                    True,
                    self.utf8_offsets[start],
                    self.utf8_offsets[end],
                    self.utf16_offsets[start],
                    self.utf16_offsets[end],
                    match.group(),
                )
            )
        return values

    def materialize_split_detailed(self, input_text: str) -> list[tuple[str | None, bool, int]]:
        values = self.pattern.split(input_text)
        stride = self.pattern.groups + 1
        return [
            (value, index % stride != 0, index % stride)
            for index, value in enumerate(values)
        ]

    def replace_callback(self, match: re.Match[str]) -> str:
        detailed = materialize_detailed(match, self.utf8_offsets, self.utf16_offsets)
        self.callback_checksum = combine(
            self.callback_checksum,
            detailed_checksum(detailed),
        )
        self.callback_digest = digest_add(self.callback_digest, 0xCA11_BACC)
        self.callback_digest = digest_detailed(self.callback_digest, detailed)
        return self.replacement

    def subn_evaluator(self, input_text: str, encode_utf8: bool) -> tuple[Any, int, int, int]:
        self.callback_checksum = 0
        self.callback_digest = SEMANTIC_DIGEST_OFFSET
        result, count = self.pattern.subn(self.evaluator, input_text)
        return (
            result.encode("utf-8") if encode_utf8 else result,
            count,
            self.callback_checksum,
            self.callback_digest,
        )

    def replace_evaluator(self, input_text: str) -> tuple[str, int, int]:
        self.callback_checksum = 0
        self.callback_digest = SEMANTIC_DIGEST_OFFSET
        result = self.pattern.sub(self.evaluator, input_text)
        return result, self.callback_checksum, self.callback_digest

    def checksum(self, result: Any) -> int:
        operation = self.operation
        if operation == "IsMatch":
            return 1 if result else 0
        if operation in {"Search", "SearchFromOffset", "Match", "FullMatch"}:
            return simple_match_checksum(result, self.utf16_offsets)
        if operation == "SearchDetailed":
            return detailed_checksum(result)
        if operation in {"Count", "CountFromOffset"}:
            return result
        if operation in {"FindAllStrings", "FindAllStringsFromOffset"}:
            return findall_checksum(result, self.pattern.groups, False)
        if operation == "FindAllUtf8":
            return findall_checksum(result, self.pattern.groups, True)
        if operation == "FindIterDetailed":
            checksum = len(result)
            for match in result:
                checksum = combine(checksum, detailed_checksum(match))
            return checksum
        if operation == "FindAllStructural":
            checksum = len(result)
            for success, _, _, start_utf16, end_utf16, value in result:
                checksum = combine(
                    checksum,
                    1 if success else 0,
                    start_utf16,
                    end_utf16,
                    checksum_string(value),
                )
            return checksum
        if operation in {"ReplaceString", "ReplaceUtf8", "ReplaceStringLimited"}:
            return checksum_bytes(result) if isinstance(result, bytes) else checksum_string(result)
        if operation == "ReplaceEvaluatorString":
            value, callback_checksum, _ = result
            return combine(checksum_string(value), callback_checksum)
        if operation in {"SubnString", "SubnUtf8"}:
            value, count = result
            value_checksum = checksum_bytes(value) if isinstance(value, bytes) else checksum_string(value)
            return combine(value_checksum, count)
        if operation in {"SubnEvaluatorString", "SubnEvaluatorUtf8"}:
            value, count, callback_checksum, _ = result
            value_checksum = checksum_bytes(value) if isinstance(value, bytes) else checksum_string(value)
            return combine(combine(value_checksum, count), callback_checksum)
        if operation in {"SplitStrings", "SplitStringsLimited"}:
            checksum = len(result)
            for value in result:
                checksum = combine(checksum, -1 if value is None else checksum_string(value))
            return checksum
        if operation == "SplitDetailed":
            checksum = len(result)
            for value, is_capture, capture_group_number in result:
                checksum = combine(
                    checksum,
                    -1 if value is None else checksum_string(value),
                    1 if is_capture else 0,
                    capture_group_number,
                    0,
                )
            return checksum
        raise ValueError(f"Unsupported PythonRe operation: {operation}")

    def semantic_digest(self, result: Any) -> int:
        operation = self.operation
        digest = digest_add(SEMANTIC_DIGEST_OFFSET, SEMANTIC_OPERATION_TAGS[operation])
        if operation == "IsMatch":
            return digest_add(digest, 1 if result else 0)
        if operation in {"Search", "SearchFromOffset", "Match", "FullMatch"}:
            if result is None:
                return digest_add(digest, 0)
            start, end = result.span()
            digest = digest_add(
                digest,
                1,
                self.utf8_offsets[start],
                self.utf8_offsets[end],
                self.utf16_offsets[start],
                self.utf16_offsets[end],
            )
            return digest_string(digest, result.group())
        if operation == "SearchDetailed":
            return digest_detailed(digest, result)
        if operation in {"Count", "CountFromOffset"}:
            return digest_add(digest, result)
        if operation in {"FindAllStrings", "FindAllStringsFromOffset"}:
            return digest_findall(digest, result, self.pattern.groups, False)
        if operation == "FindAllUtf8":
            return digest_findall(digest, result, self.pattern.groups, True)
        if operation == "FindIterDetailed":
            digest = digest_add(digest, len(result))
            for match in result:
                digest = digest_detailed(digest, match)
            return digest
        if operation == "FindAllStructural":
            digest = digest_add(digest, len(result))
            for success, start_bytes, end_bytes, start_utf16, end_utf16, value in result:
                digest = digest_add(
                    digest,
                    1 if success else 0,
                    start_bytes,
                    end_bytes,
                    start_utf16,
                    end_utf16,
                )
                digest = digest_string(digest, value)
            return digest
        if operation in {"ReplaceString", "ReplaceUtf8", "ReplaceStringLimited"}:
            return digest_bytes(digest, result) if isinstance(result, bytes) else digest_string(digest, result)
        if operation == "ReplaceEvaluatorString":
            value, _, callback_digest = result
            digest = digest_string(digest, value)
            return digest_add(digest, callback_digest)
        if operation in {"SubnString", "SubnUtf8"}:
            value, count = result
            digest = digest_bytes(digest, value) if isinstance(value, bytes) else digest_string(digest, value)
            return digest_add(digest, count)
        if operation in {"SubnEvaluatorString", "SubnEvaluatorUtf8"}:
            value, count, _, callback_digest = result
            digest = digest_bytes(digest, value) if isinstance(value, bytes) else digest_string(digest, value)
            return digest_add(digest, count, callback_digest)
        if operation in {"SplitStrings", "SplitStringsLimited"}:
            digest = digest_add(digest, len(result))
            for value in result:
                if value is None:
                    digest = digest_add(digest, 0)
                else:
                    digest = digest_add(digest, 1)
                    digest = digest_string(digest, value)
            return digest
        if operation == "SplitDetailed":
            digest = digest_add(digest, len(result))
            for value, is_capture, capture_group_number in result:
                if value is None:
                    digest = digest_add(digest, 0)
                else:
                    digest = digest_add(digest, 1)
                    digest = digest_string(digest, value)
                digest = digest_add(
                    digest,
                    1 if is_capture else 0,
                    capture_group_number,
                )
            return digest
        raise ValueError(f"Unsupported PythonRe operation: {operation}")


def measure(operation: Callable[[], Any], maximum_iterations: int, sample_count: int) -> tuple[dict[str, Any], Any]:
    warmup_started = time.perf_counter_ns()
    warmup_calls = 0
    result: Any = None
    while warmup_calls < MAX_WARMUP_CALLS:
        result = operation()
        warmup_calls += 1
        if time.perf_counter_ns() - warmup_started >= WARMUP_SECONDS * 1_000_000_000:
            break

    warmup_elapsed = time.perf_counter_ns() - warmup_started
    average_warmup_nanoseconds = warmup_elapsed / warmup_calls
    calibrated_iterations = max(1, math.ceil(WARMUP_SECONDS * 1_000_000_000 / average_warmup_nanoseconds))
    iterations = min(maximum_iterations, calibrated_iterations)
    microseconds: list[float] = []
    for _ in range(sample_count):
        started = time.perf_counter_ns()
        for _ in range(iterations):
            result = operation()
        elapsed = time.perf_counter_ns() - started
        microseconds.append(elapsed / iterations / 1_000)

    microseconds.sort()
    return (
        {
            "MedianMicroseconds": statistics.median(microseconds),
            "MinimumMicroseconds": microseconds[0],
            "MaximumMicroseconds": microseconds[-1],
            "EffectiveIterations": iterations,
            "WarmupCalls": warmup_calls,
            "WarmupMilliseconds": warmup_elapsed / 1_000_000,
        },
        result,
    )


def measure_predecoded(
    runner: CaseRunner,
    maximum_iterations: int,
    sample_count: int,
    expected_checksum: int,
) -> tuple[dict[str, Any], Any]:
    warmup_started = time.perf_counter_ns()
    warmup_calls = 0
    result: Any = None
    while warmup_calls < MAX_WARMUP_CALLS:
        _, result = runner.execute_predecoded_batch(1)
        warmup_calls += 1
        if time.perf_counter_ns() - warmup_started >= WARMUP_SECONDS * 1_000_000_000:
            break

    warmup_elapsed = time.perf_counter_ns() - warmup_started
    average_warmup_nanoseconds = warmup_elapsed / warmup_calls
    calibrated_iterations = max(1, math.ceil(WARMUP_SECONDS * 1_000_000_000 / average_warmup_nanoseconds))
    iterations = min(maximum_iterations, calibrated_iterations)
    microseconds: list[float] = []
    for _ in range(sample_count):
        elapsed, result = runner.execute_predecoded_batch(iterations)
        actual_checksum = runner.checksum(result)
        if actual_checksum != expected_checksum:
            raise RuntimeError(
                "CPython predecoded timed result disagrees with preflight: "
                f"expected {expected_checksum}, actual {actual_checksum}."
            )
        microseconds.append(elapsed / iterations / 1_000)

    microseconds.sort()
    return (
        {
            "MedianMicroseconds": statistics.median(microseconds),
            "MinimumMicroseconds": microseconds[0],
            "MaximumMicroseconds": microseconds[-1],
            "EffectiveIterations": iterations,
            "WarmupCalls": warmup_calls,
            "WarmupMilliseconds": warmup_elapsed / 1_000_000,
        },
        result,
    )


def file_sha256(path: str) -> str | None:
    if not os.path.isfile(path):
        return None
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def find_runtime_library() -> str | None:
    executable_directory = os.path.dirname(sys.executable)
    if os.name == "nt":
        candidate = os.path.join(
            executable_directory,
            f"python{sys.version_info.major}{sys.version_info.minor}.dll",
        )
        return candidate if os.path.isfile(candidate) else None

    library_name = sysconfig.get_config_var("LDLIBRARY")
    library_directory = sysconfig.get_config_var("LIBDIR")
    if library_name and library_directory:
        candidate = os.path.join(library_directory, library_name)
        return candidate if os.path.isfile(candidate) else None
    return None


def stream_environment() -> dict[str, Any]:
    timer = time.get_clock_info("perf_counter")
    runtime_library = find_runtime_library()
    gil_probe = getattr(sys, "_is_gil_enabled", None)
    return {
        "Implementation": platform.python_implementation(),
        "Version": platform.python_version(),
        "VersionDetail": sys.version,
        "HexVersion": sys.hexversion,
        "Git": getattr(sys, "_git", None),
        "CacheTag": sys.implementation.cache_tag,
        "Compiler": platform.python_compiler(),
        "SoAbi": sysconfig.get_config_var("SOABI"),
        "DebugBuild": hasattr(sys, "gettotalrefcount"),
        "GilEnabled": gil_probe() if gil_probe is not None else None,
        "Executable": sys.executable,
        "ExecutableSha256": file_sha256(sys.executable),
        "RuntimeLibrary": runtime_library,
        "RuntimeLibrarySha256": file_sha256(runtime_library) if runtime_library else None,
        "Platform": platform.platform(),
        "Architecture": platform.machine(),
        "RunnerSha256": file_sha256(os.path.abspath(__file__)),
        "Timer": {
            "Implementation": timer.implementation,
            "ResolutionSeconds": timer.resolution,
            "Monotonic": timer.monotonic,
            "Adjustable": timer.adjustable,
        },
    }


def write_stream_message(message: dict[str, Any]) -> None:
    sys.stdout.write(json.dumps(message, separators=(",", ":")) + "\n")
    sys.stdout.flush()


def require_positive_integer(command: dict[str, Any], name: str, maximum: int) -> int:
    value = command.get(name)
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0 or value > maximum:
        raise ValueError(f"{name} must be between 1 and {maximum}.")
    return value


def measure_lifecycle_operation(
    operation: Any,
    iterations: int,
    samples: int,
) -> dict[str, Any]:
    warmup_started = time.perf_counter_ns()
    warmup_calls = 0
    checksum = 0
    while warmup_calls < max(32, iterations * 2):
        checksum ^= operation()
        warmup_calls += 1
        if time.perf_counter_ns() - warmup_started >= 20_000_000:
            break

    warmup_elapsed = time.perf_counter_ns() - warmup_started
    microseconds: list[float] = []
    for _ in range(samples):
        local = 0
        started = time.perf_counter_ns()
        for _ in range(iterations):
            local ^= operation()
        elapsed = time.perf_counter_ns() - started
        checksum ^= local
        microseconds.append(elapsed / iterations / 1_000)

    if checksum == -1:
        raise RuntimeError("Unreachable lifecycle checksum sentinel.")
    microseconds.sort()
    return {
        "MedianMicroseconds": statistics.median(microseconds),
        "MinimumMicroseconds": microseconds[0],
        "MaximumMicroseconds": microseconds[-1],
        "ManagedAllocatedBytes": None,
        "WarmupCalls": warmup_calls,
        "WarmupMilliseconds": warmup_elapsed / 1_000_000,
    }


def measure_lifecycle(command: dict[str, Any]) -> dict[str, Any]:
    pattern = command.get("Pattern")
    encoded_input = command.get("InputBase64")
    options = command.get("Options")
    if not isinstance(pattern, str) or not isinstance(encoded_input, str) or not isinstance(options, int):
        raise ValueError("Lifecycle pattern, input, and options are required.")
    input_text = base64.b64decode(encoded_input, validate=True).decode("utf-8", "strict")
    iterations = require_positive_integer(command, "Iterations", 128)
    samples = require_positive_integer(command, "Samples", 9)
    flags = flags_from_options(options)
    compiler = re._compiler

    def compile_pattern() -> int:
        return compiler.compile(pattern, flags).groups

    def compile_first_search() -> int:
        return 1 if compiler.compile(pattern, flags).search(input_text) is not None else 0

    first_search_matched = compile_first_search() == 1
    return {
        "FirstSearchMatched": first_search_matched,
        "Compile": measure_lifecycle_operation(compile_pattern, iterations, samples),
        "CompileFirstSearch": measure_lifecycle_operation(compile_first_search, iterations, samples),
    }


def measure_stream_lane(
    runner: CaseRunner,
    lane: str,
    iterations: int,
    expected_checksum: int,
    expected_semantic_digest: int,
    expected_consumption_token: int,
) -> dict[str, Any]:
    collections_before = [entry["collections"] for entry in gc.get_stats()]
    process_started = time.process_time_ns()
    if lane == "Predecoded":
        elapsed, result, consumption_checksum = runner.execute_predecoded_qualification_batch(iterations)
        checksum = runner.checksum(result)
        semantic_digest = runner.semantic_digest(result)
        if checksum != expected_checksum:
            raise RuntimeError(
                "CPython streaming result disagrees with preflight: "
                f"expected {expected_checksum}, actual {checksum}."
            )
        if semantic_digest != expected_semantic_digest:
            raise RuntimeError(
                "CPython streaming semantic digest disagrees with preflight: "
                f"expected {expected_semantic_digest}, actual {semantic_digest}."
            )
        expected_consumption_checksum = expected_consumption_token * iterations
        if consumption_checksum != expected_consumption_checksum:
            raise RuntimeError(
                "CPython streaming result consumption disagrees with preflight: "
                f"expected {expected_consumption_checksum}, actual {consumption_checksum}."
            )
    elif lane == "Bytes":
        elapsed, result, consumption_checksum = runner.execute_bytes_qualification_batch(iterations)
        checksum = runner.byte_checksum(result)
        semantic_digest = runner.byte_semantic_digest(result)
        if checksum != expected_checksum:
            raise RuntimeError(
                "CPython bytes-control result disagrees with preflight: "
                f"expected {expected_checksum}, actual {checksum}."
            )
        if semantic_digest != expected_semantic_digest:
            raise RuntimeError(
                "CPython bytes-control semantic digest disagrees with preflight: "
                f"expected {expected_semantic_digest}, actual {semantic_digest}."
            )
        expected_consumption_checksum = expected_consumption_token * iterations
        if consumption_checksum != expected_consumption_checksum:
            raise RuntimeError(
                "CPython bytes-control result consumption disagrees with preflight: "
                f"expected {expected_consumption_checksum}, actual {consumption_checksum}."
            )
    elif lane == "EmptyLoop":
        elapsed, checksum = runner.execute_empty_batch(iterations)
        semantic_digest = 0
        consumption_checksum = 0
    elif lane == "BoundTrivialCall":
        elapsed, checksum = runner.execute_bound_trivial_batch(iterations)
        semantic_digest = 0
        consumption_checksum = 0
    else:
        raise ValueError(f"Unsupported CPython streaming lane: {lane}")
    process_elapsed = time.process_time_ns() - process_started
    collections_after = [entry["collections"] for entry in gc.get_stats()]
    return {
        "Lane": lane,
        "Iterations": iterations,
        "ElapsedNanoseconds": elapsed,
        "ProcessCpuNanoseconds": process_elapsed,
        "Checksum": checksum,
        "SemanticDigest": semantic_digest,
        "ConsumptionChecksum": consumption_checksum,
        "GcCollections": [
            after - before
            for before, after in zip(collections_before, collections_after, strict=True)
        ],
    }


def run_stream_worker() -> int:
    if platform.python_implementation() != "CPython":
        raise RuntimeError("The PythonRe baseline runner requires CPython.")

    write_stream_message(
        {
            "ProtocolVersion": STREAM_PROTOCOL_VERSION,
            "Kind": "Ready",
            "Environment": stream_environment(),
        }
    )
    runner: CaseRunner | None = None
    expected_checksum = 0
    expected_semantic_digest = 0
    expected_consumption_token = 0
    for line in sys.stdin:
        if not line.strip():
            continue
        try:
            command = json.loads(line)
            if command.get("ProtocolVersion") != STREAM_PROTOCOL_VERSION:
                raise ValueError("Unsupported PythonRe CPython streaming protocol.")

            kind = command.get("Kind")
            if kind == "Prepare":
                runner = CaseRunner(command)
                predecoded_result = runner.execute_predecoded()
                decoded_result = runner.execute_decode_then_re()
                expected_checksum = runner.checksum(predecoded_result)
                decoded_checksum = runner.checksum(decoded_result)
                expected_semantic_digest = runner.semantic_digest(predecoded_result)
                expected_consumption_token = runner.consumption_token(predecoded_result)
                decoded_semantic_digest = runner.semantic_digest(decoded_result)
                byte_control_checksum = None
                byte_control_semantic_digest = None
                byte_control_consumption_token = None
                if runner.byte_pattern is not None:
                    _, byte_result, byte_consumption = runner.execute_bytes_qualification_batch(1)
                    byte_control_checksum = runner.byte_checksum(byte_result)
                    byte_control_semantic_digest = runner.byte_semantic_digest(byte_result)
                    byte_control_consumption_token = byte_consumption
                if expected_checksum != decoded_checksum:
                    raise RuntimeError(
                        "CPython predecoded/decode preflight checksums differ: "
                        f"{expected_checksum} versus {decoded_checksum}."
                    )
                if expected_semantic_digest != decoded_semantic_digest:
                    raise RuntimeError(
                        "CPython predecoded/decode semantic digests differ: "
                        f"{expected_semantic_digest} versus {decoded_semantic_digest}."
                    )
                write_stream_message(
                    {
                        "ProtocolVersion": STREAM_PROTOCOL_VERSION,
                        "Kind": "Prepared",
                        "Checksum": expected_checksum,
                        "SemanticDigest": expected_semantic_digest,
                        "ConsumptionChecksum": expected_consumption_token,
                        "ByteControlAvailable": runner.byte_pattern is not None,
                        "ByteControlChecksum": byte_control_checksum,
                        "ByteControlSemanticDigest": byte_control_semantic_digest,
                        "ByteControlConsumptionChecksum": byte_control_consumption_token,
                        "InputUtf8Bytes": len(runner.input_bytes),
                        "InputCodePoints": len(runner.input_text),
                        "InputUtf16CodeUnits": runner.utf16_offsets[-1],
                    }
                )
                continue

            if kind == "Shutdown":
                write_stream_message(
                    {
                        "ProtocolVersion": STREAM_PROTOCOL_VERSION,
                        "Kind": "Shutdown",
                    }
                )
                return 0

            if kind == "MeasureLifecycle":
                write_stream_message(
                    {
                        "ProtocolVersion": STREAM_PROTOCOL_VERSION,
                        "Kind": "LifecycleMeasured",
                        "Lifecycle": measure_lifecycle(command),
                    }
                )
                continue

            if runner is None:
                raise RuntimeError("Prepare must complete before timing commands.")

            lane = command.get("Lane")
            if not isinstance(lane, str):
                raise ValueError("Lane must be a string.")
            if kind == "Measure":
                iterations = require_positive_integer(command, "Iterations", STREAM_MAX_ITERATIONS)
                result = measure_stream_lane(
                    runner,
                    lane,
                    iterations,
                    expected_checksum,
                    expected_semantic_digest,
                    expected_consumption_token,
                )
                result.update({"ProtocolVersion": STREAM_PROTOCOL_VERSION, "Kind": "Measured"})
                write_stream_message(result)
                continue

            if kind == "Calibrate":
                target_nanoseconds = require_positive_integer(
                    command,
                    "TargetNanoseconds",
                    1_000_000_000,
                )
                maximum_iterations = require_positive_integer(
                    command,
                    "MaximumIterations",
                    STREAM_MAX_ITERATIONS,
                )
                pilot_iterations = 1
                pilot = measure_stream_lane(
                    runner,
                    lane,
                    pilot_iterations,
                    expected_checksum,
                    expected_semantic_digest,
                    expected_consumption_token,
                )
                while (
                    pilot["ElapsedNanoseconds"] < STREAM_CALIBRATION_PILOT_NANOSECONDS
                    and pilot_iterations < maximum_iterations
                ):
                    elapsed = max(pilot["ElapsedNanoseconds"], 1)
                    growth = max(2, math.ceil(STREAM_CALIBRATION_PILOT_NANOSECONDS / elapsed))
                    pilot_iterations = min(maximum_iterations, pilot_iterations * growth)
                    pilot = measure_stream_lane(
                        runner,
                        lane,
                        pilot_iterations,
                        expected_checksum,
                        expected_semantic_digest,
                        expected_consumption_token,
                    )

                calibrated_iterations = min(
                    maximum_iterations,
                    max(
                        1,
                        round(target_nanoseconds * pilot_iterations / max(pilot["ElapsedNanoseconds"], 1)),
                    ),
                )
                calibrated: dict[str, Any] | None = None
                fastest_nanoseconds_per_operation = math.inf
                for _ in range(3):
                    calibrated = measure_stream_lane(
                        runner,
                        lane,
                        calibrated_iterations,
                        expected_checksum,
                        expected_semantic_digest,
                        expected_consumption_token,
                    )
                    fastest_nanoseconds_per_operation = min(
                        fastest_nanoseconds_per_operation,
                        calibrated["ElapsedNanoseconds"] / calibrated_iterations,
                    )
                    if calibrated["ElapsedNanoseconds"] < 30_000_000:
                        calibrated_iterations = min(
                            maximum_iterations,
                            max(
                                1,
                                round(
                                    calibrated_iterations
                                    * target_nanoseconds
                                    / max(calibrated["ElapsedNanoseconds"], 1)
                                ),
                            ),
                        )
                if calibrated is None:
                    raise RuntimeError("CPython streaming calibration produced no confirmation.")
                calibrated_iterations = min(
                    maximum_iterations,
                    max(1, math.ceil(target_nanoseconds / fastest_nanoseconds_per_operation)),
                )
                calibrated = measure_stream_lane(
                    runner,
                    lane,
                    calibrated_iterations,
                    expected_checksum,
                    expected_semantic_digest,
                    expected_consumption_token,
                )
                calibrated.update(
                    {
                        "ProtocolVersion": STREAM_PROTOCOL_VERSION,
                        "Kind": "Calibrated",
                        "PilotIterations": pilot_iterations,
                        "PilotElapsedNanoseconds": pilot["ElapsedNanoseconds"],
                    }
                )
                write_stream_message(calibrated)
                continue

            if kind == "Warm":
                iterations = require_positive_integer(command, "Iterations", STREAM_MAX_ITERATIONS)
                minimum_milliseconds = require_positive_integer(command, "MinimumMilliseconds", 1_000)
                minimum_calls = require_positive_integer(
                    command,
                    "MinimumCalls",
                    STREAM_MAX_ITERATIONS,
                )
                maximum_batches = require_positive_integer(command, "MaximumBatches", 1_000)
                warmup_started = time.perf_counter_ns()
                calls = 0
                batches = 0
                checksum = expected_checksum
                while batches < maximum_batches:
                    result = measure_stream_lane(
                        runner,
                        lane,
                        iterations,
                        expected_checksum,
                        expected_semantic_digest,
                        expected_consumption_token,
                    )
                    checksum = result["Checksum"]
                    calls += iterations
                    batches += 1
                    if (
                        time.perf_counter_ns() - warmup_started >= minimum_milliseconds * 1_000_000
                        and calls >= minimum_calls
                    ):
                        break
                write_stream_message(
                    {
                        "ProtocolVersion": STREAM_PROTOCOL_VERSION,
                        "Kind": "Warmed",
                        "Lane": lane,
                        "Iterations": calls,
                        "Batches": batches,
                        "ElapsedNanoseconds": time.perf_counter_ns() - warmup_started,
                        "Checksum": checksum,
                    }
                )
                continue

            raise ValueError(f"Unsupported PythonRe CPython streaming command: {kind}")
        except Exception as exception:
            write_stream_message(
                {
                    "ProtocolVersion": STREAM_PROTOCOL_VERSION,
                    "Kind": "Error",
                    "ErrorType": type(exception).__name__,
                    "Message": str(exception),
                }
            )
            return 1

    return 0


def main() -> int:
    if len(sys.argv) == 2 and sys.argv[1] == "--stream":
        return run_stream_worker()

    if platform.python_implementation() != "CPython":
        raise RuntimeError("The PythonRe baseline runner requires an official CPython-compatible executable.")

    request = json.load(sys.stdin)
    if request.get("ProtocolVersion") != PROTOCOL_VERSION:
        raise ValueError("Unsupported PythonRe CPython benchmark protocol.")

    runner = CaseRunner(request)
    iterations = request["Iterations"]
    samples = request["Samples"]
    predecoded_preflight = runner.execute_predecoded()
    decoded_preflight = runner.execute_decode_then_re()
    expected_checksum = runner.checksum(predecoded_preflight)
    decoded_preflight_checksum = runner.checksum(decoded_preflight)
    if expected_checksum != decoded_preflight_checksum:
        raise RuntimeError(
            "CPython predecoded/decode preflight checksums differ: "
            f"{expected_checksum} versus {decoded_preflight_checksum}."
        )

    predecoded, predecoded_result = measure_predecoded(runner, iterations, samples, expected_checksum)
    decoded, decoded_result = measure(runner.execute_decode_then_re, iterations, samples)
    predecoded_checksum = runner.checksum(predecoded_result)
    decoded_checksum = runner.checksum(decoded_result)
    if predecoded_checksum != expected_checksum or decoded_checksum != expected_checksum:
        raise RuntimeError(
            "CPython timed results disagree with preflight: "
            f"expected {expected_checksum}, predecoded {predecoded_checksum}, decoded {decoded_checksum}."
        )

    response = {
        "ProtocolVersion": PROTOCOL_VERSION,
        "Environment": {
            "Implementation": platform.python_implementation(),
            "Version": platform.python_version(),
            "Executable": sys.executable,
            "Platform": platform.platform(),
        },
        "Checksum": predecoded_checksum,
        "PredecodedRe": predecoded,
        "DecodeThenRe": decoded,
    }
    json.dump(response, sys.stdout, separators=(",", ":"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
