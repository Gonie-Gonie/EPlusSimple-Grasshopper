from __future__ import annotations

import hashlib
import json
from pathlib import Path, PurePosixPath
import unittest


ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "fixtures" / "reference" / "python-0.7.0"
MANIFEST = BASELINE / "manifest.json"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


class ReferenceManifestIntegrityTests(unittest.TestCase):
    def test_manifest_exactly_hashes_every_reviewed_baseline_file(self) -> None:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(
            "dragons.python-reference.manifest.v1",
            manifest.get("schema"),
        )
        entries = manifest.get("files")
        self.assertIsInstance(entries, list)

        declared_paths: list[str] = []
        for entry in entries:
            self.assertEqual({"path", "bytes", "sha256"}, set(entry))
            relative = entry["path"]
            self.assertIsInstance(relative, str)
            posix = PurePosixPath(relative)
            self.assertFalse(posix.is_absolute())
            self.assertNotIn("..", posix.parts)
            self.assertEqual(relative, posix.as_posix())

            artifact = BASELINE.joinpath(*posix.parts)
            self.assertTrue(artifact.is_file(), relative)
            self.assertEqual(entry["bytes"], artifact.stat().st_size, relative)
            self.assertRegex(entry["sha256"], r"^[0-9a-f]{64}$")
            self.assertEqual(entry["sha256"], sha256_file(artifact), relative)
            declared_paths.append(relative)

        self.assertEqual(sorted(declared_paths), declared_paths)
        self.assertEqual(len(set(declared_paths)), len(declared_paths))
        actual_paths = sorted(
            path.relative_to(BASELINE).as_posix()
            for path in BASELINE.rglob("*")
            if path.is_file() and path != MANIFEST
        )
        self.assertEqual(declared_paths, actual_paths)


if __name__ == "__main__":
    unittest.main()
