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

export type HarukiMvCharacterRequest = {
  characterId?: number;
  bodyBundleName?: string;
  bodyAssetName?: string;
  faceBundleName?: string;
  faceAssetName?: string;
  headOptionalBundleName?: string;
  headOptionalAssetName?: string;
  timelineBindingName?: string;
  standaloneMotionBundleName?: string;
  standaloneMotionAssetNames?: string[];
  /** Master gameCharacters height in centimetres. */
  characterHeight: number;
  heelOffset?: number;
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
  characters: HarukiMvCharacterRequest[];
  cutIns?: HarukiMvCutInRequest[];
  audioBundleName?: string;
  audioAssetName?: string;
};

export type HarukiMvBridge = {
  loadBundleSet(request: HarukiMvBundleSetRequest): Promise<unknown>;
  instantiatePrefab(request: HarukiMvPrefabRequest): Promise<unknown>;
  readMvData(request: HarukiMvAssetRequest): Promise<unknown>;
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
