import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { chromium } from "@playwright/test";

const buildRoot = path.resolve(
  process.env.HARUKI_MV_BUILD_ROOT ?? "unity/Haruki3DMV/Build/HarukiMV"
);
const bundleRoot = process.env.HARUKI_MV_BUNDLE_SET
  ? path.resolve(process.env.HARUKI_MV_BUNDLE_SET)
  : null;
const prefabBundle = process.env.HARUKI_MV_PREFAB_BUNDLE ?? null;
const prefabAsset = process.env.HARUKI_MV_PREFAB_ASSET ?? "stage";
const mvDataBundle = process.env.HARUKI_MV_DATA_BUNDLE ?? null;
const expectedMusicId = Number(process.env.HARUKI_MV_EXPECT_MUSIC_ID ?? 0);
const assembleSampleMv = process.env.HARUKI_MV_ASSEMBLE_SAMPLE === "1";
const enableSampleCutIns = process.env.HARUKI_MV_ENABLE_CUTINS === "1";
const skipSampleMusicInfo = process.env.HARUKI_MV_SKIP_MUSIC_INFO === "1";
const logBrowserConsole = process.env.HARUKI_MV_LOG_CONSOLE === "1";
const allowRenderErrors = process.env.HARUKI_MV_ALLOW_RENDER_ERRORS === "1";
const seekSeconds = Number(process.env.HARUKI_MV_SEEK_SECONDS ?? 12.5);
const audioStartTimeoutMs = Number(process.env.HARUKI_MV_AUDIO_START_TIMEOUT_MS ?? 120_000);
const screenshotPath = process.env.HARUKI_MV_SCREENSHOT
  ? path.resolve(process.env.HARUKI_MV_SCREENSHOT)
  : null;

const mimeTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "application/javascript"],
  [".css", "text/css"],
  [".json", "application/json"],
  [".wasm", "application/wasm"],
]);

