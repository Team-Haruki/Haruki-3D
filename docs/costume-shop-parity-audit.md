# CostumeShop 6.6.2 parity audit

This audit maps the final reverse-engineering evidence package to the browser
rendering kernel. It is intentionally organized by ownership: facts that belong
to the updater/exporter or to a host UI are not copied into the renderer.

Authoritative evidence:

- directory: `/data/xy/pjsk-research/20260725-final-boundaries`
- primary ledger: `search.md`
- model pseudocode: `pjsk_bundle_model_pseudocode.md`

Older archives and experimental notes are historical only when they disagree
with these final boundaries.

Status vocabulary:

- **implemented**: the browser kernel has the behavior and a regression check.
- **producer boundary**: the exporter/updater must encode the result; the
  browser consumes it.
- **host boundary**: a caller owns the behavior; it is not part of rendering.
- **not applicable**: the evidence belongs to Live/FUnit rather than
  CostumeShop.
- **bounded**: the evidence itself does not identify an exact value. The kernel
  must not invent one.

## Ownership summary

| Evidence domain | Owner | Status |
| --- | --- | --- |
| AssetBundle dependency/download state machine | updater/exporter | producer boundary |
| 5-of-8-byte bundle transform | updater/exporter | producer boundary |
| Masterdata preset/custom resolution | registry producer | producer boundary |
| Runtime package decoding and exact part selection | kernel | implemented |
| CostumeShop camera projection and captured poses | kernel | implemented |
| Drag/pinch input and mutable camera interaction state | web host | host boundary |
| Full character rebuild after resolved part change | kernel | implemented |
| Body/face/head_optional model assembly | exporter + kernel | implemented |
| Materials, C/S/H, lighting and skin ramp | exporter + kernel | implemented |
| FaceSDF and hair shadow | exporter + kernel | implemented |
| Outline geometry, color path and fixed state | kernel | implemented |
| Eye/eyelash queue, stencil and through-hair pass | kernel | implemented |
| Toe projected shadows | kernel | implemented |
| UTJ/Sekai SpringBone and ExtraBone | exporter + kernel | implemented |
| Parent/Aim/Rotation constraints | exporter + kernel | implemented |
| Live timeline and FUnit replacement controls | Live/MV runtime | not applicable |
| UI and cache policy | web host | host boundary |
| Latest-wins part selection | kernel | implemented |

## Evidence ledger

The final boundary documents supersede older archive “blocked” notes and
experimental renderer branches.

| Archive topic | Applied result |
| --- | --- |
| AssetBundle stream/manager/loader and 5-of-8 transform | Updater/exporter preserve logical name, physical hash/path, recursive dependency closure and Unity resource identity; the browser consumes converted packages. |
| CostumeShop initialization, download states and latest-wins queue | The kernel supersedes queued intermediate selections and rejects an in-flight stale selection before model mutation; downloader UI remains a host concern. |
| Masterdata costume/hair/accessory mapping | Registry-producer boundary; compact runtime entries preserve role, original source and compatibility decisions. |
| Camera, zoom, vertical move and rotation | The kernel provides the official fixed preview/capture poses and FOV. Pointer/pinch state belongs to the host. |
| Complete part rebuild and motion restoration | Implemented by resolved-identity comparison, one combined import and same-role animation-position restoration. |
| ModelCombineSetup and accessory mounting | Implemented by native prefab assembly, body-host rebinding and exported `part`/face-adjustment data. |
| ConstraintSetup | Parent/Aim/Rotation constraints are rebound and updated before dynamics; height scaling applies only to ParentConstraint translation offsets. |
| Character material, skin and HSVC setup | Exporter preserves raw material inventory; the kernel applies the driver-final C/S/H/skin/ambient/specular/rim path. |
| FaceSDF, hair shadow and head basis | Implemented without FaceSphere, guessed neck decals or character MPB emulation. |
| Eye/eyelash queues, stencil and overlay | Implemented for the single-character preview slot, including material-driven eyelash parameters. |
| Outline C# writers and driver-final GPU variants | Implemented as an expanded Toon pass with exact fixed state, width, packed second normal and clip offset. |
| Material and projected shadows | Kept as separate systems; only toe targets are accepted. |
| UTJ/Sekai SpringBone, colliders, force providers and ExtraBone | Implemented in the CostumeShop runtime; FUnit simulation is not mixed into it. |
| Live/streaming/FUnit replacement controls | Not applicable to the CostumeShop browser kernel. Their exported records remain metadata rather than a second simulator. |
| Auxiliary MRT outputs | Not applicable to final browser color; the kernel does not fabricate unused GBuffer consumers. |

