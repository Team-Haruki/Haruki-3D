# Haruki 3DMV Web Host

The `haruki-3d-engine/mv` module hosts one original Unity WebGL/WASM build. It
does not translate MV scenes, Timeline tracks, character assembly, or Sekai's
URP renderer into Three.js.

The matching Unity 2022.3.62f2 project lives in `unity/Haruki3DMV`. Its WebGL
player is compiled to JavaScript glue, a data archive, and WebAssembly; the
TypeScript package remains the host rather than a second renderer.

## Build the Unity WebGL/WASM player

Install Unity 2022.3.62f2 with WebGL Build Support, then run:

```bash
UNITY_EDITOR=/path/to/Editor/Unity npm run build:mv:unity
```

The repository-local toolchain path `/data/xy/.toolchains/unity-2022.3.62f2`
is detected automatically. The command runs Unity EditMode tests before
writing the generated player to `unity/Haruki3DMV/Build/HarukiMV`. It fails if a
matching editor is unavailable; it never creates placeholder WASM artifacts.
Unity license and package-manager state are isolated under
`/data/xy/.toolchains/unity-home` rather than the system user's home.

To rebuild recovered Sekai assets together with the player, point the build at
the recovered Unity project and the exporter's `mv-source-set.json`:

```bash
HARUKI_MV_RECOVERED_PROJECT=/path/to/ExportedProject \
HARUKI_MV_SOURCE_SET_MANIFEST=/path/to/mv-source-set.json \
npm run build:mv:unity
```

AssetRipper cannot recover portable ShaderLab source from the Android shader
programs. The default recovered build therefore rejects every
`DummyShaderTextExporter` placeholder. To rebuild a local evidence bundle while
working only on object assembly, Timeline, or lifecycle code, opt in explicitly:

```bash
HARUKI_MV_ALLOW_DUMMY_SHADERS=1 \
HARUKI_MV_RECOVERED_PROJECT=/path/to/ExportedProject \
HARUKI_MV_SOURCE_SET_MANIFEST=/path/to/mv-source-set.json \
npm run build:mv:unity
```

That switch is development-only. It does not make the placeholder shaders
correct, and builds made with it must not be published. Leaving the variable
unset retains the strict release gate.

The browser smoke test likewise fails on invalid WebGL draw-buffer output and
missing float/range material properties. When exercising an old evidence build
that is already known to contain placeholder shaders, that renderer-only gate
may be bypassed explicitly with `HARUKI_MV_ALLOW_RENDER_ERRORS=1`. This flag is
also development-only and must never be used to qualify a release artifact.

The source-set catalog lists the independent AssetBundles needed by one launch,
so neither the music ID nor stage ID is compiled into the viewer. It is not a
new monolithic MV package. Every source bundle remains a separately requested
and cacheable file, is rebuilt for WebGL under its original logical bundle
name, and keeps its dependency edges in the generated `deps.json`. Main-MV
character declarations provide the default formation and may be replaced by
the player's formation; a CutIn may declare a fixed character. The build does
not strip missing game scripts, replace materials, synthesize a camera, or
invent a light rig.

Built-in Unity Timeline script references recovered by AssetRipper are remapped
to the matching Unity Timeline 1.7.6 types before rebuilding. Sekai-specific
Track/Clip and recovered component GUIDs are separately remapped to the matching
runtime classes in this project; they are never substituted for built-in
Timeline types. Binary textures and other recovered assets are copied without
content rewriting. The build then scans every imported YAML object and fails
with the recovered type name when a referenced game script is still unresolved.

The Unity MV player is built on an activated development machine with
`npm run build:mv:unity`; it is not part of GitHub Actions. The command runs
EditMode tests, compiles the real WebGL player, and validates the generated
files locally. Do not upload the machine-local `UnityEntitlementLicense.xml`
or commit activation material. The repository ignores `.alf`, `.ulf`, and
entitlement files and tests that none are tracked.

The committed Unity project provides a browser bridge, recursive AssetBundle
dependency loading, additive scene loading, and one playback coordinator for
every scene root. Its exact recovered Timeline binding uses
`PlayableBinding.streamName` to select the shared target and passes
`PlayableBinding.sourceObject` to `PlayableDirector.SetGenericBinding`, then
sets time to zero and evaluates. Recovered Sekai component and custom Timeline
contracts are explicit runtime inputs; the bridge does not pretend that a
serialized state holder replaces a missing renderer pass or shader.

