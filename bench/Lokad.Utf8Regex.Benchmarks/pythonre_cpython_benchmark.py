"""Measure PythonRe benchmark cases against the CPython stdlib re engine.

The .NET benchmark host starts this script once per case and sends one JSON
request on stdin. Interpreter startup, JSON transport, and pattern compilation
are outside the timed region. Keep this runner dependency-free: it is intended
to execute with an official, unmodified CPython installation.
"""

from __future__ import annotations

import base64
import json
import math
import platform
import re
import statistics
import sys
import time
from collections.abc import Callable
from typing import Any


PROTOCOL_VERSION = 1
WARMUP_SECONDS = 0.1
MAX_WARMUP_CALLS = 65_536


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


def build_utf16_offsets(value: str) -> tuple[int, ...]:
    offsets = [0]
    utf16_offset = 0
    for character in value:
        utf16_offset += 2 if ord(character) > 0xFFFF else 1
        offsets.append(utf16_offset)
    return tuple(offsets)


def simple_match_checksum(match: re.Match[str] | None, utf16_offsets: tuple[int, ...]) -> int:
    if match is None:
        return 0
    return combine(
        1,
        utf16_offsets[match.start()],
        utf16_offsets[match.end()],
    )


DetailedGroup = tuple[bool, int, int, str]
DetailedMatch = tuple[DetailedGroup, ...] | None


def materialize_detailed(
    match: re.Match[str] | None,
    utf16_offsets: tuple[int, ...],
) -> DetailedMatch:
    if match is None:
        return None

    groups: list[DetailedGroup] = []
    for group_index in range(match.re.groups + 1):
        start, end = match.span(group_index)
        if start < 0:
            groups.append((False, 0, 0, ""))
        else:
            groups.append(
                (
                    True,
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
    for success, start, end, value in match:
        checksum = combine(checksum, 1 if success else 0, start, end, checksum_string(value))
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
        self.utf16_offsets = build_utf16_offsets(self.input_text)
        self.replacement = request["Replacement"]
        self.pattern = re.compile(request["Pattern"], flags_from_options(request["Options"]))
        self.callback_checksum = 0
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
        if operation == "Match":
            return self.pattern.match(input_text)
        if operation == "FullMatch":
            return self.pattern.fullmatch(input_text)
        if operation == "SearchDetailed":
            return materialize_detailed(self.pattern.search(input_text), self.utf16_offsets)
        if operation == "Count":
            return sum(1 for _ in self.pattern.finditer(input_text))
        if operation == "FindAllStrings":
            return self.pattern.findall(input_text)
        if operation == "FindAllUtf8":
            return self.encode_findall(self.pattern.findall(input_text))
        if operation == "FindIterDetailed":
            return [
                materialize_detailed(match, self.utf16_offsets)
                for match in self.pattern.finditer(input_text)
            ]
        if operation == "ReplaceString":
            return self.pattern.sub(self.replacement, input_text)
        if operation == "ReplaceUtf8":
            return self.pattern.sub(self.replacement, input_text).encode("utf-8")
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
        elif operation == "Match":
            for _ in range(iterations):
                result = pattern.match(input_text)
        elif operation == "FullMatch":
            for _ in range(iterations):
                result = pattern.fullmatch(input_text)
        elif operation == "SearchDetailed":
            for _ in range(iterations):
                result = materialize_detailed(pattern.search(input_text), self.utf16_offsets)
        elif operation == "Count":
            for _ in range(iterations):
                result = sum(1 for _ in pattern.finditer(input_text))
        elif operation == "FindAllStrings":
            for _ in range(iterations):
                result = pattern.findall(input_text)
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
                    materialize_detailed(match, self.utf16_offsets)
                    for match in pattern.finditer(input_text)
                ]
        elif operation == "ReplaceString":
            for _ in range(iterations):
                result = pattern.sub(self.replacement, input_text)
        elif operation == "ReplaceUtf8":
            for _ in range(iterations):
                result = pattern.sub(self.replacement, input_text).encode("utf-8")
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
                result_text, count = pattern.subn(self.evaluator, input_text)
                result = result_text, count, self.callback_checksum
        elif operation == "SubnEvaluatorUtf8":
            for _ in range(iterations):
                self.callback_checksum = 0
                result_text, count = pattern.subn(self.evaluator, input_text)
                result = result_text.encode("utf-8"), count, self.callback_checksum
        elif operation == "SplitStrings":
            for _ in range(iterations):
                result = pattern.split(input_text)
        else:
            raise ValueError(f"Unsupported PythonRe operation: {operation}")
        return time.perf_counter_ns() - started, result

    def encode_findall(self, values: list[Any]) -> list[Any]:
        if self.pattern.groups <= 1:
            return [value.encode("utf-8") for value in values]
        return [tuple(value.encode("utf-8") for value in item) for item in values]

    def replace_callback(self, match: re.Match[str]) -> str:
        self.callback_checksum = combine(
            self.callback_checksum,
            detailed_checksum(materialize_detailed(match, self.utf16_offsets)),
        )
        return self.replacement

    def subn_evaluator(self, input_text: str, encode_utf8: bool) -> tuple[Any, int, int]:
        self.callback_checksum = 0
        result, count = self.pattern.subn(self.evaluator, input_text)
        return (result.encode("utf-8") if encode_utf8 else result), count, self.callback_checksum

    def checksum(self, result: Any) -> int:
        operation = self.operation
        if operation == "IsMatch":
            return 1 if result else 0
        if operation in {"Search", "Match", "FullMatch"}:
            return simple_match_checksum(result, self.utf16_offsets)
        if operation == "SearchDetailed":
            return detailed_checksum(result)
        if operation == "Count":
            return result
        if operation == "FindAllStrings":
            return findall_checksum(result, self.pattern.groups, False)
        if operation == "FindAllUtf8":
            return findall_checksum(result, self.pattern.groups, True)
        if operation == "FindIterDetailed":
            checksum = len(result)
            for match in result:
                checksum = combine(checksum, detailed_checksum(match))
            return checksum
        if operation in {"ReplaceString", "ReplaceUtf8"}:
            return checksum_bytes(result) if isinstance(result, bytes) else checksum_string(result)
        if operation in {"SubnString", "SubnUtf8"}:
            value, count = result
            value_checksum = checksum_bytes(value) if isinstance(value, bytes) else checksum_string(value)
            return combine(value_checksum, count)
        if operation in {"SubnEvaluatorString", "SubnEvaluatorUtf8"}:
            value, count, callback_checksum = result
            value_checksum = checksum_bytes(value) if isinstance(value, bytes) else checksum_string(value)
            return combine(combine(value_checksum, count), callback_checksum)
        if operation == "SplitStrings":
            checksum = len(result)
            for value in result:
                checksum = combine(checksum, -1 if value is None else checksum_string(value))
            return checksum
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


def main() -> int:
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
