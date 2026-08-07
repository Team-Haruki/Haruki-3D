import type { UnityWebGLBuildConfig } from "./unityWebGLBuild";

export type UnityWebGLInstance = {
  SendMessage(gameObject: string, method: string, parameter?: string | number): void;
  SetFullscreen?(fullscreen: 0 | 1): void;
  GetMemoryInfo?(): UnityWebGLMemoryInfo;
  Quit(): Promise<unknown>;
};

export type UnityWebGLMemoryInfo = {
  totalWASMHeapSize: number;
  usedWASMHeapSize: number;
  totalJSHeapSize: number;
  usedJSHeapSize: number;
};

export type UnityWebGLCreateInstance = (
  canvas: HTMLCanvasElement,
  config: UnityWebGLBuildConfig,
  onProgress?: (progress: number) => void
) => Promise<UnityWebGLInstance>;

export type HarukiMvRuntimeState =
  | "idle"
  | "loading"
  | "ready"
  | "failed"
  | "destroying"
  | "destroyed";

export type HarukiMvRuntimeOptions = {
  canvas: HTMLCanvasElement;
  build: UnityWebGLBuildConfig;
  /** Generated Unity factory. When omitted, loaderUrl is loaded once. */
  createUnityInstance?: UnityWebGLCreateInstance;
  loaderUrl?: string;
  onProgress?: (progress: number) => void;
};

export type HarukiMvRuntime = {
  readonly build: UnityWebGLBuildConfig;
  readonly state: HarukiMvRuntimeState;
  prepare(): Promise<UnityWebGLInstance>;
  sendMessage(gameObject: string, method: string, parameter?: string | number): void;
  setFullscreen(fullscreen: boolean): void;
  getMemoryInfo(): UnityWebGLMemoryInfo | null;
  destroy(): Promise<void>;
};

const loaderFactories = new Map<string, Promise<UnityWebGLCreateInstance>>();

/** Hosts one original Unity WebGL/WASM MV player. */
export function createHarukiMvRuntime(options: HarukiMvRuntimeOptions): HarukiMvRuntime {
  const build = validateBuildConfig(options.build);
  const loaderUrl = String(options.loaderUrl ?? "").trim();
  if (!options.createUnityInstance && !loaderUrl) {
    throw new Error("createUnityInstance or loaderUrl is required for the MV runtime.");
  }

  let state: HarukiMvRuntimeState = "idle";
  let instance: UnityWebGLInstance | null = null;
  let loading: Promise<UnityWebGLInstance> | null = null;
  let destroying: Promise<void> | null = null;
  let destroyRequested = false;

  const prepare = () => {
    if (destroyRequested) {
      return Promise.reject(new Error("Haruki MV runtime has been destroyed."));
    }
    if (instance) {
      return Promise.resolve(instance);
    }
    if (!loading) {
      state = "loading";
      const factory = options.createUnityInstance
        ? Promise.resolve(options.createUnityInstance)
        : loadUnityWebGLCreateInstance(loaderUrl);
      loading = factory
        .then((createUnityInstance) => createUnityInstance(
          options.canvas,
          build,
          options.onProgress
        ))
        .then((created) => {
          instance = created;
          if (!destroyRequested) {
            state = "ready";
          }
          return created;
        })
        .catch((error: unknown) => {
          loading = null;
          if (!destroyRequested) {
            state = "failed";
          }
          throw error;
        });
    }
    return loading;
  };

  const requireInstance = () => {
    if (!instance || state !== "ready") {
      throw new Error("Haruki MV runtime is not ready; await prepare() first.");
    }
    return instance;
  };

  return {
    build,
    get state() {
      return state;
    },
    prepare,
    sendMessage(gameObject, method, parameter) {
      const loaded = requireInstance();
      if (parameter === undefined) {
        loaded.SendMessage(gameObject, method);
      } else {
        loaded.SendMessage(gameObject, method, parameter);
      }
    },
    setFullscreen(fullscreen) {
      const loaded = requireInstance();
      if (!loaded.SetFullscreen) {
        throw new Error("This Unity MV build does not expose SetFullscreen.");
      }
      loaded.SetFullscreen(fullscreen ? 1 : 0);
    },
    getMemoryInfo() {
      return state === "ready" && instance?.GetMemoryInfo
        ? instance.GetMemoryInfo()
        : null;
    },
    destroy() {
      if (destroying) return destroying;
      destroyRequested = true;
      state = "destroying";
      const pending = loading
        ? loading.catch(() => null)
        : Promise.resolve(instance);
      destroying = pending.then(async (loaded) => {
        try {
          if (loaded) {
            await loaded.Quit();
          }
        } finally {
          instance = null;
          loading = null;
          state = "destroyed";
        }
      });
      return destroying;
    },
  };
}

export function loadUnityWebGLCreateInstance(
  loaderUrl: string
): Promise<UnityWebGLCreateInstance> {
  const url = String(loaderUrl ?? "").trim();
  if (!url) {
    return Promise.reject(new Error("loaderUrl is required."));
  }
  const cached = loaderFactories.get(url);
  if (cached) {
    return cached;
  }
  if (typeof document === "undefined") {
    return Promise.reject(new Error("Unity WebGL loader requires a browser document."));
  }

  const loading = new Promise<UnityWebGLCreateInstance>((resolve, reject) => {
    const previousFactory = (
      globalThis as typeof globalThis & {
        createUnityInstance?: UnityWebGLCreateInstance;
      }
    ).createUnityInstance;
    const script = document.createElement("script");
    script.async = true;
    script.src = url;
    script.onload = () => {
      const createUnityInstance = (
        globalThis as typeof globalThis & {
          createUnityInstance?: UnityWebGLCreateInstance;
        }
      ).createUnityInstance;
      if (
        typeof createUnityInstance !== "function" ||
        createUnityInstance === previousFactory
      ) {
        reject(new Error(`Unity loader ${url} did not expose createUnityInstance.`));
        return;
      }
      resolve(createUnityInstance);
    };
    script.onerror = () => {
      reject(new Error(`Failed to load Unity WebGL loader ${url}.`));
    };
    const parent = document.head ?? document.body;
    if (!parent) {
      reject(new Error("Unity WebGL loader requires document.head or document.body."));
      return;
    }
    parent.appendChild(script);
  }).catch((error: unknown) => {
    loaderFactories.delete(url);
    throw error;
  });
  loaderFactories.set(url, loading);
  return loading;
}

function validateBuildConfig(config: UnityWebGLBuildConfig) {
  if (!config?.dataUrl || !config.frameworkUrl || !config.codeUrl) {
    throw new Error("Unity MV build requires dataUrl, frameworkUrl, and codeUrl.");
  }
  return { ...config };
}