function resolveRequest(requestPath) {
  const decoded = decodeURIComponent(requestPath.split("?", 1)[0]);
  const [root, relative] = decoded.startsWith("/bundles/") && bundleRoot
    ? [bundleRoot, decoded.slice("/bundles/".length)]
    : [buildRoot, decoded === "/" ? "index.html" : decoded.replace(/^\//, "")];
  const filePath = path.resolve(root, relative);
  if (filePath !== root && !filePath.startsWith(`${root}${path.sep}`)) {
    return null;
  }
  return filePath;
}

const server = http.createServer((request, response) => {
  const filePath = resolveRequest(request.url ?? "/");
  if (!filePath || !fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
    response.writeHead(404).end();
    return;
  }

  const isGzip = filePath.endsWith(".gz");
  const contentPath = isGzip ? filePath.slice(0, -3) : filePath;
  response.setHeader("Content-Type", mimeTypes.get(path.extname(contentPath)) ?? "application/octet-stream");
  if (isGzip) response.setHeader("Content-Encoding", "gzip");
  response.setHeader("Cache-Control", "no-store");
  fs.createReadStream(filePath).pipe(response);
});

await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const address = server.address();
const browser = await chromium.launch({
  headless: true,
  args: ["--enable-unsafe-swiftshader", "--use-angle=swiftshader"],
});

try {
  const page = await browser.newPage();
  const bridgeDispatchErrors = [];
  const rendererErrors = new Set();
  page.on("console", (message) => {
    const value = message.text();
    if (/MissingMethodException: Method 'Haruki\.MV\./.test(value)) {
      bridgeDispatchErrors.push(value);
    }
    if (/GL_INVALID_OPERATION: glDrawElements: Active draw buffers with missing fragment shader outputs/.test(value) ||
        /doesn't have a float or range property/.test(value)) {
      rendererErrors.add(value);
    }
    if (logBrowserConsole) {
      console.log(`[browser:${message.type()}] ${message.text()}`);
    }
  });
  if (logBrowserConsole) {
    page.on("pageerror", (error) => {
      console.log(`[browser:pageerror] ${error.stack ?? error.message}`);
    });
  }
  await page.addInitScript(() => {
    window.harukiMvEvents = [];
    window.addEventListener("haruki-mv", (event) => window.harukiMvEvents.push(event.detail));
  });
  await page.goto(`http://127.0.0.1:${address.port}/`, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(
    () => window.harukiMvEvents.some((event) => event.type === "ready"),
    null,
    { timeout: 120_000 }
  );

  const requestRenderProfile = async (method, type, request) => {
    await page.evaluate(
      ({ method, request }) => window.harukiMvUnityInstance.SendMessage(
        "HarukiMvBridge",
        method,
        JSON.stringify(request)
      ),
      { method, request }
    );
    await page.waitForFunction(
      ({ type, requestId }) => window.harukiMvEvents.some((event) => {
        if (event.type !== type) return false;
        try {
          return JSON.parse(event.payload).requestId === requestId;
        } catch {
          return false;
        }
      }),
      { type, requestId: request.requestId },
      { timeout: 30_000 }
    );
    return page.evaluate(
      ({ type, requestId }) => {
        const event = [...window.harukiMvEvents].reverse().find((candidate) => {
          if (candidate.type !== type) return false;
          try {
            return JSON.parse(candidate.payload).requestId === requestId;
          } catch {
            return false;
          }
        });
        return JSON.parse(event.payload);
      },
      { type, requestId: request.requestId }
    );
  };
  const invokeAndReadState = async (method, payload = "") => {
    const previousCount = await page.evaluate(() =>
      window.harukiMvEvents.filter((event) => event.type === "state").length
    );
    await page.evaluate(
      ({ method, payload }) => window.harukiMvUnityInstance.SendMessage(
        "HarukiMvBridge",
        method,
        payload
      ),
      { method, payload }
    );
    await page.waitForFunction(
      (count) => window.harukiMvEvents.filter((event) => event.type === "state").length > count,
      previousCount,
      { timeout: 30_000 }
    );
    return page.evaluate(() => JSON.parse(
      [...window.harukiMvEvents].reverse().find((event) => event.type === "state").payload
    ));
  };
  const profileRequest = (requestId, outputResolution, width, height) => ({
    requestId,
    width,
    height,
    dpi: 0,
    refreshRate: 60,
    quality: 0,
    playMode: 4,
    outputResolution,
    use120Fps: false,
  });
  const fourK = await requestRenderProfile(
    "GetRenderProfile",
    "render-profile-ready",
    profileRequest("smoke-profile-4k", 4, 3840, 2160)
  );
  if (fourK.renderWidth !== 3840 || fourK.renderHeight !== 2160) {
    throw new Error(`Unity 4K profile mismatch: ${JSON.stringify(fourK)}`);
  }
  const fullHd = await requestRenderProfile(
    "ApplyRenderProfile",
    "render-profile-applied",
    profileRequest("smoke-profile-1080p", 2, 1920, 1080)
  );
  if (fullHd.renderWidth !== 1920 || fullHd.renderHeight !== 1080) {
    throw new Error(`Unity 1080p profile mismatch: ${JSON.stringify(fullHd)}`);
  }
  await page.waitForFunction(
    () => {
      const canvas = document.querySelector("#unity-canvas");
      return canvas?.width === 1920 && canvas?.height === 1080;
    },
    null,
    { timeout: 30_000 }
  );
  const displayAspect = await page.evaluate(() => {
    const canvas = document.querySelector("#unity-canvas");
    const bounds = canvas.getBoundingClientRect();
    return bounds.width / bounds.height;
  });
  if (Math.abs(displayAspect - (16 / 9)) > 0.001) {
    throw new Error(`Unity display canvas is not 16:9: ${displayAspect}`);
  }
  console.log("Unity WebGL render-profile smoke passed: 4K query and 1080p backing buffer.");

  if (bundleRoot) {
    await page.evaluate(() => {
      window.harukiMvUnityInstance.SendMessage(
        "HarukiMvBridge",
        "LoadBundleSet",
        JSON.stringify({ baseUrl: `${location.origin}/bundles` })
      );
    });
    await page.waitForFunction(
      () => window.harukiMvEvents.some((event) => event.type === "bundle-set-ready" || event.type === "error"),
      null,
      { timeout: 180_000 }
    );
    const result = await page.evaluate(() => window.harukiMvEvents.at(-1));
    if (result.type === "error") {
      throw new Error(`Unity bundle-set smoke failed: ${result.payload}`);
    }
    console.log(`Unity WebGL bundle-set smoke passed: ${result.payload}`);
    if (mvDataBundle) {
      await page.evaluate(
        ({ bundleName }) => {
          window.harukiMvUnityInstance.SendMessage(
            "HarukiMvBridge",
            "ReadMvData",
            JSON.stringify({ bundleName, assetName: "data" })
          );
        },
        { bundleName: mvDataBundle }
      );
      await page.waitForFunction(
        () => window.harukiMvEvents.some(
          (event) => event.type === "mv-data-ready" || event.type === "error"
        ),
        null,
        { timeout: 120_000 }
      );
      const dataResult = await page.evaluate(() =>
        [...window.harukiMvEvents].reverse().find(
          (event) => event.type === "mv-data-ready" || event.type === "error"
        )
      );
      if (dataResult.type === "error") {
        throw new Error(`Unity MVData smoke failed: ${dataResult.payload}`);
      }
      const mvDataPayload = JSON.parse(dataResult.payload);
      const mvData = typeof mvDataPayload.dataJson === "string"
        ? JSON.parse(mvDataPayload.dataJson)
        : mvDataPayload;
      if (!Number.isInteger(mvData.id) || !Number.isInteger(mvData.stageInfo?.id)) {
        throw new Error(`Unity MVData smoke returned an incomplete payload: ${dataResult.payload}`);
      }
      if (expectedMusicId > 0 && mvData.id !== expectedMusicId) {
        throw new Error(`Unity MVData expected music ${expectedMusicId}, received ${mvData.id}.`);
      }
      console.log(
        `Unity WebGL MVData smoke passed: music=${mvData.id}, ` +
        `stage=${mvData.stageInfo.id}, characters=${mvData.characterInfos?.length ?? 0}`
      );
    }
    if (assembleSampleMv) {
      await page.evaluate(
        (request) => {
          window.harukiMvUnityInstance.SendMessage(
            "HarukiMvBridge",
            "LoadMv",
            JSON.stringify(request)
          );
        },
        {
          musicId: expectedMusicId,
          enableCutIns: enableSampleCutIns,
          canSkipDisplayMusicInfo: skipSampleMusicInfo,
          characters: [
            {
              characterId: 5,
              bodyBundleName: "live_pv/model/characterv2/body/05/0001/ladies_m",
              faceBundleName: "live_pv/model/characterv2/face/05/0001",
              characterHeight: 158,
            },
            {
              characterId: 7,
              bodyBundleName: "live_pv/model/characterv2/body/07/0001/ladies_m",
              faceBundleName: "live_pv/model/characterv2/face/07/0001",
              characterHeight: 156,
            },
            {
              characterId: 6,
              bodyBundleName: "live_pv/model/characterv2/body/06/0001/ladies_m",
              faceBundleName: "live_pv/model/characterv2/face/06/0001",
              characterHeight: 163,
            },
            {
              characterId: 8,
              bodyBundleName: "live_pv/model/characterv2/body/08/0001/ladies_s",
              faceBundleName: "live_pv/model/characterv2/face/08/0001",
              characterHeight: 168,
            },
            {
              characterId: 22,
              bodyBundleName: "live_pv/model/characterv2/body/22/0003/ladies_s",
              faceBundleName: "live_pv/model/characterv2/face/22/0003",
              characterHeight: 152,
            },
          ],
          cutIns: enableSampleCutIns
            ? [{
                musicId: 101120,
                reuseMainMember: false,
                characters: [{
                  characterId: 5,
                  bodyBundleName: "live_pv/model/characterv2/body/05/9001/ladies_m",
                  faceBundleName: "live_pv/model/characterv2/face/05/9001",
                  characterHeight: 158,
                }],
              }]
            : [],
        }
      );
      await page.waitForFunction(
        () => window.harukiMvEvents.some(
          (event) => event.type === "mv-ready" || event.type === "error"
        ),
        null,
        { timeout: 120_000 }
      );
      const playerResult = await page.evaluate(() =>
        [...window.harukiMvEvents].reverse().find(
          (event) => event.type === "mv-ready" || event.type === "error"
        )
      );
      if (playerResult.type === "error") {
        throw new Error(`Unity MV assembly smoke failed: ${playerResult.payload}`);
      }
      console.log(`Unity WebGL MV assembly smoke passed: ${playerResult.payload}`);

      const initialState = await invokeAndReadState("GetState");
      const expectedInitialTime = skipSampleMusicInfo ? 5.5 : 0;
      if (initialState.state !== "paused" ||
          Math.abs(initialState.timeSeconds - expectedInitialTime) > 0.001) {
        throw new Error(`Unity MV initial state mismatch: ${JSON.stringify(initialState)}`);
      }
      const playingState = await invokeAndReadState(
        "SetPaused",
        JSON.stringify({ paused: false })
      );
      if (playingState.state !== "preparing") {
        throw new Error(`Unity MV did not enter preparing state: ${JSON.stringify(playingState)}`);
      }
      let progressedState;
      const audioStartDeadline = Date.now() + audioStartTimeoutMs;
      do {
        await page.waitForTimeout(250);
        progressedState = await invokeAndReadState("GetState");
      } while (!(progressedState.timeSeconds > expectedInitialTime) &&
          Date.now() < audioStartDeadline);
      if (!(progressedState.timeSeconds > expectedInitialTime)) {
        const stalledDiagnostics = await requestRenderProfile(
          "GetDiagnostics",
          "diagnostics-ready",
          { requestId: "smoke-stalled-audio-diagnostics" }
        );
        throw new Error(
          `Unity MV clock did not advance: ${JSON.stringify({
            state: progressedState,
            audioLoadState: stalledDiagnostics.audioLoadState,
            audioStarted: stalledDiagnostics.audioStarted,
            audioIsPlaying: stalledDiagnostics.audioIsPlaying,
            audioTimeSeconds: stalledDiagnostics.audioTimeSeconds,
          })}`
        );
      }
      if (progressedState.state !== "playing") {
        throw new Error(`Unity MV did not enter playing state: ${JSON.stringify(progressedState)}`);
      }
      const playingDiagnostics = await requestRenderProfile(
        "GetDiagnostics",
        "diagnostics-ready",
        { requestId: "smoke-playing-diagnostics" }
      );
      if (playingDiagnostics.audioClipName !== "se_0112_01" ||
          playingDiagnostics.audioLoadState !== "Loaded" ||
          playingDiagnostics.audioStarted !== true ||
          playingDiagnostics.audioIsPlaying !== true ||
          !(playingDiagnostics.audioDurationSeconds > 127) ||
          !(playingDiagnostics.audioTimeSeconds > expectedInitialTime)) {
        throw new Error(
          `Unity MV audio did not start correctly: ${JSON.stringify(playingDiagnostics)}`
        );
      }
      const pausedState = await invokeAndReadState(
        "SetPaused",
        JSON.stringify({ paused: true })
      );
      await page.waitForTimeout(250);
      const frozenState = await invokeAndReadState("GetState");
      if (frozenState.state !== "paused" ||
          Math.abs(frozenState.timeSeconds - pausedState.timeSeconds) > 0.001) {
        throw new Error(`Unity MV clock advanced while paused: ${JSON.stringify(frozenState)}`);
      }
      const soughtState = await invokeAndReadState(
        "Seek",
        JSON.stringify({ timeSeconds: seekSeconds })
      );
      if (Math.abs(soughtState.timeSeconds - seekSeconds) > 0.001) {
        throw new Error(`Unity MV seek mismatch: ${JSON.stringify(soughtState)}`);
      }
      await page.waitForTimeout(250);
      const diagnostics = await requestRenderProfile(
        "GetDiagnostics",
        "diagnostics-ready",
        { requestId: "smoke-diagnostics" }
      );
      const { materials, ...diagnosticSummary } = diagnostics;
      console.log(
        `Unity WebGL MV diagnostics: ${JSON.stringify({
          ...diagnosticSummary,
          materialCount: materials.length,
        })}`
      );
      if (enableSampleCutIns) {
        await page.evaluate(() => {
          window.harukiMvUnityInstance.SendMessage(
            "HarukiMvBridge",
            "SetCutInActive",
            JSON.stringify({ cutInOrder: 0, active: true })
          );
          window.harukiMvUnityInstance.SendMessage(
            "HarukiMvBridge",
            "SetCutInActive",
            JSON.stringify({ cutInOrder: 0, active: false })
          );
        });
      }
      if (screenshotPath) {
        await page.evaluate(() => new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve))
        ));
        fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });
        const screenshot = await page.locator("#unity-canvas").screenshot({ path: screenshotPath });
        const pixelStats = await page.evaluate(async (encodedPng) => {
          const image = new Image();
          image.src = `data:image/png;base64,${encodedPng}`;
          await image.decode();
          const surface = document.createElement("canvas");
          surface.width = image.naturalWidth;
          surface.height = image.naturalHeight;
          const context = surface.getContext("2d", { willReadFrequently: true });
          context.drawImage(image, 0, 0);
          const pixels = context.getImageData(0, 0, surface.width, surface.height).data;
          let visiblePixels = 0;
          for (let offset = 0; offset < pixels.length; offset += 4) {
            if (pixels[offset] > 8 || pixels[offset + 1] > 8 || pixels[offset + 2] > 8) {
              visiblePixels += 1;
            }
          }
          return { width: surface.width, height: surface.height, visiblePixels };
        }, screenshot.toString("base64"));
        if (pixelStats.visiblePixels === 0) {
          throw new Error(`Unity MV rendered an all-black frame: ${JSON.stringify(pixelStats)}`);
        }
        console.log(`Unity WebGL MV screenshot written to ${screenshotPath}`);
      }
      const retriedState = await invokeAndReadState("Retry");
      if (retriedState.state !== "paused" || retriedState.timeSeconds !== 0) {
        throw new Error(`Unity MV retry state mismatch: ${JSON.stringify(retriedState)}`);
      }
      console.log("Unity WebGL MV playback lifecycle smoke passed.");
    }
    if (prefabBundle) {
      await page.evaluate(
        ({ bundleName, assetName }) => {
          window.harukiMvUnityInstance.SendMessage(
            "HarukiMvBridge",
            "InstantiatePrefab",
            JSON.stringify({ bundleName, assetName, frameCamera: true })
          );
        },
        { bundleName: prefabBundle, assetName: prefabAsset }
      );
      await page.waitForFunction(
        () => window.harukiMvEvents.some((event) => event.type === "prefab-ready" || event.type === "error"),
        null,
        { timeout: 120_000 }
      );
      const prefabResult = await page.evaluate(() =>
        [...window.harukiMvEvents].reverse().find(
          (event) => event.type === "prefab-ready" || event.type === "error"
        )
      );
      if (prefabResult.type === "error") {
        throw new Error(`Unity prefab smoke failed: ${prefabResult.payload}`);
      }
      console.log(`Unity WebGL prefab smoke passed: ${prefabResult.payload}`);
      if (screenshotPath) {
        await page.evaluate(() => new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve))
        ));
        fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });
        await page.locator("#unity-canvas").screenshot({ path: screenshotPath });
        console.log(`Unity WebGL screenshot written to ${screenshotPath}`);
      }
    }
    if (assembleSampleMv) {
      await requestRenderProfile(
        "Dispose",
        "disposed",
        { requestId: "smoke-dispose" }
      );
      const disposedState = await invokeAndReadState("GetState");
      if (disposedState.state !== "empty" || disposedState.timeSeconds !== 0) {
        throw new Error(`Unity MV dispose state mismatch: ${JSON.stringify(disposedState)}`);
      }
      console.log("Unity WebGL MV disposal smoke passed.");
    }
  } else {
    console.log("Unity WebGL startup smoke passed.");
  }
  if (bridgeDispatchErrors.length !== 0) {
    throw new Error(
      `Unity bridge dispatch hit incompatible component methods:\n${bridgeDispatchErrors.join("\n")}`
    );
  }
  if (!allowRenderErrors && rendererErrors.size !== 0) {
    throw new Error(
      `Unity renderer emitted invalid draw/material errors:\n${[...rendererErrors].join("\n")}`
    );
  }
  if (allowRenderErrors && rendererErrors.size !== 0) {
    console.warn(
      "Unity renderer development bypass accepted known non-publishable errors:\n" +
      [...rendererErrors].join("\n")
    );
  }
} finally {
  await browser.close();
  await new Promise((resolve) => server.close(resolve));
}