## Input, registry and package resolution

| Official invariant | Implementation |
| --- | --- |
| A role key includes character and unit. | `runtimeRoleId` and `parseRuntimeRoleId`; Miku variants remain distinct. |
| Package identity includes region/base URL, master version, requested selection, resolved part package paths and motion source. | `composeRuntimeCombinedCharacterAsset()` builds its identity from all of those values rather than the costume ID alone. |
| A part source retains logical bundle, physical SHA-256/path, recursive dependency closure, Unity resource name and object type. | The updater writes the dependency index and the exporter writes these fields into each part package; color-variation bundles retain their own identity too. |
| Presets are authoritative and compatibility blacklists apply only to custom head/hair assembly. | The compact registry is generated upstream; `runtimePackageLoader.ts` and `runtimePartComposer.ts` consume resolved entries without reinterpreting masterdata. |
| The selected body, head, hair and head_optional are resolved before model construction. | `CustomWardrobeController.ensureSelectionPackages()` loads all selected contributors before `composeRuntimeCombinedCharacterAsset()`. |
| A complete head and a detachable head_optional are different sources. | `resolveHeadRegistryEntry`, `resolveOptionalHeadRegistryEntry`, `tryRuntimePartSlot` and package-path disambiguation preserve the original source. |
| A repeated resolved selection does not rebuild. | `Haruki3DEngine.applyCustomSelection()` compares the combined identity before import. |
| A changed resolved selection rebuilds one complete character graph. | `importCombinedCharacter(..., disposeBeforeLoad: true)` is called once after all parts resolve. |
| Same-role changes preserve motion time; role changes force the role motion. | Animation selection matching and `restorePosition()` implement this boundary. |

The official download UI is not a renderer responsibility.
`Haruki3DEngine` gives each part selection a generation, drops queued stale
requests and checks an in-flight request again after its package downloads,
before model mutation.

## Camera and host boundary

| Official invariant | Implementation |
| --- | --- |
| Perspective camera, FOV 25. | `cameraRuntime.ts` CostumeShop profile. |
| Startup target `(0, 1.25, 0)` and camera `(0, 1.25, 2.3)`. | `getCostumeShopCameraPose("official-default")`. |
| The full-body captured profile uses the same FOV and official z endpoint `4.5`. | `getCostumeShopCameraPose("full-body")`. |
| Horizontal drag rotates CameraRoot around world up; vertical input does not pitch the camera. | Documented host input contract; the kernel does not own pointer/pinch handlers. |
| A part change does not reset camera rotation/zoom. | Host state is outside character import and therefore remains stable. |
| Directional and rim lights are children of `CameraRoot`. | Stored light vectors are the yaw-zero local directions; a host rotation must rotate camera and lights together. The captured round-9 world vector already included about `-2.447°` of CameraRoot yaw and is not a yaw-zero default. |
| The game creates a square costume-preview RenderTexture. | Camera positions and FOV remain official for every host viewport, but a non-square direct canvas exposes a different horizontal field than the square in-game preview. Exact UI framing requires the host to render or composite a square preview surface; it must not compensate by moving character bones or changing the vertical camera limits. |

The capture host may use OrbitControls for operator convenience, but that is
not claimed as official CostumeShop interaction. Exact mutable yaw/zoom/move
input is intentionally a host API concern rather than hidden engine state.

Local JP runtime validation on 2026-07-25 used all 31 role keys at four motion
phases. All 124 samples ended at the exact full-body camera state
`position=(0, 0.85, 4.5)`, `target=(0, 0.85, 0)`, `FOV=25`, `aspect=1`.
Projected head coordinates stayed inside NDC
`x=[-0.0918824, 0.0962628]`, `y=[0.3713295, 0.6445972]`.

## Model assembly

