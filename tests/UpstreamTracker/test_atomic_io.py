from __future__ import annotations

import os
import unittest

from support import TemporaryWorkspace

from dragons_upstream_tracker.atomic_io import write_text_atomically


class AtomicIoTests(unittest.TestCase):
    def test_planted_hardlinks_are_never_used_as_temporary_output(self) -> None:
        with TemporaryWorkspace() as workspace:
            victim = workspace.write("victim.txt", "must-survive\n")
            output = workspace.path / "generated" / "report.json"
            output.parent.mkdir(parents=True)
            planted = output.with_suffix(output.suffix + ".tmp")
            os.link(victim, planted)

            write_text_atomically(output, "generated\n")

            self.assertEqual("must-survive\n", victim.read_text(encoding="utf-8"))
            self.assertEqual("must-survive\n", planted.read_text(encoding="utf-8"))
            self.assertEqual("generated\n", output.read_text(encoding="utf-8"))
            self.assertEqual([], list(output.parent.glob(f".{output.name}.*.tmp")))

    def test_existing_hardlinked_destination_is_replaced_not_followed(self) -> None:
        with TemporaryWorkspace() as workspace:
            victim = workspace.write("victim.txt", "must-survive\n")
            output = workspace.path / "report.json"
            os.link(victim, output)

            write_text_atomically(output, "generated\n")

            self.assertEqual("must-survive\n", victim.read_text(encoding="utf-8"))
            self.assertEqual("generated\n", output.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
