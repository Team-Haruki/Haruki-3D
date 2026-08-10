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
to the matching Unity Timeline 1.7.6 types before rebuilding. The remap is
limited to `timeline.playable` YAML; binary textures and other recovered assets
are copied byte-for-byte into the Unity import tree. Sekai-specific Track and
Clip classes are not substitutes for built-in Timeline types. Until their
original behavior is recovered and implemented, Unity reports those scripts as
missing and the corresponding effects remain unsupported.

Pushes that touch the MV project also run `.github/workflows/unity-mv.yml`.
The workflow runs EditMode tests, compiles the real WebGL player, validates its
four generated build files, and uploads `haruki-3dmv-webgl`. It needs the
protected `unity-build` environment secrets `UNITY_LICENSE`, `UNITY_EMAIL`, and
`UNITY_PASSWORD`; moving the build to GitHub does not remove Unity's
editor-license requirement. `UNITY_LICENSE` must contain the activated `.ulf`
file expected by GameCI. Do not upload the machine-local
`UnityEntitlementLicense.xml`, and do not commit either file. The repository
ignores `.alf`, `.ulf`, and entitlement files and tests that none are tracked.
The GameCI actions are pinned to full commit SHAs so a moving tag cannot change
the code that receives those secrets.

The committed Unity project provides a browser bridge, recursive AssetBundle
dependency loading, additive scene loading, and one playback coordinator for
every scene root. Its exact recovered Timeline binding uses
`PlayableBinding.streamName` to select the shared target and passes
`PlayableBinding.sourceObject` to `PlayableDirector.SetGenericBinding`, then
sets time to zero and evaluates. Original Sekai scripts and custom Timeline
types remain inputs; the bridge does not pretend to reproduce missing game
behavior.

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

This is not yet a complete `Background3DPlayer`. `CameraNode`, `LightNode`,
the game-global `MusicVideoModel` registration layer, and
Sekai's custom Timeline Track/Clip runtime remain unimplemented until their
recovered behavior is landed. Stage feature flags that require the missing
HeightFog, reflection, distortion, monitor, or water controllers are preserved
as data but are not replaced with generic Unity effects. The lower-level
runtime still exposes `sendMessage()` for original project methods that are not
part of this stable host contract.
CutIn root/Timeline ordering is implemented; model-state, SpringBone suppression,
transition color, and sub-camera switching still depend on those original game
components and are not simulated here.

The remaining hard boundaries are deliberate. The complete Transform hierarchy
of `Camera/MainCamera_MV` and `Camera/SubCamera` is not present. Stage component
targets, override precedence, and all four supported texture properties are now
known. Penlight's binding target is confirmed as `Transform`; its original
`PenlightParameter` implementation still has to be imported with the recovered
script rather than replaced by a guessed clone. Released AssetBundles preserve
custom Timeline class, assembly,
and bundle-local MonoScript PathID, but not the original Unity project `.meta`
GUID. Recovered dummy scripts therefore cannot safely be promoted to empty
Track/Clip implementations: each type still needs its exact serialized fields
and runtime behavior, after which the rebuild may deliberately remap it to this
project's own script GUID. Until those facts are closed, the player fails
explicitly instead of silently substituting a generic camera, light rig, stage
controller, penlight driver, or no-op Timeline track.

### ClauseKai public-build boundary

The ClauseKai WebGL build is useful as a behavioral oracle, but it is not a
drop-in copy of Sekai's camera/light runtime. Its player data contains only the
demo bootstrap `Main Camera` and `Directional Light`; it does not contain the
complete official `Camera/MainCamera_MV` or `Camera/SubCamera` prefab hierarchy.
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