| Official invariant | Implementation |
| --- | --- |
| Body prefab selection is exactly `gameCharacters.figure + breastSize`; no neighboring body variant is a fallback. | Registry/exporter resolution fails the entry when the selected bundle is absent. Five-region role-8 audits resolve every usable entry to `ladies_s`. |
| Body is the final host; face is an assembly input. | `unityPrefabRuntime.ts` builds the body-rooted source graph and exposes the surviving face Neck as the live body attach point, never the destroyed body Neck. |
| Move body Head children into face Head. | `applyOfficialModelCombineSetup()` drains the body Head children into the surviving face Head while preserving their authored local TRS. |
| Patch body Neck/Head bone slots to face Neck/Head. | Native skinned-mesh rebinding uses the assembled node map. |
| Destroy the replaced body Head and Neck nodes. | Assembly replaces those paths in the active graph. |
| Face renderer root bones use the body renderer root. | Native mesh diagnostics retain both the authored head root and the effective body root; Three skinning uses the patched live bone map and disables frustum culling. |
| Every renderer from the temporary face object moves beside the body renderer. | Assembly derives the complete active head renderer inventory from exported native meshes; it does not hard-code only `Face`, `Hair` and `Acc`. |
| The face renderer predicate is the exact renderer name `Face`; body is the first body renderer. | Exported assembly metadata declares those sources; the runtime does not guess by material color. |
| Unity bind poses remain mesh-local after renderer reparenting. | Native installation converts each exported Unity bind pose to Three.js bone-inverse space with `unityBindPose * inverse(rendererBindMatrix)`. This prevents a non-identity face renderer transform from being applied twice; role 8 is the regression fixture because its face renderers carry an authored local offset. |
| head_optional mounts at the exported `part` node (`a01..a05`). | `mountHeadOptionalPrefabGraphs()` attaches the `optional` prefab root to the active named node. |
| Per-face accessory adjustment is applied after mounting. | `resolveAccessoryTransformAdjustment()` applies the `CharacterAccessoryTransformController` entry selected by face id. |

The final 31-role JP validation checked the assembled graph after animation.
Every role returned HTTP 200, installed native meshes, resolved all SpringBone
nodes and retained a live face Neck/Head on the body host. Thirty common face
graphs removed 14 replaced/temporary transforms. Luka's deeper face blend-shape
control tree correctly removed 40; the official operation is structural
destruction of the temporary face wrapper, not a fixed node-count rule.

## Constraints

| Official invariant | Implementation |
| --- | --- |
| Rotation, Parent and Aim sources are rebound by active transform name/path. | `unityConstraintRuntime.ts` and `resolveReboundConstraintSourceNode`. |
| ParentConstraint translation offsets scale with character height. | Constraint runtime receives the active height and scales translation offsets. |
| Constraints run after model combination and before SpringBone simulation. | Engine frame order is assembly sync, ExtraBone/constraints, then SpringBone. |
| Constraint state is continuously applied, not only copied once at load. | `UnityConstraintRuntime.update()` runs in the runtime frame. |

## UTJ/Sekai SpringBone and ExtraBone

| Official invariant | Implementation |
| --- | --- |
| CostumeShop uses UTJ/Sekai SpringBone, not FUnit SpringBone. | The composed setup is explicitly UTJ/Sekai; FUnit records remain metadata-only. |
| Managers own depth-sorted active bones. | `unityPrefabSpringRuntimeAdapter.ts` rebuilds manager ownership and sorts active chains. |
| Direct colliders and the six colliderFlag prefix groups are rebound after composition. | Composer rebuilds `colliderBindings`, manager caches and binding decisions. |
| Sphere, capsule and panel collision are distinct. | `utjSpringBoneRuntime.ts` implements each shape and its local-space solve. |
| Bone length, pivot, angle limit, time step and force-provider behavior are retained. | UTJ runtime contains the corresponding solvers and update order. |
| Only serialized ExtraBone components execute; an `EX_*` node name is not enough. | `sekaiExtraBoneRuntime.ts` reads actual exported ExtraBone entries. |
| Six arm/forearm/elbow helpers use the official rotation order. | ExtraBone runtime and its regression test cover the six-case conversion. |
| Part replacement resets/settles the rebuilt simulation. | Character import disposes old managers, creates the new runtime and performs warm-up/reset. |

Live MV slow/control tracks, streaming formation overrides and FUnit merge
helpers are deliberately excluded. They are not called by CostumeShop and
would create a second, conflicting spring implementation.

## Base character shading

