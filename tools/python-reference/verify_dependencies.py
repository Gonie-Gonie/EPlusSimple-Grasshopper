"""Verify the exact repo-local dependency tree used by the Python oracle."""

from __future__ import annotations

import argparse
import importlib.metadata
import re
import sys
from pathlib import Path


_EXACT_REQUIREMENT = re.compile(
    r"^(?P<name>[A-Za-z0-9][A-Za-z0-9._-]*)==(?P<version>[^\s;]+)$"
)
_REQUIRED_ORACLE_ROOTS = frozenset(
    {
        "colorama",
        "eppy",
        "et-xmlfile",
        "numpy",
        "openpyxl",
        "pandas",
        "python-dateutil",
        "pytz",
        "shapely",
        "six",
        "tqdm",
        "tzdata",
    }
)


def _canonical_name(value: str) -> str:
    return re.sub(r"[-_.]+", "-", value).lower()


def _load_lock(path: Path) -> dict[str, tuple[str, str]]:
    locked: dict[str, tuple[str, str]] = {}
    for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        match = _EXACT_REQUIREMENT.fullmatch(line)
        if match is None:
            raise SystemExit(
                f"Dependency lock line {line_number} is not an exact name==version pin: {line!r}"
            )
        display_name = match.group("name")
        canonical_name = _canonical_name(display_name)
        if canonical_name in locked:
            raise SystemExit(f"Dependency lock contains duplicate package {display_name!r}.")
        locked[canonical_name] = (display_name, match.group("version"))
    if not locked:
        raise SystemExit("Dependency lock is empty.")
    return locked


def _installed_distributions(
    dependency_root: Path,
) -> dict[str, tuple[str, str, importlib.metadata.Distribution]]:
    installed: dict[str, tuple[str, str, importlib.metadata.Distribution]] = {}
    for distribution in importlib.metadata.distributions(path=[str(dependency_root)]):
        display_name = distribution.metadata.get("Name")
        if not display_name:
            raise SystemExit(
                f"Installed distribution metadata has no Name: {distribution._path}"
            )
        canonical_name = _canonical_name(display_name)
        if canonical_name in installed:
            raise SystemExit(f"Dependency root contains duplicate package {display_name!r}.")
        installed[canonical_name] = (display_name, distribution.version, distribution)
    return installed


def _verify_exact_set(
    locked: dict[str, tuple[str, str]],
    installed: dict[str, tuple[str, str, importlib.metadata.Distribution]],
) -> None:
    missing = sorted(set(locked) - set(installed))
    unexpected = sorted(set(installed) - set(locked))
    drifted = sorted(
        name
        for name in set(locked) & set(installed)
        if locked[name][1] != installed[name][1]
    )
    problems: list[str] = []
    if missing:
        problems.append("missing=" + ",".join(missing))
    if unexpected:
        problems.append("unexpected=" + ",".join(unexpected))
    if drifted:
        details = [
            f"{name}:{installed[name][1]}!=locked-{locked[name][1]}" for name in drifted
        ]
        problems.append("version-drift=" + ",".join(details))
    if problems:
        raise SystemExit("Repo-local dependency set does not match the lock: " + "; ".join(problems))


def _verify_required_oracle_roots(
    locked: dict[str, tuple[str, str]],
) -> None:
    missing = sorted(_REQUIRED_ORACLE_ROOTS - set(locked))
    if missing:
        raise SystemExit(
            "Dependency lock omits required oracle roots: " + ",".join(missing)
        )


def _verify_transitive_closure(
    installed: dict[str, tuple[str, str, importlib.metadata.Distribution]],
    pip_wheel: Path,
) -> None:
    sys.path.insert(0, str(pip_wheel))
    from pip._vendor.packaging.markers import default_environment
    from pip._vendor.packaging.requirements import InvalidRequirement, Requirement

    marker_environment = default_environment()
    marker_environment["extra"] = ""
    problems: list[str] = []
    for package_name in sorted(installed):
        display_name, _, distribution = installed[package_name]
        for requirement_text in distribution.requires or ():
            try:
                requirement = Requirement(requirement_text)
            except InvalidRequirement as error:
                problems.append(f"{display_name}: invalid requirement {requirement_text!r}: {error}")
                continue
            if requirement.marker is not None and not requirement.marker.evaluate(marker_environment):
                continue
            required_name = _canonical_name(requirement.name)
            required = installed.get(required_name)
            if required is None:
                problems.append(f"{display_name}: missing required package {requirement.name}")
                continue
            required_version = required[1]
            if requirement.specifier and not requirement.specifier.contains(
                required_version, prereleases=True
            ):
                problems.append(
                    f"{display_name}: {requirement.name} {required_version} does not satisfy "
                    f"{requirement.specifier}"
                )
    if problems:
        raise SystemExit("Dependency lock is not a valid transitive closure: " + "; ".join(problems))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dependency-root", type=Path, required=True)
    parser.add_argument("--requirements", type=Path, required=True)
    parser.add_argument("--pip-wheel", type=Path, required=True)
    args = parser.parse_args()

    for path in (args.dependency_root, args.requirements, args.pip_wheel):
        if not path.exists():
            raise SystemExit(f"Required dependency verification path does not exist: {path}")

    locked = _load_lock(args.requirements)
    _verify_required_oracle_roots(locked)
    installed = _installed_distributions(args.dependency_root)
    _verify_exact_set(locked, installed)
    _verify_transitive_closure(installed, args.pip_wheel)
    print(f"Verified {len(installed)} exact repo-local Python dependencies and their closure.")


if __name__ == "__main__":
    main()
