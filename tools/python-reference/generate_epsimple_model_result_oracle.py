"""Generate the pinned EPlusSimple ``GreenRetrofitResult`` behavior oracle.

Only public-symbol inventory indices 373 through 386 are promoted by this
corpus.  Every observation executes the upstream Python 0.7.0 result class;
the separately pinned SimpleDragon routes document where the native port is
directly equivalent and where it intentionally changes the boundary.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
from copy import deepcopy
import hashlib
import importlib
import importlib.metadata
import importlib.util
import inspect
import json
import math
import os
from pathlib import Path
import re
import shutil
import struct
import sys
import tempfile
from types import SimpleNamespace
from typing import Any, Callable, Iterator


SCHEMA = "goniegonie.python-reference.epsimple-model-result.v1"
SOURCE_PATH = "src/epsimple/core/model.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 36_949
EXPECTED_SOURCE_SHA256 = (
    "sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447"
)
EXPECTED_ADJACENT_RECEIPTS_SHA256 = (
    "sha256:96babe847ec683f6d00c65cedafe8d7030673247389323fb879ef650531bfb1f"
)

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_PLATFORM = "win32"
REQUIRED_POINTER_WIDTH_BITS = 64
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
EXPECTED_DEPENDENCIES = {
    "eppy": "0.5.63",
    "numpy": "2.3.1",
    "pandas": "2.3.0",
    "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2",
    "six": "1.16.0",
    "tzdata": "2024.2",
}

SUPPORT_PATH = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
EXPECTED_SUPPORT_BYTES = 21_114
EXPECTED_SUPPORT_SHA256 = (
    "sha256:4d2dd8d0c487af7a24f93f1e79b9b27ed19676cf7909a8039d90248fd7d6e1bc"
)
BOOTSTRAP_PATH = Path(__file__).resolve().with_name("bootstrap_reference.py")
EXPECTED_BOOTSTRAP_BYTES = 1_232
EXPECTED_BOOTSTRAP_SHA256 = (
    "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f"
)


def _load_support() -> Any:
    if SUPPORT_PATH.stat().st_size != EXPECTED_SUPPORT_BYTES:
        raise RuntimeError("Strict JSON support byte length drifted.")
    spec = importlib.util.spec_from_file_location(
        "_goniegonie_epsimple_model_result_support", SUPPORT_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load strict JSON support: {SUPPORT_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if module.sha256_file(SUPPORT_PATH) != EXPECTED_SUPPORT_SHA256:
        raise RuntimeError("Strict JSON support hash drifted.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
load_json_without_duplicates = SUPPORT.load_json_without_duplicates

WEATHER_RESOURCES = (
    {
        "bytes": 16_318,
        "path": "epsimple/_data/weather/\uae30\ud6c4\uc9c0\uc5ed.csv",
        "sha256": "sha256:a6949a4b3bc967aefc419f64b1da2b7180fd33a333fed0951560951831614c06",
    },
    {
        "bytes": 38_455,
        "path": "epsimple/_data/weather/\ud589\uc815\uad6c\uc5ed\ubcc4\uae30\uc0c1\ub370\uc774\ud130.csv",
        "sha256": "sha256:ec667eeb0ade076272d23f89956add7b0f0ec7eeac6106c02a1c9c4888aa788e",
    },
)

NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 23_665,
        "path": "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitCsvExporter.cs",
        "sha256": "sha256:533ee8789aa9e02951216416be168b43c0ad7c20fc8da0c256a72650806fc32f",
    },
    {
        "bytes": 17_506,
        "path": "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitResultBuilder.cs",
        "sha256": "sha256:9a9f1bc3c38814776c3c0ac888423418215c42bb7c270848b72b480751438b3b",
    },
    {
        "bytes": 19_280,
        "path": "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GreenRetrofitResultModels.cs",
        "sha256": "sha256:5181cc98bb9e193cae2c6c29b33ca74d6e98bf7e44f11e0e3855d9f591f4e8f7",
    },
    {
        "bytes": 14_845,
        "path": "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GrrReader.cs",
        "sha256": "sha256:498b12addde1cfc0c4e6c3931dd5c079e185cc2f45a9fa2cb5cde700f4075130",
    },
    {
        "bytes": 5_023,
        "path": "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GrrWriter.cs",
        "sha256": "sha256:802f6fb7592f1d48504f6d26b50a5d29e0e5305d5379265effb9efe080d5e65a",
    },
)

_TARGET_ROWS = (
    (373, "GreenRetrofitResult", "class", "8b4073860c0a5ec5215658188d0e02cbdd83c2e792e35fc1de93180d2b76e2e0", "ad17da15ebe3f9a8b13f618e3a7d4d8a5d867b8573aab129f9bc0758c0449792", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (374, "GreenRetrofitResult.VALID_DIGITS", "constant", "ff1cddacd1d221d604e80997d48ef03662bbeb531c45337abde8fcc3f9fc30df", "aa336779f69a8902021215ad36bc8925e1d599b84b1c2149a383d3313065b1a2", "ddcc9e26678f237b5f7892c086072a5962980b4d4b13bcee47bd9c0d98a52cc6"),
    (375, "GreenRetrofitResult.__init__", "function", "856dd66b378dc69ca9fdf702af477ca308850afa30e1f79ddaf07c77007d2143", "e3ea637489f15196a395d06b8784e4240a686044f045de1addec871f7ee124b0", "7d8dee39517322f67931eb9ae4eeab47423ca33acb4bd9d48732687b11009213"),
    (376, "GreenRetrofitResult.area", "function", "37a89b1c8b8b29e09038b198162ad3edfe11206794c9b30e104febcdce483f89", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "7335d117f821d4cc789535e20d1f1cb563895a2e27b6fcdbe9c5bf3a1978d037"),
    (377, "GreenRetrofitResult.calc_domestic_hotwater_site_energy", "function", "4e80e0ef21caa93b8a0d7450676b1173677faec1ac8f3d15ad550f290b920c4c", "01ce55e2ae511cb78ed4504c328bc6d4e06786c1bbe7157feb8bd6958d2a5ede", "3d20f42d58aa292c0cda8f36c2c29aba9fcb94cb3a65fe138eed6a7d40fcb26d"),
    (378, "GreenRetrofitResult.get_dhw_servers", "function", "a63f6fa21523147d50860abe9915f96111ca6ace3621e57716040c9f8cc22ff3", "d2b4c877c3074459e858c8ddab98b4b507ad32ac856cab0c0358b2ff4487fce6", "757d1859c51226b31facdfb68107b5a90ce8e7c8d260e6ccb327e31f9203183c"),
    (379, "GreenRetrofitResult.get_domestic_hotwater_energy", "function", "b7774317313c4c32bb28168900a4ccd0af9162b9e9149f7bb58f5605784ed592", "c2d47451050e60f15a22d16146acba292a2a641fff5670ab1cec00ba7f863d58", "d43efb9ead93c11dacb01c2a869c6801e637018483c9524b390640381d1e0eb8"),
    (380, "GreenRetrofitResult.summarize", "function", "93d2bbd846d5cf13baf88fcbacddc16e948ca205b53c7e4f25fd5887dcdc3f87", "808df99bb5631c7829bf7bce92d37533bbddbb2e35281ff3add1b89d35acbab7", "c2c71105186ffc370ee09c436ac894ee6bf797989dd622d36f302633f6009b6e"),
    (381, "GreenRetrofitResult.to_co2", "function", "72b97e85ef6741a8eb2dfcdb37de2a27b37772b2ec054fee14a061d3a3f2d358", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "73699d8b52634390a3efab78dceae92be86d304fb90fbc8acc4c6092b0a2f0e6"),
    (382, "GreenRetrofitResult.to_cost", "function", "7d1d1cd964d4ab0842510bf94bac7aea393ed53469ed7ecdea1d7979057bf266", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "3363e164857a1bc4c9f5f2e9904602b4d9912b9901888e2f5e55197c4c993f30"),
    (383, "GreenRetrofitResult.to_dict", "function", "010fb59959bd7ec395c6e22acccaeb73626df3fa276c4fb7e5ed1c3172a8f8d3", "b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "ff7f831331299a45e9c62ac55581b0c4dc6d311580a9abc84e73b53e2763324b"),
    (384, "GreenRetrofitResult.to_site_uses", "function", "48114e1462753ab48eac6ca7d648438ad7e4381d4900cdbfd7618c701562bafa", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "b8a49f1b2b0bcbaf6c27042f1b6926bdd6954194a3db29531bdd8668d4052b7f"),
    (385, "GreenRetrofitResult.to_source_uses", "function", "842eb853a7216a84eab7ccc5a04d7454fc7f2572ea9c8e0bc32f73d6ffc84291", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "d9c7d1b27a50ae9b04a5278c1d1881309fc297af097af411791f2f1d77e73d5d"),
    (386, "GreenRetrofitResult.write", "function", "67ef521c2bdac4646a52e20ba8da306765197f8cc27846cb9d715d605d21db2e", "5294543e03913904c918f3367755b0cffe7f63c47d17de87fcd55fa0a846c288", "be074b70585f464b6e6172733e6fa39c8f8d94e716eddc77260516689568c898"),
)


def _receipts(rows: tuple[tuple[Any, ...], ...]) -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "body_hash": "sha256:" + body_hash,
            "inventory_index": index,
            "kind": kind,
            "path": SOURCE_PATH,
            "signature_hash": "sha256:" + signature_hash,
            "symbol": symbol,
            "symbol_hash": "sha256:" + symbol_hash,
        }
        for index, symbol, kind, symbol_hash, signature_hash, body_hash in rows
    )


TARGET_RECEIPTS = _receipts(_TARGET_ROWS)
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
TARGET_INDICES = tuple(range(373, 387))
ADJACENT_INDICES = tuple((*range(337, 373), 387, 388))
TARGET_HASHES = {item["symbol"]: item["symbol_hash"] for item in TARGET_RECEIPTS}

EQUIVALENT_SYMBOLS = {
    "GreenRetrofitResult.VALID_DIGITS",
    "GreenRetrofitResult.area",
    "GreenRetrofitResult.get_domestic_hotwater_energy",
    "GreenRetrofitResult.summarize",
    "GreenRetrofitResult.to_co2",
    "GreenRetrofitResult.to_cost",
    "GreenRetrofitResult.to_dict",
    "GreenRetrofitResult.to_site_uses",
    "GreenRetrofitResult.to_source_uses",
}
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"epsimple-model-result-{item['inventory_index']}-{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}

NATIVE_ROUTES = {
    "GreenRetrofitResult": "GonieGonie.SimpleDragon.GreenRetrofitResult",
    "GreenRetrofitResult.VALID_DIGITS": "GonieGonie.SimpleDragon.GreenRetrofitResult.ValidDigits",
    "GreenRetrofitResult.__init__": "GreenRetrofitResult.FromSiteUses(double, EnergyUseBreakdown) and GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)",
    "GreenRetrofitResult.area": "GonieGonie.SimpleDragon.GreenRetrofitResult.TotalArea",
    "GreenRetrofitResult.calc_domestic_hotwater_site_energy": "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build domestic-hot-water projection",
    "GreenRetrofitResult.get_dhw_servers": "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build source-system filtering and grouping",
    "GreenRetrofitResult.get_domestic_hotwater_energy": "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build usage-profile domestic-hot-water demand",
    "GreenRetrofitResult.summarize": "GonieGonie.SimpleDragon.GreenRetrofitResult.PerAreaSummaries and GrossSummaries",
    "GreenRetrofitResult.to_co2": "GonieGonie.SimpleDragon.GreenRetrofitResult.Carbon",
    "GreenRetrofitResult.to_cost": "GonieGonie.SimpleDragon.GreenRetrofitResult.Cost",
    "GreenRetrofitResult.to_dict": "GonieGonie.SimpleDragon.GrrWriter.Serialize(GreenRetrofitResult, bool)",
    "GreenRetrofitResult.to_site_uses": "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build and GreenRetrofitResult.SiteUses",
    "GreenRetrofitResult.to_source_uses": "GonieGonie.SimpleDragon.GreenRetrofitResult.SourceUses",
    "GreenRetrofitResult.write": "GonieGonie.SimpleDragon.GrrWriter.WriteFile(string, GreenRetrofitResult, bool)",
}

_ADAPTATION_BASES = {
    "GreenRetrofitResult": "immutable-complete-result-tree-rather-than-model-result-wrapper",
    "GreenRetrofitResult.__init__": "validated-factory-and-diagnostic-build-result-boundary",
    "GreenRetrofitResult.calc_domestic_hotwater_site_energy": "typed-server-filtering-first-id-wins-and-structured-diagnostics",
    "GreenRetrofitResult.get_dhw_servers": "typed-boiler-district-filtering-rather-than-arbitrary-hotwater-object",
    "GreenRetrofitResult.write": "deterministic-grr-writer-with-terminal-newline",
}
ADAPTATIONS = {
    symbol: (
        "direct-native-"
        + re.sub(r"[^a-z0-9]+", "-", symbol.lower()).strip("-")
        + "-"
        + TARGET_HASHES[symbol][7:15]
        if CLASSIFICATIONS[symbol] == "equivalent"
        else "reviewed-native-adaptation-"
        + _ADAPTATION_BASES[symbol]
        + "-"
        + TARGET_HASHES[symbol][7:15]
    )
    for symbol in TARGET_SYMBOLS
}

NATIVE_ROUTE_AUDIT = {
    "from_csv": {
        "native_route": "InvisibleDragon EnergyPlus result parsing then GreenRetrofitResultBuilder.Build",
        "python_member_exists": False,
        "status": "composed-native-route",
    },
    "from_result": {
        "native_route": "GreenRetrofitResult.FromSiteUses and GreenRetrofitResultBuilder.Build",
        "python_member_exists": False,
        "status": "composed-native-route",
    },
    "from_sqlite": {
        "native_route": "No SimpleDragon public SQLite-specific constructor; structured EnergyPlusSimulationResult is the boundary",
        "python_member_exists": False,
        "status": "intentional-absence",
    },
    "to_dict": {
        "native_route": "GrrWriter.Serialize emits the pinned GRR dictionary topology",
        "python_member_exists": True,
        "status": "equivalent-output-route",
    },
    "to_json": {
        "native_route": "GrrWriter.Serialize",
        "python_member_exists": False,
        "status": "renamed-native-route",
    },
    "to_monthly_csv": {
        "native_route": "GreenRetrofitCsvExporter.SerializeMonthly",
        "python_member_exists": False,
        "status": "native-extension",
    },
    "to_monthly_json": {
        "native_route": "No monthly-only JSON route; GrrWriter.Serialize emits the complete monthly GRR tree",
        "python_member_exists": False,
        "status": "intentional-absence",
    },
    "write": {
        "native_route": "GrrWriter.WriteFile",
        "python_member_exists": True,
        "status": "adapted-native-route",
    },
}

PREFIX = "epsimple-model-result."
CASE_SPECS = (
    ("R01", "class-init-area-valid-digits", "lifecycle", ("GreenRetrofitResult", "GreenRetrofitResult.VALID_DIGITS", "GreenRetrofitResult.__init__", "GreenRetrofitResult.area"), ()),
    ("D01", "domestic-hotwater-demand-calendar", "domestic-hotwater", ("GreenRetrofitResult.get_domestic_hotwater_energy",), ("GreenRetrofitResult.area",)),
    ("D02", "domestic-hotwater-server-selection", "domestic-hotwater", ("GreenRetrofitResult.get_dhw_servers",), ("GreenRetrofitResult",)),
    ("D03", "domestic-hotwater-site-energy", "domestic-hotwater", ("GreenRetrofitResult.calc_domestic_hotwater_site_energy",), ("GreenRetrofitResult.get_domestic_hotwater_energy", "GreenRetrofitResult.get_dhw_servers")),
    ("S01", "site-use-table-pv-and-boundaries", "metric", ("GreenRetrofitResult.to_site_uses",), ("GreenRetrofitResult.calc_domestic_hotwater_site_energy",)),
    ("S02", "source-use-factors-and-enum-alias", "metric", ("GreenRetrofitResult.to_source_uses",), ("GreenRetrofitResult.to_site_uses",)),
    ("S03", "carbon-factors", "metric", ("GreenRetrofitResult.to_co2",), ("GreenRetrofitResult.to_site_uses",)),
    ("S04", "cost-factors", "metric", ("GreenRetrofitResult.to_cost",), ("GreenRetrofitResult.to_site_uses",)),
    ("S05", "summary-per-area-gross-and-shape-boundaries", "summary", ("GreenRetrofitResult.summarize",), ("GreenRetrofitResult.area",)),
    ("J01", "dictionary-tree-and-call-topology", "serialization", ("GreenRetrofitResult.to_dict",), ("GreenRetrofitResult.to_site_uses", "GreenRetrofitResult.to_source_uses", "GreenRetrofitResult.to_co2", "GreenRetrofitResult.to_cost", "GreenRetrofitResult.summarize")),
    ("J02", "write-json-bytes-overwrite-and-errors", "serialization", ("GreenRetrofitResult.write",), ("GreenRetrofitResult.to_dict",)),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 11

# Sealed after direct execution through the exact CPython 3.12.7 bootstrap.
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:93d5310b577faa8c6a19a409ee5dea4e23b5ff2aa086e5e8b42746a133dbf00f"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:998782cc65bc94d43ffc7538fae747639503f673586bc2815aaddac4dddc1fe1"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:681dcec3e9b192e373cd31e5accd673f97c2d7234d87e5394c27a70aa14a7ca8"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-model-result.carbon-factors": "sha256:0de2828a139d54ef81d706a0598c12815dd28ffc6ec1c9f0df57c9067ba46781",
    "epsimple-model-result.class-init-area-valid-digits": "sha256:585099dc1fb37b8b3d04ad9fdca922098ac1ff9942f6c30ab73137e92d94c249",
    "epsimple-model-result.cost-factors": "sha256:06cea7332d5ce760785d2aaf16cd64a49c64d1936033fb2207fc8ae9872870b1",
    "epsimple-model-result.dictionary-tree-and-call-topology": "sha256:c0800b402b6c1c7a0b44447934c3ecfdb67c3a6b5b3964fe783203270b0361e4",
    "epsimple-model-result.domestic-hotwater-demand-calendar": "sha256:724a1a94b4764ac8c8f9752f9279d2cde897922c1a92c88f337bfa8e2c24696d",
    "epsimple-model-result.domestic-hotwater-server-selection": "sha256:fe7747b329f96eac8c0206338e21c40bd2d7467d114f677b9c1228e444010b85",
    "epsimple-model-result.domestic-hotwater-site-energy": "sha256:0bd8c10ff6fe534ad259336a9e761dff32bd89c788d38e93f470a6a5d1b3b316",
    "epsimple-model-result.site-use-table-pv-and-boundaries": "sha256:5dfb907078fcde732c138a8da8dcc69d8dee9ca7e28d5e44db660c6871ece454",
    "epsimple-model-result.source-use-factors-and-enum-alias": "sha256:0632f3db7d87659a2c4aec581abbfe3aec3983caaa5bd4f7b0f1f075b1d4c485",
    "epsimple-model-result.summary-per-area-gross-and-shape-boundaries": "sha256:de1915cdea10a86c600f60ec7243528421f80d775729d1c692c876db5c33b8a7",
    "epsimple-model-result.write-json-bytes-overwrite-and-errors": "sha256:348a57163273bb5b62fc7beda1ec3b1f44f9bddcfd49f46423e38d4618766c9f",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-model-result.carbon-factors": "sha256:fc20fea5762e3fa4332ba18f67a4887e2269039ccac22d1ab873b5c95408027c",
    "epsimple-model-result.class-init-area-valid-digits": "sha256:4711bebc356a1f5750e9c376006c4c8aaccaab4801800f8b101df84a5797cec3",
    "epsimple-model-result.cost-factors": "sha256:f32e918255fd5a752c65635ce9878c7ae8fe406a2007e3d1644c37774057cf7a",
    "epsimple-model-result.dictionary-tree-and-call-topology": "sha256:c3cd6c1c1cc439050757731424eafcd774c9093035c1da2cdd0db1c42d4b3bf5",
    "epsimple-model-result.domestic-hotwater-demand-calendar": "sha256:3f013b028e16376338fb619bce6ac8a2c56b5baa91c091f7e88f9c44e6b1e580",
    "epsimple-model-result.domestic-hotwater-server-selection": "sha256:7153bb1e6c23caf60f71a43b2f0078cb3757272a4e2354f928076abdff428045",
    "epsimple-model-result.domestic-hotwater-site-energy": "sha256:55d6fa67b07f33630af20d31c0c98cd5731f06852acc6bbc9e68b025e1f4998f",
    "epsimple-model-result.site-use-table-pv-and-boundaries": "sha256:fb0b61d263fc7a1bb48ceee75e5bbf12b9a52ba1508c055c0066a2dd25961a1e",
    "epsimple-model-result.source-use-factors-and-enum-alias": "sha256:896a7ec68973d272609602938ac1d0b2b6f8e19267a7f9d14b8b64f9c8c1f745",
    "epsimple-model-result.summary-per-area-gross-and-shape-boundaries": "sha256:171a430247f305b222655aba236977355ac3ba78ebf50b5021f4cdc544d5dcfa",
    "epsimple-model-result.write-json-bytes-overwrite-and-errors": "sha256:ff638ac05baae61326c38c68c18fdd57fd44f5b6d12104dad60de9edc2ffe9bf",
}
EXPECTED_CASES_SHA256 = (
    "sha256:ac4b9647caba8c1c40edc1314936fcfaaf1cfc155e0ed51f54839094484bf3cf"
)

RAW_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,}")
WINDOWS_PATH_PATTERN = re.compile(r"(?i)(?:^|[\s=:'\"])[a-z]:[\\/]")
POSIX_PATH_PATTERN = re.compile(
    r"(?:^|[\s=:'\"])/(?:home|tmp|users|var|private|mnt|workspace)(?:/|\\)",
    re.IGNORECASE,
)
GUID_PATTERN = re.compile(
    r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b"
)
TIMESTAMP_PATTERN = re.compile(r"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "code": code,
            "context_symbols": list(context),
            "id": PREFIX + slug,
            "subfamily": subfamily,
            "target_symbols": list(targets),
        }
        for code, slug, subfamily, targets, context in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Model-result case order drifted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Model-result targets are not an exact one-case partition.")
    declared = {
        symbol
        for definition in definitions
        for symbol in (*definition["target_symbols"], *definition["context_symbols"])
    }
    if not declared.issubset(set(TARGET_SYMBOLS)):
        raise RuntimeError("Model-result context escaped the bounded target set.")
    return definitions


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    commit = upstream_commit.lower()
    if commit != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if path.stat().st_size != EXPECTED_INVENTORY_BYTES:
        raise SystemExit("The public-symbol inventory byte length drifted.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash drifted.")
    value = load_json_without_duplicates(path)
    SUPPORT.require_exact_keys(
        value,
        {
            "content_sha256",
            "files",
            "schema",
            "scope_sha256",
            "summary",
            "symbols",
            "upstream_commit",
        },
        "Public-symbol inventory",
    )
    if value["schema"] != "goniegonie.upstream-public-symbol-inventory.v2":
        raise SystemExit("The public-symbol inventory schema drifted.")
    if value["upstream_commit"].lower() != commit:
        raise SystemExit("The public-symbol inventory commit drifted.")
    aggregate = canonical_sha256(
        {
            "files": value["files"],
            "scope_sha256": value["scope_sha256"],
            "symbols": value["symbols"],
            "upstream_commit": value["upstream_commit"],
        }
    )
    if aggregate != value["content_sha256"] or aggregate != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory aggregate receipt drifted.")
    source_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if [item for item in value["files"] if item["path"] == SOURCE_PATH] != [source_file]:
        raise SystemExit("The EPlusSimple model source receipt drifted.")
    for receipt in TARGET_RECEIPTS:
        index = receipt["inventory_index"]
        if value["symbols"][index] != _descriptor(receipt):
            raise SystemExit(f"Model-result inventory receipt drifted at index {index}.")
    source_rows = [
        {**item, "inventory_index": index}
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_rows] != list(range(337, 389)):
        raise SystemExit("The model.py source declaration range drifted.")
    adjacent = [
        item for item in source_rows if item["inventory_index"] in ADJACENT_INDICES
    ]
    if canonical_sha256(adjacent) != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise SystemExit("The adjacent model.py declaration receipt drifted.")
    if sorted((*TARGET_INDICES, *ADJACENT_INDICES)) != list(range(337, 389)):
        raise RuntimeError("The target/adjacent source partition is incomplete.")
    return {
        "adjacent_receipts_sha256": canonical_sha256(adjacent),
        "content_sha256": aggregate,
        "files": value["files"],
        "source_file": source_file,
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": list(TARGET_RECEIPTS),
    }


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _validate_native_sources() -> None:
    root = _repository_root()
    for receipt in NATIVE_SOURCE_RECEIPTS:
        path = root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Reviewed native result source drifted: {receipt['path']}")


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    relative = Path(SOURCE_PATH).relative_to("src")
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        source = root / relative
        if (
            source.is_file()
            and source.stat().st_size == EXPECTED_SOURCE_BYTES
            and sha256_file(source) == EXPECTED_SOURCE_SHA256
        ):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned EPlusSimple source root must be importable.")
    return unique[0]


def _validate_source_tree(source_root: Path) -> None:
    source = source_root / Path(SOURCE_PATH).relative_to("src")
    if (
        not source.is_file()
        or source.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(source) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported EPlusSimple model source drifted.")
    for receipt in WEATHER_RESOURCES:
        path = source_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Pinned weather resource drifted: {receipt['path']}")


def _clear_local_modules() -> None:
    for name in list(sys.modules):
        if name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon.")):
            sys.modules.pop(name, None)


@contextmanager
def _isolated_import(source_root: Path) -> Iterator[Any]:
    source_root = source_root.resolve()
    _validate_source_tree(source_root)
    saved = {
        name: module
        for name, module in sys.modules.items()
        if name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon."))
    }
    _clear_local_modules()
    sys.path.insert(0, str(source_root))
    try:
        module = importlib.import_module("epsimple.core.model")
        expected = source_root / Path(SOURCE_PATH).relative_to("src")
        if Path(module.__file__).resolve() != expected:
            raise SystemExit("Imported epsimple.core.model did not resolve to pinned source.")
        yield module
    finally:
        _clear_local_modules()
        sys.modules.update(saved)
        try:
            sys.path.remove(str(source_root))
        except ValueError:
            pass


def _copy_source_tree(source_root: Path, relocated_root: Path) -> None:
    relocated_root.mkdir(parents=True)
    for package in ("epsimple", "idragon"):
        shutil.copytree(source_root / package, relocated_root / package)


def _loaded_local_modules(
    source_root: Path, inventory: dict[str, Any]
) -> list[dict[str, Any]]:
    files = {item["path"]: item for item in inventory["files"]}
    result: list[dict[str, Any]] = []
    for name, module in sorted(sys.modules.items()):
        if not (name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon."))):
            continue
        filename = getattr(module, "__file__", None)
        if not filename or Path(filename).suffix != ".py":
            continue
        path = Path(filename).resolve()
        try:
            relative = path.relative_to(source_root.resolve()).as_posix()
        except ValueError as error:
            raise RuntimeError(f"Local module {name} escaped the source root.") from error
        inventory_path = "src/" + relative
        receipt = files.get(inventory_path)
        if receipt is None or sha256_file(path) != receipt["content_hash"]:
            raise RuntimeError(f"Loaded local module receipt drifted: {name}")
        result.append(
            {
                "ast_sha256": receipt["ast_hash"],
                "bytes": path.stat().st_size,
                "module": name,
                "path": inventory_path,
                "sha256": receipt["content_hash"],
            }
        )
    return result


def _runtime_member(module: Any, symbol: str) -> Any:
    value: Any = module
    for token in symbol.split("."):
        value = inspect.getattr_static(value, token)
    return value


def _runtime_signatures(module: Any) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for symbol in TARGET_SYMBOLS:
        value = _runtime_member(module, symbol)
        if isinstance(value, property):
            result[symbol] = {
                "getter": str(inspect.signature(value.fget)),
                "setter": None if value.fset is None else str(inspect.signature(value.fset)),
                "type": "property",
            }
        elif callable(value):
            try:
                signature = str(inspect.signature(value))
            except (TypeError, ValueError):
                signature = "unavailable"
            result[symbol] = {"signature": signature, "type": type(value).__name__}
        else:
            result[symbol] = {
                "type": f"{type(value).__module__}.{type(value).__name__}",
                "value": str(value),
            }
    return result


def _dependencies() -> dict[str, str]:
    return {name: importlib.metadata.version(name) for name in EXPECTED_DEPENDENCIES}


def _runtime_receipt() -> dict[str, Any]:
    dependencies = _dependencies()
    return {
        "dependencies": dependencies,
        "dependencies_sha256": canonical_sha256(dependencies),
        "implementation": sys.implementation.name,
        "platform": sys.platform,
        "pointer_width_bits": struct.calcsize("P") * 8,
        "python_hash_algorithm": sys.hash_info.algorithm,
        "python_hash_seed": 0,
        "python_hash_width_bits": sys.hash_info.width,
        "python_version": ".".join(map(str, sys.version_info[:3])),
    }


def _validate_generation_runtime() -> None:
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for model-result generation.")
    if sys.platform != REQUIRED_PLATFORM or struct.calcsize("P") * 8 != REQUIRED_POINTER_WIDTH_BITS:
        raise SystemExit("The pinned 64-bit Windows Python runtime is required.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    if (
        BOOTSTRAP_PATH.stat().st_size != EXPECTED_BOOTSTRAP_BYTES
        or sha256_file(BOOTSTRAP_PATH) != EXPECTED_BOOTSTRAP_SHA256
    ):
        raise SystemExit("The Python reference bootstrap receipt drifted.")
    _validate_native_sources()


def _number(value: int | float | bool) -> dict[str, Any]:
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, int):
        return {"kind": "int", "value": str(value)}
    value = float(value)
    if math.isfinite(value):
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    return {
        "kind": "float-nonfinite",
        "value": "nan"
        if math.isnan(value)
        else ("positive-infinity" if value > 0 else "negative-infinity"),
    }


def _normalise(value: Any) -> Any:
    if value is None or isinstance(value, str):
        return value
    if isinstance(value, bool):
        return _number(value)
    if isinstance(value, int):
        return _number(value)
    if isinstance(value, float):
        return _number(value)
    if hasattr(value, "item") and callable(value.item):
        try:
            scalar = value.item()
        except (TypeError, ValueError):
            scalar = value
        if scalar is not value:
            return _normalise(scalar)
    if isinstance(value, dict):
        return {str(key): _normalise(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [_normalise(item) for item in value]
    if hasattr(value, "columns") and hasattr(value, "index") and hasattr(value, "iloc"):
        return {
            "columns": [str(item) for item in value.columns],
            "data": [
                [_normalise(value.iloc[row, column]) for column in range(len(value.columns))]
                for row in range(len(value.index))
            ],
            "index": [str(item) for item in value.index],
        }
    raise RuntimeError(f"Unsupported observation value: {type(value).__name__}")


def _exception(
    operation: Callable[[], Any], *, include_message: bool = True
) -> dict[str, Any]:
    try:
        operation()
    except BaseException as error:  # noqa: BLE001 - exact Python boundary is evidence.
        result = {"outcome": "raised", "type": type(error).__name__}
        if include_message:
            result["message_sha256"] = (
                "sha256:" + hashlib.sha256(str(error).encode("utf-8")).hexdigest()
            )
        return result
    return {"outcome": "returned"}


def _new_result(
    module: Any,
    *,
    area: float = 100.0,
    zones: list[Any] | None = None,
    sources: list[Any] | None = None,
    tables: dict[str, Any] | None = None,
) -> Any:
    model = SimpleNamespace(
        area=area,
        source_system=[] if sources is None else sources,
        zone=[] if zones is None else zones,
    )
    result = SimpleNamespace(tbl={} if tables is None else tables, err=None)
    return module.GreenRetrofitResult(model, result)


def _lifecycle_facts(module: Any) -> dict[str, Any]:
    model = SimpleNamespace(area=123.45, source_system=[], zone=[])
    raw_result = SimpleNamespace(tbl={}, err=None)
    wrapped = module.GreenRetrofitResult(model, raw_result)
    error_frame = module.pd.DataFrame(
        [{"type": "Severe", "title": "bounded-result-severe"}]
    )
    area_probes = []
    for value in (0.0, -1.0, math.nan, math.inf):
        probe = _new_result(module, area=value)
        area_probes.append({"input": _number(value), "observed": _number(probe.area)})
    requested_names = (
        "from_csv",
        "from_result",
        "from_sqlite",
        "to_json",
        "to_monthly_csv",
        "to_monthly_json",
    )
    return {
        "area_probes": area_probes,
        "area_setter": _exception(lambda: setattr(wrapped, "area", 1.0)),
        "bases": [base.__name__ for base in module.GreenRetrofitResult.__bases__],
        "class_module": module.GreenRetrofitResult.__module__,
        "empty_table_is_accepted": wrapped.result.tbl == {},
        "missing_tbl": _exception(
            lambda: module.GreenRetrofitResult(model, SimpleNamespace(err=None))
        ),
        "model_identity_retained": wrapped.model is model,
        "requested_route_member_presence": {
            name: hasattr(module.GreenRetrofitResult, name) for name in requested_names
        },
        "result_identity_retained": wrapped.result is raw_result,
        "tbl_none": _exception(
            lambda: module.GreenRetrofitResult(
                model, SimpleNamespace(tbl=None, err=error_frame)
            )
        ),
        "valid_digits": _number(module.GreenRetrofitResult.VALID_DIGITS),
        "valid_digits_type": type(module.GreenRetrofitResult.VALID_DIGITS).__name__,
    }


def _profile(domestic_hotwater: Any, positive: list[bool]) -> Any:
    dayschedules = [SimpleNamespace(has_positive=value) for value in positive]

    class Profile:
        def __init__(self) -> None:
            self.domestic_hotwater = domestic_hotwater
            self.calls = 0

        def _get_occupied_mask(self) -> Any:
            self.calls += 1
            return SimpleNamespace(dayschedules=dayschedules)

    return Profile()


def _demand_facts(module: Any) -> dict[str, Any]:
    dates = list(module.dragon.Schedule.TIME_TUPLE)
    if len(dates) != 365:
        raise RuntimeError("Pinned Dragon schedule calendar length drifted.")
    profile_a = _profile(
        50.0,
        [
            (item.month, item.day) in {(1, 1), (1, 31), (2, 1), (12, 31)}
            for item in dates
        ],
    )
    profile_b = _profile(
        25.0,
        [item.day == 1 and item.month in {1, 6, 12} for item in dates],
    )
    zones = [
        SimpleNamespace(area=100.0, profile=profile_a),
        SimpleNamespace(area=40.0, profile=profile_b),
    ]
    wrapped = _new_result(module, area=140.0, zones=zones)
    energy = wrapped.get_domestic_hotwater_energy()

    truncated_profile = _profile(10.0, [True, False, True])
    truncated = _new_result(
        module,
        zones=[SimpleNamespace(area=100.0, profile=truncated_profile)],
    ).get_domestic_hotwater_energy()
    invalid_profile = _profile("bad", [True] + [False] * 364)
    invalid = _new_result(
        module,
        zones=[SimpleNamespace(area=10.0, profile=invalid_profile)],
    )
    return {
        "calendar": {
            "days": _number(len(dates)),
            "first": dates[0].isoformat(),
            "last": dates[-1].isoformat(),
        },
        "energy": _normalise(energy),
        "invalid_domestic_hotwater": _exception(
            invalid.get_domestic_hotwater_energy
        ),
        "profile_call_counts": [_number(profile_a.calls), _number(profile_b.calls)],
        "truncated_dayschedule_energy": _normalise(truncated),
        "zone_without_profile": _exception(
            _new_result(module, zones=[SimpleNamespace(area=1.0)]).get_domestic_hotwater_energy
        ),
    }


def _boiler(
    module: Any,
    identifier: str,
    *,
    fuel: Any | None = None,
    hotwater: bool = True,
    efficiency: float = 0.8,
) -> Any:
    return module.Boiler(
        "Boiler " + identifier,
        module.Fuel.NATURALGAS if fuel is None else fuel,
        hotwater,
        efficiency,
        None,
        ID=identifier,
    )


def _district(module: Any, identifier: str, *, hotwater: bool = True) -> Any:
    return module.DistrictHeating(
        "District " + identifier, hotwater, ID=identifier
    )


def _server_snapshot(server: Any) -> dict[str, Any]:
    return {
        "efficiency": _normalise(getattr(server, "efficiency", None)),
        "fuel": getattr(server, "fuel", None),
        "hotwater_supply": _normalise(getattr(server, "hotwater_supply", None)),
        "id": getattr(server, "ID", "auto-id-not-recorded"),
        "type": type(server).__name__,
    }


def _server_facts(module: Any) -> dict[str, Any]:
    empty = _new_result(module)
    fallback = empty.get_dhw_servers()
    first = _boiler(module, "DUPLICATE", efficiency=0.7)
    last = _boiler(module, "DUPLICATE", efficiency=0.9)
    district = _district(module, "DISTRICT")
    ignored = _boiler(module, "IGNORED", hotwater=False)
    unsupported = SimpleNamespace(
        ID="UNSUPPORTED", hotwater_supply=True, marker="unsupported"
    )
    wrapped = _new_result(
        module,
        sources=[ignored, first, district, last, unsupported],
    )
    selected = wrapped.get_dhw_servers()
    missing_id = _new_result(
        module, sources=[SimpleNamespace(hotwater_supply=True)]
    )
    return {
        "duplicate_last_write_wins": selected[0] is last,
        "fallback": {
            "count": _number(len(fallback)),
            "id_has_generated_source_prefix": fallback[0].ID.startswith(
                "SRCE-AUTOID0x"
            ),
            "snapshot_without_unstable_id": {
                key: value
                for key, value in _server_snapshot(fallback[0]).items()
                if key != "id"
            },
        },
        "missing_id": _exception(missing_id.get_dhw_servers),
        "selected": [_server_snapshot(item) for item in selected],
        "unsupported_hotwater_object_is_selected": any(
            item is unsupported for item in selected
        ),
    }


def _calculation_result(
    module: Any, demand: list[float], servers: list[Any], *, area: float = 100.0
) -> Any:
    wrapped = _new_result(module, area=area)
    wrapped.get_domestic_hotwater_energy = lambda: list(demand)
    wrapped.get_dhw_servers = lambda: list(servers)
    return wrapped


def _dhw_calculation_facts(module: Any) -> dict[str, Any]:
    demand = [80.0 + (month * 8.0) for month in range(12)]
    boiler = _boiler(module, "BOILER", efficiency=0.8)
    district = _district(module, "DISTRICT")
    mixed = _calculation_result(module, demand, [boiler, district])
    sequential = _calculation_result(
        module,
        [1.0] * 12,
        [
            _boiler(module, "ROUND-A", efficiency=0.3),
            _boiler(module, "ROUND-B", efficiency=0.3),
        ],
        area=3.0,
    )
    zero_area = _calculation_result(module, demand, [boiler], area=0.0)
    zero_efficiency = _boiler(module, "ZERO", efficiency=0.8)
    zero_efficiency._Boiler__efficiency = 0.0
    zero_eff = _calculation_result(module, demand, [zero_efficiency])
    unsupported = _calculation_result(
        module,
        demand,
        [SimpleNamespace(ID="UNSUPPORTED", hotwater_supply=True)],
    )
    no_servers = _calculation_result(module, demand, [])
    return {
        "mixed_boiler_district": _normalise(
            mixed.calc_domestic_hotwater_site_energy()
        ),
        "no_servers": _exception(no_servers.calc_domestic_hotwater_site_energy),
        "sequential_rounding": _normalise(
            sequential.calc_domestic_hotwater_site_energy()
        ),
        "unsupported_server": _exception(
            unsupported.calc_domestic_hotwater_site_energy
        ),
        "zero_area": _exception(zero_area.calc_domestic_hotwater_site_energy),
        "zero_efficiency": _exception(
            zero_eff.calc_domestic_hotwater_site_energy
        ),
    }


def _site_tables(module: Any) -> dict[str, Any]:
    count = 14
    electricity = module.pd.DataFrame(
        {
            "HEATING [kWh]": [100.5 + index for index in range(count)],
            "COOLING [kWh]": [40.25 + index * 0.5 for index in range(count)],
            "INTERIORLIGHTS [kWh]": [10.0 + index for index in range(count)],
            "EXTERIORLIGHTS [kWh]": [2.0 + index * 0.25 for index in range(count)],
            "INTERIOREQUIPMENT [kWh]": [30.0 + index for index in range(count)],
            "FANS [kWh]": [5.0 + index * 0.1 for index in range(count)],
            "PUMPS [kWh]": [3.0 + index * 0.2 for index in range(count)],
            "HEATRECOVERY [kWh]": [1.0 + index * 0.3 for index in range(count)],
            "IGNORED [kWh]": [999.0] * count,
        }
    )
    natural_gas = module.pd.DataFrame(
        {
            "HEATING [kWh]": [50.0 + index for index in range(12)],
            "WATERSYSTEMS [kWh]": [777.0] * 12,
        }
    )
    balance = module.pd.DataFrame(
        {
            "ELECTRICITYPRODUCED:FACILITY [kWh]": [
                100.0 + index * 10.0 for index in range(12)
            ],
            "ELECTRICITYSURPLUSSOLD:FACILITY [kWh]": [
                20.0 if index % 2 == 0 else 500.0 for index in range(12)
            ],
        }
    )
    return {
        "EndUseEnergyConsumptionElectricityMonthly": electricity,
        "EndUseEnergyConsumptionNaturalGasMonthly": natural_gas,
        "ELECTRICITYBALANCEMONTHLY": balance,
    }


def _site_use_facts(module: Any) -> dict[str, Any]:
    tables = _site_tables(module)
    wrapped = _new_result(module, area=100.0, tables=tables)
    frame = wrapped.to_site_uses()
    short_table = module.pd.DataFrame({"HEATING [kWh]": [10.0, 20.0]})
    short = _new_result(
        module,
        area=10.0,
        tables={"EndUseEnergyConsumptionElectricityMonthly": short_table},
    ).to_site_uses()
    malformed_balance = module.pd.DataFrame(
        {"ELECTRICITYPRODUCED:FACILITY [kWh]": [1.0] * 12}
    )
    malformed = _new_result(
        module,
        tables={"ELECTRICITYBALANCEMONTHLY": malformed_balance},
    )
    zero_area_table = module.pd.DataFrame({"HEATING [kWh]": [1.0] * 12})
    zero_area = _new_result(
        module,
        area=0.0,
        tables={"EndUseEnergyConsumptionElectricityMonthly": zero_area_table},
    )
    return {
        "balance_columns_mutated": list(
            tables["ELECTRICITYBALANCEMONTHLY"].columns
        ),
        "full_frame": _normalise(frame),
        "malformed_balance": _exception(malformed.to_site_uses),
        "short_table_frame": _normalise(short),
        "water_system_table_is_overwritten": all(
            value == 0.0 for value in frame.loc["NATURALGAS", "hotwater"]
        ),
        "zero_area": _exception(zero_area.to_site_uses),
    }


def _matrix_dataframe(module: Any) -> Any:
    fuels = [item.name for item in module.Fuel]
    uses = (
        "heating",
        "cooling",
        "lighting",
        "equipment",
        "circulation",
        "hotwater",
        "generators",
    )
    frame = module.pd.DataFrame(index=fuels, columns=uses)
    for row, fuel in enumerate(fuels):
        for column, use in enumerate(uses):
            frame.loc[fuel, use] = [
                ((row + 1) * 100.0) + ((column + 1) * 10.0) + month + 0.25
                for month in range(12)
            ]
    return frame


def _factor_facts(module: Any, method_name: str, enum_name: str) -> dict[str, Any]:
    site = _matrix_dataframe(module)
    wrapped = _new_result(module, area=321.0)
    calls = 0

    def observed_site() -> Any:
        nonlocal calls
        calls += 1
        return deepcopy(site)

    wrapped.to_site_uses = observed_site
    converted = getattr(wrapped, method_name)()
    enum_values = [item.value for item in getattr(module, enum_name)]
    return {
        "converted": _normalise(converted),
        "declared_fuel_rows": [item.name for item in module.Fuel],
        "enum_iteration_value_count": _number(len(enum_values)),
        "enum_iteration_values": _normalise(enum_values),
        "input_unchanged": _normalise(site) == _normalise(_matrix_dataframe(module)),
        "site_call_count": _number(calls),
    }


def _summary_facts(module: Any) -> dict[str, Any]:
    frame = _matrix_dataframe(module)
    wrapped = _new_result(module, area=12.5)
    ragged = deepcopy(frame)
    ragged.iloc[0, 0] = ragged.iloc[0, 0][:-1]
    empty = frame.iloc[0:0].copy()
    missing = frame.drop(columns=["generators"])
    negative_area = _new_result(module, area=-2.0)
    return {
        "empty_rows": _exception(lambda: wrapped.summarize(empty, gross=False)),
        "gross": _normalise(wrapped.summarize(frame, gross=True)),
        "missing_generators": _exception(
            lambda: wrapped.summarize(missing, gross=False)
        ),
        "negative_area_gross": _normalise(
            negative_area.summarize(frame, gross=True)
        ),
        "per_area": _normalise(wrapped.summarize(frame, gross=False)),
        "ragged_monthly_truncation": _normalise(
            wrapped.summarize(ragged, gross=False)
        ),
    }


def _dict_facts(module: Any) -> dict[str, Any]:
    site = _matrix_dataframe(module)
    wrapped = _new_result(module, area=321.5)
    calls = 0

    def observed_site() -> Any:
        nonlocal calls
        calls += 1
        return deepcopy(site)

    wrapped.to_site_uses = observed_site
    tree = wrapped.to_dict()
    failing = _new_result(module)
    failing.to_site_uses = lambda: (_ for _ in ()).throw(ValueError("bounded-site"))
    return {
        "full_tree": _normalise(tree),
        "root_order": list(tree),
        "site_call_count": _number(calls),
        "site_failure_propagation": _exception(failing.to_dict),
        "summary_metric_order": list(tree["summary_per_area"]),
    }


def _write_facts(module: Any, work_root: Path) -> dict[str, Any]:
    work_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="epsimple-model-result-write-", dir=work_root
    ) as temporary:
        directory = Path(temporary)
        target = directory / "result.json"
        wrapped = _new_result(module)
        first_payload = {
            "building": {"total_area": 12.5},
            "nested": [[1, 2], [3, 4]],
            "series": [1, 2, 3],
            "text": "bounded [ text ]",
            "unicode": "\uacb0\uacfc",
        }
        wrapped.to_dict = lambda: deepcopy(first_payload)
        returned = wrapped.write(target)
        first_bytes = target.read_bytes()
        first_text = first_bytes.decode("utf-8")

        second_payload = {"series": [9, 8], "unicode": "\ub36e\uc5b4\uc4f0\uae30"}
        wrapped.to_dict = lambda: deepcopy(second_payload)
        wrapped.write(target)
        second_bytes = target.read_bytes()
        second_text = second_bytes.decode("utf-8")

        missing = directory / "missing" / "result.json"
        directory_target = directory / "directory-target"
        directory_target.mkdir()
        return {
            "directory_target": _exception(
                lambda: wrapped.write(directory_target), include_message=False
            ),
            "first": {
                "bom": first_bytes.startswith(b"\xef\xbb\xbf"),
                "bytes": _number(len(first_bytes)),
                "ends_with_newline": first_bytes.endswith(b"\n"),
                "json_round_trip": _normalise(json.loads(first_text)),
                "bracket_text_whitespace_collapsed": (
                    json.loads(first_text)["text"] != first_payload["text"]
                ),
                "nested_inner_lists_compacted": "[1, 2]" in first_text
                and "[3, 4]" in first_text,
                "series_compacted": "[1, 2, 3]" in first_text,
                "sha256": "sha256:" + hashlib.sha256(first_bytes).hexdigest(),
            },
            "missing_parent": _exception(
                lambda: wrapped.write(missing), include_message=False
            ),
            "overwrite": {
                "bytes": _number(len(second_bytes)),
                "changed": first_bytes != second_bytes,
                "json_round_trip": _normalise(json.loads(second_text)),
                "sha256": "sha256:" + hashlib.sha256(second_bytes).hexdigest(),
            },
            "returned_none": returned is None,
        }


def _execute_cases(
    module: Any, work_root: Path
) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _lifecycle_facts(module),
        EXPECTED_CASE_IDS[1]: _demand_facts(module),
        EXPECTED_CASE_IDS[2]: _server_facts(module),
        EXPECTED_CASE_IDS[3]: _dhw_calculation_facts(module),
        EXPECTED_CASE_IDS[4]: _site_use_facts(module),
        EXPECTED_CASE_IDS[5]: _factor_facts(
            module, "to_source_uses", "Site2Source"
        ),
        EXPECTED_CASE_IDS[6]: _factor_facts(module, "to_co2", "Site2CO2"),
        EXPECTED_CASE_IDS[7]: _factor_facts(module, "to_cost", "Site2Cost"),
        EXPECTED_CASE_IDS[8]: _summary_facts(module),
        EXPECTED_CASE_IDS[9]: _dict_facts(module),
        EXPECTED_CASE_IDS[10]: _write_facts(module, work_root),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("Model-result observation order drifted.")
    return observations


def _coverage_by_symbol() -> dict[str, str]:
    result: dict[str, str] = {}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol] = definition["id"]
    if set(result) != set(TARGET_SYMBOLS):
        raise RuntimeError("Model-result symbol coverage drifted.")
    return result


def _expected_contract(signatures: dict[str, Any]) -> dict[str, Any]:
    counts = Counter(CLASSIFICATIONS.values())
    expectations = {
        symbol: {
            "adaptation": ADAPTATIONS[symbol],
            "assertion_id": ASSERTION_IDS[symbol],
            "classification": CLASSIFICATIONS[symbol],
            "native_route": NATIVE_ROUTES[symbol],
        }
        for symbol in TARGET_SYMBOLS
    }
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {
            "equivalent": counts["equivalent"],
            "exception": counts["exception"],
        },
        "classifications": CLASSIFICATIONS,
        "closure": {
            "adjacent_count": len(ADJACENT_INDICES),
            "adjacent_indices": list(ADJACENT_INDICES),
            "exact_one_case_target_partition": True,
            "full_model_source_partition": True,
            "source_declaration_count": len(TARGET_INDICES) + len(ADJACENT_INDICES),
            "target_count": len(TARGET_RECEIPTS),
            "target_indices": list(TARGET_INDICES),
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "coverage_by_symbol": _coverage_by_symbol(),
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "exact_cpython_behavior_oracle": True,
            "expected_receipt_count": len(TARGET_RECEIPTS),
            "native_csv_or_sqlite_execution_claim": False,
            "path_independent_relocated_import": True,
            "target_coverage_complete": True,
        },
        "expectations": expectations,
        "native_route_audit": NATIVE_ROUTE_AUDIT,
        "native_routes": NATIVE_ROUTES,
        "runtime_signatures": signatures,
    }


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {case["id"]: canonical_sha256(case) for case in cases}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source_root: Path | None = None,
) -> dict[str, Any]:
    if commit.lower() != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if inventory["content_sha256"] != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory receipt is not exact.")
    _validate_generation_runtime()
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    _validate_source_tree(imported_root)
    work_root = _repository_root() / "temp" / "reference" / "model-result-work"
    work_root.mkdir(parents=True, exist_ok=True)

    with _isolated_import(imported_root) as module:
        signatures = _runtime_signatures(module)
        observations = _execute_cases(module, work_root / "primary")
        primary_modules = _loaded_local_modules(imported_root, inventory)

    with tempfile.TemporaryDirectory(
        prefix="epsimple-model-result-relocation-", dir=work_root
    ) as temporary:
        relocated_root = Path(temporary) / "src"
        _copy_source_tree(imported_root, relocated_root)
        with _isolated_import(relocated_root) as relocated_module:
            relocated_signatures = _runtime_signatures(relocated_module)
            relocated_observations = _execute_cases(
                relocated_module, work_root / "relocated"
            )
            relocated_modules = _loaded_local_modules(relocated_root, inventory)

    if signatures != relocated_signatures:
        raise RuntimeError("Model-result runtime signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("Model-result observations changed after relocation.")
    if primary_modules != relocated_modules:
        raise RuntimeError("Model-result loaded modules changed after relocation.")

    signature_hash = canonical_sha256(signatures)
    module_hash = canonical_sha256(primary_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and signature_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise SystemExit("Pinned model-result runtime signatures drifted.")
    if module_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned model-result loaded-module receipt drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise SystemExit("Pinned model-result relocation observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned model-result fact hashes drifted.\n"
            + strict_json_dumps(fact_hashes, indent=2)
        )
    cases: list[dict[str, Any]] = []
    for definition in case_definitions():
        identifier = definition["id"]
        case = dict(definition)
        case["python"] = {
            "facts": observations[identifier],
            "facts_sha256": fact_hashes[identifier],
            "outcome": "observed",
        }
        cases.append(case)
    case_hashes = case_sha256(cases)
    aggregate = cases_sha256(cases)
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise SystemExit(
            "Pinned model-result case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned model-result aggregate case hash drifted.")

    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(signatures),
        "fact_sha256": fact_hashes,
        "native_review": {
            "route_audit": NATIVE_ROUTE_AUDIT,
            "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        },
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "adjacent_receipts_sha256": inventory["adjacent_receipts_sha256"],
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "inventory": {
                "bytes": EXPECTED_INVENTORY_BYTES,
                "content_sha256": EXPECTED_INVENTORY_SHA256,
                "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
            },
            "isolated_import": {
                "loaded_local_modules": primary_modules,
                "loaded_local_modules_sha256": module_hash,
                "relocated_observations_sha256": relocation_hash,
                "relocated_source_copy": "byte-identical-epsimple-and-idragon-trees",
                "source_location_count": 2,
            },
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "weather_resources": list(WEATHER_RESOURCES),
        },
    }
    validate_oracle(result)
    return result


def _validate_safe_string(value: str, location: str) -> None:
    for pattern, label in (
        (RAW_ADDRESS_PATTERN, "raw object address"),
        (WINDOWS_PATH_PATTERN, "absolute Windows path"),
        (POSIX_PATH_PATTERN, "absolute POSIX path"),
        (GUID_PATTERN, "GUID-like value"),
        (TIMESTAMP_PATTERN, "timestamp"),
    ):
        if pattern.search(value):
            raise RuntimeError(f"Forbidden {label} at {location}.")


def _validate_typed_value(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind not in {"bool", "int", "float", "float-nonfinite"}:
        return False
    if kind == "bool":
        if set(value) != {"kind", "value"} or not isinstance(value["value"], bool):
            raise RuntimeError(f"Noncanonical bool encoding at {location}.")
    elif kind == "int":
        token = value.get("value")
        if (
            set(value) != {"kind", "value"}
            or not isinstance(token, str)
            or str(int(token)) != token
        ):
            raise RuntimeError(f"Noncanonical int encoding at {location}.")
    elif kind == "float":
        if set(value) != {"hex", "kind", "repr"}:
            raise RuntimeError(f"Noncanonical float encoding at {location}.")
        parsed = float.fromhex(value["hex"])
        if (
            not math.isfinite(parsed)
            or parsed.hex() != value["hex"]
            or repr(parsed) != value["repr"]
        ):
            raise RuntimeError(f"Noncanonical finite float at {location}.")
    else:
        if set(value) != {"kind", "value"} or value["value"] not in {
            "nan",
            "positive-infinity",
            "negative-infinity",
        }:
            raise RuntimeError(f"Noncanonical nonfinite float at {location}.")
    return True


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if value is None or isinstance(value, bool) or isinstance(value, int):
        return
    if isinstance(value, float):
        raise RuntimeError(f"Raw float is forbidden at {location}.")
    if isinstance(value, str):
        _validate_safe_string(value, location)
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        if _validate_typed_value(value, location):
            return
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string JSON key at {location}.")
            _validate_safe_string(key, location + ".<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsafe value type {type(value).__name__} at {location}.")


def load_json_without_duplicates_text(text: str) -> dict[str, Any]:
    def hook(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, item in pairs:
            if key in result:
                raise ValueError(f"Duplicate key: {key}")
            result[key] = item
        return result

    value = json.loads(
        text,
        object_pairs_hook=hook,
        parse_constant=lambda token: (_ for _ in ()).throw(ValueError(token)),
    )
    if not isinstance(value, dict):
        raise RuntimeError("Strict JSON text root is not an object.")
    return value


def validate_oracle(value: dict[str, Any]) -> None:
    expected_keys = {
        "case_sha256",
        "cases",
        "cases_sha256",
        "consumer_contract",
        "fact_sha256",
        "native_review",
        "runtime",
        "schema",
        "symbols",
        "target_receipts",
        "upstream",
    }
    if not isinstance(value, dict) or set(value) != expected_keys:
        raise RuntimeError("Model-result oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("Model-result schema drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("Model-result target receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Model-result symbol descriptors drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("Model-result runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("Model-result runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("Model-result consumer contract drifted.")
    if value["native_review"] != {
        "route_audit": NATIVE_ROUTE_AUDIT,
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
    }:
        raise RuntimeError("Model-result native review receipt drifted.")

    expected_runtime = {
        "dependencies": EXPECTED_DEPENDENCIES,
        "dependencies_sha256": canonical_sha256(EXPECTED_DEPENDENCIES),
        "implementation": "cpython",
        "platform": REQUIRED_PLATFORM,
        "pointer_width_bits": REQUIRED_POINTER_WIDTH_BITS,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }
    if value["runtime"] != expected_runtime:
        raise RuntimeError("Model-result runtime receipt drifted.")

    upstream = value["upstream"]
    if not isinstance(upstream, dict) or set(upstream) != {
        "adjacent_receipts_sha256",
        "commit",
        "inventory",
        "isolated_import",
        "source",
        "weather_resources",
    }:
        raise RuntimeError("Model-result upstream key set drifted.")
    expected_static = {
        "adjacent_receipts_sha256": EXPECTED_ADJACENT_RECEIPTS_SHA256,
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
        "weather_resources": list(WEATHER_RESOURCES),
    }
    for key, expected in expected_static.items():
        if upstream.get(key) != expected:
            raise RuntimeError(f"Model-result upstream field drifted: {key}")
    isolated = upstream["isolated_import"]
    if not isinstance(isolated, dict) or set(isolated) != {
        "loaded_local_modules",
        "loaded_local_modules_sha256",
        "relocated_observations_sha256",
        "relocated_source_copy",
        "source_location_count",
    }:
        raise RuntimeError("Model-result isolated-import key set drifted.")
    loaded = isolated["loaded_local_modules"]
    if (
        not isinstance(loaded, list)
        or isolated["loaded_local_modules_sha256"] != canonical_sha256(loaded)
        or canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Model-result loaded-module receipt drifted.")
    if isolated["source_location_count"] != 2:
        raise RuntimeError("Model-result relocation count drifted.")
    if isolated["relocated_source_copy"] != "byte-identical-epsimple-and-idragon-trees":
        raise RuntimeError("Model-result relocation mode drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and isolated["relocated_observations_sha256"]
        != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise RuntimeError("Model-result relocation observation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Model-result case count drifted.")
    if [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Model-result case order drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(f"Model-result case key set drifted: {definition['id']}")
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(f"Model-result case definition drifted: {definition['id']}")
        python = case["python"]
        if set(python) != {"facts", "facts_sha256", "outcome"} or python["outcome"] != "observed":
            raise RuntimeError(f"Model-result observation drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"Model-result inline fact hash drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Model-result fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned model-result fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Model-result case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned model-result case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Model-result aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned model-result aggregate case hash drifted.")
    counts = Counter(symbol for case in cases for symbol in case["target_symbols"])
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Model-result exact target closure drifted.")
    _validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Model-result strict JSON round trip drifted.")


def main() -> None:
    args = parse_args()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    oracle = build_oracle(inventory, args.upstream_commit)
    encoded = strict_json_dumps(oracle, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(encoded, encoding="utf-8", newline="\n")
    persisted = load_json_without_duplicates(args.output)
    validate_oracle(persisted)
    if args.output.read_text(encoding="utf-8") != encoded:
        raise SystemExit("Persisted model-result oracle is not byte-identical.")
    counts = Counter(CLASSIFICATIONS.values())
    print(
        f"Generated {args.output} with {len(TARGET_RECEIPTS)} targets, "
        f"{EXPECTED_CASE_COUNT} cases, {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, and aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()
