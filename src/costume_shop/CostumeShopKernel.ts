import {
  createHarukiBaseCharacterRuntime,
  type HarukiBaseCharacterRuntime,
  type HarukiBaseRuntimeEngine,
} from "../base/browserCharacterRuntime";
import { previewLightDefaults, type PreviewLightState } from "../data/sampleScene";
import { CostumeShopEngine } from "./CostumeShopEngine";

export type CostumeShopKernelOptions = {
  canvas: HTMLCanvasElement;
  assetBaseUrl: string;
  initialLight?: PreviewLightState;
  ktx2TranscoderPath?: string;
};

export type CostumeShopKernel = HarukiBaseCharacterRuntime;

export function createCostumeShopKernel(
  options: CostumeShopKernelOptions
): CostumeShopKernel {
  const assetBaseUrl = String(options.assetBaseUrl ?? "").trim();
  if (!assetBaseUrl) {
    throw new Error("assetBaseUrl is required to create the CostumeShop kernel.");
  }

  const engine = new CostumeShopEngine({
    canvas: options.canvas,
    initialLight: { ...(options.initialLight ?? previewLightDefaults) },
    autoRender: false,
    manageResize: false,
    ktx2TranscoderPath: options.ktx2TranscoderPath,
  });
  return createCostumeShopKernelRuntime(engine, assetBaseUrl);
}

export function createCostumeShopKernelRuntime(
  engine: HarukiBaseRuntimeEngine,
  assetBaseUrl: string
): CostumeShopKernel {
  return createHarukiBaseCharacterRuntime(engine, assetBaseUrl);
}

// Backward-compatible names. The default package entry remains CostumeShop.
export const createHaruki3DKernel = createCostumeShopKernel;
export const createHaruki3DKernelRuntime = createCostumeShopKernelRuntime;
export type Haruki3DKernel = CostumeShopKernel;
export type Haruki3DKernelOptions = CostumeShopKernelOptions;