`loadBundleSet()` accepts the `deps.json` format used by ClauseKAI. It validates
the requested dependency closure, rejects missing/cyclic entries, and loads
dependencies before roots. The referenced files must already be rebuilt for
the WebGL target; source iOS/Android bundles are deliberately rejected by Unity.

## Start a generated Unity build

```ts
import {
  createHarukiMvBridge,
  createHarukiMvRuntime,
  resolveUnityWebGLBuild,
} from "haruki-3d-engine/mv";

const build = resolveUnityWebGLBuild({
  buildBaseUrl: "/3dmv/Build",
  streamingAssetsUrl: "/3dmv/StreamingAssets",
  buildName: "HarukiMV",
  compression: "gzip",
  companyName: "Team Haruki",
  productName: "Haruki 3DMV",
  productVersion: "1.0.0",
});

const mv = createHarukiMvRuntime({
  canvas: document.querySelector("canvas")!,
  loaderUrl: build.loaderUrl,
  build: build.config,
  onProgress(progress) {
    console.log(progress);
  },
});

await mv.prepare();
const bridge = createHarukiMvBridge(mv);
await bridge.loadBundleSet({
  baseUrl: "/3dmv/StreamingAssets/sekai_webgl_bundles",
});
const mvData = await bridge.readMvData({
  bundleName: "live_pv/mv_data/0112",
  assetName: "data",
});
const renderProfile = await bridge.applyRenderProfile({
  resolution: "1080p",
  refreshRate: 120,
  use120Fps: false,
});
await bridge.loadMv({
  musicId: 112,
  enableCutIns: false,
  characters: runtimeMembers.map((member) => ({
    characterId: member.characterId,
    bodyBundleName: member.bodyBundleName,
    faceBundleName: member.faceBundleName,
    characterHeight: member.characterHeight,
    heelOffset: member.heelOffset,
  })),
});
bridge.seek(12.5);
bridge.setPaused(false);

// On page/component disposal:
await mv.destroy();
```

Bridge operations that emit a completion event are single-flight promises.
Await each download/read/assembly call before starting the next one. `loadMv()`
assembles from the dependency catalog; `loadScene()` loads a complete authored
scene bundle. They are alternative ownership paths and must not be mixed in one
session without disposing the current path first.

The loader script is shared by URL. Unity instance creation is single-flight;
a failed creation may be retried. Destroying during creation waits for the
instance and calls Unity `Quit()` exactly once.

`readMvData()` loads the original `MusicVideoData` asset from a rebuilt bundle
and emits `mv-data-ready`. The compatible Unity type uses the original script
GUID and serialized field names, so stage selection, character slots, heel
offsets, decorations, penlight, camera flags, post-effect flags, and cut-in IDs
come from the bundle rather than a parallel host-side copy.

`getRenderProfile()` and `applyRenderProfile()` support explicit landscape
video outputs: `720p`
(1280×720), `1080p` (1920×1080), `1440p` (2560×1440), and `4k-uhd`
(3840×2160). `custom` accepts an explicit width and height. These modes are
pixel contracts and deliberately ignore DPI. `device` retains the recovered
6.7.0 `ScreenConfig` calculation: for the captured 3200×2136, 440-DPI device,
High MusicVideo returns 3200×2136 while Default returns 1617×1080.

All modes return the internal render size, the aspect-derived post-effect work
surface (`height=256`), and the feasible target frame rate. The getter is a
side-effect-free query. The applying form writes `Application.targetFrameRate`,
resets scalable buffers to 1:1, and calls Unity `Screen.SetResolution`; CSS
presentation remains outside the engine. `resolveUnityWebGLBuild()` disables
Unity's CSS-to-backing-buffer synchronization and fixes WebGL device pixel ratio
to one, so the selected pixel dimensions are not silently replaced by element
size or browser DPI. `4k-uhd` is named explicitly because
DCI 4K (4096×2160) is a different standard.

