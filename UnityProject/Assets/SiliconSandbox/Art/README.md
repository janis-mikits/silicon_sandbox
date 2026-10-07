# SiliconSandbox first-playable visual assets

Original reusable art, 7 October 2026. **Imported into the Unity player; final visual acceptance in the player remains a manual check.** Overview: `Review~/asset-overview.png`; orthographic inspection: `Review~/asset-top.png`. The rendered wires are examples, not saved circuit topology. X is shown at one red pulse phase.

## Files and budgets

`FILES.txt` lists the art-source files relative to this directory; Unity-generated `.meta`, shared materials under `Generated/`, and the runtime art reference under `Resources/` were added during integration. All meshes are triangulated FBX 7.4, one mesh and one material slot per file, no colliders, cameras, animation, or embedded pins. `asset-manifest.json` records exact bounds and vertex counts. Hard-edge/UV splits can increase Unity's imported vertex count.

| Meshes under `Meshes/` | Triangles each | Material |
| --- | ---: | --- |
| `SS_SourceBody.fbx`, `SS_AndBody.fbx`, `SS_SrBody.fbx`, `SS_ModuleBody.fbx` | 44 | SS_Atlas |
| `SS_SourceValue_0.fbx`, `SS_SourceValue_1.fbx`, `SS_SourceValue_X.fbx`, `SS_SourceValue_Z.fbx` | 2 | SS_Atlas |
| `SS_Pin.fbx`, `SS_WireStraight.fbx` | 44 | SS_Tint |
| `SS_WireElbow.fbx` | 68 | SS_Tint |
| `SS_Junction.fbx` | 80 | SS_Tint |
| `SS_IdentityRing.fbx` | 96 | SS_Tint |

`Textures/SS_SymbolAtlas_64.png` is the only texture. Unity's import step assigns **two shared Unity materials**: `SS_Atlas` (opaque, white tint, atlas as albedo, metallic 0, smoothness approximately 0.28) and `SS_Tint` (opaque white albedo, no texture, metallic 0, smoothness approximately 0.28). Both use the existing built-in Standard shader; no custom shader or package is required. The runtime renderer updates signal/identity colors through per-instance property blocks rather than allocating materials per object. It preserves repeating X-red animation and separate selection outlines.

`Source~/SiliconSandbox_Assets.blend` contains the original meshes at their common export origin; isolate a named object to edit it. `Source~/SiliconSandbox_Review.blend` contains the gallery, lights and camera. `Source~/build_assets.py` regenerates all art and both renders. `Source~/inspect_references.py` loads four relevant reference pairs read-only. `Source~/verify_assets.py` validates the exports. Unity should ignore the `Source~` and `Review~` directories; import the FBX/PNG assets, not the Blender gallery.

## Scale, orientation and placement

One cell = one Unity unit. Coordinates below are Unity local `(x east, y up, z north)`. FBX export is Y-up / -Z-forward with baked space conversion. In Blender source, Unity `(x,y,z)` maps to `(x,-z,y)`. Check the asymmetric elbow and SR preview after Unity import before integrating; do not compensate for an import-axis error by changing authored pins.

- Bodies use a cell-center origin. Place at `anchorCell + (0.5,0.5,0.5)`, rotate with the existing authored orientation, and use scale 1. Source/AND/SR bounds are exactly `[-0.39,+0.39]` on every axis; module bounds are `[-0.41,+0.41]`. These preserve the current 0.78/0.82-cell body sizes. Bevels remove material inside those bounds. Do not apply the old primitive's 0.78/0.82 scale again.
- Source value plates share the body origin. Display exactly one plate for the **current driven** 0/1/X/Z value. The plate lies at local y=0.3905, only 0.0005 above the top surface, with x/z extent ±0.37. Swap its shared mesh on a settled value change, without rebuilding the body. There is no arrow or baked on/off meaning.
- AND/SR markings are on the top face itself. Module top is an intentionally blank name field; put the existing dynamic instance name flush on that surface (about y=0.4105, local x/z within ±0.30), oriented with the body. Fit/clip long names according to the integration owner's UI policy; these assets do not establish a truncation rule. Do not use a floating or billboard label for the built-in identity/name.
- Pin origin is the **authored face point**. Cylinder axis is local +Y, radius 0.10, axial range -0.14 to +0.0625. Rotate +Y to the outward face normal. The inward portion reaches the recessed body; the tip protrudes 1/16 cell beyond the face. Place at `cell + pointQ/4`, preserving every authored coordinate and current target identity. Do not place at the cylinder's geometric center or infer attachments from its contact.
- Straight wire axis is +Y, endpoints `(0,-0.5,0)` and `(0,+0.5,0)`, diameter 0.25. Rotate onto the actual visual route direction; scale **only local Y** to the endpoint distance. Keep radial scale 1. Origin is the segment midpoint. The ordinary wire body receives settled signal color.
- Elbow center is the route corner `(0,0,0)`. Its end centers are `(-0.5,0,0)` and `(0,0,+0.5)`. It is a watertight 90° miter, diameter 0.25 along both arms. Rotate to the authored turn; use it only where those half-cell arms fit. For other lengths, keep the corner unscaled and adapt adjoining straight pieces; nonuniform elbow scale distorts diameter. Do not replace arbitrary authored spans with a forced 90° route.
- Junction is a faceted sphere of radius 0.17 at the actual visible join, used only for 3–6 incident directions **on the same net**. Two directions use straight/elbow geometry without this marker. Concatenation is outside this asset set.
- Identity ring is optional, 0.036 long along +Y, outer radius 0.128. Rotate with a straight section and slide to an unobstructed end region; keep it separate from the signal material property block. One ring per connector is sufficient for the preview language. It must not cover a junction or target outline.

