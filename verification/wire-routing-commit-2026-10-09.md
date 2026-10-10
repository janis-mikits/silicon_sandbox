# Routing-only commit verification — 9 October 2026

Routing, exact geometry persistence, pin corridors, bounded failure handling, and their renderer dependencies were staged independently of the shared working tree. Three mixed files were partially staged using routing-only Git blobs: `OneBitWorldInteraction.cs`, `docs/building-and-interface.md`, and `docs/physical-connections.md`. No working copy of those files was overwritten. Shift-placement hunks and `ShiftPlacementPlayTests.cs` (including its meta file) are excluded.

## Independent verification

Base HEAD: `15d63375d7c47790d295c36a52f37d32bb660b33`.

Exported the index with `git checkout-index --all --prefix=<absolute snapshot directory>/` into `UnityProject/Temp/RoutingCommitCheck/`. This snapshot contained HEAD plus only the staged changes; no untracked or unstaged shared work was copied. From that directory, ran:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Exit 0: **222 Edit Mode and 30 Play Mode tests passed**, zero failures/skips/inconclusive results. macOS build, player startup, save/reopen, and exit-autosave checks passed. The Play Mode total includes the three jump tests already committed in the base checkout; it excludes the untracked Shift-placement suite. Windows and CI were not run. Logs/XML are in `UnityProject/Temp/RoutingCommitCheck/UnityProject/Logs/Verification/`.

After verification, every exported tracked file was compared against its staged Git blob: no differences. This report is the only subsequent staged addition. Reviewed the cached code/documentation diffs and confirmed no Shift-placement additions. `git diff --cached --check -- ':!*.meta'` and `git -c core.whitespace=-blank-at-eol diff --cached --check` passed. The ordinary full whitespace check reports only Unity-generated empty YAML values with trailing spaces in the new FBX metadata; these standard importer files were left intact.

## Required asset dependencies

The renderer uses the 0.125-cell wire meshes, 0.1375-cell pin, matching identity ring, and directional junction cores. Included the five changed pin/wire/junction/ring FBX files plus all 63 directional wire FBXs with their GUID/import metadata, resource references, and importer code. The complete family is required by `OneBitVisualArt.IsComplete` and verified for circular ports; runtime junction cores are selected by direction mask. Leaving these uncommitted would produce missing resources or mismatched geometry/colliders from HEAD. No lettering texture, Blender source, generator, overview image, art manifest, or art-review change is included. Existing HEAD art sources are not the regeneration source for this updated exported wire family; updated art-generation work remains separate in the shared checkout.

The routing safety budget can reject unusually expensive valid routes. See the feature and regression reports for functional limits. No demo worlds were deleted. No push is included in this operation.

## Exact staged files

174 files, including this report. Paths are repository-relative:

```text
INDEX.md
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_IdentityRing.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Junction.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Pin.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_WireElbow.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_WireStraight.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_01_E.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_01_E.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_02_W.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_02_W.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_03_EW.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_03_EW.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_04_U.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_04_U.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_05_EU.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_05_EU.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_06_WU.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_06_WU.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_07_EWU.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_07_EWU.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_08_D.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_08_D.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_09_ED.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_09_ED.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_10_WD.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_10_WD.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_11_EWD.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_11_EWD.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_12_UD.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_12_UD.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_13_EUD.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_13_EUD.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_14_WUD.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_14_WUD.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_15_EWUD.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_15_EWUD.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_16_N.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_16_N.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_17_EN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_17_EN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_18_WN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_18_WN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_19_EWN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_19_EWN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_20_UN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_20_UN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_21_EUN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_21_EUN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_22_WUN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_22_WUN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_23_EWUN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_23_EWUN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_24_DN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_24_DN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_25_EDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_25_EDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_26_WDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_26_WDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_27_EWDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_27_EWDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_28_UDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_28_UDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_29_EUDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_29_EUDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_30_WUDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_30_WUDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_31_EWUDN.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_31_EWUDN.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_32_S.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_32_S.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_33_ES.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_33_ES.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_34_WS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_34_WS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_35_EWS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_35_EWS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_36_US.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_36_US.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_37_EUS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_37_EUS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_38_WUS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_38_WUS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_39_EWUS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_39_EWUS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_40_DS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_40_DS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_41_EDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_41_EDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_42_WDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_42_WDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_43_EWDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_43_EWDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_44_UDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_44_UDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_45_EUDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_45_EUDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_46_WUDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_46_WUDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_47_EWUDS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_47_EWUDS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_48_NS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_48_NS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_49_ENS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_49_ENS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_50_WNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_50_WNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_51_EWNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_51_EWNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_52_UNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_52_UNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_53_EUNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_53_EUNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_54_WUNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_54_WUNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_55_EWUNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_55_EWUNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_56_DNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_56_DNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_57_EDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_57_EDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_58_WDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_58_WDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_59_EWDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_59_EWDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_60_UDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_60_UDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_61_EUDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_61_EUDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_62_WUDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_62_WUDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_63_EWUDNS.fbx
UnityProject/Assets/SiliconSandbox/Art/Meshes/SS_Wire_63_EWUDNS.fbx.meta
UnityProject/Assets/SiliconSandbox/Art/Resources/SiliconSandboxVisualArt.asset
UnityProject/Assets/SiliconSandbox/Core/Application/OneBitModuleSnapshotBuilder.cs
UnityProject/Assets/SiliconSandbox/Core/Application/OneBitPinRoutePlanner.cs
UnityProject/Assets/SiliconSandbox/Core/Application/OneBitTopologyEdits.cs
UnityProject/Assets/SiliconSandbox/Core/Application/OneBitWorldEdits.cs
UnityProject/Assets/SiliconSandbox/Core/Application/PinConnectionCorridors.cs
UnityProject/Assets/SiliconSandbox/Core/Application/PinConnectionCorridors.cs.meta
UnityProject/Assets/SiliconSandbox/Core/Application/QuarterWireRouter.cs
UnityProject/Assets/SiliconSandbox/Core/Application/QuarterWireRouter.cs.meta
UnityProject/Assets/SiliconSandbox/Core/Authoring/OneBitAuthoredTopology.cs
UnityProject/Assets/SiliconSandbox/Core/Graph/OneBitTopologyGraphBuilder.cs
UnityProject/Assets/SiliconSandbox/Core/Graph/OneBitVisualTopology.cs
UnityProject/Assets/SiliconSandbox/Core/Persistence/V1DesignJsonWriter.cs
UnityProject/Assets/SiliconSandbox/Core/Persistence/WorldManifestIntegrity.cs
UnityProject/Assets/SiliconSandbox/Core/Persistence/WorldManifestJsonReader.cs
UnityProject/Assets/SiliconSandbox/Core/Persistence/WorldManifestJsonWriter.cs
UnityProject/Assets/SiliconSandbox/Core/Persistence/WorldV1JsonReader.cs
UnityProject/Assets/SiliconSandbox/Editor/Build/FirstPlayableArtImport.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/OneBitPinRoutePlannerTests.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/OneBitWorldConnectorTests.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/OneBitWorldPlacementTests.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/PinConnectionCorridorTests.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/PinConnectionCorridorTests.cs.meta
UnityProject/Assets/SiliconSandbox/Tests/EditMode/WireRouteFailureTests.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/WireRouteFailureTests.cs.meta
UnityProject/Assets/SiliconSandbox/Tests/EditMode/WorldManifestJsonReaderTests.cs
UnityProject/Assets/SiliconSandbox/Tests/EditMode/WorldManifestJsonWriterTests.cs
UnityProject/Assets/SiliconSandbox/Tests/PlayMode/VisualArtPlayTests.cs
UnityProject/Assets/SiliconSandbox/Unity/Interaction/OneBitWorldInteraction.cs
UnityProject/Assets/SiliconSandbox/Unity/Presentation/OneBitVisualArt.cs
UnityProject/Assets/SiliconSandbox/Unity/Presentation/OneBitWorldView.cs
UnityProject/Assets/SiliconSandbox/Unity/Presentation/WireMeshGeometry.cs
UnityProject/Assets/SiliconSandbox/Unity/Presentation/WireMeshGeometry.cs.meta
docs/building-and-interface.md
docs/first-playable-data-schema.md
docs/physical-connections.md
docs/saving-and-recovery.md
first-playable-requirement-test-matrix.md
verification/pin-connection-corridors-2026-10-09.md
verification/wire-routing-2026-10-09.md
verification/wire-routing-commit-2026-10-09.md
verification/wire-routing-freeze-2026-10-09.md
```