Recovered bundle roots now have stable runtime addresses where the original
asset contract is known:

| Logical bundle | Address |
| --- | --- |
| `live_pv/mv_data/{id}` | `data` |
| `live_pv/timeline/{id}/{node}` | `timeline` |
| `live_pv/model/stage/{id}` | `stage` |
| `live_pv/model/stage_decoration/{id}` | `decoration` |
| `live_pv/model/camera_decoration/{id}` | `decoration` |
| `live_pv/model/penlight/{id}` | `penlight` |
| `live_pv/model/{character|characterv2}/body/{model}/{figure}` | `body` |
| `live_pv/model/{character|characterv2}/face/{model}` | `face` |
| `live_pv/model/{character|characterv2}/head_optional/{model}` | `head_optional` |

These names select the recovered root asset only; Unity still packs the root's
referenced meshes, materials, textures, and other dependencies into the same
independent bundle. Other groups retain their recovered asset names until an
official root-address contract is known.

Character models do not have a song-wide V1/V2 switch. The original runtime
constructs both candidates for each resolved body, face, head optional, and
color-variation part, selects the loaded `characterv2` bundle when present,
and falls back to the matching legacy `character` bundle otherwise. This is a
per-part decision: an old MV may use a V2 body and face together with a V1-only
head optional. MVData and Timeline roots themselves are shared and have no V2
variant.

`state` reports `idle`, `loading`, `ready`, `failed`, `destroying`, or
`destroyed`. `getMemoryInfo()` exposes Unity's WASM/JS heap counters when the
generated loader supports them. Product UI and user-facing errors remain host
concerns.

## Official runtime target and current boundary

The browser bridge is only the host boundary. A complete player must keep the
official runtime ordering recovered from `Background3DPlayer`:

1. load the manifest and recursive bundle dependency closure;
2. construct stage and characters using the original Unity object graph;
3. bind character graphs and all PlayableDirectors;
4. attach light, effect, monitor, water, post-processing, and audio systems;
5. start playback only after scene and audio preparation completes;
6. apply pause and absolute seek to every director, graph, driver, post effect,
   spring policy, and audio follower;
7. dispose scene instances, PlayableGraphs, bundles, and caches together.

Prefab instantiation preserves the recovered object's authored transform. It
does not frame the object from its renderer bounds or modify any camera; the
official main camera is created and adjusted by `CameraNode` and
`CameraAdjustment` later in the runtime chain.

Character motion keeps the authored `Position` transform and
`Animator.applyRootMotion = false`. A stopped motion holds its sampled pose; the
host must not add a position-reset or anti-sliding correction. Do not enable a
JSON fallback driver for a property already driven by an original Timeline.

The typed bridge currently covers bundle/scene loading, prefab instantiation,
MVData reading, MV assembly, optional CutIn activation, pause, seek, state, and
disposal. `loadMv()` receives the runtime character body/face bundle choices
for slots whose `MusicVideoData` deliberately leaves them blank. Fixed
`face`/`body` entries resolve from the loaded catalog. CutIn is disabled by
default; disabled, absent, and unavailable child IDs never block the main
player. Available children are built as independent inactive players and are
activated explicitly by CutIn order. Activating one child disables the main
player and every other child; ending it restores the main player. This is a
player switch, not a Timeline swap on one character object.
Normal CutIns may set `reuseMainMember: true`; the child MV's first character
ID is matched against the main MV declarations and reuses that slot's final
runtime member; the member's actual character ID may differ from the MV slot
declaration. Another CutIn selection still needs an explicit `characters` entry,
because its deck/multi costume override policy is not represented by this host
API. Each requested CutIn must choose exactly one of those sources; omission is
rejected instead of silently treating a Normal CutIn as a fixed child model.
Switching directly between two children first completes the old child's End
through the main root, then starts the new child's Begin.

The Unity runtime now contains
the confirmed core of the official `TimelineNode`: it creates the six Stage,
Character, Camera, Light, Effect, and Penlight directors, loads their original
`timeline` assets, records default bindings in one shared dictionary, waits for
the object nodes to replace those entries, binds every output, evaluates at
zero, and starts every director at the same requested music time. Missing
per-node Timeline bundles fall back to the corresponding
`live_pv/timeline/0001/{node}` bundle, matching the recovered resource-level
fallback rather than synthesizing missing tracks.

