"""Generate the pinned Dragon construction-family core behavior oracle.

The corpus directly executes the 0.7.0 Python constructors, properties,
setters, ``Construction.reversed`` method, and ``MaterialRoughness`` enum.
Representation, equality/hash, and IDF-emission members are deliberately
excluded because they are covered by separate reviewed slices or policy.
Run through ``bootstrap_reference.py`` so imports resolve only from the
pinned CPython 3.12.7 dependency and upstream source roots.
"""

from __future__ import annotations

import argparse
from collections import Counter
import functools
import importlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-construction-core.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_INVENTORY_FILE_BYTES = 518_067
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8"
)
SOURCE_PATH = "src/idragon/dragon/construction.py"
EXPECTED_SOURCE_BYTES = 11_652
EXPECTED_SOURCE_SHA256 = (
    "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"
)
EXPECTED_ADJACENT_EXCLUSIONS_SHA256 = (
    "sha256:d1d45b9dd7d044356c10fba104b430da82a95b5ac26ab5e97b25d37e36ecce47"
)
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


_RECEIPT_ROWS = (
    (593, "Construction", "class", "451c832ae468ffe5d8cf9a462538dbd45df5d81c0d9a789d22b8ebc9cdc662c1", "ba362f152f2885654833496ce8ef79e40b4f76c9554bb3989187ff8284fce155", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (594, "Construction.U", "function", "a29f2b11c458a80277b67b155ad434d7df69ed93e32c9bdaa7595bcfa41e111a", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "5f09fc8f718e7deb1038c464ee7d1e34423a33647b9de4949669ccfd75149556"),
    (597, "Construction.__init__", "function", "c99eac6b7f0a56aefc53f3d6f67771870aa324f3e3e455a884ae3d046bcacbee", "76989db56f17a4e7cf4e9650efa5d9dc699fdd1ff26cb47e5f7dec91875f1eeb", "7d59358f4dd18ddf785f769c3a8e03e7b622706aa36e532ac72652edecef029c"),
    (598, "Construction.heat_capacity", "function", "cebc9acb26c61981719622bc8621a2e46e71a856f7d735aa0ad1bac3ba924c3d", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "585bb77a5040a6115541e30c1fcb41c03555d1ae57eb35302c2260a1e2d89cd4"),
    (599, "Construction.reversed", "function", "f3f8b2b13f2d35ab827dc50d35300299ffb4fdff84c23e1b61e1f75a9bd66ae6", "d9c98612307a4a23f460b96edc97ecfdae896510d7e3f3802c2db48397a27f7f", "526ddebecadebd67c23b4b2a8a0a292019e29c6e12ddeed5a95ee48cbb09dc1a"),
    (600, "Construction.thickness", "function", "bfcb0ba0853de75fd7d905f75e1faf50c62c402bb560153f99095f2ae700b42a", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "69c8c7c7538e98047d16fd22ceee2d69f8bed95d94fc4ef170b4dfdb49d5e05b"),
    (602, "Glazing", "class", "5615eebbd32c5598a861819f7d6b3a78e196195e4e75720f127efcb569e3b183", "84f330d84d5789f02f6715bbe7f62359d20d33eb2035f16bba511e8d33e81e4d", "c3822492f76a666adcf5e3d03eec2d98eb4a6512284ac5b3ce410a4cc62f977f"),
    (603, "Glazing.G", "function", "cb8ad4be46db878574e1b2bd7d6acb89d14d3790e40fb103a36b9d2b55c06608", "97886dfb340546a8c9fe4b9b8cf3189e8c60725c258ed5cc38acf05f58f2d713", "f3304233b10860727f115b9eca9eda8e095ccc8555a79fabd8f2313d832e6106"),
    (604, "Glazing.U", "function", "98ebe259795ce2a3c2e4409a7f4b07ea156ce03ec82f75f80eaacf9443ea2c74", "42d4877d0fcd09ffdc6f0adcf3878d8cf5fd4c4ebb84c3608e7ace68b461ded5", "3977456b908976b9f55edaf502ef11be78fe953ddaf51c51a06931c2cde34355"),
    (605, "Glazing.__init__", "function", "bfe7247a3ea9282f15591f6ddd95981b7d9d17090daa27f37963202cc5ec64f6", "3ccc7e8fdb247dcebe0a23c3b938e5c7f50dd3477011a83848d3706ae18a6313", "e2c9469ffa1c8710f555422ab1321056d884717d5fd4d25f0b48996bf928a094"),
    (609, "Layer", "class", "e6a3fe0d1609d906a38b41716b1b7c4a8023d8a8d2d994372be5220fe7ffa25b", "29e85eb3dad7d92146453af9d99d862c4cdd12d807f41103a0bb07d4352ea3b4", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (610, "Layer.U", "function", "be30888f37ab4d68b8032d65be2328c94c7297647b1b3ec7f6750a4d45bb60ac", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "8db6a84f5441c6db866cd4b88715713d72dbe230e1199221a541bbcfcaf90e69"),
    (613, "Layer.__init__", "function", "60e437a193c85e3989efa3af43401cb41ed1dd2e4b67e71ca84e3a9c7f1eb05d", "4740434b9f3416b14a21d0b6efca354d514bebcb96032dfc271db5e79e04d8b6", "2ef34c4ddec19e44978b0a382b7838d272519b84f9531e6caeb095545c00fa2a"),
    (614, "Layer.heat_capacity", "function", "ab4d9ecc8b11fd1a97ce37861d9e672f36bf362cc6c0db3e1e0171a57483c31b", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "a230a49334316e1915520b5477710079b654fb2f509587ac266ad2312c825ec6"),
    (615, "Layer.material", "function", "6454844c03c2d78d936689815e815ac76301c7f135efdc361c6700b4a0391f61", "66d560f35bfbc32d89fb4c0926bd7f201a3482083381e3386e142545ced868df", "6b29070572dbb73df177ec301f5cf08d86910d98ef3656807a3723b86fb11caa"),
    (616, "Layer.thickness", "function", "d7d789d7eddbbdc9b7f7db4e683689ddc362be53336654fe9930319b1ea25899", "8f13b51352c870f880244d9dacb7b0fb97dc8e5823221a7cf6a23fcbf4186d88", "f52129f61c499bb427a8e956033d38541ef4b21f599c3a4e703f7b0744d496a0"),
    (618, "Material", "class", "15ad6614da4693f24dc519c4a8ebc1503c18e90f5e7407194e7af2ee478878c2", "f374687b6dfa7d96b4b87d055f10b1c4045aef186851919903647548c74ae2bd", "f04919503f8232602615439697060f553b3b3db5b31404e7eb9b49ee20e57d65"),
    (620, "Material.__init__", "function", "d78cab39fcf7243e0cd0c59653ff7514b95dbddf3cc9a28eb14a8834bfd9791d", "3ba00224b6905eea7a43603fcc36c1c6adca5b5b3e4c781df75be38d9d1ec690", "267759cb86c1bd9f1390036d0858ee51930be747648796d9236b5260311f6d21"),
    (621, "Material.conductivity", "function", "b733b56b8a0acfcefc97c11b3fef116d8a1a5a29c847ed24e600839289383471", "68da20c9424bcb4ac2882491f00f8c9c26c63e331453583af45c04a260c45453", "f512da9e579d342352c80b0e5ceb0af993e59c64ac17ebd7f46067d0df112c94"),
    (622, "Material.density", "function", "231363247e3bc2f63cd6b88174bb6e3f732f56e00f0abab5bc9eeb69d2ef8893", "8d7e015ab764fc82bd4de0f7447db18903e71574e2ee810518866ea31f0700b7", "7a3173329ae1f0c334b6362b9c1c7cc7f1aaf20be9ddea3d34ebf29c9804cfe9"),
    (623, "Material.roughness", "function", "be23eedd7fa255d7489768c6081e40cbc6361e17736f66dbea5609b89105465b", "07c6d8f20d92daeb00700a584eb95634eb7c7ac7b43f41de5497eedd93da1b0e", "369a99e17d94426a26a3ccda42dff5a360fceb9aee71166f0528f011c30b4d84"),
    (624, "Material.solar_absorptance", "function", "ae7ce02bf1109ed4279c351fa9497272fef93c019b587fd237073bf1055d315f", "4629f155a28541d892173c40a499ea8bbb660522be6762d87df9f0fa254ded61", "35af69a9a977f53ab5fd828a823ec7a1576f7042e3ec606cecd2da3b85b933c6"),
    (625, "Material.specific_heat", "function", "abf4a2ea739fe17a9d04c787331534748bfd530f11baddf215ea17e5363f011b", "3f02e26053465c1d64093f2c803d1f146085da49b32a8561b50291b3df8fea37", "0580b0014f432929c0452ccb674124ad60495272b5976e1380bb90ae7aa21701"),
    (626, "Material.thermal_absorptance", "function", "f17730ed4aa6cc5d8aa673527cd0b43e3ef83ead9df7d5d5910ad26eaa87f784", "a74f738d6f56e6f5c6b72d89a54e8c1d98b783c82afa8928f23d97945b17be5e", "22377bd3ebaf63e005fda95e22d5f2583df3d7fd1361109e072ff4d41b90fdaa"),
    (627, "Material.visible_absorptance", "function", "ecf6d77de8ef2e870df1470b8113e2beaa1154e5ae54d6581d7c62840df71c9c", "b0839747d975780bcb3b558e0f548e211db34a5e1d793d5ba106f7e6500bee18", "0b8fe267c85e1a9d3e9d5ea832e8cff40ce6e03b51246ee94a09794a2d60d7ab"),
    (628, "MaterialRoughness", "class", "fc281859031701e047f11e96eac77ca6cb530ce23493a7ef77c1e0000d31ff08", "6dc25edcfe258d38350c660f1cd3ff872cf05d4b629faa638d7454a7de510903", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (629, "MaterialRoughness.MEDIUMROUGH", "constant", "eda0d7d5e27bc9a869d83138fab36b72b59c5dc4cc013d3c8de181be8a683aa1", "d7d80ec873529a9c0869d0bc7b0e8317ac669fd979cb825bdeb0b0bee5787bb6", "e45dfab6985e92e259a2d5d60f612bc723282af67f90ec2c1e22c13b51818296"),
    (630, "MaterialRoughness.MEDIUMSMOOTH", "constant", "6d574d5473f00de478de3c31bb49dd0092748208c1b62b098f054b43b4d97023", "506ea60b4d768c655ac5410139c43c6be4ffa35b836b2b95361a2e4258dcad52", "fa9d3ec25e11b97efaf1b3be70b04cf9097ee8105e7227ba7990e79ef05a2da0"),
    (631, "MaterialRoughness.ROUGH", "constant", "beaf152fac9bd6bc2352e8a1ac6295cc9ba66bcf1d941a54753822672b961918", "3018df3727ef92da5fb87e1920b4010e9a09bcbac5ef474924ce9639f5fffeb0", "ca49338828392c58c98b8ab74e4cf7148e4330939bc5ef3ef113e6043bec419d"),
    (632, "MaterialRoughness.SMOOTH", "constant", "fce6deeb54d0293397f0279fb5ad9e25ff06f0bce3a0430a18d2935ad80739ff", "56f2e0fce6e496a46a96e9d9ee4d5906ce3c133af204314a8d0af340d7eaca4c", "61392d9f0253e489e1057e14f6418c347a182c4ea465fd7ce135c400d3848f5b"),
    (633, "MaterialRoughness.VERYROUGH", "constant", "9848a0c66d3e174bd16efcbaac5d3f3a1b3f0e657f223306860a8f874ed0c7fd", "b25aff6d388d615132acdb141dd8f540f94a7c05359633712ad75e575845eaf3", "846c33a8465f5b9f11ed409dc51fa0086409cad0f5eb21d889981ca0f68f6e92"),
    (634, "MaterialRoughness.__str__", "function", "f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8"),
    (635, "NoMassConstruction", "class", "9dff867c894980d4bda1f7c0cc731348382bef441677df6a70b79ebf876c23a2", "24508dea22fc922a71630543e4fb07ae0250b502a350f455ff9d2e3ece31eb95", "c3822492f76a666adcf5e3d03eec2d98eb4a6512284ac5b3ce410a4cc62f977f"),
    (636, "NoMassConstruction.U", "function", "98ebe259795ce2a3c2e4409a7f4b07ea156ce03ec82f75f80eaacf9443ea2c74", "42d4877d0fcd09ffdc6f0adcf3878d8cf5fd4c4ebb84c3608e7ace68b461ded5", "3977456b908976b9f55edaf502ef11be78fe953ddaf51c51a06931c2cde34355"),
    (637, "NoMassConstruction.__init__", "function", "4749789207a6ac2baa1695fc65c6c280636e4dd352a9a7ae0369b7857a395338", "88df136720eb992f7f9723304f000865e76befe28194b7682b4efd4b4afbde01", "0405b61d480a332d56909d06933cfec963f67cf958fd3bf548d9a07ea2d47f63"),
)
TARGET_RECEIPTS = tuple(
    {
        "body_hash": "sha256:" + body_hash,
        "inventory_index": index,
        "kind": kind,
        "path": SOURCE_PATH,
        "signature_hash": "sha256:" + signature_hash,
        "symbol": symbol,
        "symbol_hash": "sha256:" + symbol_hash,
    }
    for index, symbol, kind, symbol_hash, signature_hash, body_hash in _RECEIPT_ROWS
)
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
EQUIVALENT_SYMBOLS = (
    "Construction.U",
    "Construction.heat_capacity",
    "Construction.thickness",
    "Layer.U",
    "Layer.heat_capacity",
    "MaterialRoughness.MEDIUMROUGH",
    "MaterialRoughness.MEDIUMSMOOTH",
    "MaterialRoughness.ROUGH",
    "MaterialRoughness.SMOOTH",
    "MaterialRoughness.VERYROUGH",
    "MaterialRoughness.__str__",
)
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}

ADAPTATIONS = {
    "Construction": "immutable-validated-native-construction-451c832a",
    "Construction.U": "direct-native-construction-u-value",
    "Construction.__init__": "typed-nonempty-native-construction-init-c99eac6b",
    "Construction.heat_capacity": "direct-native-construction-heat-capacity",
    "Construction.reversed": "immutable-validated-native-construction-reverse-f3f8b2b1",
    "Construction.thickness": "direct-native-construction-thickness",
    "Glazing": "immutable-validated-native-glazing-5615eebb",
    "Glazing.G": "immutable-bounded-native-glazing-g-cb8ad4be",
    "Glazing.U": "immutable-finite-native-glazing-u-98ebe259",
    "Glazing.__init__": "validated-immutable-native-glazing-init-bfe7247a",
    "Layer": "immutable-validated-native-layer-e6a3fe0d",
    "Layer.U": "direct-native-layer-u-value",
    "Layer.__init__": "validated-immutable-native-layer-init-60e437a1",
    "Layer.heat_capacity": "direct-native-layer-heat-capacity",
    "Layer.material": "immutable-required-native-layer-material-6454844c",
    "Layer.thickness": "immutable-finite-native-layer-thickness-d7d789d7",
    "Material": "immutable-validated-native-material-15ad6614",
    "Material.__init__": "validated-immutable-native-material-init-d78cab39",
    "Material.conductivity": "immutable-finite-native-material-conductivity-b733b56b",
    "Material.density": "immutable-finite-native-material-density-23136324",
    "Material.roughness": "immutable-strongly-typed-native-material-roughness-be23eedd",
    "Material.solar_absorptance": "immutable-finite-native-material-solar-absorptance-ae7ce02b",
    "Material.specific_heat": "immutable-finite-native-material-specific-heat-abf4a2ea",
    "Material.thermal_absorptance": "immutable-finite-native-material-thermal-absorptance-f17730ed",
    "Material.visible_absorptance": "immutable-finite-native-material-visible-absorptance-ecf6d77d",
    "MaterialRoughness": "strongly-typed-native-material-roughness-enum-fc281859",
    "MaterialRoughness.MEDIUMROUGH": "direct-native-material-roughness-medium-rough",
    "MaterialRoughness.MEDIUMSMOOTH": "direct-native-material-roughness-medium-smooth",
    "MaterialRoughness.ROUGH": "direct-native-material-roughness-rough",
    "MaterialRoughness.SMOOTH": "direct-native-material-roughness-smooth",
    "MaterialRoughness.VERYROUGH": "direct-native-material-roughness-very-rough",
    "MaterialRoughness.__str__": "direct-native-material-roughness-string",
    "NoMassConstruction": "immutable-validated-native-no-mass-construction-9dff867c",
    "NoMassConstruction.U": "immutable-finite-native-no-mass-u-98ebe259",
    "NoMassConstruction.__init__": "validated-immutable-native-no-mass-init-47497892",
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"dragon-construction-core-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}
NATIVE_TARGETS = {
    "Construction": "Dragons.InvisibleDragon.Construction.Construction immutable class",
    "Construction.U": "Construction.UValue",
    "Construction.__init__": "Construction(string, IEnumerable<Layer>)",
    "Construction.heat_capacity": "Construction.HeatCapacityJoulesPerSquareMetreKelvin",
    "Construction.reversed": "Construction.Reverse",
    "Construction.thickness": "Construction.ThicknessMetres",
    "Glazing": "Dragons.InvisibleDragon.Construction.Glazing immutable record",
    "Glazing.G": "Glazing.SolarHeatGainCoefficient",
    "Glazing.U": "Glazing.UValueWattsPerSquareMetreKelvin",
    "Glazing.__init__": "Glazing(string, double, double)",
    "Layer": "Dragons.InvisibleDragon.Construction.Layer immutable record",
    "Layer.U": "Layer.UValue",
    "Layer.__init__": "Layer(string, Material, double)",
    "Layer.heat_capacity": "Layer.HeatCapacityJoulesPerSquareMetreKelvin",
    "Layer.material": "Layer.Material get-only required reference",
    "Layer.thickness": "Layer.ThicknessMetres get-only finite double",
    "Material": "Dragons.InvisibleDragon.Construction.Material immutable record",
    "Material.__init__": "Material validated typed constructor",
    "Material.conductivity": "Material.ConductivityWattsPerMetreKelvin",
    "Material.density": "Material.DensityKilogramsPerCubicMetre",
    "Material.roughness": "Material.Roughness",
    "Material.solar_absorptance": "Material.SolarAbsorptance",
    "Material.specific_heat": "Material.SpecificHeatJoulesPerKilogramKelvin",
    "Material.thermal_absorptance": "Material.ThermalAbsorptance",
    "Material.visible_absorptance": "Material.VisibleAbsorptance",
    "MaterialRoughness": "Dragons.InvisibleDragon.Construction.MaterialRoughness enum",
    "MaterialRoughness.MEDIUMROUGH": "MaterialRoughness.MediumRough",
    "MaterialRoughness.MEDIUMSMOOTH": "MaterialRoughness.MediumSmooth",
    "MaterialRoughness.ROUGH": "MaterialRoughness.Rough",
    "MaterialRoughness.SMOOTH": "MaterialRoughness.Smooth",
    "MaterialRoughness.VERYROUGH": "MaterialRoughness.VeryRough",
    "MaterialRoughness.__str__": "MaterialRoughness.ToString",
    "NoMassConstruction": "Dragons.InvisibleDragon.Construction.NoMassConstruction immutable record",
    "NoMassConstruction.U": "NoMassConstruction.UValueWattsPerSquareMetreKelvin",
    "NoMassConstruction.__init__": "NoMassConstruction(string, double)",
}

ADJACENT_EXCLUSION_IDENTITIES = (
    (588, "AirBoundary"),
    (589, "AirBoundary.__init__"),
    (590, "AirBoundary.__repr__"),
    (591, "AirBoundary.__str__"),
    (592, "AirBoundary.to_idf_object"),
    (595, "Construction.__eq__"),
    (596, "Construction.__hash__"),
    (601, "Construction.to_idf_object"),
    (606, "Glazing.__repr__"),
    (607, "Glazing.__str__"),
    (608, "Glazing.to_idf_object"),
    (611, "Layer.__eq__"),
    (612, "Layer.__hash__"),
    (617, "Layer.to_idf_object"),
    (619, "Material.__eq__"),
    (638, "NoMassConstruction.__repr__"),
    (639, "NoMassConstruction.__str__"),
    (640, "NoMassConstruction.to_idf_object"),
)
EXCLUDED_SYMBOLS = tuple(item[1] for item in ADJACENT_EXCLUSION_IDENTITIES)

RUNTIME_SIGNATURES = {
    "Construction": "(name, *args)",
    "Construction.U": "property:fget=(self) -> 'float'",
    "Construction.__init__": "(self, name, *args)",
    "Construction.heat_capacity": "property:fget=(self) -> 'float'",
    "Construction.reversed": "(self, name: 'str' = None) -> 'Construction'",
    "Construction.thickness": "property:fget=(self) -> 'float'",
    "Glazing": "(name: 'str', U: 'int | float', G: 'int | float') -> 'None'",
    "Glazing.G": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Glazing.U": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Glazing.__init__": "(self, name: 'str', U: 'int | float', G: 'int | float') -> 'None'",
    "Layer": "(name: 'str', material: 'Material', thickness: 'int | float') -> 'None'",
    "Layer.U": "property:fget=(self) -> 'float'",
    "Layer.__init__": "(self, name: 'str', material: 'Material', thickness: 'int | float') -> 'None'",
    "Layer.heat_capacity": "property:fget=(self) -> 'float'",
    "Layer.material": "property:fget=(self) -> 'Material';fset=(self, value: 'Material') -> 'None'",
    "Layer.thickness": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material": "(name: 'str', conductivity: 'int | float', density: 'int | float', specific_heat: 'int | float', *, thermal_absorptance: 'int | float' = 0.9, solar_absorptance: 'int | float' = 0.7, visible_absorptance: 'int | float' = 0.7, roughness: 'MaterialRoughness' = <MaterialRoughness.ROUGH: 'Rough'>) -> 'None'",
    "Material.__init__": "(self, name: 'str', conductivity: 'int | float', density: 'int | float', specific_heat: 'int | float', *, thermal_absorptance: 'int | float' = 0.9, solar_absorptance: 'int | float' = 0.7, visible_absorptance: 'int | float' = 0.7, roughness: 'MaterialRoughness' = <MaterialRoughness.ROUGH: 'Rough'>) -> 'None'",
    "Material.conductivity": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material.density": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material.roughness": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material.solar_absorptance": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material.specific_heat": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material.thermal_absorptance": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Material.visible_absorptance": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "MaterialRoughness": "(*values)",
    "MaterialRoughness.MEDIUMROUGH": "enum-member:'MediumRough'",
    "MaterialRoughness.MEDIUMSMOOTH": "enum-member:'MediumSmooth'",
    "MaterialRoughness.ROUGH": "enum-member:'Rough'",
    "MaterialRoughness.SMOOTH": "enum-member:'Smooth'",
    "MaterialRoughness.VERYROUGH": "enum-member:'VeryRough'",
    "MaterialRoughness.__str__": "(self) -> 'str'",
    "NoMassConstruction": "(name: 'str', U: 'int | float') -> 'None'",
    "NoMassConstruction.U": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "NoMassConstruction.__init__": "(self, name: 'str', U: 'int | float') -> 'None'",
}

PREFIX = "dragon-construction-core."
CASE_SPECS = (
    ("c01-roughness-topology-order-values", "C01", "roughness", ("MaterialRoughness", "MaterialRoughness.MEDIUMROUGH", "MaterialRoughness.MEDIUMSMOOTH", "MaterialRoughness.ROUGH", "MaterialRoughness.SMOOTH", "MaterialRoughness.VERYROUGH"), ()),
    ("c02-roughness-strings", "C02", "roughness", ("MaterialRoughness.__str__", "MaterialRoughness.MEDIUMROUGH", "MaterialRoughness.MEDIUMSMOOTH", "MaterialRoughness.ROUGH", "MaterialRoughness.SMOOTH", "MaterialRoughness.VERYROUGH"), ("MaterialRoughness",)),
    ("c03-roughness-construction-invalid", "C03", "roughness", ("MaterialRoughness",), ("MaterialRoughness.ROUGH",)),
    ("c04-material-default-state", "C04", "material", ("Material", "Material.__init__", "Material.conductivity", "Material.density", "Material.roughness", "Material.solar_absorptance", "Material.specific_heat", "Material.thermal_absorptance", "Material.visible_absorptance"), ("MaterialRoughness.ROUGH",)),
    ("c05-material-explicit-mutation", "C05", "material", ("Material", "Material.__init__", "Material.conductivity", "Material.density", "Material.roughness", "Material.solar_absorptance", "Material.specific_heat", "Material.thermal_absorptance", "Material.visible_absorptance"), ("MaterialRoughness",)),
    ("c06-material-type-range-nonfinite", "C06", "material", ("Material", "Material.__init__", "Material.conductivity", "Material.density", "Material.solar_absorptance", "Material.specific_heat", "Material.thermal_absorptance", "Material.visible_absorptance"), ("Material.roughness",)),
    ("c07-layer-state-derived", "C07", "layer", ("Layer", "Layer.__init__", "Layer.U", "Layer.heat_capacity", "Layer.material", "Layer.thickness"), ("Material",)),
    ("c08-layer-mutation", "C08", "layer", ("Layer", "Layer.U", "Layer.heat_capacity", "Layer.material", "Layer.thickness"), ("Layer.__init__", "Material")),
    ("c09-layer-type-range-nonfinite", "C09", "layer", ("Layer", "Layer.__init__", "Layer.material", "Layer.thickness"), ("Material",)),
    ("c10-construction-layer-overload-metrics", "C10", "construction", ("Construction", "Construction.__init__", "Construction.U", "Construction.heat_capacity", "Construction.thickness"), ("Layer",)),
    ("c11-construction-material-thickness-overload", "C11", "construction", ("Construction", "Construction.__init__", "Construction.U", "Construction.heat_capacity", "Construction.thickness"), ("Material", "Layer")),
    ("c12-construction-reverse-order-alias", "C12", "construction", ("Construction.reversed",), ("Construction", "Layer")),
    ("c13-construction-empty-mixed-mutation", "C13", "construction", ("Construction", "Construction.__init__", "Construction.U", "Construction.heat_capacity", "Construction.thickness"), ("Layer",)),
    ("c14-glazing-state", "C14", "glazing", ("Glazing", "Glazing.__init__", "Glazing.G", "Glazing.U"), ()),
    ("c15-glazing-mutation", "C15", "glazing", ("Glazing", "Glazing.G", "Glazing.U"), ("Glazing.__init__",)),
    ("c16-glazing-type-range-nonfinite", "C16", "glazing", ("Glazing", "Glazing.__init__", "Glazing.G", "Glazing.U"), ()),
    ("c17-no-mass-state", "C17", "no-mass", ("NoMassConstruction", "NoMassConstruction.__init__", "NoMassConstruction.U"), ()),
    ("c18-no-mass-mutation", "C18", "no-mass", ("NoMassConstruction", "NoMassConstruction.U"), ("NoMassConstruction.__init__",)),
    ("c19-no-mass-type-range-nonfinite", "C19", "no-mass", ("NoMassConstruction", "NoMassConstruction.__init__", "NoMassConstruction.U"), ()),
)
EXPECTED_CASE_IDS = tuple(PREFIX + item[0] for item in CASE_SPECS)
EXPECTED_CASE_COUNT = 19
EXPECTED_FACT_SHA256 = {
    "dragon-construction-core.c01-roughness-topology-order-values": "sha256:fb51fd91e5637240d7a6da09836e6bdd8b85f2d30f662621b8532189c46d2bcd",
    "dragon-construction-core.c02-roughness-strings": "sha256:cc1ac9ebacac7d957fd68614b67fee1dfb3d6e51265b20395fd1d4fe8bef7511",
    "dragon-construction-core.c03-roughness-construction-invalid": "sha256:da8fa7beb6c5a74aedf13e63aae17207d3bfbfb4e80d17298d1ebaa10a93cd5d",
    "dragon-construction-core.c04-material-default-state": "sha256:184ba31b6e46e25b121fe1c8bc19505893d07d41ecdf51986779edda9c527cd3",
    "dragon-construction-core.c05-material-explicit-mutation": "sha256:5eebcc878f8d4ac423b7a318b7d97cadf26055a18698063ad38870a71d42710f",
    "dragon-construction-core.c06-material-type-range-nonfinite": "sha256:50faa50ebc5d917cbf222d828829f9d20983451e05b5e60a063cbb321ac741cd",
    "dragon-construction-core.c07-layer-state-derived": "sha256:69f70fe55ef583a907ea2ae4fbded820a24b4c45a5fa8a07263de6cf2ebd5459",
    "dragon-construction-core.c08-layer-mutation": "sha256:d6332bd771a010ac19c456be2e193b153713f5b568f9d0195a649356115e7c70",
    "dragon-construction-core.c09-layer-type-range-nonfinite": "sha256:156fbc165abbc9f2dd49276eb4d1e65411c5f16ec665a15a96cb36d2b4171e1e",
    "dragon-construction-core.c10-construction-layer-overload-metrics": "sha256:dbd14c4557f8fd12d2d51412c0e3fa404f58136ce2cac87d54ebbcace621c43a",
    "dragon-construction-core.c11-construction-material-thickness-overload": "sha256:2940d133afc8a562898db97211e89fbdf7c3d8f338ec7a07b20b4dc98dc3e98a",
    "dragon-construction-core.c12-construction-reverse-order-alias": "sha256:059530700ef5d34ebc3939e6ef4a2a95f0f35eef7bc9de59635ec124b52c2b21",
    "dragon-construction-core.c13-construction-empty-mixed-mutation": "sha256:6270ae55a5b6503df6d6e22b12e05432afea6aeb5e1368ba061eec8cd8e999a0",
    "dragon-construction-core.c14-glazing-state": "sha256:3f0359249755a4f6c4a55f4387cf72fd9e6f710978c421202eec4e2d9ec76bcc",
    "dragon-construction-core.c15-glazing-mutation": "sha256:d23b7b2463f345d7c1d333a521b6a103ad02b0600abe86fbe18f8cb2a00d429a",
    "dragon-construction-core.c16-glazing-type-range-nonfinite": "sha256:a8691b0d4c78800821ff352fcb73ce4484f09fd83580be4c6a5160540bc41e25",
    "dragon-construction-core.c17-no-mass-state": "sha256:011aed09008e53ed697e418cee557470af8bd1a4c4a689dee99311377f046534",
    "dragon-construction-core.c18-no-mass-mutation": "sha256:a58d875d86907abb5372837b59ce01c2bd4923df571ff2ed31442f1283d20d3b",
    "dragon-construction-core.c19-no-mass-type-range-nonfinite": "sha256:777e0788faf0e0cb6203383cfb01ac923e1fbfc5c01dda52a491224dbe30574d",
}
EXPECTED_CASE_SHA256 = {
    "dragon-construction-core.c01-roughness-topology-order-values": "sha256:c4266132669908f2b35052eb3d4a4ba333d3796a62fd576f454ceb248829d00f",
    "dragon-construction-core.c02-roughness-strings": "sha256:e8a8b21b8cef4ec3a89cdd0aa51f1d8b0236fd4ab33759d43926485af5759a8d",
    "dragon-construction-core.c03-roughness-construction-invalid": "sha256:ff6f58eee8b9ca7c78610b7b92641efb11d59d5859d7fb3e86daa2c8e7200fdd",
    "dragon-construction-core.c04-material-default-state": "sha256:2005e917e4967a1e6af70138e0ed268cb9ad00244218d652b62a6d54a938da15",
    "dragon-construction-core.c05-material-explicit-mutation": "sha256:faf71576b8a1f76975b625e7b77d17b4520255b3366776ffef46d171b2336669",
    "dragon-construction-core.c06-material-type-range-nonfinite": "sha256:cb4b04be8465462854c8a9414c0233a0a5b1329bdccd3c343735207ec82b4152",
    "dragon-construction-core.c07-layer-state-derived": "sha256:9e69e1f9ee1c7feb11b39ce4802ace7ded2623732f6bc5829141211279c4af77",
    "dragon-construction-core.c08-layer-mutation": "sha256:87610cde3897986bf93bd2e05b85b1e6f78c8beec4505847c7de8bedfa262c06",
    "dragon-construction-core.c09-layer-type-range-nonfinite": "sha256:9b427557c028aea7ae04536fc179936b3387241997e53c44f5913c8ade6ebbe6",
    "dragon-construction-core.c10-construction-layer-overload-metrics": "sha256:d21f55c56918210bfae8ad1aeffec91c76fda501795f6ec856be7d235e92ae37",
    "dragon-construction-core.c11-construction-material-thickness-overload": "sha256:fabc2f949175b596356b7983b8ebfb0897fbd90ff777678fb19f42bef46f9872",
    "dragon-construction-core.c12-construction-reverse-order-alias": "sha256:fe117dec40f6cbcedd539cb16c10d029c584f6064410aa686e84420190208a57",
    "dragon-construction-core.c13-construction-empty-mixed-mutation": "sha256:4cf7ad5509467ce6d504dafa364d7cbda063e8a340f1be9b0ab71c96b2aa1303",
    "dragon-construction-core.c14-glazing-state": "sha256:a5a26c5f1b8cc8704718e237e876d889385e61f64e5d30eed0bf57af46677f52",
    "dragon-construction-core.c15-glazing-mutation": "sha256:6b74c53539e27007370b612dcc3b562d069d20d7987700da5bbb93c795c9e077",
    "dragon-construction-core.c16-glazing-type-range-nonfinite": "sha256:f2e8731a88587006c2974afc69fcba756168d95772716a13ed66eb11e704e588",
    "dragon-construction-core.c17-no-mass-state": "sha256:c295176f998c7acf18f4258ad5dfb36726c8d13029a328da49bc1b66e48f7838",
    "dragon-construction-core.c18-no-mass-mutation": "sha256:e2904ef9a9a5c1a8a4885e083acebf8ab1a0af9c0447296cf0f775dfa3d85c0c",
    "dragon-construction-core.c19-no-mass-type-range-nonfinite": "sha256:a2fff3fb6496867fcadc7bb659bb682a64ca552b94888862d8040822c52c8a56",
}

UNRESOLVED_BOUNDARIES = (
    "arbitrary-descriptors-subclasses-proxies-and-monkeypatching-not-observed",
    "decimal-fraction-and-foreign-numeric-protocols-not-observed",
    "huge-integer-overflow-beyond-bounded-constructor-probes-not-observed",
    "all-nan-payloads-signed-zero-and-infinity-combinations-not-observed",
    "attribute-deletion-copy-pickle-and-reflection-bypass-not-observed",
    "concurrent-source-class-and-instance-mutation-not-observed",
    "equality-hash-representation-and-idf-emission-covered-by-separate-slices",
)


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_construction_air_boundary_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_construction_core_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load construction core support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_SOURCE_AST_SHA256 != EXPECTED_SOURCE_AST_SHA256
    ):
        raise RuntimeError("Construction core support is not exactly pinned.")
    return module


AIR_SUPPORT = _load_core_support()
CORE = AIR_SUPPORT.CORE
SUPPORT = AIR_SUPPORT.SUPPORT
SOURCE_RECEIPTS = AIR_SUPPORT.SOURCE_RECEIPTS
EXPECTED_DEPENDENCIES = AIR_SUPPORT.EXPECTED_DEPENDENCIES
strict_json_dumps = AIR_SUPPORT.strict_json_dumps
canonical_sha256 = AIR_SUPPORT.canonical_sha256
sha256_file = AIR_SUPPORT.sha256_file
load_json_without_duplicates = AIR_SUPPORT.load_json_without_duplicates
RAW_ADDRESS_PATTERN = AIR_SUPPORT.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = AIR_SUPPORT.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = AIR_SUPPORT.GUID_PATTERN
TIMESTAMP_PATTERN = AIR_SUPPORT.TIMESTAMP_PATTERN

SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == SOURCE_PATH else (),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    expected = {item["symbol"]: _descriptor(item) for item in TARGET_RECEIPTS}
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(helper, name) for name in names}
    try:
        helper.SOURCE_PATH = source["path"]
        helper.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        helper.EXPECTED_SYMBOL_HASHES = {
            symbol: expected[symbol]["symbol_hash"] for symbol in source["symbols"]
        }
        helper.TARGET_SYMBOLS = tuple(source["symbols"])
        result = helper.load_exact_inventory(path, commit)
    finally:
        for name, value in original.items():
            setattr(helper, name, value)
    expected_file = {
        "ast_hash": source["ast_sha256"],
        "content_hash": source["source_sha256"],
        "path": source["path"],
    }
    expected_symbols = [expected[symbol] for symbol in source["symbols"]]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def _exclusion_reason(index: int) -> str:
    if index in (590, 591, 606, 607, 638, 639):
        return "out-of-scope-representation-not-retargeted"
    if index in (592, 601, 608, 617, 640):
        return "resolved-idf-emission-not-retargeted"
    if index in (595, 596, 611, 612, 619):
        return "resolved-equality-hash-not-retargeted"
    if index in (588, 589):
        return "resolved-air-boundary-core-not-retargeted"
    raise RuntimeError(f"Unknown construction exclusion index: {index}")


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    if path.stat().st_size != EXPECTED_INVENTORY_FILE_BYTES:
        raise SystemExit("The public-symbol inventory byte length is not pinned.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash is not pinned.")
    raw = load_json_without_duplicates(path)
    inventories = [
        _load_source_inventory(path, commit, source) for source in SOURCE_SPECS
    ]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in inventories):
        raise SystemExit("The public-symbol inventory content hash is not exact.")

    for receipt in TARGET_RECEIPTS:
        observed = {
            **raw["symbols"][receipt["inventory_index"]],
            "inventory_index": receipt["inventory_index"],
        }
        if observed != receipt:
            raise SystemExit(
                f"Exact indexed construction target drifted: {receipt['symbol']}."
            )

    exclusions = []
    for index, symbol in ADJACENT_EXCLUSION_IDENTITIES:
        observed = raw["symbols"][index]
        if observed["symbol"] != symbol or observed["path"] != SOURCE_PATH:
            raise SystemExit(f"Adjacent construction exclusion drifted at index {index}.")
        exclusions.append(
            {
                **observed,
                "inventory_index": index,
                "reason": _exclusion_reason(index),
            }
        )
    return {
        "adjacent_exclusions": exclusions,
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in inventories],
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": [dict(item) for item in TARGET_RECEIPTS],
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "context_symbols": list(context),
            "executor": "dragon-construction-core",
            "expected_dotnet": {
                "adaptations": sorted({ADAPTATIONS[symbol] for symbol in targets}),
                "classifications": {
                    symbol: CLASSIFICATIONS[symbol] for symbol in targets
                },
                "outcome": "adapted-or-equivalent-as-pinned",
            },
            "id": PREFIX + slug,
            "scenario": scenario,
            "subfamily": subfamily,
            "target_symbols": list(targets),
        }
        for slug, scenario, subfamily, targets, context in CASE_SPECS
    )