## Work left uncommitted

The following paths retain unstaged or untracked work. Mixed files contain only their excluded Shift-placement changes after the routing commit; all other listed changes are separate art/Shift work. The ignored staged-only verification snapshot is also retained under UnityProject/Temp for its logs and built player.

```text
UnityProject/Assets/SiliconSandbox/Art/FILES.txt
UnityProject/Assets/SiliconSandbox/Art/README.md
UnityProject/Assets/SiliconSandbox/Art/Review~/asset-overview.png
UnityProject/Assets/SiliconSandbox/Art/Review~/asset-top.png
UnityProject/Assets/SiliconSandbox/Art/Review~/changed-files.txt
UnityProject/Assets/SiliconSandbox/Art/Review~/integration-verification.md
UnityProject/Assets/SiliconSandbox/Art/Review~/mac-benchmark-20261007.txt
UnityProject/Assets/SiliconSandbox/Art/Review~/pin-wire-fit.png
UnityProject/Assets/SiliconSandbox/Art/Review~/texture-orientation-verification.md
UnityProject/Assets/SiliconSandbox/Art/Review~/unity-wire-fit.png
UnityProject/Assets/SiliconSandbox/Art/Review~/unity-wire-fit.tga
UnityProject/Assets/SiliconSandbox/Art/Review~/verification.json
UnityProject/Assets/SiliconSandbox/Art/Review~/wire-variants.png
UnityProject/Assets/SiliconSandbox/Art/Source~/SiliconSandbox_Assets.blend
UnityProject/Assets/SiliconSandbox/Art/Source~/SiliconSandbox_Review.blend
UnityProject/Assets/SiliconSandbox/Art/Source~/build_assets.py
UnityProject/Assets/SiliconSandbox/Art/Source~/convert_unity_review.py
UnityProject/Assets/SiliconSandbox/Art/Source~/render_wire_variants.py
UnityProject/Assets/SiliconSandbox/Art/Source~/verify_assets.py
UnityProject/Assets/SiliconSandbox/Art/Source~/wire_variants.py
UnityProject/Assets/SiliconSandbox/Art/Textures/SS_SymbolAtlas_64.png
UnityProject/Assets/SiliconSandbox/Art/asset-manifest.json
UnityProject/Assets/SiliconSandbox/Art/wire-variants.json
UnityProject/Assets/SiliconSandbox/Art/wire-variants.json.meta
UnityProject/Assets/SiliconSandbox/Tests/PlayMode/ShiftPlacementPlayTests.cs
UnityProject/Assets/SiliconSandbox/Tests/PlayMode/ShiftPlacementPlayTests.cs.meta
UnityProject/Assets/SiliconSandbox/Unity/Interaction/OneBitWorldInteraction.cs
docs/building-and-interface.md
docs/graphics-and-future-tools.md
docs/harnesses-and-net-links.md
docs/open-decisions-and-cautions.md
docs/physical-connections.md
```