| Official invariant | Implementation |
| --- | --- |
| Body uses Toon; face/hair use the captured Toon-v3 paths. | Body, face and head material runtimes select separate shader factories. |
| Base character passes cull back faces, write depth and do not blend. | Body and face ShaderMaterials use `FrontSide`, depth write and no transparency. |
| C, S and H textures are sampled independently. | Main, shadow and value/control textures have independent uniforms and exact runtime slots. |
| The driver-final Toon variants use `_MainTex_ST` for the common C/S/H coordinate and raw UV1 for FaceSDF. | Body/face C/S/H share an isolated per-material main-UV matrix; FaceSDF does not invent a UV0 fallback or a separate transform. The outline shares the same Toon path. |
| KTX2 color textures decode to linear in the sampler and are converted back to the game's Gamma-space working values. | `sekaiGammaTexture()` is used at texture reads; output does not apply a second Three.js colorspace conversion. |
| `_LAMBERT` selects half-Lambert; it is not a forced engine-wide toggle. | `uUseLambert` follows each exported material/keyword. |
| H.b participates in the official shadow threshold path. | `sekaiBaseShadow()` implements the captured formula. |
| H.r `>= 0.5` selects the skin path. | Body and face shaders use the exact step selector. |
| Skin is a three-band default/shadow1/shadow2 ramp multiplied by global shadow color. | `sekaiSkinRamp()` and `evaluateSekaiSkinColor()`. |
| Material HSVC uses the captured hue rotation, value, contrast and saturation order. | `sekaiApplyHsvc()`. |
| Ambient uses the character ambient overlay formula and exported per-part color. | `sekaiApplyCharacterAmbient()`. |
| Directional, ambient, specular, rim and dark-rim defaults match the coherent frame. | `sampleScene.ts` and `characterLightingRuntime.ts`. |
| There is no CostumeShop FaceSphere contribution. | No FaceSphere branch or guessed sphere uniform exists. |
| There is no character MPB override; ID 318 is URP Blitter `_BlitScaleBias`. | No renderer-level character MPB emulation exists. |
| `_FinalSat`, `_Brightness`, `_HighlightRolloff`, `_UseSkinColor`, `_SkinMaskMode` and `_FaceSkinShadowStrength` are not valid CostumeShop controls. | These invented controls were removed from types, uniforms, shader code and runtime updates. |

## FaceSDF and hair shadow

| Official invariant | Implementation |
| --- | --- |
| FaceSDF samples normal and mirrored SDF and selects by the sign of head-direction X. | `sekaiFaceShadow()` selects directly; no guessed mirror/bias control remains. |
| The optional face limiter uses the exported range and width/fade parameters. | Face uniforms are populated from raw material values. |
| Hair shadow is enabled only by `_HAIR_SHADOW`; Lambert remains a separate keyword/value. | Runtime material parsing preserves both independently. |
| `_HeadPosition = head.TransformPoint(offset)`. | `Haruki3DEngine` applies the exported controller offset with `localToWorld()`. |
| The hair Base vertex variant replaces its lighting normal with `normalize(N + _HeadNormalBlend * (normalize(P - _HeadPosition) - N))`. | The hair-only vertex branch consumes `_HeadPosition` and the raw `_HeadNormalBlend`; the captured Base UBO value is `0.7`. |
| The coherent captured offset is `(-0.07, 0, 0)`, but it is data, not a global constant. | The exporter-provided controller value is used; the kernel does not hard-code it for every head. |

## Outline

| Official invariant | Implementation |
| --- | --- |
| Inverted hull: front-face culling, depth write, no blending, ShaderLab Less (reversed Vulkan Greater). | Outline materials use `BackSide`, depth write, no blending and `LessDepth`. |
| Distance/FOV width uses captured min/max/near/far and Hermite FOV curve. | `sekaiOutlineRuntime.ts`. |
| COLOR.r is a continuous width multiplier. | Vertex expansion multiplies by the unmodified red channel. |
| second normal is reconstructed from `(UV1.x, UV1.y, UV2.x)` in the tangent basis. | `_OUTLINE_SECOND_NORMAL` runtime branch. |
| `_OutlineOffset` applies a projected-camera-origin clip term scaled by COLOR.b. | Outline vertex shader implements the driver-final term. |
| The outline fragment is not a flat-color `MeshBasicMaterial` path. It samples and shades the same character data before outline color blending. | Character outline materials clone/share the source Toon uniforms and fragment path, replacing only the final output-color blend. |
| The active controller color/blending is applied after Toon shading in linear light. | The browser hook decodes its sRGB-shaped Toon intermediate, mixes the linear controller color, then encodes the result again; direct Gamma-space mixing is the known dirty-outline regression. |
| Global outline defaults are black with blending `0.5`. | CostumeShop outline controller defaults. |
| Eye and eyelight have no Outline pass; face skin, eyebrow and eyelash do. | Outline group filtering excludes only `eye` and `eyelight`. Eyebrow and eyelash receive a Toon-v3 outline source with their own textures/raw material values, while `COLOR.r` remains the continuous per-vertex width scale. |