def _encode(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, int):
        return {"kind": "int", "value": str(value)}
    if isinstance(value, float):
        if math.isnan(value):
            return {"kind": "float-nonfinite", "value": "nan"}
        if math.isinf(value):
            return {
                "kind": "float-nonfinite",
                "value": "positive-infinity" if value > 0 else "negative-infinity",
            }
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if isinstance(value, str):
        return {"kind": "str", "value": value}
    if isinstance(value, (list, tuple)):
        return {
            "items": [_encode(item) for item in value],
            "kind": "list" if isinstance(value, list) else "tuple",
        }
    if isinstance(value, dict):
        return {
            "items": [
                {"key": _encode(key), "value": _encode(item)}
                for key, item in value.items()
            ],
            "kind": "dict",
        }
    raise RuntimeError(f"Unsupported construction fact value: {type(value).__name__}")


def _typed(value: Any) -> dict[str, Any]:
    return {"runtime_type": type(value).__name__, "value": _encode(value)}


def _error(error: Exception) -> dict[str, Any]:
    return {
        "args": [_encode(item) for item in error.args],
        "message": str(error),
        "type": type(error).__name__,
    }


def _event(call: Callable[[], Any], phase: str) -> tuple[dict[str, Any], Any]:
    try:
        value = call()
    except Exception as error:
        return ({"error": _error(error), "outcome": "raised", "phase": phase}, None)
    return (
        {
            "outcome": "returned",
            "phase": phase,
            "return_type": type(value).__name__,
            "returned_none": value is None,
        },
        value,
    )


