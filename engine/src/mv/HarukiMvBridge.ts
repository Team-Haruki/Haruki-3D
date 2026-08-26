import type { HarukiMvRuntime } from "./HarukiMvRuntime";

export const HARUKI_MV_BRIDGE_OBJECT = "HarukiMvBridge";

export type HarukiMvSceneRequest = {
  baseUrl: string;
  manifestBundleName: string;
  sceneBundleName: string;
  sceneName: string;
  audioObjectPath?: string;
  preloadBundleNames?: string[];
};

export type HarukiMvBundleSetRequest = {
  baseUrl: string;
  manifestName?: string;
  bundleSuffix?: string;
};

export type HarukiMvPrefabRequest = {
  bundleName: string;
  assetName: string;
};

export type HarukiMvAssetRequest = {
  bundleName: string;
  assetName: string;
};

export const HARUKI_MV_RENDER_PRESETS = {
  "720p": { width: 1280, height: 720 },
  "1080p": { width: 1920, height: 1080 },
  "1440p": { width: 2560, height: 1440 },
  "4k-uhd": { width: 3840, height: 2160 },
} as const;

export type HarukiMvFixedRenderResolution = keyof typeof HARUKI_MV_RENDER_PRESETS;

type HarukiMvRenderTiming = {
  refreshRate: number;
  use120Fps: boolean;
};

type HarukiMvDeviceRenderProfileRequest = HarukiMvRenderTiming & {
  resolution?: "device";
  width: number;
  height: number;
  dpi: number;
  quality: "default" | "high" | "virtual-live-default";
  playMode: "ingame-3dmv" | "music-video";
};

type HarukiMvFixedRenderProfileRequest = HarukiMvRenderTiming & {
  resolution: HarukiMvFixedRenderResolution;
};

type HarukiMvCustomRenderProfileRequest = HarukiMvRenderTiming & {
  resolution: "custom";
  width: number;
  height: number;
};

export type HarukiMvRenderProfileRequest =
  | HarukiMvDeviceRenderProfileRequest
  | HarukiMvFixedRenderProfileRequest
  | HarukiMvCustomRenderProfileRequest;

export type HarukiMvRenderProfile = {
  renderWidth: number;
  renderHeight: number;
  postEffectWidth: number;
  postEffectHeight: number;
  targetFrameRate: number;
};

export type HarukiMvCharacterRequest = {
  characterId?: number;
  bodyBundleName?: string;
  bodyAssetName?: string;
  /** Optional official C/S/H color-variation bundle for the body. */
  bodyColorBundleName?: string;
  faceBundleName?: string;
  faceAssetName?: string;
  headOptionalBundleName?: string;
  headOptionalAssetName?: string;
  /** Optional official C/S/H color-variation bundle for head_optional. */
  headOptionalColorBundleName?: string;
  /** Official MasterCostume3DModel.part bone used to mount head_optional. */
  headOptionalPart?: string;
  timelineBindingName?: string;
  standaloneMotionBundleName?: string;
  standaloneMotionAssetNames?: string[];
  /** Master character figure: true only for the game's man model family. */
  isFigureMan?: boolean;
  /** Master gameCharacters height in centimetres. */
  characterHeight: number;
  heelOffset?: number;
  /** Apply the three official MasterGameCharacterUnit skin colors below. */
  overrideSkinColors?: boolean;
  defaultSkinColor?: { r: number; g: number; b: number; a: number };
  shadow1SkinColor?: { r: number; g: number; b: number; a: number };
  shadow2SkinColor?: { r: number; g: number; b: number; a: number };
};

export type HarukiMvCutInRequest = {
  musicId: number;
  /** Normal CutIn: reuse the final main member selected by the child character ID. */
  reuseMainMember?: boolean;
  characters?: HarukiMvCharacterRequest[];
};

export type HarukiMvPlayerRequest = {
  musicId: number;
  enableCutIns?: boolean;
  /** Match official BootData: start at 5.5 seconds when music info is skipped. */
  canSkipDisplayMusicInfo?: boolean;
  characters: HarukiMvCharacterRequest[];
  cutIns?: HarukiMvCutInRequest[];
};

