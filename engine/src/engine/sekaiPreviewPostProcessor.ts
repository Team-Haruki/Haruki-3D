export const sekaiPreviewPostProcessDefaults = {
  // CostumeShop renders into this fixed intermediate; device pixels belong
  // to the later UI presentation and must not increase WebGL rasterization.
  maxOutputSize: 1024,
  enabled: false,
} as const;

export function resolveSekaiPreviewPixelRatio(
  width: number,
  height: number,
  requestedPixelRatio: number
) {
  const safeWidth = Math.max(1, Number.isFinite(width) ? width : 1);
  const safeHeight = Math.max(1, Number.isFinite(height) ? height : 1);
  const safeRequestedRatio = Math.max(
    0.1,
    Number.isFinite(requestedPixelRatio) ? requestedPixelRatio : 1
  );
  return Math.min(
    safeRequestedRatio,
    2,
    sekaiPreviewPostProcessDefaults.maxOutputSize /
      Math.max(safeWidth, safeHeight)
  );
}