def _assign(value: Any, attribute: str, replacement: Any) -> None:
    setattr(value, attribute, replacement)


def _roughness_state(value: Any) -> dict[str, Any]:
    return {
        "is_str": isinstance(value, str),
        "name": value.name,
        "runtime_type": type(value).__name__,
        "string": str(value),
        "value": value.value,
    }


def _material_state(value: Any) -> dict[str, Any]:
    return {
        "attribute_names": sorted(vars(value)),
        "conductivity": _typed(value.conductivity),
        "density": _typed(value.density),
        "name": _typed(value.name),
        "roughness": _roughness_state(value.roughness),
        "runtime_type": type(value).__name__,
        "solar_absorptance": _typed(value.solar_absorptance),
        "specific_heat": _typed(value.specific_heat),
        "thermal_absorptance": _typed(value.thermal_absorptance),
        "visible_absorptance": _typed(value.visible_absorptance),
    }


def _layer_state(value: Any) -> dict[str, Any]:
    return {
        "U": _typed(value.U),
        "attribute_names": sorted(vars(value)),
        "heat_capacity": _typed(value.heat_capacity),
        "material": _material_state(value.material),
        "name": _typed(value.name),
        "runtime_type": type(value).__name__,
        "thickness": _typed(value.thickness),
    }