export type HarukiMvBridge = {
  loadBundleSet(request: HarukiMvBundleSetRequest): Promise<unknown>;
  instantiatePrefab(request: HarukiMvPrefabRequest): Promise<unknown>;
  readMvData(request: HarukiMvAssetRequest): Promise<unknown>;
  getRenderProfile(request: HarukiMvRenderProfileRequest): Promise<HarukiMvRenderProfile>;
  /** Applies the selected output pixels and frame-rate target inside Unity. */
  applyRenderProfile(request: HarukiMvRenderProfileRequest): Promise<HarukiMvRenderProfile>;
  loadMv(request: HarukiMvPlayerRequest): Promise<unknown>;
  setCutInActive(cutInOrder: number, active: boolean): void;
  loadScene(request: HarukiMvSceneRequest): Promise<unknown>;
  setPaused(paused: boolean): void;
  seek(timeSeconds: number): void;
  retry(): void;
  getState(): void;
  disposeScene(): Promise<unknown>;
};

let bridgeSequence = 0;

/** Typed browser-side contract for the Unity HarukiMvBridge component. */
export function createHarukiMvBridge(runtime: HarukiMvRuntime): HarukiMvBridge {
  let operationPending = false;
  const bridgeId = ++bridgeSequence;
  let requestSequence = 0;

  const sendAndWait = (
    method: string,
    request: unknown,
    successEvent: string
  ): Promise<unknown> => {
    if (operationPending) {
      throw new Error("Another MV bridge operation is still pending.");
    }
    const eventTarget = (
      globalThis as typeof globalThis & { window?: EventTarget }
    ).window;
    if (!eventTarget?.addEventListener) {
      throw new Error("MV bridge completion events require a browser window.");
    }
    if (runtime.signal?.aborted) {
      throw runtime.signal.reason instanceof Error
        ? runtime.signal.reason
        : new Error("Haruki MV runtime has been destroyed.");
    }

    const requestId = `haruki-mv-${bridgeId}-${++requestSequence}`;
    const correlatedRequest = {
      ...(request as Record<string, unknown>),
      requestId,
    };
    operationPending = true;
    return new Promise((resolve, reject) => {
      const cleanup = () => {
        operationPending = false;
        eventTarget.removeEventListener("haruki-mv", onEvent);
        runtime.signal?.removeEventListener("abort", onAbort);
      };
      const onAbort = () => {
        cleanup();
        reject(runtime.signal?.reason instanceof Error
          ? runtime.signal.reason
          : new Error("Haruki MV runtime has been destroyed."));
      };
      const onEvent = (event: Event) => {
        const detail = (event as CustomEvent<{ type?: string; payload?: string }>).detail;
        if (!detail || (detail.type !== successEvent && detail.type !== "error")) {
          return;
        }
        let payload: unknown = detail.payload;
        try {
          payload = detail.payload ? JSON.parse(detail.payload) : undefined;
        } catch {
          // Preserve a non-JSON Unity payload verbatim.
        }
        if (typeof payload !== "object" || payload === null ||
          !("requestId" in payload) ||
          (payload as { requestId?: unknown }).requestId !== requestId) {
          return;
        }
        cleanup();
        if (detail.type === "error") {
          const message = typeof payload === "object" && payload !== null &&
            "message" in payload
            ? String((payload as { message: unknown }).message)
            : String(payload ?? "MV bridge operation failed.");
          reject(new Error(message));
          return;
        }
        if (typeof payload === "object" && payload !== null &&
          "dataJson" in payload &&
          typeof (payload as { dataJson?: unknown }).dataJson === "string") {
          resolve(JSON.parse((payload as { dataJson: string }).dataJson));
          return;
        }
        resolve(payload);
      };
      eventTarget.addEventListener("haruki-mv", onEvent);
      runtime.signal?.addEventListener("abort", onAbort, { once: true });
      try {
        runtime.sendMessage(
          HARUKI_MV_BRIDGE_OBJECT,
          method,
          JSON.stringify(correlatedRequest)
        );
      } catch (error) {
        cleanup();
        reject(error);
      }
    });
  };

  return {
    loadBundleSet(request) {
      if (!request.baseUrl?.trim()) {
        throw new TypeError("MV bundle-set request baseUrl is required.");
      }
      return sendAndWait("LoadBundleSet", request, "bundle-set-ready");
    },
    instantiatePrefab(request) {
      if (!request.bundleName?.trim() || !request.assetName?.trim()) {
        throw new TypeError("MV prefab request bundleName and assetName are required.");
      }
      return sendAndWait("InstantiatePrefab", request, "prefab-ready");
    },
    readMvData(request) {
      if (!request.bundleName?.trim() || !request.assetName?.trim()) {
        throw new TypeError("MV data request bundleName and assetName are required.");
      }
      return sendAndWait("ReadMvData", request, "mv-data-ready");
    },
    getRenderProfile(request) {
      return sendAndWait(
        "GetRenderProfile",
        createRenderProfilePayload(request),
        "render-profile-ready"
      ) as Promise<HarukiMvRenderProfile>;
    },
    applyRenderProfile(request) {
      return sendAndWait(
        "ApplyRenderProfile",
        createRenderProfilePayload(request),
        "render-profile-applied"
      ) as Promise<HarukiMvRenderProfile>;
    },
    loadMv(request) {
      if (!Number.isInteger(request.musicId) || request.musicId <= 0) {
        throw new RangeError("MV musicId must be a positive integer.");
      }
      if (!Array.isArray(request.characters)) {
        throw new TypeError("MV character requests are required.");
      }
      return sendAndWait("LoadMv", request, "mv-ready");
    },
    setCutInActive(cutInOrder, active) {
      if (!Number.isInteger(cutInOrder) || cutInOrder < 0) {
        throw new RangeError("MV CutIn order must be a non-negative integer.");
      }
      runtime.sendMessage(
        HARUKI_MV_BRIDGE_OBJECT,
        "SetCutInActive",
        JSON.stringify({ cutInOrder, active })
      );
    },
    loadScene(request) {
      for (const field of [
        "baseUrl",
        "manifestBundleName",
        "sceneBundleName",
        "sceneName",
      ] as const) {
        if (!request[field]?.trim()) {
          throw new TypeError(`MV scene request ${field} is required.`);
        }
      }
      return sendAndWait("LoadScene", request, "scene-ready");
    },
    setPaused(paused) {
      runtime.sendMessage(
        HARUKI_MV_BRIDGE_OBJECT,
        "SetPaused",
        JSON.stringify({ paused })
      );
    },
    seek(timeSeconds) {
      if (!Number.isFinite(timeSeconds)) {
        throw new RangeError("MV seek time must be finite.");
      }
      runtime.sendMessage(
        HARUKI_MV_BRIDGE_OBJECT,
        "Seek",
        JSON.stringify({ timeSeconds })
      );
    },
    retry() {
      runtime.sendMessage(HARUKI_MV_BRIDGE_OBJECT, "Retry", "");
    },
    getState() {
      runtime.sendMessage(HARUKI_MV_BRIDGE_OBJECT, "GetState", "");
    },
    disposeScene() {
      return sendAndWait("Dispose", {}, "disposed");
    },
  };
}