Pause and resume preserve the recovered call order: every director is paused or
resumed first and then assigned the same absolute music time. Retry enumerates
every output track implementing the MV retry contract before restoring the
Effect director's `MvLiveEffectTimelineManager` state. Its setup stores the
main/CutIn flag and order, initializes playing to `!isCutIn`, and initializes
switch execution to false; the same values are retained for retry.

Reference Blend parameters follow the recovered `Sekai.Timeline.Common`
runtime rather than normalizing each input playable independently. When a
Timeline is loaded, every float, Vector2, Vector3, and Color blend receives its
own `TimelineClip.start` and `TimelineClip.end`. Mixers then pass the shared
absolute Director timestamp to `CalcBlend`. `Const` returns `beginValue`;
`Blend` evaluates the serialized curve over that clip interval and uses the
same clamped Unity `Lerp` implementation confirmed in the 6.7.0 ARM64 code.
This shared setup covers post effects, MeshFlare, HeightFog, and WaterSurface,
including seeks into a clip that does not begin at zero.

Absolute seek assigns one requested time to all six directors and immediately
evaluates them. Character body motions split across multiple clips use a nested
`AnimationMixerPlayable`: cumulative clip start times select exactly one active
input, the selected clip receives segment-local time, and the final endpoint is
held after the total duration. There is no crossfade, velocity integration, or
second root-position correction. This small runtime primitive follows the
public ClauseKAI Unity behavior while retaining the authored `Position` and
`PositionOffset` hierarchy.

The confirmed data-only part of the player chain is also implemented without
inventing object behavior: unpadded main-MV and six-digit CutIn bundle paths,
plus compatibility with the four-digit main-MV directories present in the
current exported catalog, main-character
counting that excludes insert slots, field-for-field inherited-stage
resolution, and the camera's three separate arrays for actual character
height, actual heel offset, and MV-default heel offset. Camera vertical
adjustment uses the recovered formula
`actualHeight * (actualHeelOffset + 0.883) - selectedDefaultHeight *
(mvDefaultHeelOffset + 0.883)` and clamps the LateUpdate blend factor before
interpolating the two target offsets. The normal 3DMV light array is preserved
in its recovered order: GlobalSettings, AmbientLight, DirectionalLight,
SpotLight, CharacterRimLight, CharacterAmbientLight, and ShadowLight. The enum
also records ShadowLight_VL, FlareLight, and PointLight, which are not members
of that seven-item array. These are data contracts; object creation remains the
responsibility of the corresponding node. The recovered
`Background3DPlayer.OnLoad` renderer policy is available as
`MvPlayerRenderSettings`: character skinned meshes cast and receive no Unity
shadows, generate no motion vectors, and use neither light nor reflection
probes. These helpers are ready for the corresponding object nodes; they do not
stand in for those nodes.

Stage override dictionaries follow the recovered precedence as well: entries
from the current MV are inserted first, then additional MV dictionaries only
fill absent texture names in their declared order.

The directly recoverable player nodes are now wired. `MvCharacterNode` keeps a
single `Position` hierarchy, grafts missing face/accessory branches into it,
remaps every attached `SkinnedMeshRenderer` bone, disables Animator root motion,
applies the official renderer policy, and writes the slot binding into the
shared Timeline dictionary. Normal and insert tracks have separate index
domains (`Character{n}` and `Character{n}_insert`), and each selected key also
registers the exact `{key}_MV` alias when the loaded Timeline declares either
form. Runtime-selected characters must supply
their body, face, master `characterHeight` in centimetres, and heel offset.
Standard 3DMV fixes the setup height rate to `1.0`, so final model height is
`characterHeight * 0.01` metres
and is retained for the recovered `CharacterModel.Setup` and camera contracts;
it is not reinterpreted as a direct `Position.localScale`. Fixed MVData models
resolve body, face, and head optional independently with the recovered
V2-first/V1-fallback rule, but still require the runtime member's master
height. Gender tracks can be selected explicitly through
`timelineBindingName`; automatic `MotionType.Gender` dispatch remains outside
the stable contract until its serialized enum value is recovered.
Standalone multi-clip motion is available only when an original Character
Timeline is not driving the same Animator.

