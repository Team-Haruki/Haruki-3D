export type UnityWebGLBuildConfig = {
  dataUrl: string;
  frameworkUrl: string;
  codeUrl: string;
  streamingAssetsUrl?: string;
  companyName?: string;
  productName?: string;
  productVersion?: string;
  devicePixelRatio?: number;
};

export type UnityWebGLInstance = {
  SendMessage(gameObject: string, method: string, parameter?: string | number): void;
  SetFullscreen?(fullscreen: 0 | 1): void;
  Quit(): Promise<unknown>;
};

export type UnityWebGLCreateInstance = (
  canvas: HTMLCanvasElement,
  config: UnityWebGLBuildConfig,
  onProgress?: (progress: number) => void
) => Promise<UnityWebGLInstance>;

export type HarukiMvRuntimeOptions = {
  canvas: HTMLCanvasElement;
  build: UnityWebGLBuildConfig;
  createUnityInstance: UnityWebGLCreateInstance;
  onProgress?: (progress: number) => void;
};

export type HarukiMvRuntime = {
  prepare(): Promise<UnityWebGLInstance>;
  sendMessage(gameObject: string, method: string, parameter?: string | number): void;
  setFullscreen(fullscreen: boolean): void;
  destroy(): Promise<void>;
};

/** Hosts the original Unity WebGL/WASM MV player without translating it. */
export function createHarukiMvRuntime(options: HarukiMvRuntimeOptions): HarukiMvRuntime {
  let instance: UnityWebGLInstance | null = null;
  let loading: Promise<UnityWebGLInstance> | null = null;
  let destroying: Promise<void> | null = null;
  let destroyed = false;

  const prepare = () => {
    if (destroyed) {
      return Promise.reject(new Error("Haruki MV runtime has been destroyed."));
    }
    if (instance) {
      return Promise.resolve(instance);
    }
    if (!loading) {
      loading = options.createUnityInstance(
        options.canvas,
        options.build,
        options.onProgress
      ).then((created) => {
        instance = created;
        return created;
      }).catch((error: unknown) => {
        loading = null;
        throw error;
      });
    }
    return loading;
  };

  const requireInstance = () => {
    if (!instance) {
      throw new Error("Haruki MV runtime is not ready; await prepare() first.");
    }
    return instance;
  };

  return {
    prepare,
    sendMessage(gameObject, method, parameter) {
      requireInstance().SendMessage(gameObject, method, parameter);
    },
    setFullscreen(fullscreen) {
      const loaded = requireInstance();
      if (!loaded.SetFullscreen) {
        throw new Error("This Unity MV build does not expose SetFullscreen.");
      }
      loaded.SetFullscreen(fullscreen ? 1 : 0);
    },
    destroy() {
      if (destroying) return destroying;
      destroyed = true;
      const pending = loading
        ? loading.catch(() => null)
        : Promise.resolve(instance);
      destroying = pending.then(async (loaded) => {
        if (loaded) {
          await loaded.Quit();
        }
        instance = null;
        loading = null;
      });
      return destroying;
    },
  };
}