def _construction_state(value: Any, include_metrics: bool = True) -> dict[str, Any]:
    result = {
        "attribute_names": sorted(vars(value)),
        "layer_names": [_typed(layer.name) for layer in value.layers],
        "layer_states": [_layer_state(layer) for layer in value.layers],
        "layers_runtime_type": type(value.layers).__name__,
        "name": _typed(value.name),
        "runtime_type": type(value).__name__,
    }
    if include_metrics:
        result.update(
            {
                "U": _typed(value.U),
                "heat_capacity": _typed(value.heat_capacity),
                "thickness": _typed(value.thickness),
            }
        )
    return result


def _glazing_state(value: Any) -> dict[str, Any]:
    return {
        "G": _typed(value.G),
        "U": _typed(value.U),
        "attribute_names": sorted(vars(value)),
        "name": _typed(value.name),
        "runtime_type": type(value).__name__,
    }


def _no_mass_state(value: Any) -> dict[str, Any]:
    return {
        "U": _typed(value.U),
        "attribute_names": sorted(vars(value)),
        "name": _typed(value.name),
        "runtime_type": type(value).__name__,
    }


def _fact(
    scenario: str,
    subfamily: str,
    observations: dict[str, Any],
    source_state: dict[str, Any],
    timeline: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "observations": observations,
        "scenario": scenario,
        "source_state": source_state,
        "subfamily": subfamily,
        "timeline": timeline,
    }


