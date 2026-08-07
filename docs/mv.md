# Haruki 3DMV Web Host

The `haruki-3d-engine/mv` module hosts one original Unity WebGL/WASM build. It
does not translate MV scenes, Timeline tracks, character assembly, or Sekai's
URP renderer into Three.js.

## Start a generated Unity build

```ts
import {
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
mv.sendMessage(bridgeObjectName, loadMethodName, JSON.stringify(request));

// On page/component disposal:
await mv.destroy();
```

The loader script is shared by URL. Unity instance creation is single-flight;
a failed creation may be retried. Destroying during creation waits for the
instance and calls Unity `Quit()` exactly once.

`state` reports `idle`, `loading`, `ready`, `failed`, `destroying`, or
`destroyed`. `getMemoryInfo()` exposes Unity's WASM/JS heap counters when the
generated loader supports them. Product UI and user-facing errors remain host
concerns.

## Unity build contract

The Unity project owns the actual `HarukiMvBridge`. Its coordinator must keep
the official runtime ordering:

1. load the manifest and recursive bundle dependency closure;
2. construct stage and characters using the original Unity object graph;
3. bind character graphs and all PlayableDirectors;
4. attach light, effect, monitor, water, post-processing, and audio systems;
5. start playback only after scene and audio preparation completes;
6. apply pause and absolute seek to every director, graph, driver, post effect,
   spring policy, and audio follower;
7. dispose scene instances, PlayableGraphs, bundles, and caches together.

Character motion keeps the authored `Position` transform and
`Animator.applyRootMotion = false`. A stopped motion holds its sampled pose; the
host must not add a position-reset or anti-sliding correction. Do not enable a
JSON fallback driver for a property already driven by an original Timeline.

The JavaScript module deliberately exposes raw `sendMessage()` because the
Unity project's bridge method names and request schema are its interface. They
must not be guessed in the browser package.

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