`MvStageNode` loads the base stage and ordered decorations, applies parent-stage
inheritance and current-before-additional texture precedence, clones CutIn
materials, and replaces exactly the recovered `_MainTex`, `_ColorTex`,
`_LightMapTex`, and `_SubTex` slots. Current decorations remain authored;
additional inherited decorations receive the optional override. It binds
`ControlGroupBase`, TextMeshPro typewriter/overwrite targets, indexed
`StageObjDrawCameraSelectController` targets, and the planar-reflection
`WaterSurfaceController` when those recovered components are present.
Every replaced source `Texture2D` is retained by name for the later monitor
refresh/default-texture contract.
`MvCameraAdjustment` keeps the three official height
arrays separate and applies the recovered two-target LateUpdate formula. The
player assembler constructs main/CutIn roots and optional audio; the playback
coordinator owns their exclusive activation, shared absolute clock, and
coordinated disposal.

`MvPenlightNode` loads `live_pv/model/penlight/{id}` at address `penlight`, calls
the recovered `PenlightParameter.Initialize()` when that component is present,
then binds the root and every child Transform name to its GameObject. It does
not synthesize Penlight children or animation constants when the original
component script is absent.

The evidence-closed object runtime is now present rather than represented by
generic placeholders. It includes `CameraNode`, the seven-category `LightNode`,
the main/CutIn `MusicVideoModel` registry, the recovered custom Timeline
Track/Clip field layouts and mixers, HeightFog/WaterSurface state, LiveEffect,
LiveMonitor, Penlight parameter packing, ExtraBone, both Sekai and UTJ spring
components, character hair/eye setup, and the official Neck/Head face-skeleton
graft. `MotionType.Gender` selects the recovered
`Character{formationIndex}_Male/Female` key from the member's master figure;
the other motion types retain the normal or insert key, as the original
`GetCharacterKey` does. Camera decorations are loaded from their original
bundle and their root/TextMeshPro bindings are registered. These components
preserve serialized state and Timeline behavior, but state alone cannot produce
the final pixels without the renderer artifacts listed below.

### 0112 reconstruction audit (2026-08-15)

The source-set manifest now contains the complete known 0112 launch closure:
34 independently cacheable bundles totalling 46,423,867 bytes. It includes the
previously omitted MeshFlare common controller, WaterCaustics, and WaterEye
preset bundles. No raw mobile bundle is merged into a song-wide package.

The current strict build does not set `HARUKI_MV_ALLOW_DUMMY_SHADERS` or
`HARUKI_MV_ALLOW_RENDER_ERRORS`. Unity EditMode reports 190/190 tests passed,
the recovered-source WebGL build reports `Build Finished, Result: Success`,
and headless Chromium passes Unity startup plus the 4K-query/1080p-backing-
buffer render-profile smoke. An earlier full 0112 browser exercise also loaded
the stage and five characters, assembled the main MV and one CutIn, reported
the authored duration as 181.216666666666 seconds, and passed
play/pause/seek/retry/dispose. The 2026-08-15 smoke proves the newly built player
starts; it does not repeat that expensive full-song visual comparison.

The active character shader path now covers the captured Toon-v3 lighting,
FaceSDF controller values, eyelash clipping, hair shadow, outline, rim branch,
eye distortion/flipbook/highlight, fog, spotlight, CoC, and three MRT outputs.
The runtime also drives the captured per-frame eye clock, face/light vectors,
formation light arrays, main/CutIn global ownership, WaterEye presets, and the
IL2CPP-confirmed MeshFlare position/scale/theta formulas. The recovered old
Stage ColorMap, Texture, LightMap, LightMap-Transparent, LightMap-Cutout and
LightMap-Emission aliases use their exact formulas; unrelated families retain
the bounded WebGL-compatible fallback until a selected MV proves they are
needed.