def _material(construction: Any, name: Any = "Base") -> Any:
    return construction.Material(name, 0.03, 1000.0, 100.0)


def _setter_probes(
    factory: Callable[[], Any], attribute: str, values: tuple[Any, ...]
) -> list[dict[str, Any]]:
    result = []
    for index, probe in enumerate(values):
        value = factory()
        event, _ = _event(
            lambda value=value, probe=probe: _assign(value, attribute, probe),
            f"set-{attribute}-{index:02d}",
        )
        item = {"event": event, "input": _typed(probe)}
        if event["outcome"] == "returned":
            item["stored"] = _typed(getattr(value, attribute))
        result.append(item)
    return result


def _c01(construction: Any) -> dict[str, Any]:
    members = list(construction.MaterialRoughness)
    return _fact(
        "C01",
        "roughness",
        {
            "class_is_str_subclass": issubclass(construction.MaterialRoughness, str),
            "member_count": len(members),
            "members": [_roughness_state(member) for member in members],
        },
        {"member_names_in_iteration_order": [member.name for member in members]},
        [{"outcome": "returned", "phase": "iterate-members", "return_type": "list", "returned_none": False}],
    )


def _c02(construction: Any) -> dict[str, Any]:
    members = list(construction.MaterialRoughness)
    strings = [str(member) for member in members]
    return _fact(
        "C02",
        "roughness",
        {
            "formatted": [f"<{member}>" for member in members],
            "joined": "|".join(members),
            "strings": strings,
            "values_equal_strings": [member == str(member) for member in members],
        },
        {"members": [_roughness_state(member) for member in members]},
        [{"outcome": "returned", "phase": "convert-all-to-strings", "return_type": "list", "returned_none": False}],
    )


def _c03(construction: Any) -> dict[str, Any]:
    probes = (
        "VeryRough", "Rough", "MediumRough", "MediumSmooth", "Smooth",
        construction.MaterialRoughness.ROUGH, "veryrough", " Rough ", "", None, 1, True,
    )
    results = []
    timeline = []
    for index, probe in enumerate(probes):
        event, value = _event(
            lambda probe=probe: construction.MaterialRoughness(probe),
            f"construct-enum-{index:02d}",
        )
        timeline.append(event)
        item = {"input": _typed(probe), "outcome": event["outcome"]}
        if value is not None:
            item["member"] = _roughness_state(value)
            item["same_identity_as_input"] = value is probe
        else:
            item["error"] = event["error"]
        results.append(item)
    return _fact("C03", "roughness", {"probes": results}, {"probe_count": len(probes)}, timeline)


def _c04(construction: Any) -> dict[str, Any]:
    event, material = _event(
        lambda: construction.Material("Default", 0.72, 1920, 840),
        "construct-default-material",
    )
    if material is None:
        raise RuntimeError("C04 material construction unexpectedly failed.")
    state = _material_state(material)
    return _fact("C04", "material", {"material": state}, {"after_constructor": state}, [event])


