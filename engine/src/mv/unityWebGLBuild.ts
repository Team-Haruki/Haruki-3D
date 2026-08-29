export type UnityWebGLBuildConfig = {
  dataUrl: string;
  frameworkUrl: string;
  codeUrl: string;
  streamingAssetsUrl?: string;
  companyName?: string;
  productName?: string;
  productVersion?: string;
  devicePixelRatio?: number;
  matchWebGLToCanvasSize?: boolean;
  autoSyncPersistentDataPath?: boolean;
  arguments?: string[];
  webglContextAttributes?: WebGLContextAttributes;
  cacheControl?: (url: string) => "must-revalidate" | "immutable" | "no-store";
  showBanner?: (message: string, type: "error" | "warning" | string) => void;
};

export type UnityWebGLCompression = "none" | "gzip" | "brotli";

export type UnityWebGLBuildOptions = {
  buildBaseUrl: string;
  streamingAssetsUrl?: string;
  buildName?: string;
  compression?: UnityWebGLCompression;
  companyName?: string;
  productName?: string;
  productVersion?: string;
};

export type ResolvedUnityWebGLBuild = {
  loaderUrl: string;
  config: UnityWebGLBuildConfig;
};

export function resolveUnityWebGLBuild(
  options: UnityWebGLBuildOptions
): ResolvedUnityWebGLBuild {
  const baseUrl = requireDirectoryUrl(options.buildBaseUrl, "buildBaseUrl");
  const buildName = String(options.buildName ?? "WebGL").trim();
  if (!buildName || buildName.includes("/") || buildName.includes("\\")) {
    throw new Error("buildName must be one file stem without path separators.");
  }
  const suffix = compressionSuffix(options.compression ?? "none");
  return {
    loaderUrl: `${baseUrl}${buildName}.loader.js`,
    config: {
      dataUrl: `${baseUrl}${buildName}.data${suffix}`,
      frameworkUrl: `${baseUrl}${buildName}.framework.js${suffix}`,
      codeUrl: `${baseUrl}${buildName}.wasm${suffix}`,
      // MV output pixels are controlled by Screen.SetResolution. Keeping the
      // WebGL loader tied to CSS size would silently overwrite 1080p/4K.
      devicePixelRatio: 1,
      matchWebGLToCanvasSize: false,
      ...(options.streamingAssetsUrl
        ? { streamingAssetsUrl: trimTrailingSlash(options.streamingAssetsUrl) }
        : {}),
      ...(options.companyName ? { companyName: options.companyName } : {}),
      ...(options.productName ? { productName: options.productName } : {}),
      ...(options.productVersion ? { productVersion: options.productVersion } : {}),
    },
  };
}

function compressionSuffix(compression: UnityWebGLCompression) {
  switch (compression) {
    case "none": return "";
    case "gzip": return ".gz";
    case "brotli": return ".br";
    default: throw new Error(`Unsupported Unity WebGL compression: ${compression}.`);
  }
}

function requireDirectoryUrl(value: string, name: string) {
  const url = String(value ?? "").trim();
  if (!url) {
    throw new Error(`${name} is required.`);
  }
  return `${trimTrailingSlash(url)}/`;
}

function trimTrailingSlash(value: string) {
  const trimmed = value.trim();
  let end = trimmed.length;
  while (end > 1 && trimmed[end - 1] === "/") {
    end -= 1;
  }
  return trimmed.slice(0, end);
}
