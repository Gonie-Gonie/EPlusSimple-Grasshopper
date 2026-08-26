from __future__ import annotations

from pathlib import Path
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace, write_configuration

from goniegonie_upstream_tracker.config import load_configuration
from goniegonie_upstream_tracker.compatibility import load_compatibility_configuration
from goniegonie_upstream_tracker.errors import ConfigurationError
from goniegonie_upstream_tracker.yaml_subset import parse_yaml_subset


class ConfigurationTests(unittest.TestCase):
    def test_repository_manifests_validate_as_one_configuration(self) -> None:
        configuration = load_configuration(
            REPOSITORY_ROOT / "upstream" / "upstream.lock.json",
            REPOSITORY_ROOT / "upstream" / "port-map.yml",
            REPOSITORY_ROOT / "upstream" / "compatibility-exceptions.yml",
        )

        self.assertEqual("goniegonie.upstream-lock.v1", configuration.lock.schema)
        self.assertGreater(len(configuration.mappings), 0)
        self.assertTrue(
            all(
                mapping.dotnet_project.startswith("GonieGonie.")
                for mapping in configuration.mappings
            )
        )
        self.assertEqual(213, len(configuration.exceptions))
        compatibility = load_compatibility_configuration(
            configuration,
            REPOSITORY_ROOT / "upstream" / "compatibility-scope.json",
            REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json",
            REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json",
            repository_root=REPOSITORY_ROOT,
        )
        self.assertEqual(24, len(compatibility.inventory.files))
        self.assertEqual(1242, len(compatibility.inventory.symbols))
        self.assertEqual(
            len(compatibility.inventory.symbols),
            len(compatibility.matrix.entries),
        )
        self.assertEqual(647, len(compatibility.needs_reverification))
        self.assertEqual(
            136,
            sum(
                entry.classification == "equivalent"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertEqual(
            207,
            sum(
                entry.classification == "exception"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertIsNotNone(compatibility.symbol_evidence)
        symbol_evidence = compatibility.symbol_evidence
        assert symbol_evidence is not None
        self.assertEqual(343, len(symbol_evidence.entries))
        self.assertEqual(343, len(symbol_evidence.receipts))
        self.assertEqual(
            "sha256:5f5c377936ac1478b66faa79deab2dfee2ff25bc00ebd53735a2a0ccfce596fe",
            symbol_evidence.content_sha256,
        )
        self.assertEqual(
            252,
            sum(
                entry.classification == "out_of_scope"
                for entry in compatibility.matrix.entries
            ),
        )

        by_key = compatibility.matrix.entries_by_key
        expected_construction_family = {
            "AirBoundary.to_idf_object": (
                592,
                "model-context-air-boundary-idf-emission",
                "dragon-construction-air-boundary-to-idf-object-639a205f",
            ),
            "Construction.to_idf_object": (
                601,
                "model-context-construction-idf-emission",
                "dragon-construction-construction-to-idf-object-71a76f27",
            ),
            "Glazing.to_idf_object": (
                608,
                "model-context-glazing-idf-emission",
                "dragon-construction-glazing-to-idf-object-3350beaf",
            ),
            "Layer.to_idf_object": (
                617,
                "model-context-layer-idf-emission",
                "dragon-construction-layer-to-idf-object-66e6d458",
            ),
            "NoMassConstruction.to_idf_object": (
                640,
                "model-context-no-mass-construction-idf-emission",
                "dragon-construction-no-mass-construction-to-idf-object-2bc3fe98",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in (
            expected_construction_family.items()
        ):
            key = ("src/idragon/dragon/construction.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        expected_zone_idf = {
            "Zone.to_idf_hvac_default_object": (
                1092,
                "model-context-zone-hvac-default-idf-emission",
                "dragon-shape-zone-to-idf-hvac-default-object-ff678ec2",
            ),
            "Zone.to_idf_load_object": (
                1093,
                "model-context-zone-load-idf-emission",
                "dragon-shape-zone-to-idf-load-object-d19165f0",
            ),
            "Zone.to_idf_object": (
                1094,
                "model-context-zone-idf-emission",
                "dragon-shape-zone-to-idf-object-479f4d74",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in expected_zone_idf.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        expected_surface_idf = {
            "Surface.to_idf_object": (
                1045,
                "legacy-rectangular-surface-idf-emission",
                "dragon-shape-surface-to-idf-object-a03c4d52",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in (
            expected_surface_idf.items()
        ):
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        expected_opening_adjacency = {
            "Blind": (
                1025,
                "permissive-python-blind-state-75f7c91c",
                "dragon-shape-opening-adjacency-core-1025-75f7c91c",
                "sha256:c075c45deb7cda1407a72808c0d277d6efd5316085b144095d2bf517386973f7",
                "permissive-python-blind-state",
            ),
            "Blind.__init__": (
                1026,
                "permissive-python-blind-state-574e9b5a",
                "dragon-shape-opening-adjacency-core-1026-574e9b5a",
                "sha256:5388e0665f85bc41bb818f26c8f6fa89cf76898e26e6df4d36abe0c373570bee",
                "permissive-python-blind-state",
            ),
            "Door": (
                1028,
                "permissive-python-door-state-717d717a",
                "dragon-shape-opening-adjacency-core-1028-717d717a",
                "sha256:fa39e26798264f38d0e9a43f1940c2863f70f05203b2b1e8c74faa3c5eb8afd9",
                "permissive-python-door-state",
            ),
            "Door.__init__": (
                1029,
                "permissive-python-door-state-efd71c81",
                "dragon-shape-opening-adjacency-core-1029-efd71c81",
                "sha256:a7a103e8899887ec153199f974e4050ca493cda73d31b7c64e562ebba60ab30e",
                "permissive-python-door-state",
            ),
            "Shade": (
                1030,
                "permissive-python-shade-state-9404da04",
                "dragon-shape-opening-adjacency-core-1030-9404da04",
                "sha256:45b6423f0139944ee5c8cb4726894b9dc4585b05cdd343bddb24b830fe43c35f",
                "permissive-python-shade-state",
            ),
            "Shade.__init__": (
                1031,
                "permissive-python-shade-state-f76ed298",
                "dragon-shape-opening-adjacency-core-1031-f76ed298",
                "sha256:6d4294f2c7584ecee38272e92a20d323ba5d26bbe1b6cf488d5749dfdfc8c496",
                "permissive-python-shade-state",
            ),
            "Shading": (
                1033,
                "directly-instantiable-empty-python-shading-4dba9833",
                "dragon-shape-opening-adjacency-core-1033-4dba9833",
                "sha256:8b9e5ab60f25bf306e117d4df92ea08a784482435e73166da9ecdb52ab860bbc",
                "directly-instantiable-empty-python-shading",
            ),
            "Surface.__init__": (
                1035,
                "aliased-python-surface-opening-inputs-ef349ef4",
                "dragon-shape-opening-adjacency-core-1035-ef349ef4",
                "sha256:4b3ebe4b809eb5da6aa98988847d7dff288a1fd3370583166b27b0210feb31ee",
                "aliased-python-surface-opening-inputs",
            ),
            "Surface.blinded_window": (
                1039,
                "fresh-python-blinded-window-projection-f520fbfe",
                "dragon-shape-opening-adjacency-core-1039-f520fbfe",
                "sha256:69f056b8beb1e306a0c7ad7c83365a09e184365bb579bddfec8a349663b17d03",
                "fresh-python-blinded-window-projection",
            ),
            "Surface.boundary": (
                1040,
                "mutable-reciprocal-python-surface-adjacency-7753d967",
                "dragon-shape-opening-adjacency-core-1040-7753d967",
                "sha256:84d8d135700b571157f91ee6f2c3b9ae859d233d0dc5beadfd4eb16704174189",
                "mutable-reciprocal-python-surface-adjacency",
            ),
            "Surface.get_subsurface": (
                1042,
                "legacy-linear-scale-subsurface-projection-7e43708d",
                "dragon-shape-opening-adjacency-core-1042-7e43708d",
                "sha256:0705e84c47890ec56447c65eff16bb31d5340d79e19921ae7b985ecb037855b3",
                "legacy-linear-scale-subsurface-projection",
            ),
            "SurfaceBoundaryCondition": (
                1048,
                "lowercase-python-surface-boundary-enum-73a8b86f",
                "dragon-shape-opening-adjacency-core-1048-73a8b86f",
                "sha256:08ce2c6a908b36f58f12d985c5e38cb513acbae7176e30362366de85347ed0b0",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ADIABATIC": (
                1049,
                "lowercase-python-surface-boundary-enum-1d0e3d46",
                "dragon-shape-opening-adjacency-core-1049-1d0e3d46",
                "sha256:71a4f989a069bbfa082ed1c95b6d592cb559164bbc3e59bbd3e4516acbd121b8",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.GROUND": (
                1050,
                "lowercase-python-surface-boundary-enum-0992cbf6",
                "dragon-shape-opening-adjacency-core-1050-0992cbf6",
                "sha256:93821fe3c3d6a6237b2810173fd5930d7650eead430add543f323b70ec45cc48",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.OUTDOOR": (
                1051,
                "lowercase-python-surface-boundary-enum-8560160a",
                "dragon-shape-opening-adjacency-core-1051-8560160a",
                "sha256:fa3365775b6703acd140afe11e50b9f546326dcb8b74d608086d15e6f3393b20",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ZONE": (
                1052,
                "lowercase-python-surface-boundary-enum-3ec06789",
                "dragon-shape-opening-adjacency-core-1052-3ec06789",
                "sha256:ce75591156a712c29b635ee22f70387d62faee6d006cf41308a4649819ed6abc",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.__str__": (
                1053,
                "lowercase-python-surface-boundary-enum-f40e4929",
                "dragon-shape-opening-adjacency-core-1053-f40e4929",
                "sha256:334050b16537ed04fbda77c181a7442d51c502b0d35014eedc670fd651e85786",
                "lowercase-python-surface-boundary-enum",
            ),
            "Window": (
                1081,
                "permissive-python-window-state-af640a9a",
                "dragon-shape-opening-adjacency-core-1081-af640a9a",
                "sha256:19224d3273d70c3effeced094b414ca5df1fdb1316e39f9ba9f79236dd760c51",
                "permissive-python-window-state",
            ),
            "Window.__init__": (
                1082,
                "permissive-python-window-state-3ce851bd",
                "dragon-shape-opening-adjacency-core-1082-3ce851bd",
                "sha256:dc72750acd6bdc3c06076d2f61c0cd7e8b1ef02b5e7499c142a0142d1bc426d9",
                "permissive-python-window-state",
            ),
        }
        native_source_hashes = {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Shading.cs": "sha256:99b426d76894461ca1f29e41dfba08204ee43a72f6133f3588eedd7e79b3affd",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Openings.cs": "sha256:4da15fd6ee228d471bc1a249abf23f7dbff5687ff0f1dabb9dc820b512aee494",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Surface.cs": "sha256:545dc79dd89e84acf6d714e79da7b2cda059dfcaa3b4f74d291ad572ebd51264",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/SurfaceBoundary.cs": "sha256:c0ba4cf5a93eb2678aee2c698320121f5bfbd68f7febb3dc901fe700da1499d9",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/SurfaceAdjacency.cs": "sha256:83d67c465446be31133fcd17d2e3cbbab9b6b320a28a3f2608ad55c99450fb59",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs": "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905",
        }
        expected_native_symbols = {
            "Blind": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Blind"),
            "Blind.__init__": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Blind.Blind"),
            "Door": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Door"),
            "Door.__init__": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Door.Door"),
            "Shade": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Shade"),
            "Shade.__init__": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Shade.Shade"),
            "Shading": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.IShadingDevice"),
            "Surface.__init__": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Surface"),
            "Surface.blinded_window": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Windows"),
            "Surface.boundary": ("Shape/SurfaceAdjacency.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceAdjacency.Match"),
            "Surface.get_subsurface": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.CreateCenteredSubsurface"),
            "SurfaceBoundaryCondition": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition"),
            "SurfaceBoundaryCondition.ADIABATIC": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Adiabatic"),
            "SurfaceBoundaryCondition.GROUND": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Ground"),
            "SurfaceBoundaryCondition.OUTDOOR": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Outdoors"),
            "SurfaceBoundaryCondition.ZONE": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Zone"),
            "SurfaceBoundaryCondition.__str__": ("Model/EnergyModelIdfAssembler.cs", "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.BuildingSurface"),
            "Window": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Window"),
            "Window.__init__": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Window.Window"),
        }
        test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "OpeningAdjacencyCoreOracleParityTests.cs"
        )
        test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Model."
            "OpeningAdjacencyCoreOracleParityTests."
            "MatchesPinnedOpeningAdjacencyCoreThroughBoundedNativeRoutes"
        )
        test_hash = (
            "sha256:7ad3b9251f5c73e5a710d4ce7ef836d63e79000c2c3e6a00952ca61ccfaa5aa2"
        )
        opening_families = set()
        for symbol, (
            index,
            exception_id,
            assertion_id,
            receipt_hash,
            adaptation_family,
        ) in expected_opening_adjacency.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertTrue(exception_id.startswith(adaptation_family + "-"), symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(1, len(evidence_entry.receipts))
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(receipt_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(test_path, receipt.test_path, symbol)
            self.assertEqual(test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(test_hash, receipt.test_source_sha256, symbol)
            self.assertIn(f"Adaptation family {adaptation_family}", receipt.assertion)
            suffix, native_symbol = expected_native_symbols[symbol]
            self.assertTrue(evidence_entry.implementation_path.endswith(suffix), symbol)
            self.assertEqual(native_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                native_source_hashes[evidence_entry.implementation_path],
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            opening_families.add(adaptation_family)
        self.assertEqual(19, len(expected_opening_adjacency))
        self.assertEqual(10, len(opening_families))
        self.assertNotIn("Surface.to_idf_object", expected_opening_adjacency)
        expected_geometry_core = {
            "Surface": (
                1034,
                "exception",
                "permissive-python-surface-polygon-model-cb620c55",
                "dragon-shape-geometry-core-1034-cb620c55",
                "sha256:2867a6b5a26a756fa6aeaf65068a2e9fa7321b0e6923e77ae9524036b686472d",
                "permissive-python-surface-polygon-model",
            ),
            "Surface.area": (
                1038,
                "exception",
                "first-triple-oriented-python-surface-area-f254ab66",
                "dragon-shape-geometry-core-1038-f254ab66",
                "sha256:6cc3a7db18daa625d5dab183570f02c32e5319f911ac9f3517bff23d8d6aa191",
                "first-triple-oriented-python-surface-area",
            ),
            "Surface.center": (
                1041,
                "exception",
                "vertex-mean-python-surface-center-f0c05c2b",
                "dragon-shape-geometry-core-1041-f0c05c2b",
                "sha256:8074898397022e774997a6a725b80781111550897ab44740e89dc3238b2ff455",
                "vertex-mean-python-surface-center",
            ),
            "Surface.height": (
                1043,
                "exception",
                "z-span-python-surface-height-d479fe2f",
                "dragon-shape-geometry-core-1043-d479fe2f",
                "sha256:a528074f8ab957267bac2b8ce3557194ff474c111b17321c8b97ea797b5df5a9",
                "z-span-python-surface-height",
            ),
            "Surface.normal": (
                1044,
                "exception",
                "first-triple-python-surface-normal-3f089c8c",
                "dragon-shape-geometry-core-1044-3f089c8c",
                "sha256:81ec416bc7639194804e4380e03f99006dbf6a16d09525ccadec25da28321eb8",
                "first-triple-python-surface-normal",
            ),
            "Surface.type": (
                1046,
                "exception",
                "mutable-string-coerced-python-surface-type-ae4bdcc7",
                "dragon-shape-geometry-core-1046-ae4bdcc7",
                "sha256:d312aa561bdbb6cab2484f8a078a702bb56d38be42f3bcaa2800f5532b518888",
                "mutable-string-coerced-python-surface-type",
            ),
            "Surface.vertex": (
                1047,
                "exception",
                "aliased-mutable-python-surface-vertices-7ed5c6b3",
                "dragon-shape-geometry-core-1047-7ed5c6b3",
                "sha256:0810dc08418bc78deac6f89786a849f5c20390ecc994c9395bcad0c715ce3cd7",
                "aliased-mutable-python-surface-vertices",
            ),
            "SurfaceType": (
                1054,
                "exception",
                "lowercase-python-surface-type-enum-61a37f9d",
                "dragon-shape-geometry-core-1054-61a37f9d",
                "sha256:e28446741c641805190b1565b43de667c6062361b1409215d5f7ad5d4314c31f",
                "lowercase-python-surface-type-enum",
            ),
            "SurfaceType.CEILING": (
                1055,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1055-9ece8323",
                "sha256:05316ed46e42810cf7e5b0de1ba338d5f502c25a6a4c97e208fd5cdb9c435919",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.FLOOR": (
                1056,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1056-c8c4f240",
                "sha256:4bab1a992d34a81b0e0b280baba6fe6d305cf430d82c817ccfef4b853ab574cb",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.WALL": (
                1057,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1057-ca6d5593",
                "sha256:e4f63e8852e3c5be771dc461fd372e655c9683e9b3ed30ce928643eb0409029c",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.__str__": (
                1058,
                "exception",
                "lowercase-python-surface-type-enum-f40e4929",
                "dragon-shape-geometry-core-1058-f40e4929",
                "sha256:14ae314f685943e78011024dcc0b279c48b972f9c72053029849eef98547e65b",
                "lowercase-python-surface-type-enum",
            ),
            "Vertex": (
                1059,
                "exception",
                "permissive-mutable-python-vertex-state-78650289",
                "dragon-shape-geometry-core-1059-78650289",
                "sha256:0f2962d8fab43164ac0ad84f04fe7bbbbe53ceaa9893f7b4093b4209aacfd34f",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.__add__": (
                1060,
                "exception",
                "untyped-python-vertex-algebra-a5c7ecea",
                "dragon-shape-geometry-core-1060-a5c7ecea",
                "sha256:9b3e942fa977cb5374c53c5539b263d8e66bffb8a7ca3d2dba42cea26460bc22",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__deepcopy__": (
                1061,
                "exception",
                "python-vertex-copy-iteration-zero-addition-2c79da1a",
                "dragon-shape-geometry-core-1061-2c79da1a",
                "sha256:7cc15089ff3974303b8ff66c82264f1e4575280cebe7dc53b695df31229e19aa",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__init__": (
                1063,
                "exception",
                "permissive-mutable-python-vertex-state-be3c69c5",
                "dragon-shape-geometry-core-1063-be3c69c5",
                "sha256:a31d5463a22afc15fbfa422477e455ddbfe768412d716d1f76c4d4f0a4afd733",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.__iter__": (
                1064,
                "exception",
                "python-vertex-copy-iteration-zero-addition-e95d7ce5",
                "dragon-shape-geometry-core-1064-e95d7ce5",
                "sha256:5c429ab04b30eb052ab4fdd421665fb13e870a75b834a1f0a85307bd464002dc",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__mul__": (
                1065,
                "exception",
                "untyped-python-vertex-algebra-323878e1",
                "dragon-shape-geometry-core-1065-323878e1",
                "sha256:608a929646e45db53d8800b571e607ff4f191b15109a359ad311f91684840e38",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__radd__": (
                1066,
                "exception",
                "python-vertex-copy-iteration-zero-addition-a473d0f3",
                "dragon-shape-geometry-core-1066-a473d0f3",
                "sha256:388fb6ca4786fe66e539f699caf4937ef824dc6118441a12a19e01dbb7ab049a",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__rmul__": (
                1068,
                "exception",
                "untyped-python-vertex-algebra-1dbe33d3",
                "dragon-shape-geometry-core-1068-1dbe33d3",
                "sha256:4e09af087fb303452aaf3761ce473e61cf23286f9600a8efde211d9460a05f16",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__sub__": (
                1070,
                "exception",
                "untyped-python-vertex-algebra-4ee38e65",
                "dragon-shape-geometry-core-1070-4ee38e65",
                "sha256:bf1d02133fd2651444492a88f3b27ed4bd9b7cc1d4ffb0351232a7ca652ccc87",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__truediv__": (
                1071,
                "exception",
                "untyped-python-vertex-algebra-94f397b8",
                "dragon-shape-geometry-core-1071-94f397b8",
                "sha256:9fcfa8d48d9d35d62788f9b2ad7f218fa224e37cdf6ecfc32fbbe02ddc9fdf53",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.are_coplanar": (
                1072,
                "exception",
                "legacy-first-triple-angular-coplanarity-905ebbf2",
                "dragon-shape-geometry-core-1072-905ebbf2",
                "sha256:ae0fab18d3b9c344c9ca4b66c991a16a958cf93a6323b69faf7736d3160d1f87",
                "legacy-first-triple-angular-coplanarity",
            ),
            "Vertex.cross": (
                1073,
                "exception",
                "untyped-python-vertex-metrics-6bc5db49",
                "dragon-shape-geometry-core-1073-6bc5db49",
                "sha256:47acbfb4038b9fcc6998c29b6c2221e72bf7613ea55689cfb3b5cabf1a926c08",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.distance": (
                1074,
                "exception",
                "untyped-python-vertex-metrics-88c4cb9f",
                "dragon-shape-geometry-core-1074-88c4cb9f",
                "sha256:ba64225df24c6f348dbe1c61a31f6d7abfee9b5198e9aeb7effbacd487f45f47",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.dot": (
                1075,
                "exception",
                "untyped-python-vertex-metrics-1aaf5930",
                "dragon-shape-geometry-core-1075-1aaf5930",
                "sha256:f84f3567eecb80518afa19d48a74cd56841ff2d559f7374c3c29fa2a80da1d8e",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.norm": (
                1076,
                "exception",
                "untyped-python-vertex-metrics-e41eae31",
                "dragon-shape-geometry-core-1076-e41eae31",
                "sha256:0d5f53eedec478840c4d78919bde15e99901b3011389dd7eecd5c6c10efdd324",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.unit": (
                1077,
                "exception",
                "zero-preserving-python-vertex-unit-4267bc06",
                "dragon-shape-geometry-core-1077-4267bc06",
                "sha256:510b8d8341d45c68b2503628e1d23a91794e4a48266264c38831046f9fb518d6",
                "zero-preserving-python-vertex-unit",
            ),
            "Vertex.x": (
                1078,
                "exception",
                "permissive-mutable-python-vertex-state-d859bad0",
                "dragon-shape-geometry-core-1078-d859bad0",
                "sha256:1b9c809756cb95c699a83bce8a074a73178f778e48bfeb9b094b6afa2f6a85b5",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.y": (
                1079,
                "exception",
                "permissive-mutable-python-vertex-state-ff0bcc12",
                "dragon-shape-geometry-core-1079-ff0bcc12",
                "sha256:b203ff08791766ecee97c0d65b2e6242bef726c134d29f1e9f590422c1ad7846",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.z": (
                1080,
                "exception",
                "permissive-mutable-python-vertex-state-64899aff",
                "dragon-shape-geometry-core-1080-64899aff",
                "sha256:39d8c23b19ef20d1ca83a4785214fb9470c910d6af5f9d34fd23a6f5a97ecfac",
                "permissive-mutable-python-vertex-state",
            ),
        }
        geometry_native_source_hashes = {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/PlanarPolygon.cs": "sha256:73a1dd052fb12ed0802a6236d21484e2b680cbe3f0f4005ade6a61995111c653",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Surface.cs": "sha256:545dc79dd89e84acf6d714e79da7b2cda059dfcaa3b4f74d291ad572ebd51264",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/SurfaceBoundary.cs": "sha256:c0ba4cf5a93eb2678aee2c698320121f5bfbd68f7febb3dc901fe700da1499d9",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Vector3.cs": "sha256:02536827db9d1c6ff48a46678871e4d736d9536228f0de370a9fb2c5294b9ede",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Vertex.cs": "sha256:f37b229b45b23c23ddc54ed85aea1b93a201a74c30c7b29793f268e364435a67",
        }
        expected_geometry_native_symbols = {
            "Surface": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface"),
            "Surface.area": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.GrossArea"),
            "Surface.center": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Center"),
            "Surface.height": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Height"),
            "Surface.normal": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Normal"),
            "Surface.type": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Type"),
            "Surface.vertex": ("Shape/PlanarPolygon.cs", "GonieGonie.InvisibleDragon.Shape.PlanarPolygon.Vertices"),
            "SurfaceType": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType"),
            "SurfaceType.CEILING": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType.Ceiling"),
            "SurfaceType.FLOOR": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType.Floor"),
            "SurfaceType.WALL": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType.Wall"),
            "SurfaceType.__str__": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType"),
            "Vertex": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex"),
            "Vertex.__add__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.op_Addition"),
            "Vertex.__deepcopy__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex"),
            "Vertex.__init__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.Vertex"),
            "Vertex.__iter__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.X"),
            "Vertex.__mul__": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.op_Multiply"),
            "Vertex.__radd__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.op_Addition"),
            "Vertex.__rmul__": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.op_Multiply"),
            "Vertex.__sub__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.op_Subtraction"),
            "Vertex.__truediv__": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.op_Division"),
            "Vertex.are_coplanar": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.AreCoplanar"),
            "Vertex.cross": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Cross"),
            "Vertex.distance": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.DistanceTo"),
            "Vertex.dot": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Dot"),
            "Vertex.norm": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Length"),
            "Vertex.unit": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Normalize"),
            "Vertex.x": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.X"),
            "Vertex.y": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.Y"),
            "Vertex.z": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.Z"),
        }
        geometry_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "GeometryCoreOracleParityTests.cs"
        )
        geometry_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Model.GeometryCoreOracleParityTests."
            "MatchesPinnedGeometryCoreThroughBoundedNativeRoutes"
        )
        geometry_test_hash = (
            "sha256:6b9541530d1cd8f029ebd4596c87b019f6b2fecccd7426b12d62400fdb553edf"
        )
        geometry_families = set()
        geometry_assertions = {}
        equivalent_geometry_symbols = set()
        exception_geometry_symbols = set()
        for symbol, (
            index,
            classification,
            exception_id,
            assertion_id,
            receipt_hash,
            adaptation_family,
        ) in expected_geometry_core.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual(classification, entry.classification, symbol)
            expected_refs = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if classification == "exception":
                assert exception_id is not None
                self.assertEqual(exception_id, entry.exception_id, symbol)
                self.assertTrue(exception_id.startswith(adaptation_family + "-"), symbol)
                expected_refs.insert(
                    0, f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
                exception_geometry_symbols.add(symbol)
            else:
                self.assertEqual("equivalent", classification, symbol)
                self.assertIsNone(entry.exception_id, symbol)
                equivalent_geometry_symbols.add(symbol)
            self.assertEqual(tuple(expected_refs), entry.evidence, symbol)
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertEqual(receipt_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(geometry_test_path, receipt.test_path, symbol)
            self.assertEqual(geometry_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(geometry_test_hash, receipt.test_source_sha256, symbol)
            self.assertIn(f"Adaptation family {adaptation_family}", receipt.assertion)
            self.assertIn(
                "Other facts co-recorded in the same case observations are context-only",
                receipt.assertion,
                symbol,
            )
            suffix, native_symbol = expected_geometry_native_symbols[symbol]
            self.assertTrue(evidence_entry.implementation_path.endswith(suffix), symbol)
            self.assertEqual(native_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                geometry_native_source_hashes[evidence_entry.implementation_path],
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            geometry_families.add(adaptation_family)
            geometry_assertions[symbol] = receipt.assertion
        self.assertEqual(31, len(expected_geometry_core))
        self.assertEqual(15, len(geometry_families))
        self.assertEqual(
            {"SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL"},
            equivalent_geometry_symbols,
        )
        self.assertEqual(28, len(exception_geometry_symbols))
        self.assertIn(
            "V04 zero, nonfinite and exception observations are absent and are not claimed",
            geometry_assertions["Vertex.__rmul__"],
        )
        self.assertIn(
            "No V04 behavior or native exception boundary is claimed",
            geometry_assertions["Vertex.__sub__"],
        )
        self.assertIn(
            "The V04 multiplication and division exceptions are context-only",
            geometry_assertions["Vertex.__add__"],
        )
        self.assertIn(
            "coordinate-projection and reverse-addition observations are context-only",
            geometry_assertions["Vertex.__deepcopy__"],
        )
        self.assertIn(
            "No Vertex.ToVector implementation binding, copy behavior or reverse-addition behavior is claimed",
            geometry_assertions["Vertex.__iter__"],
        )
        self.assertIn(
            "Copy, iteration, multiplication and division observations are context-only",
            geometry_assertions["Vertex.__radd__"],
        )
        self.assertIn(
            "T14 parsing and integer-cast observations co-recorded in the receipt are context-only",
            geometry_assertions["SurfaceType.__str__"],
        )
        self.assertFalse(set(expected_geometry_core) & set(expected_opening_adjacency))
        self.assertFalse(set(expected_geometry_core) & set(expected_zone_idf))
        for preserved_out_of_scope in (
            "Surface.__repr__",
            "Surface.__str__",
            "Vertex.__eq__",
            "Vertex.__repr__",
            "Vertex.__str__",
        ):
            self.assertEqual(
                "out_of_scope",
                by_key[("src/idragon/dragon/shape.py", preserved_out_of_scope)].classification,
                preserved_out_of_scope,
            )
        expected_zone_core = {
            "Zone": (
                1083,
                "permissive-mutable-python-zone-container",
                "dragon-shape-zone-core-1083-4830290e",
                "sha256:0a8e3c4a13829403a767b44e261874a5474efcd68a45690d82d9ef7390c6a9b3",
                "permissive-mutable-python-zone-container",
            ),
            "Zone.__init__": (
                1084,
                "unchecked-aliased-python-zone-construction",
                "dragon-shape-zone-core-1084-fad03092",
                "sha256:981c4ce3873cc1f2a318798c5c17acf4930c1dc22d62030ea867d266b2066323",
                "unchecked-aliased-python-zone-construction",
            ),
            "Zone.floor_area": (
                1085,
                "python-floor-identity-filter-and-dynamic-sum",
                "dragon-shape-zone-core-1085-21fe276d",
                "sha256:03ac624fcb7cab7c9747dae4110451bb050787739d64c531c1020551cb599c88",
                "python-floor-identity-filter-and-dynamic-sum",
            ),
            "Zone.floor_surface": (
                1086,
                "python-floor-identity-filter-and-fresh-list",
                "dragon-shape-zone-core-1086-53382328",
                "sha256:27e26141d68464f70c03765f293ee32f249eeaef215d22295bcf64397148f214",
                "python-floor-identity-filter-and-fresh-list",
            ),
            "Zone.idf_airexhaustnodelistname": (
                1087,
                "mutable-unvalidated-python-zone-name-formatting-48c6fddb",
                "dragon-shape-zone-core-1087-48c6fddb",
                "sha256:09948bfe118818e91b075083c372b79e6ba897ad914e3b294023ca4549851d57",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.idf_airinletnodelistname": (
                1088,
                "mutable-unvalidated-python-zone-name-formatting-97745304",
                "dragon-shape-zone-core-1088-97745304",
                "sha256:535e3cdeaf89f909847737d3c3ee01d4e1472dca2d3d753ade3e4d9e01e8490b",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.idf_equipmentlistname": (
                1089,
                "mutable-unvalidated-python-zone-name-formatting-ad9ccd78",
                "dragon-shape-zone-core-1089-ad9ccd78",
                "sha256:b42ff0074cb2373baf4b5e50eabb52a10e9a1f035b1d26504b46c6e8c8d0496a",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.supply": (
                1091,
                "embedded-python-zone-supply-coercion-and-mutation",
                "dragon-shape-zone-core-1091-1b5900c0",
                "sha256:c4a3dfe679378e960361262fc322ce34960c2d32f2f8bf21f41f9f6ce987ccae",
                "embedded-python-zone-supply-coercion-and-mutation",
            ),
        }
        zone_native_sources = {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Zone.cs": "sha256:37bd33ef649a03988255edd9f95bbb0f1ffb7c63cbf8fd1ddb784ebb071b8920",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs": "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HvacAbstractions.cs": "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a",
        }
        expected_zone_native_symbols = {
            "Zone": ("Shape/Zone.cs", "GonieGonie.InvisibleDragon.Shape.Zone"),
            "Zone.__init__": (
                "Shape/Zone.cs",
                "GonieGonie.InvisibleDragon.Shape.Zone.Zone",
            ),
            "Zone.floor_area": (
                "Shape/Zone.cs",
                "GonieGonie.InvisibleDragon.Shape.Zone.FloorArea",
            ),
            "Zone.floor_surface": (
                "Shape/Zone.cs",
                "GonieGonie.InvisibleDragon.Shape.Zone.FloorSurfaces",
            ),
            "Zone.idf_airexhaustnodelistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.idf_airinletnodelistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.idf_equipmentlistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.supply": (
                "Hvac/HvacAbstractions.cs",
                "GonieGonie.InvisibleDragon.Hvac.ZoneHvacAssignment",
            ),
        }
        zone_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "ZoneCoreOracleParityTests.cs"
        )
        zone_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Model.ZoneCoreOracleParityTests."
            "MatchesPinnedZoneCoreThroughTypedNativeRoutes"
        )
        zone_test_hash = (
            "sha256:46b3d12a353c2e083ec81260692ede77b6658452d6f0815328597884ee8a0582"
        )
        zone_assertions = {}
        zone_families = set()
        for symbol, (
            index,
            exception_id,
            assertion_id,
            receipt_hash,
            adaptation_family,
        ) in expected_zone_core.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertTrue(
                exception_id == adaptation_family
                or exception_id.startswith(adaptation_family + "-"),
                symbol,
            )
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertEqual(receipt_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(zone_test_path, receipt.test_path, symbol)
            self.assertEqual(zone_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(zone_test_hash, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            self.assertIn(f"Adaptation family {adaptation_family}", receipt.assertion)
            suffix, native_symbol = expected_zone_native_symbols[symbol]
            self.assertTrue(evidence_entry.implementation_path.endswith(suffix), symbol)
            self.assertEqual(native_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                zone_native_sources[evidence_entry.implementation_path],
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            zone_assertions[symbol] = receipt.assertion
            zone_families.add(adaptation_family)
        self.assertEqual(8, len(expected_zone_core))
        self.assertEqual(6, len(zone_families))
        self.assertIn("not a deep-copy claim", zone_assertions["Zone.floor_surface"])
        self.assertIn(
            "nonfinite, huge or mixed overflow and coercion, missing or raising area",
            zone_assertions["Zone.floor_area"],
        )
        self.assertIn(
            "no deep-copy claim is made",
            zone_assertions["Zone.__init__"],
        )
        for naming_symbol in (
            "Zone.idf_airexhaustnodelistname",
            "Zone.idf_airinletnodelistname",
            "Zone.idf_equipmentlistname",
        ):
            self.assertIn("custom string-conversion", zone_assertions[naming_symbol])
        self.assertIn(
            "context HVAC symbols, virtual subclasses, descriptor tampering",
            zone_assertions["Zone.supply"],
        )
        self.assertFalse(set(expected_zone_core) & set(expected_geometry_core))
        self.assertFalse(set(expected_zone_core) & set(expected_opening_adjacency))
        self.assertFalse(set(expected_zone_core) & set(expected_zone_idf))
        for preserved_symbol, expected_classification in {
            "Window": "exception",
            "Window.__init__": "exception",
            "Zone.is_conditioned": "exception",
            "Zone.to_idf_hvac_default_object": "exception",
            "Zone.to_idf_load_object": "exception",
            "Zone.to_idf_object": "exception",
            "SurfaceType.FLOOR": "equivalent",
        }.items():
            self.assertEqual(
                expected_classification,
                by_key[("src/idragon/dragon/shape.py", preserved_symbol)].classification,
                preserved_symbol,
            )
        for context_symbol, expected_classification in {
            "ElectricRadiator": "needs_reverification",
            "ElectricRadiator.__init__": "needs_reverification",
            "ElectricRadiator.heatable": "needs_reverification",
            "SupplyGroup": "needs_reverification",
            "SupplyGroup.__init__": "exception",
            "SupplySystem": "needs_reverification",
        }.items():
            self.assertEqual(
                expected_classification,
                by_key[("src/idragon/dragon/hvac.py", context_symbol)].classification,
                context_symbol,
            )
        expected_source_system_idf = {
            "AbsorptionChiller.to_idf_object": (
                644,
                "legacy-context-absorption-chiller-idf-emission",
                "dragon-hvac-absorption-chiller-to-idf-object-17d5fb8a",
            ),
            "Boiler.to_idf_object": (
                655,
                "compact-native-boiler-idf-emission",
                "dragon-hvac-boiler-to-idf-object-b63a454b",
            ),
            "Boiler.to_idf_object_as_generator": (
                656,
                "fresh-native-boiler-generator-idf-emission",
                "dragon-hvac-boiler-to-idf-object-as-generator-d239b10e",
            ),
            "Chiller.to_idf_object": (
                660,
                "legacy-context-chiller-idf-emission",
                "dragon-hvac-chiller-to-idf-object-fc75129f",
            ),
            "ClosedSingleSpeedCoolingTower.to_idf_main_object": (
                663,
                "cooling-tower-context-closed-single-speed-main-idf-emission",
                "dragon-hvac-closed-single-speed-cooling-tower-to-idf-main-object-0e14065a",
            ),
            "ClosedTwoSpeedCoolingTower.to_idf_main_object": (
                666,
                "cooling-tower-context-closed-two-speed-main-idf-emission",
                "dragon-hvac-closed-two-speed-cooling-tower-to-idf-main-object-30402683",
            ),
            "CompressorType.to_idf_curve_object": (
                672,
                "chiller-context-compressor-curve-idf-emission",
                "dragon-hvac-compressor-type-to-idf-curve-object-8ca6c2d0",
            ),
            "CoolingTower.to_idf_main_object": (
                684,
                "contextual-native-cooling-tower-main-idf-contract",
                "dragon-hvac-cooling-tower-to-idf-main-object-4615e08c",
            ),
            "CoolingTower.to_idf_object": (
                685,
                "legacy-context-cooling-tower-idf-emission",
                "dragon-hvac-cooling-tower-to-idf-object-74287ab5",
            ),
            "HeatPump.to_idf_object": (
                743,
                "compact-native-heat-pump-idf-emission",
                "dragon-hvac-heat-pump-to-idf-object-b8cb28ab",
            ),
            "OpenSingleSpeedCoolingTower.to_idf_main_object": (
                746,
                "cooling-tower-context-open-single-speed-main-idf-emission",
                "dragon-hvac-open-single-speed-cooling-tower-to-idf-main-object-102bccd9",
            ),
            "OpenTwoSpeedCoolingTower.to_idf_main_object": (
                749,
                "cooling-tower-context-open-two-speed-main-idf-emission",
                "dragon-hvac-open-two-speed-cooling-tower-to-idf-main-object-7fd75338",
            ),
            "SourceSystem.to_idf_object": (
                788,
                "contextual-native-source-system-idf-contract",
                "dragon-hvac-source-system-to-idf-object-63aa5eab",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in (
            expected_source_system_idf.items()
        ):
            key = ("src/idragon/dragon/hvac.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        api_entries = [
            entry
            for entry in compatibility.matrix.entries
            if entry.path == "src/epsimple/api.py"
        ]
        self.assertEqual(10, len(api_entries))
        self.assertTrue(
            all(entry.classification == "out_of_scope" for entry in api_entries)
        )
        self.assertTrue(
            all(
                entry.evidence[0].startswith("upstream/scope-decisions.json#")
                for entry in api_entries
            )
        )
        self.assertEqual(
            "exception",
            by_key[("src/epsimple/utils.py", "GRJSON_FORMAT")].classification,
        )
        self.assertEqual(
            "immutable-validated-grm-template",
            by_key[("src/epsimple/utils.py", "GRJSON_FORMAT")].exception_id,
        )
        expected_constants_metadata = {
            "Directory": (
                568,
                "resolved-native-runtime-and-resource-layout",
                "constants-metadata-568-5b876ad7",
                "sha256:b010e27fab04726eca7bad08cc9862c6f9614f44bd86658c6fa519f909de7c58",
                "sha256:1260f23bad5142f44afb51fbbaf1a335b256712d9d1fe3670f3c84f238ecc1ae",
                "src/Shared/GonieGonie.EnergyPlus.Runtime/RuntimeResolver.cs",
                "GonieGonie.EnergyPlus.Runtime.RuntimeResolver",
                "sha256:5c4170c2f4648a5fab93ff092c2c307589bd909d436437ccc66280bf4ac487f6",
            ),
            "Directory.ENERGYPLUS_DIR": (
                569,
                "explicit-validated-native-energyplus-runtime-root",
                "constants-metadata-569-7e01ceac",
                "sha256:e245c641ed9b9a37d6e2f7f17c52f6f44ae274516b4edfe740c4866e81436960",
                "sha256:9197d216c12052e6553557ef964bd06d48f09c212f4598773f1d7f299e763eba",
                "src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs",
                "GonieGonie.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.RootPath",
                "sha256:3b2beace10108918cfc69b06be42da966fc138e3f2fcff1c9bf39d2d5cdce84c",
            ),
            "Directory.IDD_DIR": (
                570,
                "validated-native-idd-path-resolution",
                "constants-metadata-570-1f0c2815",
                "sha256:bcdb44f08ca85537eca206313be08bf31384dec386fd842bce38e96b3553d1f9",
                "sha256:ef814651c1d0e843ae53c9dd6fc834ccd2271ea975d2e132d809d644eb53cc88",
                "src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs",
                "GonieGonie.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.IddPath",
                "sha256:3b2beace10108918cfc69b06be42da966fc138e3f2fcff1c9bf39d2d5cdce84c",
            ),
            "Directory.PROFILE_DIR": (
                571,
                "typed-native-profile-data-without-package-profile-directory",
                "constants-metadata-571-f65d5eae",
                "sha256:5536cae6af137a72c7c927cc4221cd1eea35e0f541b74922275683420ec267eb",
                "sha256:0c358b1bdfba0186aaf1fbae8d5c9768b2e34c3528cf02df1b720e145eabb25d",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Profile/Profile.cs",
                "GonieGonie.InvisibleDragon.Profile.Profile",
                "sha256:99c3e0557ba737aa74cfb0f15faf0730d9f7215a6b66f7f6b6b2044cf4013c72",
            ),
            "PackageInfo": (
                572,
                "static-native-package-information",
                "constants-metadata-572-aaf5b98d",
                "sha256:a3741a5a5870ff30f6e840e266c436abda507ff96c93e974cbc68df211e28168",
                "sha256:9add953f90477a1c6294d5c2eba362c862fa6f0aef2bd1b25c4f598ac5d3910f",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
            "PackageInfo.NAME": (
                573,
                "native-invisibledragon-package-name",
                "constants-metadata-573-3942a963",
                "sha256:1539ac6658af20566740884fc3f1d4802e99a0a96b9760a06d69f0f37420804b",
                "sha256:ce337d684f985b7fe8402969f81e473a06ad4a872a1fb0acf66469ddf6f58d73",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo.Name",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
            "PackageInfo.REQUIRED_PYTHON": (
                574,
                "compiled-native-target-framework-contract",
                "constants-metadata-574-cf74d0eb",
                "sha256:c666b0f9e6499a34acc0a98f3f01584e73add9a9d64dd3e66842e79012714aad",
                "sha256:d16c7e2050430c23a42db97ee0e60db6e4b9d34667a037539879581bdbeaf7aa",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
            "PackageInfo.VERSION": (
                575,
                "native-semantic-version-string",
                "constants-metadata-575-a8260e5f",
                "sha256:9ab52755f1c9d6600068d446d4df9420aa0c37df3a365668dc241334f5ca63d7",
                "sha256:450d23845ac4b76a1a1b9b129d52ef1fc8f824d22eb94a17f43126bb22caab00",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo.Version",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
        }
        constants_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Common/"
            "ConstantsMetadataOracleParityTests.cs"
        )
        constants_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Common.ConstantsMetadataOracleParityTests."
            "MatchesPinnedConstantsMetadataThroughBoundedNativeAdaptations"
        )
        constants_test_hash = (
            "sha256:fe0809967c5fcc94c70e1805a215e670d709dbf72d3ba1888d78b5bd55e404ef"
        )
        exceptions_by_id = {
            item.identifier: item for item in configuration.exceptions
        }
        for symbol, (
            index,
            exception_id,
            assertion_id,
            direct_hash,
            collector_hash,
            implementation_path,
            implementation_symbol,
            implementation_hash,
        ) in expected_constants_metadata.items():
            key = ("src/idragon/constants.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            exception = exceptions_by_id[exception_id]
            self.assertEqual(key, (exception.upstream_path, exception.upstream_symbol))
            self.assertEqual(
                compatibility.inventory.symbols[index].symbol_hash,
                exception.upstream_symbol_hash,
                symbol,
            )
            self.assertIn(("engineering_result", entry.rationale), exception.effects)
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(implementation_path, evidence_entry.implementation_path, symbol)
            self.assertEqual(implementation_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                implementation_hash,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_hash, receipt.assertion, symbol)
            self.assertEqual(collector_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(constants_test_path, receipt.test_path, symbol)
            self.assertEqual(constants_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(constants_test_hash, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
        self.assertEqual(8, len(expected_constants_metadata))
        for index, symbol in enumerate(
            (
                "SpecialTag",
                "SpecialTag.__format__",
                "SpecialTag.__repr__",
                "SpecialTag.__str__",
            ),
            start=576,
        ):
            key = ("src/idragon/constants.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual("out_of_scope", by_key[key].classification, symbol)
            self.assertTrue(
                by_key[key].evidence[0].startswith("upstream/scope-decisions.json#"),
                symbol,
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key, symbol)
        for index, symbol in enumerate(
            (
                "Directory",
                "Directory.CONSTRUCTION_DIR",
                "Directory.PROFILE_DIR",
                "Directory.WEATHER_DATA_DIR",
                "Directory.WEATHER_META_DIR",
                "PackageInfo",
                "PackageInfo.NAME",
                "PackageInfo.REQUIRED_PYTHON",
                "PackageInfo.VERSION",
            ),
            start=31,
        ):
            key = ("src/epsimple/constants.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual("needs_reverification", by_key[key].classification, symbol)
            self.assertNotIn(key, symbol_evidence.entries_by_key, symbol)
        expected_common_core = {
            "Setting": ("equivalent", None),
            "Setting.DEFAULT_EP_VERSION": ("equivalent", None),
            "Setting.DEFAULT_YEAR": ("equivalent", None),
            "Version": ("exception", "native-energyplus-version-descriptor"),
            "Version.__format__": ("equivalent", None),
            "Version.__init__": (
                "exception",
                "validated-energyplus-version-construction",
            ),
            "Version.__iter__": ("equivalent", None),
            "Version.ep_dirname": ("equivalent", None),
            "Version.iddname": ("equivalent", None),
            "Version.major": ("equivalent", None),
            "Version.minor": ("equivalent", None),
            "Version.patch": ("equivalent", None),
            "Version.to_version_anyway": (
                "exception",
                "strongly-typed-energyplus-version-coercion",
            ),
        }
        for symbol, (classification, exception_id) in expected_common_core.items():
            entry = by_key[("src/idragon/common.py", symbol)]
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
        expected_launcher_results = {
            "EnergyPlusResult": "immutable-structured-energyplus-result",
            "EnergyPlusResult.__init__": "validated-energyplus-result-file-loading",
            "EnergyPlusResult.parse_audit": "ordered-typed-energyplus-audit-parsing",
            "EnergyPlusResult.parse_bnd": "csv-aware-energyplus-boundary-parsing",
            "EnergyPlusResult.parse_err": "structured-energyplus-error-log-parsing",
            "EnergyPlusResult.parse_eso": "explicitly-unsupported-energyplus-eso",
            "EnergyPlusResult.parse_table": "typed-energyplus-tabular-parsing",
        }
        for symbol, exception_id in expected_launcher_results.items():
            entry = by_key[("src/idragon/launcher.py", symbol)]
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
        terminal_scope = {
            (
                "src/epsimple/core/model.py",
                "GreenRetrofitModel.from_excel",
            ): "scope-src-epsimple-core-model-py-greenretrofitmodel-from-excel-46935cc1",
            (
                "src/idragon/constants.py",
                "SpecialTag",
            ): "scope-src-idragon-constants-py-specialtag-3a4b3781",
        }
        for key, decision_id in terminal_scope.items():
            self.assertEqual("out_of_scope", by_key[key].classification)
            self.assertEqual(
                (f"upstream/scope-decisions.json#{decision_id}",),
                by_key[key].evidence,
            )
        self.assertEqual(
            "needs_reverification",
            by_key[("src/idragon/imugi.py", "IdfObjectList.set_wwr")].classification,
        )
        people_activity = by_key[
            (
                "src/idragon/dragon/model.py",
                "EnergyModel.create_default_idf",
            )
        ]
        self.assertEqual("equivalent", people_activity.classification)
        self.assertIsNone(people_activity.exception_id)
        self.assertEqual(
            (
                "upstream/symbol-evidence.json#dragon-model-construction-defaults-create-default-idf-585b5368",
            ),
            people_activity.evidence,
        )
        add_supply_system = by_key[
            (
                "src/idragon/dragon/model.py",
                "EnergyModel.add_supply_system",
            )
        ]
        self.assertEqual("exception", add_supply_system.classification)
        self.assertEqual(
            "model-context-supply-system-assembly",
            add_supply_system.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#model-context-supply-system-assembly",
                "upstream/symbol-evidence.json#dragon-model-add-supply-system-174532d0",
            ),
            add_supply_system.evidence,
        )
        photovoltaic_to_idf_key = (
            "src/idragon/dragon/hvac.py",
            "PhotoVoltaicPanel.to_idf_object",
        )
        photovoltaic_to_idf = by_key[photovoltaic_to_idf_key]
        self.assertEqual(
            photovoltaic_to_idf_key,
            compatibility.inventory.symbols[761].key,
        )
        self.assertEqual(photovoltaic_to_idf, compatibility.matrix.entries[761])
        self.assertEqual("exception", photovoltaic_to_idf.classification)
        self.assertEqual(
            "compact-native-photovoltaic-idf-emission",
            photovoltaic_to_idf.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#"
                "compact-native-photovoltaic-idf-emission",
                "upstream/symbol-evidence.json#"
                "dragon-hvac-photovoltaic-to-idf-object-4723273d",
            ),
            photovoltaic_to_idf.evidence,
        )
        expected_supply_group_core = {
            "SupplyGroup.__init__": (
                "exception",
                "immutable-validated-supply-group-construction",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "immutable-validated-supply-group-construction",
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-init-02b3c43a",
                ),
            ),
            "SupplyGroup.coolable": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-coolable-0f6f3f1a",
                ),
            ),
            "SupplyGroup.cooling_systems": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-cooling-systems-e2ee9492",
                ),
            ),
            "SupplyGroup.heatable": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-heatable-ab11abdd",
                ),
            ),
            "SupplyGroup.heating_systems": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-heating-systems-1fdfba66",
                ),
            ),
            "SupplyGroup.sources": (
                "exception",
                "stable-entity-id-supply-source-deduplication",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "stable-entity-id-supply-source-deduplication",
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-sources-482d0fa2",
                ),
            ),
        }
        for symbol, (classification, exception_id, evidence) in (
            expected_supply_group_core.items()
        ):
            entry = by_key[("src/idragon/dragon/hvac.py", symbol)]
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(evidence, entry.evidence, symbol)
        self.assertEqual(
            "needs_reverification",
            by_key[("src/idragon/dragon/hvac.py", "SupplyGroup")].classification,
        )
        supply_group_to_idf = by_key[
            ("src/idragon/dragon/hvac.py", "SupplyGroup.to_idf_object")
        ]
        self.assertEqual("exception", supply_group_to_idf.classification)
        self.assertEqual(
            "model-context-supply-group-idf-assembly",
            supply_group_to_idf.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#"
                "model-context-supply-group-idf-assembly",
                "upstream/symbol-evidence.json#"
                "dragon-hvac-supply-group-to-idf-object-3f9c508c",
            ),
            supply_group_to_idf.evidence,
        )
        expected_shading_material = {
            "Blind.to_idf_object": (
                "model-context-blind-shading-material-emission",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "model-context-blind-shading-material-emission",
                    "upstream/symbol-evidence.json#"
                    "dragon-shape-blind-to-idf-object-16e27412",
                ),
            ),
            "Shade.to_idf_object": (
                "model-context-shade-shading-material-emission",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "model-context-shade-shading-material-emission",
                    "upstream/symbol-evidence.json#"
                    "dragon-shape-shade-to-idf-object-75e6c8e6",
                ),
            ),
        }
        for symbol, (exception_id, evidence) in expected_shading_material.items():
            entry = by_key[("src/idragon/dragon/shape.py", symbol)]
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(evidence, entry.evidence, symbol)
        energy_model_to_idf_key = (
            "src/idragon/dragon/model.py",
            "EnergyModel.to_idf",
        )
        energy_model_to_idf = by_key[energy_model_to_idf_key]
        self.assertEqual(energy_model_to_idf_key, compatibility.inventory.symbols[821].key)
        self.assertEqual(energy_model_to_idf, compatibility.matrix.entries[821])
        self.assertEqual("exception", energy_model_to_idf.classification)
        self.assertEqual(
            "validated-fresh-energy-model-idf-assembly",
            energy_model_to_idf.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#"
                "validated-fresh-energy-model-idf-assembly",
                "upstream/symbol-evidence.json#"
                "dragon-model-energy-model-to-idf-de10251f",
            ),
            energy_model_to_idf.evidence,
        )

    def test_rejects_non_goniegonie_product_ownership(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                project="OtherCompany.Product.Core",
            )

            with self.assertRaisesRegex(ConfigurationError, "GonieGonie"):
                load_configuration(lock, port_map, exceptions)

    def test_rejects_port_path_outside_locked_modules(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                mapping_path="outside/service.py",
            )

            with self.assertRaisesRegex(ConfigurationError, "outside every locked module"):
                load_configuration(lock, port_map, exceptions)

    def test_yaml_subset_rejects_anchors_and_duplicate_keys(self) -> None:
        self.assertEqual([], parse_yaml_subset("[]\n", source_name="test.yml"))
        with self.assertRaisesRegex(ConfigurationError, "anchors"):
            parse_yaml_subset("value: &shared text\n", source_name="test.yml")
        with self.assertRaisesRegex(ConfigurationError, "duplicate key"):
            parse_yaml_subset("value: first\nvalue: second\n", source_name="test.yml")


if __name__ == "__main__":
    unittest.main()