def _c05(construction: Any) -> dict[str, Any]:
    material = construction.Material(
        "Explicit", 0.5, 900, 700,
        thermal_absorptance=0.8,
        solar_absorptance=0.6,
        visible_absorptance=0.4,
        roughness="Smooth",
    )
    snapshots = [{"phase": "initial", "state": _material_state(material)}]
    timeline = []
    mutations = (
        ("name", ["mutable", "name"]),
        ("roughness", "MediumRough"),
        ("conductivity", 0.75),
        ("density", 1200),
        ("specific_heat", 950.0),
        ("thermal_absorptance", 0.1),
        ("solar_absorptance", 0.2),
        ("visible_absorptance", 0.3),
    )
    for attribute, replacement in mutations:
        event, _ = _event(
            lambda attribute=attribute, replacement=replacement: _assign(
                material, attribute, replacement
            ),
            "mutate-" + attribute,
        )
        timeline.append(event)
        snapshots.append({"phase": "after-" + attribute, "state": _material_state(material)})
    return _fact(
        "C05", "material", {"final": _material_state(material)},
        {"snapshots": snapshots}, timeline,
    )


def _c06(construction: Any) -> dict[str, Any]:
    factory = lambda: _material(construction)
    probes = {
        "conductivity": _setter_probes(factory, "conductivity", (True, float("nan"), float("inf"), 0, -1, "bad")),
        "density": _setter_probes(factory, "density", (True, float("nan"), float("inf"), 0, "bad")),
        "specific_heat": _setter_probes(factory, "specific_heat", (100, True, float("nan"), float("inf"), 99, "bad")),
        "thermal_absorptance": _setter_probes(factory, "thermal_absorptance", (0, 1, True, float("nan"), -0.1, 1.1, float("inf"), "bad")),
        "solar_absorptance": _setter_probes(factory, "solar_absorptance", (float("nan"), 1.25, 0.25)),
        "visible_absorptance": _setter_probes(factory, "visible_absorptance", (float("nan"), -0.25, 0.25)),
    }
    event, null_name = _event(
        lambda: construction.Material(None, 0.03, 1000, 100),
        "construct-null-name",
    )
    timeline = [event] + [item["event"] for values in probes.values() for item in values]
    return _fact(
        "C06", "material",
        {"null_name_state": _material_state(null_name), "setter_probes": probes},
        {"fresh_baseline": _material_state(factory())}, timeline,
    )


def _c07(construction: Any) -> dict[str, Any]:
    material = _material(construction, "Thermal")
    event, layer = _event(
        lambda: construction.Layer("Thermal_1mm", material, 0.001),
        "construct-layer",
    )
    if layer is None:
        raise RuntimeError("C07 layer construction unexpectedly failed.")
    state = _layer_state(layer)
    return _fact(
        "C07", "layer",
        {"layer": state, "material_identity_retained": layer.material is material},
        {"material": _material_state(material)}, [event],
    )


def _c08(construction: Any) -> dict[str, Any]:
    first = _material(construction, "First")
    second = construction.Material("Second", 0.06, 2000, 200)
    layer = construction.Layer("Mutable", first, 0.001)
    snapshots = [{"phase": "initial", "state": _layer_state(layer)}]
    timeline = []
    for phase, call in (
        ("mutate-source-conductivity", lambda: _assign(first, "conductivity", 0.09)),
        ("replace-material", lambda: _assign(layer, "material", second)),
        ("replace-thickness", lambda: _assign(layer, "thickness", 0.002)),
        ("replace-name", lambda: _assign(layer, "name", ["layer", "name"])),
    ):
        event, _ = _event(call, phase)
        timeline.append(event)
        snapshots.append({"phase": "after-" + phase, "state": _layer_state(layer)})
    return _fact(
        "C08", "layer",
        {
            "final": _layer_state(layer),
            "material_is_first": layer.material is first,
            "material_is_second": layer.material is second,
        },
        {"snapshots": snapshots}, timeline,
    )


def _c09(construction: Any) -> dict[str, Any]:
    material = _material(construction)
    layer_factory = lambda: construction.Layer("Probe", material, 0.01)
    material_probes = _setter_probes(layer_factory, "material", (None, "bad"))
    thickness_probes = _setter_probes(
        layer_factory, "thickness", (True, float("nan"), float("inf"), 0, -1, "bad")
    )
    partial_material = construction.Layer.__new__(construction.Layer)
    partial_material_event, _ = _event(
        lambda: construction.Layer.__init__(partial_material, "Partial", None, 0.01),
        "partial-invalid-material",
    )
    partial_thickness = construction.Layer.__new__(construction.Layer)
    partial_thickness_event, _ = _event(
        lambda: construction.Layer.__init__(partial_thickness, "Partial", material, 0),
        "partial-invalid-thickness",
    )
    partial_states = {
        "invalid_material": {
            "attribute_names": sorted(vars(partial_material)),
            "name": _typed(partial_material.name),
        },
        "invalid_thickness": {
            "attribute_names": sorted(vars(partial_thickness)),
            "material_is_input": partial_thickness.material is material,
            "name": _typed(partial_thickness.name),
        },
    }
    timeline = (
        [item["event"] for item in material_probes]
        + [item["event"] for item in thickness_probes]
        + [partial_material_event, partial_thickness_event]
    )
    return _fact(
        "C09", "layer",
        {"material_probes": material_probes, "thickness_probes": thickness_probes},
        {"partial_constructor_states": partial_states}, timeline,
    )


def _ulp_layers(construction: Any) -> tuple[Any, Any]:
    material = _material(construction, "ULP")
    return (
        construction.Layer("Outside", material, 0.001),
        construction.Layer("Inside", material, 0.01),
    )


def _c10(construction: Any) -> dict[str, Any]:
    outside, inside = _ulp_layers(construction)
    event, value = _event(
        lambda: construction.Construction("Layered", outside, inside),
        "construct-layer-overload",
    )
    if value is None:
        raise RuntimeError("C10 construction unexpectedly failed.")
    state = _construction_state(value)
    return _fact(
        "C10", "construction",
        {
            "construction": state,
            "input_identity_order": [value.layers[0] is outside, value.layers[1] is inside],
            "ulp_witness_U": _typed(value.U),
        },
        {"input_layers": [_layer_state(outside), _layer_state(inside)]}, [event],
    )


def _c11(construction: Any) -> dict[str, Any]:
    first = _material(construction, "First")
    second = _material(construction, "Second")
    event, value = _event(
        lambda: construction.Construction("Pairs", first, 0.001, second, 0.01),
        "construct-material-thickness-overload",
    )
    bool_event, bool_value = _event(
        lambda: construction.Construction("BoolThickness", first, True),
        "construct-bool-thickness",
    )
    if value is None or bool_value is None:
        raise RuntimeError("C11 construction unexpectedly failed.")
    return _fact(
        "C11", "construction",
        {
            "bool_thickness": _construction_state(bool_value),
            "constructed": _construction_state(value),
            "generated_layer_names": [layer.name for layer in value.layers],
            "material_identity_order": [value.layers[0].material is first, value.layers[1].material is second],
        },
        {"input_materials": [_material_state(first), _material_state(second)]},
        [event, bool_event],
    )


def _c12(construction: Any) -> dict[str, Any]:
    outside, inside = _ulp_layers(construction)
    original = construction.Construction("Original", outside, inside)
    default_event, default = _event(original.reversed, "reverse-default-name")
    custom_event, custom = _event(lambda: original.reversed(""), "reverse-empty-custom-name")
    if default is None or custom is None:
        raise RuntimeError("C12 reverse unexpectedly failed.")
    snapshots = [{"phase": "before-shared-mutation", "original": _construction_state(original), "reversed": _construction_state(default)}]
    mutation_event, _ = _event(
        lambda: _assign(outside, "thickness", 0.002),
        "mutate-shared-outside-layer",
    )
    snapshots.append({"phase": "after-shared-mutation", "original": _construction_state(original), "reversed": _construction_state(default)})
    return _fact(
        "C12", "construction",
        {
            "custom_name": _typed(custom.name),
            "default_name": _typed(default.name),
            "reversed_identity_order": [default.layers[0] is inside, default.layers[1] is outside],
            "shares_every_layer": all(any(layer is candidate for candidate in original.layers) for layer in default.layers),
        },
        {"snapshots": snapshots}, [default_event, custom_event, mutation_event],
    )


def _c13(construction: Any) -> dict[str, Any]:
    empty_event, empty = _event(
        lambda: construction.Construction(None), "construct-empty-null-name"
    )
    if empty is None:
        raise RuntimeError("C13 empty construction unexpectedly failed.")
    thickness_event, thickness = _event(lambda: empty.thickness, "empty-thickness")
    capacity_event, capacity = _event(lambda: empty.heat_capacity, "empty-heat-capacity")
    u_event, _ = _event(lambda: empty.U, "empty-u")
    mixed_event, _ = _event(
        lambda: construction.Construction("Mixed", 1, "bad"), "construct-mixed-even"
    )
    odd_event, _ = _event(
        lambda: construction.Construction("Odd", 1), "construct-odd"
    )
    outside, _ = _ulp_layers(construction)
    mutable = construction.Construction("Mutable", outside)
    before = _construction_state(mutable)
    append_event, _ = _event(lambda: mutable.layers.append("bad"), "append-invalid-layer")
    metric_event, _ = _event(lambda: mutable.thickness, "metric-after-invalid-append")
    replace_event, _ = _event(lambda: _assign(mutable, "layers", []), "replace-layers-empty")
    name_event, _ = _event(lambda: _assign(mutable, "name", None), "replace-name-null")
    return _fact(
        "C13", "construction",
        {
            "empty_heat_capacity": _typed(capacity),
            "empty_thickness": _typed(thickness),
            "empty_without_metrics": _construction_state(empty, include_metrics=False),
            "mutable_after_replacement": _construction_state(mutable, include_metrics=False),
        },
        {"mutable_before": before},
        [empty_event, thickness_event, capacity_event, u_event, mixed_event, odd_event, append_event, metric_event, replace_event, name_event],
    )