Browser audio deliberately ends at a conventional Unity `AudioClip` boundary.
The publishing side decodes the selected CRI cue, writes a lossless PCM
intermediate, encodes a browser-supported Ogg Vorbis stream, imports it with the
same Unity 2022.3 editor used for the WebGL build, and publishes the resulting
auxiliary WebGL AssetBundle. `loadMv.audioBundleName` and
`loadMv.audioAssetName` are an atomic pair and address that clip. The runtime
does not ship a second CRI implementation in WebAssembly: the `AudioSource`
clock remains authoritative and Timeline applies the recovered ±63 ms follower
window. A release gate must compare decoded duration/sample rate/channel count
with the selected cue and measure start, seek, and ten-minute drift before the
audio bundle is promoted.

That successful build closes compilation and startup, not pixel identity. The
2026-08-15 static evidence additionally closes the WaterCaustics projection and
blend, both MeshFlare fragment modes, RenderCanvas prefab, modern stage program
catalog, character MRT variant boundary, three MusicItem assets, and the
audio-synchronized clock. WaterCaustics and MeshFlare are now implemented;
RenderCanvas is intentionally absent from ordinary 3DMV because four runtime
captures confirm that path is not invoked there. The remaining matrix mixes
implementation work with the few evidence gaps that genuinely remain.

| Artifact / behavior | Evidence boundary | Runtime impact | Current behavior | Required next action or evidence |
| --- | --- | --- | --- | --- |
| Per-family stage shader implementation | All 111 modern Shader objects, 1084 Vulkan programs, keyword tables, exact old LightMap-family formulas and the new RP Stage-family formulas are now available. | Aliased monitor, reflection, particle and RP families can still differ visually. | Old ColorMap, Texture, LightMap, LightMap-Transparent, LightMap-Cutout and LightMap-Emission use their recovered formulas. Reflection and unrelated generic families retain bounded compatibility shaders. | Port the remaining material families only when a selected MV uses them; Reflection must consume the real planar-reflection inputs rather than a visual approximation. |
| Character brightness MRT implementation | Variant counts and attachment layouts are closed, including the pre-fog rim/specular plus material contribution formula and the two-output Pearl/Eye exceptions. | Bloom can still differ on uncommon Accessory/CollaboE variants. | Active Toon-v3 Base now writes the recovered pre-fog `rim * formationBrightness + surface * ValueTex.g` Target2; Outline/Eyelash preserve their corresponding contribution. | Port an uncommon material family only when encountered; never emit a third target for the confirmed two-output Pearl/Eye families. |
| Proprietary `SekaiRenderer` and LensFlare | Renderer allocation/swap/release RVAs, SekaiBuffer attachments, pass ordering, final CoreBlit and LensFlare call chain are closed. No sampled MV enables LensFlare. | Ordinary output is covered; an enabled LensFlare may have wrong blend/depth/material state. | Stock `UniversalRenderer` hosts the recovered Sekai feature graph and shared attachments. | Obtain one `enableLensFlare=1` draw capture before claiming lens-flare pixels; no change is needed for MV0112 where the feature is disabled. |
| Character finishing edge cases | Color-variation paths, skin colors, accessory transform lookup/application, and final MotionType binding names are closed. | Unique/GenderUnique member selection still lacks a real upstream sample. | Body/head C/S/H variation textures, optional master skin-color triples and the serialized face-key accessory transform controller are wired into the load path; missing accessory keys preserve the official zero-scale result. | Obtain a real Unique/GenderUnique MV only for the earlier member selector. |
| Music items | Bundles `0003`, `0007`, and `0010`, prefab structure, controller call chain, padded bundle path and exact binding-name format are recovered. MV0112 contains none. | The implementation has no positive song-level opacity/UV binding sample yet. | The player loads `item`, applies official height/heel targets, formation/shader/visibility state, and binds animation, opacity and UV tracks. | Validate the closed implementation against one Timeline that actually declares the recovered MusicItem track names. |
| CRI/BGM decode | Cue-sheet load and selection, prepare/start sequence, abnormal-length fallback, and the official ±63 ms audio-synchronized visual clock are closed. | Browser publishing still needs measured drift limits. | The documented server contract converts a selected cue to an Ogg-backed Unity WebGL `AudioClip`; the recovered clock drives Timeline and the request requires an atomic bundle/address pair. | Build one production audio bundle and record start, seek and ten-minute A/V drift. Reimplementing CRI in WebAssembly is not required. |
| Fixed-timestamp visual identity | One complete ordinary 0112 lifecycle and non-black render are known. There is no authoritative game-vs-WebGL frame corpus. | Pixel regressions cannot be measured automatically. | Startup, lifecycle, buffers, resolution contracts, and non-black output are testable; visual identity is a manual boundary. | Matched game/WebGL frames plus camera, light, material, MRT, and post-effect dumps at selected timestamps. |
| Feature coverage corpus | 0112 covers five characters and one CutIn family, but not every engine feature. | Untested features may compile yet fail when first encountered. | Unknown effects are disabled or rejected instead of silently replaced. | Small source sets with music items, fog, reflection, active distortion, active caustics, modern V2-only parts, and multiple CutIns; special 26-character mode remains separate. |
| Reproducible exporter source build | The released exporter emitted the verified 34-bundle manifest. | Reproduction still depends on the sibling AssetStudio-Haruki checkout being built. | The project now resolves `../AssetStudio-Haruki` portably instead of a developer-specific Windows mount. | Rebuild the same manifest and compare bundle names, sizes and hashes. |