A `MeshBasicMaterial` fallback remains only for non-character materials that do
not expose the kernel's Toon output hook. Exported CostumeShop body/face/hair
materials take the full Toon outline path.

## Eye, eyelash and hair stencil

| Official invariant | Implementation |
| --- | --- |
| Formation stencil bit is `1 << formationId`. | The browser kernel is a single-character CostumeShop preview, so its only formation slot is 0 and its bit is `0x01`. Multi-character formations are outside this kernel. |
| Default Face/Body/Acc/Other base passes replace all stencil bits with zero. | The shared base-stencil setup uses ref 0, Always, write mask 255 and Replace. |
| Default queue 2000; eyebrow/eyelash 2001; eye 2002; hair 2451. | Material kind classification assigns the same orders. |
| SekaiEyelash uses depth Always, no depth write, back culling and SrcAlpha/OneMinusSrcAlpha while preserving destination alpha. | `configureSekaiEyelashPass()`. |
| Opacity and camera edges come from each raw material, with captured values only as defaults. | `_EyelashTransparent`, `_EyelashFaceCameraEdge1/2` are read per slot. |
| Eye/eyebrow/eyelash angle fading follows `_FaceFront`. | `updateSekaiEyelashPassView()`. |
| Hair writes the complementary stencil mask used by the overlay pass. | `configureSekaiHairStencil()`. |
| Renderer feature range is opaque 2001..2450 and has no extra renderer-level stencil override. | Runtime orders overlay meshes in the corresponding range and uses material stencil state. |

## Projected shadows

| Official invariant | Implementation |
| --- | --- |
| Material self-shadow and projected floor shadow are independent. | Character shader and `CharacterProjectedShadowController` are separate. |
| Targets are `Left_Toe` and `Right_Toe`. | Exact target names are used. |
| Cross shadow is the CostumeShop default; directional and cross are mutually exclusive. | Controller defaults to cross and toggles one mode at a time. |
| Cross floor offset is `0.015`; directional offset is `0.01`; invisible height is `0.2`. | Exact constants in `projectedShadow.ts`. |
| Toe height drives the captured alpha fade. | Each toe pair retains initial height and applies the independent fade. |

The controller constants and toe ownership are exact. The browser's projected
quad dimensions/texture are a bounded visual reconstruction because the
archive does not provide the original projected-shadow mesh/texture payload.

## Camera target and post-processing boundary

The actual CostumeShop preview renders a 1024×1024 Gamma target at render scale
1, 1× MSAA, with no FSR, antialiasing, sharpening or post-processing. The
kernel therefore defaults its Sekai preview postprocessor to disabled.

Output resolution and any optional web presentation upscale are host policy.
They must not silently change the material/lighting kernel.

## Bounded evidence

The final package and WebGL platform leave these non-blocking boundaries:

1. one validated 96-index UBO Base/Outline pair is not mapped to a Unity
   renderer/material name; its `_HeadNormalBlend=0.7` slot is named by the
   reflected Toon-v3 material layout, but its three color vectors remain
   deliberately unattributed;
2. auxiliary MRT fields are not needed for the browser's final color output;
3. WebGL has no Unity `MirrorOnce` sampler or portable per-texture mip-bias
   control. `MirrorOnce` is bounded to clamp and mip bias remains preserved as
   metadata; Repeat, Clamp, Mirror, point/bilinear/trilinear and anisotropy are
   applied;
4. projected-shadow visual geometry is reconstructed from the exact toe,
   offset, visibility and alpha behavior because its original visual asset is
   absent.

The kernel does not assign guessed material names or emulate unused GBuffer
attachments. These boundaries do not justify extra color grading, FaceSphere,
synthetic neck shadows, FUnit simulation or other heuristic branches.

## Regression surface

The relevant tests are:

- `runtime-body-material-behavior.test.mjs`
- `runtime-character-lighting-behavior.test.mjs`
- `sekai-character-lighting.test.mjs`
- `runtime-outline-behavior.test.mjs`
- `runtime-through-hair-behavior.test.mjs`
- `runtime-prefab-behavior.test.mjs`
- `runtime-spring-wind-behavior.test.mjs`
- `runtime-animation-playback-behavior.test.mjs`
- `accessory-package-selection.test.mjs`
- `kernel-lifecycle-behavior.test.mjs`
- `web-asset-headers.test.mjs`
- Exporter `PartMaterialMetadataSmoke` under
  `HARUKI_EXPORTER_CONFIG_TEST=true`

Build, unit tests, consumer build and browser shader compilation should all be
run before declaring a release candidate.
