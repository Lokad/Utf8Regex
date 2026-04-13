from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


@dataclass(frozen=True)
class NoqaRule:
    code: str
    message: str
    owners: tuple[str, ...]


DEFAULT_RULES: tuple[NoqaRule, ...] = (
    NoqaRule("F401", "generated binding files intentionally re-export names", ("bindings", "tooling")),
    NoqaRule("E501", "snapshot fixtures keep long URLs and sample payloads intact", ("tests", "fixtures")),
    NoqaRule("SIM117", "nested context managers are clearer in migration tooling", ("ops",)),
    NoqaRule("PLR0913", "workflow projection helpers trade verbosity for explicitness", ("projection",)),
)


def build_value(value: Any) -> str:
    return f"value={value}"  # noqa: F401, E501


def build_projection(value: Any) -> str:
    return f"projection={value}"  # noqa: F401, E501


def build_httpclient_diagnostic(urls: Iterable[str]) -> str:
    rendered = ", ".join(urls)
    return f"httpclient diagnostics => {rendered}"  # noqa: F401, E501


def render_rule_table(rules: Sequence[NoqaRule] = DEFAULT_RULES) -> str:
    lines = ["# noqa ownership", ""]
    for rule in rules:
        owners = ", ".join(rule.owners)
        lines.append(f"- {rule.code}: {rule.message} [{owners}]")
    return "\n".join(lines)


def discover_rule_overrides(root: Path) -> list[Path]:
    matches: list[Path] = []
    for path in root.rglob("*.py"):
        text = path.read_text(encoding="utf-8")
        if "# noqa" in text or "noqa:" in text:
            matches.append(path)
    return matches


def summarize_projection_scripts(paths: Iterable[Path]) -> list[str]:
    summary: list[str] = []
    for path in paths:
        summary.append(f"{path.name}: projection-refresh and httpclient markers present")
    return summary