function createRenderProfilePayload(request: HarukiMvRenderProfileRequest) {
  if (!Number.isInteger(request.refreshRate) || request.refreshRate <= 0) {
    throw new RangeError("MV render profile refreshRate must be a positive integer.");
  }

  const resolution = request.resolution ?? "device";
  const outputResolution = {
    device: 0,
    "720p": 1,
    "1080p": 2,
    "1440p": 3,
    "4k-uhd": 4,
    custom: 5,
  }[resolution];
  if (outputResolution === undefined) {
    throw new RangeError("MV render profile resolution is invalid.");
  }

  let width = 0;
  let height = 0;
  let dpi = 0;
  let quality = 0;
  let playMode = 4;
  if (resolution === "device") {
    const deviceRequest = request as HarukiMvDeviceRenderProfileRequest;
    for (const field of ["width", "height"] as const) {
      if (!Number.isInteger(deviceRequest[field]) || deviceRequest[field] <= 0) {
        throw new RangeError(`MV render profile ${field} must be a positive integer.`);
      }
    }
    if (!Number.isFinite(deviceRequest.dpi) || deviceRequest.dpi <= 0) {
      throw new RangeError("MV render profile dpi must be positive and finite.");
    }
    quality = {
      default: 0,
      high: 1,
      "virtual-live-default": 2,
    }[deviceRequest.quality];
    playMode = {
      "ingame-3dmv": 0,
      "music-video": 4,
    }[deviceRequest.playMode];
    if (quality === undefined || playMode === undefined) {
      throw new RangeError("MV render profile quality and playMode are invalid.");
    }
    width = deviceRequest.width;
    height = deviceRequest.height;
    dpi = deviceRequest.dpi;
  } else if (resolution === "custom") {
    const customRequest = request as HarukiMvCustomRenderProfileRequest;
    for (const field of ["width", "height"] as const) {
      if (!Number.isInteger(customRequest[field]) || customRequest[field] <= 0) {
        throw new RangeError(`MV render profile ${field} must be a positive integer.`);
      }
    }
    width = customRequest.width;
    height = customRequest.height;
  } else {
    ({ width, height } = HARUKI_MV_RENDER_PRESETS[resolution]);
  }

  return {
    width,
    height,
    dpi,
    refreshRate: request.refreshRate,
    quality,
    playMode,
    outputResolution,
    use120Fps: request.use120Fps,
  };
}
