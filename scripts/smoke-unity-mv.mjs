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
      const reusableCharacter = {
        bodyBundleName: "live_pv/model/characterv2/body/05/9001/ladies_m",
        faceBundleName: "live_pv/model/characterv2/face/05/9001",
        characterHeight: 158,
      };
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
          characters: [
            reusableCharacter,
            reusableCharacter,
            reusableCharacter,
            reusableCharacter,
            { characterHeight: 158 },
          ],
          cutIns: enableSampleCutIns
            ? [{ musicId: 101120, reuseMainMember: true }]
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
  } else {
    console.log("Unity WebGL startup smoke passed.");
  }
} finally {
  await browser.close();
  await new Promise((resolve) => server.close(resolve));
}