The Unity build performs a hard preflight over recovered YAML. It names every
unresolved MonoBehaviour, rejects referenced dummy shaders, requires an assigned
SRP, and checks that renderer indices 5 and 10 contain the captured feature
names and component types in order. Missing base-APK camera Resources use the
bounded reconstruction above; that does not waive the remaining matrix.

### ClauseKai public-build boundary

The ClauseKai WebGL build is useful as a behavioral oracle, but it is not a
drop-in copy of Sekai's camera/light runtime. Its player data contains only the
demo bootstrap `Main Camera` and `Directional Light`; it does not contain the
complete official `Core/Common/Camera/MainCamera_MV` or
`Core/Common/Camera/SubCamera` prefab hierarchy. The 6.7.0 runtime dump is the
authoritative source for reconstructing these resources; ClauseKai is not.
Its dependency set contains the original `live_pv/timeline/0110/camera` bundle,
but no corresponding light Timeline bundle.

For MV 0110 ClauseKai instead ships adapter-specific, 30 fps sampled sidecars:

- `mv_cameras/0110.json` contains main-camera position, Euler rotation, field of
  view, DOF focus distance, character-height adjustment, and sub-camera samples;
- `mv/0110_light.json` contains global/fog, ambient, directional, six character
  rim-light, and six character ambient-light sample streams.

Those files confirm field meaning, update order, shader-global names, degree to
radian conversion, and six-slot light cardinality. They do not recover the
official prefab graph, reusable Timeline clips for every MV, original custom
Track/Clip C# code, or Unity script GUIDs. Consequently the sampled 0110 data is
kept as research evidence rather than installed as a generic `CameraNode` or
`LightNode`. A compatibility importer may consume such sidecars explicitly,
but the official path must continue to require the original per-MV assets and
real runtime components.

After building, run a real headless-Chromium startup smoke with:

```bash
npm run test:mv:browser
```

Set `HARUKI_MV_BUNDLE_SET` to a directory containing WebGL-target bundles and
`deps.json` to additionally exercise dependency loading. The generated test
page exposes `window.harukiMvUnityInstance`; this is the same Unity instance the
typed host wraps and is useful for non-UI integration shells.
Set `HARUKI_MV_PREFAB_BUNDLE` and optionally `HARUKI_MV_PREFAB_ASSET` to also
instantiate a rebuilt prefab. `HARUKI_MV_SCREENSHOT` writes the rendered canvas
for visual smoke verification.

## Hosting headers

Compressed Unity files require matching response headers. For a gzip build:

| File | Content-Type | Content-Encoding |
| --- | --- | --- |
| `*.wasm.gz` | `application/wasm` | `gzip` |
| `*.framework.js.gz` | `application/javascript` | `gzip` |
| `*.data.gz` | `application/octet-stream` | `gzip` |

Use `br` for Brotli builds. StreamingAssets bundles should be served as binary
files and remain same-origin or carry the required CORS headers. Incorrect
compression headers make Unity fall back from streaming compilation or fail
startup entirely.