def _c14(construction: Any) -> dict[str, Any]:
    event, glazing = _event(
        lambda: construction.Glazing("Window", 1.6, 0.55), "construct-glazing"
    )
    if glazing is None:
        raise RuntimeError("C14 glazing unexpectedly failed.")
    state = _glazing_state(glazing)
    return _fact("C14", "glazing", {"glazing": state}, {"after_constructor": state}, [event])


def _c15(construction: Any) -> dict[str, Any]:
    glazing = construction.Glazing("Mutable", 1.6, 0.55)
    snapshots = [{"phase": "initial", "state": _glazing_state(glazing)}]
    timeline = []
    for attribute, replacement in (("name", ["mutable"]), ("U", 2.2), ("G", 1.25)):
        event, _ = _event(
            lambda attribute=attribute, replacement=replacement: _assign(glazing, attribute, replacement),
            "mutate-" + attribute,
        )
        timeline.append(event)
        snapshots.append({"phase": "after-" + attribute, "state": _glazing_state(glazing)})
    return _fact("C15", "glazing", {"final": _glazing_state(glazing)}, {"snapshots": snapshots}, timeline)


def _c16(construction: Any) -> dict[str, Any]:
    factory = lambda: construction.Glazing("Probe", 1.6, 0.55)
    probes = {
        "U": _setter_probes(factory, "U", (True, float("nan"), float("inf"), 0, -1, "bad")),
        "G": _setter_probes(factory, "G", (True, float("nan"), float("inf"), 1.25, 1, 0, -1, "bad")),
    }
    partial = construction.Glazing.__new__(construction.Glazing)
    partial_event, _ = _event(
        lambda: construction.Glazing.__init__(partial, "Partial", 1.6, 0),
        "partial-invalid-g",
    )
    return _fact(
        "C16", "glazing", {"setter_probes": probes},
        {"partial_invalid_g": {"attribute_names": sorted(vars(partial)), "U": _typed(partial.U), "name": _typed(partial.name)}},
        [item["event"] for values in probes.values() for item in values] + [partial_event],
    )


def _c17(construction: Any) -> dict[str, Any]:
    event, value = _event(
        lambda: construction.NoMassConstruction("NoMass", 2.5),
        "construct-no-mass",
    )
    if value is None:
        raise RuntimeError("C17 no-mass construction unexpectedly failed.")
    state = _no_mass_state(value)
    return _fact("C17", "no-mass", {"construction": state}, {"after_constructor": state}, [event])


def _c18(construction: Any) -> dict[str, Any]:
    value = construction.NoMassConstruction("Mutable", 2.5)
    snapshots = [{"phase": "initial", "state": _no_mass_state(value)}]
    timeline = []
    for attribute, replacement in (("name", ["mutable"]), ("U", 3.5)):
        event, _ = _event(
            lambda attribute=attribute, replacement=replacement: _assign(value, attribute, replacement),
            "mutate-" + attribute,
        )
        timeline.append(event)
        snapshots.append({"phase": "after-" + attribute, "state": _no_mass_state(value)})
    return _fact("C18", "no-mass", {"final": _no_mass_state(value)}, {"snapshots": snapshots}, timeline)


def _c19(construction: Any) -> dict[str, Any]:
    factory = lambda: construction.NoMassConstruction("Probe", 2.5)
    probes = _setter_probes(factory, "U", (True, float("nan"), float("inf"), 0, -1, "bad"))
    partial = construction.NoMassConstruction.__new__(construction.NoMassConstruction)
    partial_event, _ = _event(
        lambda: construction.NoMassConstruction.__init__(partial, "Partial", 0),
        "partial-invalid-u",
    )
    null_name_event, null_name = _event(
        lambda: construction.NoMassConstruction(None, 2.5),
        "construct-null-name",
    )
    return _fact(
        "C19", "no-mass",
        {"null_name": _no_mass_state(null_name), "setter_probes": probes},
        {"partial_invalid_u": {"attribute_names": sorted(vars(partial)), "name": _typed(partial.name)}},
        [item["event"] for item in probes] + [partial_event, null_name_event],
    )


CASE_EXECUTORS = (_c01, _c02, _c03, _c04, _c05, _c06, _c07, _c08, _c09, _c10, _c11, _c12, _c13, _c14, _c15, _c16, _c17, _c18, _c19)


def _execute_case(identifier: str, construction: Any) -> dict[str, Any]:
    try:
        index = EXPECTED_CASE_IDS.index(identifier)
    except ValueError as error:
        raise RuntimeError(f"Unknown construction core case: {identifier}") from error
    return CASE_EXECUTORS[index](construction)


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {item["id"]: canonical_sha256(item) for item in cases}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _expected_loaded_local_modules() -> list[dict[str, str]]:
    return [
        {
            "ast_sha256": source["ast_sha256"],
            "module": _module_name(source["path"]),
            "path": source["path"],
            "source_sha256": source["source_sha256"],
        }
        for source in SOURCE_SPECS
    ]


def _coverage_by_symbol() -> dict[str, list[str]]:
    result = {symbol: [] for symbol in TARGET_SYMBOLS}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol].append(definition["id"])
    return result


def _coverage_by_subfamily() -> dict[str, list[str]]:
    return {
        subfamily: [
            definition["id"]
            for definition in case_definitions()
            if definition["subfamily"] == subfamily
        ]
        for subfamily in ("roughness", "material", "layer", "construction", "glazing", "no-mass")
    }


