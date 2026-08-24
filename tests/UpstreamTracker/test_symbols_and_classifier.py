from __future__ import annotations

import unittest

from support import TemporaryWorkspace, write_configuration

from goniegonie_upstream_tracker.classifier import ChangeClassification, compare_sources
from goniegonie_upstream_tracker.config import load_configuration
from goniegonie_upstream_tracker.symbols import build_snapshot


class SymbolAndClassifierTests(unittest.TestCase):
    def test_comments_and_whitespace_do_not_change_ast_symbol_hashes(self) -> None:
        with TemporaryWorkspace() as workspace:
            first = workspace.write(
                "first/src/source/service.py",
                """class Item:
    @property
    def value(self):
        return 1

    @value.setter
    def value(self, new_value):
        self._value = new_value
""",
            )
            second = workspace.write(
                "second/src/source/service.py",
                """# a review comment

class Item:

    @property
    def value(self):
        return 1  # unchanged behavior

    @value.setter
    def value(self, new_value):
        self._value = new_value
""",
            )

            first_snapshot = build_snapshot(first.parents[2], ("src/source",), require_tracked_paths=True)
            second_snapshot = build_snapshot(second.parents[2], ("src/source",), require_tracked_paths=True)
            first_file = first_snapshot.files[0]
            second_file = second_snapshot.files[0]

            self.assertNotEqual(first_file.content_hash, second_file.content_hash)
            self.assertEqual(first_file.ast_hash, second_file.ast_hash)
            self.assertEqual(first_file.symbols, second_file.symbols)
            self.assertEqual(
                1,
                sum(symbol.name == "Item.value" for symbol in first_file.symbols),
            )

    def test_classifier_distinguishes_every_required_change_kind(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(workspace)
            configuration = load_configuration(lock, port_map, exceptions)
            baseline = workspace.path / "baseline"
            current = workspace.path / "current"
            sources = {
                "comments.py": ("def keep():\n    return 1\n", "# comment\ndef keep():\n    return 1\n"),
                "signature.py": ("def change(value):\n    return value\n", "def change(value, scale=1):\n    return value\n"),
                "body.py": ("def change(value):\n    return value + 1\n", "def change(value):\n    return value + 2\n"),
                "constant.py": ("LIMIT = 1\n", "LIMIT = 2\n"),
                "data.json": ("{\"value\":1}\n", "{\"value\":2}\n"),
            }
            for name, (before, after) in sources.items():
                workspace.write(f"baseline/src/source/{name}", before)
                workspace.write(f"current/src/source/{name}", after)
            workspace.write("baseline/src/source/deleted.py", "def removed():\n    return 1\n")
            workspace.write("current/src/source/added.py", "def created():\n    return 1\n")
            workspace.write("baseline/src/source/service.py", "class Service:\n    def run(self):\n        return 1\n")
            workspace.write("current/src/source/service.py", "class Service:\n    def run(self):\n        return 1\n")

            report = compare_sources(configuration, baseline, current)
            classifications = {change.classification for change in report.changes}

            self.assertEqual(set(ChangeClassification), classifications)
            self.assertTrue(
                any(
                    change.path.endswith("comments.py")
                    and change.classification == ChangeClassification.COMMENTS_ONLY
                    for change in report.changes
                )
            )
            self.assertTrue(
                any(
                    change.symbol == "LIMIT"
                    and change.classification == ChangeClassification.CONSTANT_CHANGED
                    for change in report.changes
                )
            )


if __name__ == "__main__":
    unittest.main()
