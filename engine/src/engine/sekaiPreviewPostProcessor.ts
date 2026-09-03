export const sekaiPreviewPostProcessDefaults = {
  // CostumeShop renders into this fixed intermediate; device pixels belong
  // to the later UI presentation and must not increase WebGL rasterization.
  maxOutputSize: 1024,
  enabled: false,
} as const;

export function resolveSekaiPreviewPixelRatio(
  width: number,
  height: number,
  requestedPixelRatio: number,
  settings: { maxOutputSize: number; enabled: boolean } = sekaiPreviewPostProcessDefaults
) {
  const safeWidth = Math.max(1, Number.isFinite(width) ? width : 1);
  const safeHeight = Math.max(1, Number.isFinite(height) ? height : 1);
  const safeRequestedRatio = Math.max(
    0.1,
    Number.isFinite(requestedPixelRatio) ? requestedPixelRatio : 1
  );
  const deviceRatio = Math.min(safeRequestedRatio, 2);
  // The 1024 intermediate only exists while the presentation pass runs.
  // Without it the canvas is the final image, so capping the backing buffer
  // below the device resolution just upscales a soft render.
  if (!settings.enabled) {
    return deviceRatio;
  }
  return Math.min(
    deviceRatio,
    settings.maxOutputSize / Math.max(safeWidth, safeHeight)
  );
}