def _expected_exclusion_contract() -> list[dict[str, Any]]:
    return [
        {"inventory_index": index, "reason": _exclusion_reason(index), "symbol": symbol}
        for index, symbol in ADJACENT_EXCLUSION_IDENTITIES
    ]


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "Eleven read-only formulas and roughness member/string mappings are direct native equivalents. "
            "The remaining twenty-four symbols require explicit adaptations because pinned Python accepts "
            "mutable names and collections, aliases mutable child objects, permits bool and nonfinite values "
            "through several validators, has distinct validation order and error timing, accepts empty "
            "constructions, and models roughness as a string Enum rather than a strongly typed native enum."
        ),
        "classification_counts": {"equivalent": 11, "exception": 24},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "adjacent_exclusions": _expected_exclusion_contract(),
            "case_coverage_by_subfamily": _coverage_by_subfamily(),
            "case_coverage_by_symbol": _coverage_by_symbol(),
            "full_construction_family_closure": False,
            "full_symbol_closure": False,
            "scope": "exact-nineteen-case-thirty-five-target-construction-core-matrix",
            "target_coverage_complete": True,
            "target_symbols": list(TARGET_SYMBOLS),
            "unresolved_boundaries": list(UNRESOLVED_BOUNDARIES),
        },
        "equivalent_symbols": list(EQUIVALENT_SYMBOLS),
        "evidence_contract": {
            "expected_receipt_count": len(TARGET_SYMBOLS),
            "full_idf_closure": False,
            "structural_only": False,
        },
        "identity_encoding": "stable-direct-is-relations-only-no-id-or-address",
        "native_binding_status": "proposed-not-yet-cross-language-verified",
        "native_targets": NATIVE_TARGETS,
        "raw_fact_encoding": "typed-scalars-exact-float-hex-recursive-state-and-phase-bound-errors",
        "runtime_signatures": RUNTIME_SIGNATURES,
        "source_import_policy": "external-temporary-copy-with-complete-loaded-local-module-audit",
        "target_receipts": [dict(item) for item in TARGET_RECEIPTS],
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _dependencies() -> dict[str, str]:
    result = {}
    for distribution in EXPECTED_DEPENDENCIES:
        try:
            result[distribution] = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError as error:
            raise RuntimeError(f"Required reference dependency is missing: {distribution}") from error
    return result


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _expected_upstream(adjacent_exclusions: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "adjacent_exclusions": adjacent_exclusions,
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "construction_source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
        "inventory_file": {
            "bytes": EXPECTED_INVENTORY_FILE_BYTES,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
        "sources": [
            {
                "ast_sha256": source["ast_sha256"],
                "path": source["path"],
                "source_sha256": source["source_sha256"],
            }
            for source in SOURCE_SPECS
        ],
    }


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        if all(
            _source_file(root, source).is_file()
            and sha256_file(_source_file(root, source)) == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def _resolve_symbol(construction: Any, symbol: str) -> Any:
    return functools.reduce(getattr, symbol.split("."), construction)


def _runtime_signature(value: Any, construction: Any) -> str:
    if isinstance(value, property):
        result = "property:fget=" + str(inspect.signature(value.fget))
        if value.fset is not None:
            result += ";fset=" + str(inspect.signature(value.fset))
        return result
    if isinstance(value, construction.MaterialRoughness):
        return "enum-member:" + repr(value.value)
    return str(inspect.signature(value))


def _runtime_signatures(construction: Any) -> dict[str, str]:
    return {
        symbol: _runtime_signature(_resolve_symbol(construction, symbol), construction)
        for symbol in TARGET_SYMBOLS
    }


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    _require_keys(facts, {"observations", "scenario", "source_state", "subfamily", "timeline"}, f"facts {identifier}")
    index = EXPECTED_CASE_IDS.index(identifier)
    spec = CASE_SPECS[index]
    if facts["scenario"] != spec[1] or facts["subfamily"] != spec[2]:
        raise RuntimeError(f"Construction case identity drifted: {identifier}")
    if not isinstance(facts["observations"], dict) or not isinstance(facts["source_state"], dict):
        raise RuntimeError(f"Construction case state shape drifted: {identifier}")
    if not isinstance(facts["timeline"], list) or not facts["timeline"]:
        raise RuntimeError(f"Construction timeline drifted: {identifier}")
    for event in facts["timeline"]:
        if event.get("outcome") not in {"raised", "returned"} or not isinstance(event.get("phase"), str):
            raise RuntimeError(f"Construction event drifted: {identifier}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    source_file = imported_root / Path(SOURCE_PATH).relative_to("src")
    if source_file.stat().st_size != EXPECTED_SOURCE_BYTES:
        raise SystemExit("Pinned construction.py byte length drifted.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        construction = importlib.import_module("idragon.dragon.construction")
        signatures = _runtime_signatures(construction)
        if RUNTIME_SIGNATURES and signatures != RUNTIME_SIGNATURES:
            raise SystemExit(
                "Pinned construction runtime signatures drifted.\nOBSERVED_SIGNATURES\n"
                + strict_json_dumps(signatures, indent=2)
            )
        observed = {
            definition["id"]: _execute_case(definition["id"], construction)
            for definition in case_definitions()
        }
        fact_hashes = {identifier: canonical_sha256(facts) for identifier, facts in observed.items()}
        if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned construction per-case facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(fact_hashes, indent=2)
            )
        cases = []
        for definition in case_definitions():
            identifier = definition["id"]
            facts = observed[identifier]
            _validate_case_facts(identifier, facts)
            case = dict(definition)
            case["python"] = {"facts": facts, "facts_sha256": fact_hashes[identifier], "outcome": "observed"}
            cases.append(case)
        case_hashes = case_sha256(cases)
        if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
            raise SystemExit(
                "Pinned construction per-case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        result = {
            "case_sha256": case_hashes,
            "cases": cases,
            "cases_sha256": cases_sha256(cases),
            "consumer_contract": _expected_consumer_contract(),
            "fact_sha256": fact_hashes,
            "runtime": {
                "dependencies": _dependencies(),
                "implementation": sys.implementation.name,
                "python_dont_write_bytecode": sys.dont_write_bytecode,
                "python_hash_algorithm": sys.hash_info.algorithm,
                "python_hash_seed": 0,
                "python_hash_width_bits": sys.hash_info.width,
                "python_version": ".".join(map(str, sys.version_info[:3])),
            },
            "schema": SCHEMA,
            "symbols": inventory["symbols"],
            "target_receipts": inventory["target_receipts"],
            "upstream": {
                **_expected_upstream(inventory["adjacent_exclusions"]),
                "loaded_local_modules": modules.loaded_local_modules,
                "sources": [
                    {
                        "ast_sha256": source["ast_sha256"],
                        "path": source["path"],
                        "source_sha256": sha256_file(_source_file(imported_root, source)),
                    }
                    for source in SOURCE_SPECS
                ],
            },
        }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _validate_safe_string(value: str, location: str) -> None:
    if ABSOLUTE_PATH_PATTERN.search(value):
        raise RuntimeError(f"Absolute path is forbidden at {location}.")
    if RAW_ADDRESS_PATTERN.search(value):
        raise RuntimeError(f"Raw address is forbidden at {location}.")
    if GUID_PATTERN.search(value):
        raise RuntimeError(f"GUID-like value is forbidden at {location}.")
    if TIMESTAMP_PATTERN.search(value):
        raise RuntimeError(f"Timestamp is forbidden at {location}.")


ENCODED_KINDS = frozenset(
    {"bool", "dict", "float", "float-nonfinite", "int", "list", "none", "str", "tuple"}
)
SOURCE_RECEIPT_KINDS = frozenset({"class", "constant", "function"})


def _validate_encoded(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind == "none":
        _require_keys(value, {"kind"}, location)
        return True
    if kind == "bool":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], bool):
            raise RuntimeError(f"Invalid encoded bool at {location}.")
        return True
    if kind == "int":
        _require_keys(value, {"kind", "value"}, location)
        try:
            if str(int(value["value"])) != value["value"]:
                raise ValueError
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded int at {location}.") from error
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], str):
            raise RuntimeError(f"Invalid encoded string at {location}.")
        _validate_safe_string(value["value"], f"{location}.value")
        return True
    if kind == "float":
        _require_keys(value, {"hex", "kind", "repr"}, location)
        try:
            decoded = float.fromhex(value["hex"])
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded float at {location}.") from error
        if not math.isfinite(decoded) or decoded.hex() != value["hex"] or repr(decoded) != value["repr"]:
            raise RuntimeError(f"Unsafe encoded float at {location}.")
        return True
    if kind == "float-nonfinite":
        _require_keys(value, {"kind", "value"}, location)
        if value["value"] not in {"nan", "negative-infinity", "positive-infinity"}:
            raise RuntimeError(f"Invalid encoded nonfinite value at {location}.")
        return True
    if kind in {"list", "tuple"}:
        _require_keys(value, {"items", "kind"}, location)
        if not isinstance(value["items"], list):
            raise RuntimeError(f"Invalid encoded sequence at {location}.")
        for index, item in enumerate(value["items"]):
            if not isinstance(item, dict) or not _validate_encoded(item, f"{location}[{index}]"):
                raise RuntimeError(f"Invalid encoded sequence item at {location}.")
        return True
    if kind == "dict":
        _require_keys(value, {"items", "kind"}, location)
        if not isinstance(value["items"], list):
            raise RuntimeError(f"Invalid encoded dict at {location}.")
        encoded_keys: set[str] = set()
        for index, item in enumerate(value["items"]):
            _require_keys(item, {"key", "value"}, f"{location}[{index}]")
            if not _validate_encoded(item["key"], f"{location}[{index}].key"):
                raise RuntimeError(f"Invalid encoded dict key at {location}.")
            encoded_key = canonical_sha256(item["key"])
            if encoded_key in encoded_keys:
                raise RuntimeError(f"Duplicate encoded dict key at {location}.")
            encoded_keys.add(encoded_key)
            if not _validate_encoded(item["value"], f"{location}[{index}].value"):
                raise RuntimeError(f"Invalid encoded dict value at {location}.")
        return True
    return False


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
        raise RuntimeError(f"Raw float is forbidden at {location}.")
    if isinstance(value, str):
        _validate_safe_string(value, location)
        return
    if value is None or isinstance(value, (bool, int)):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        if "kind" in value:
            kind = value["kind"]
            if kind in ENCODED_KINDS:
                if not _validate_encoded(value, location):
                    raise RuntimeError(f"Invalid encoded value at {location}.")
                return
            if kind not in SOURCE_RECEIPT_KINDS:
                raise RuntimeError(f"Unknown encoded value kind at {location}.")
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string JSON key at {location}.")
            _validate_safe_tree(key, f"{location}.<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsupported JSON value at {location}: {type(value).__name__}")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {"case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256", "runtime", "schema", "symbols", "target_receipts", "upstream"},
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Construction core schema drifted.")
    _validate_safe_tree(value)
    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Construction case order/count drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    fact_hashes = {}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Construction case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Construction Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Construction inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Construction fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Construction expected fact hashes drifted.")
    if value["case_sha256"] != case_sha256(cases):
        raise RuntimeError("Construction per-case hash map drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Construction expected case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Construction aggregate cases hash drifted.")

    target_counts = Counter(symbol for definition in definitions for symbol in definition["target_symbols"])
    if set(target_counts) != set(TARGET_SYMBOLS) or any(count < 1 for count in target_counts.values()):
        raise RuntimeError("Construction target coverage drifted.")
    if set(EXCLUDED_SYMBOLS).intersection(target_counts):
        raise RuntimeError("Excluded construction symbols were retargeted.")
    if Counter(CLASSIFICATIONS.values()) != Counter({"exception": 24, "equivalent": 11}):
        raise RuntimeError("Construction classification counts drifted.")
    subfamilies = Counter(definition["subfamily"] for definition in definitions)
    if subfamilies != Counter({"roughness": 3, "material": 3, "layer": 3, "construction": 4, "glazing": 3, "no-mass": 3}):
        raise RuntimeError("Construction subfamily matrix drifted.")
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Construction consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Construction runtime pin drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Construction symbol descriptors drifted.")
    if value["target_receipts"] != [dict(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Construction indexed target receipts drifted.")
    upstream = value["upstream"]
    if upstream != _expected_upstream(upstream.get("adjacent_exclusions", [])):
        raise RuntimeError("Construction upstream receipts drifted.")
    observed_exclusions = [(item["inventory_index"], item["symbol"]) for item in upstream["adjacent_exclusions"]]
    if observed_exclusions != list(ADJACENT_EXCLUSION_IDENTITIES):
        raise RuntimeError("Construction adjacent exclusion identities drifted.")
    exclusion_hash = canonical_sha256(upstream["adjacent_exclusions"])
    if EXPECTED_ADJACENT_EXCLUSIONS_SHA256 and exclusion_hash != EXPECTED_ADJACENT_EXCLUSIONS_SHA256:
        raise RuntimeError("Construction adjacent exclusion receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(strict_json_dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote dragon construction core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