The existing renderer owns exact route offsets, segment/channel identities and hit targets. A crossing uses separate straight/elbow pieces at the existing visual separation, **without** a junction marker. The gallery demonstrates a 0.32-cell centerline separation (0.07 clear air gap); that is an illustration, not a new universal channel offset or topology rule. Preserve existing authored endpoints and targeting. Do not infer joins from mesh overlap. No replacement collider, selection behavior, connection logic, scene or saved data is supplied.

## Atlas layout

64×64 pixels: four columns and four rows of 16×16 tiles, numbered from the **bottom left**. Tiles 0 shell, 1 edge, 2 `0`, 3 `1`, 4 `X`, 5 `Z`, 6 `AND`, 7 `SR`, 8 blank module name field, 9 blank source panel; 10–15 reserved dark. The UVs address texel centers within each tile. Set Point filtering, Clamp wrapping, sRGB on, no compression, and mipmaps off for the initial pixel-art review. The tiny atlas has no mip gutters; enabling ordinary mipmaps can bleed neighboring symbols. Test distance shimmer and readability in the real player before acceptance. Higher-resolution redraws can retain the same 4×4 layout/UVs; regenerate with gutters before adopting filtered mipmaps.

Body colors are neutral slate, independent of signal state. Preview signal RGB values match the existing renderer: 0 `(0.2,0.4,0.7)`, 1 `(0.1,0.95,0.2)`, Z `(0.5,0.5,0.5)`, X a representative red phase. Lighting/color management makes preview colors differ from raw RGB. Runtime X pulsing remains the renderer's responsibility.

## Verification and limits

Executed with local Blender 5.0.1, from repository root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python UnityProject/Assets/SiliconSandbox/Art/Source~/inspect_references.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python UnityProject/Assets/SiliconSandbox/Art/Source~/build_assets.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python UnityProject/Assets/SiliconSandbox/Art/Source~/verify_assets.py
```

Blender initially crashed at startup inside the sandbox; the authorized local runs outside that sandbox succeeded. `Review~/verification.json` records PASS for 13 FBX round trips, literal dimensions, triangle/material counts, UV validity, nondegenerate triangles, outward closed geometry/manifold edge incidence, atlas dimensions, and unchanged SHA-256 hashes for the eight inspected reference files. A verification attempt run after loading the gallery encountered hidden review objects; the verifier now clears hidden objects too, and the final clean run passed with exit code 0. Overview and top renders were visually reviewed. Relevant inspected pairs: `current_source_on`, `AND_gate`, `wire_straight_power_off`, `wire_bent_power_off`; the other reference models were not inspected. Reference rendering used neutral materials, not their production colors.

The integration owner imported the assets through `FirstPlayableArtImport.Ensure`, mapped them to the two shared materials, and replaced source/AND/SR/module bodies, source values, pins, straight wires, elbows, junctions, and identity rings in the runtime view. Existing authored topology and selection IDs remain in Core. The Unity 6000.3.24f1 Mac verification command in the [coverage audit](../../../../verification/first-playable-coverage-audit.md) passed 203 Edit Mode tests, 20 Play Mode tests, a macOS build, player startup, save/reopen, and exit autosave on 7 October. The new Play Mode assertions cover imported body/pin/wire geometry, module surface naming, four source symbols, and an opposing-driver case where the sources retain separate markings while the shared wire is X. A Mac performance benchmark was run on the imported build; see the coverage audit for the latest result. Windows and CI remain unverified. The art has no runtime logic. Surface labels become less readable at grazing angles/distance; the atlas uses a deliberately simple pixel font. Review the actual player visuals before accepting them.
