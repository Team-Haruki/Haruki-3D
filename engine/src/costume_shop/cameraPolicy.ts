import * as THREE from "three";

const DEFAULT_TARGET_SCALE = new THREE.Vector3(0.04835, 0.48222, 0.07241);
const DEFAULT_OFFSET_SCALE = new THREE.Vector3(-0.08532, 0.12848, 1.93551);
const DEFAULT_FOV = 35;
const LEGACY_CLOUD_CAPTURE_CENTER_Y = 0.46;
const LEGACY_CLOUD_CAPTURE_DISTANCE = 2.74;
const CAPTURE_LATERAL_SHIFT_SCALE = -0.0245;
const FULL_BODY_CAPTURE_CENTER_Y = 0.765;
const COSTUME_SHOP_CAMERA = {
  zoomDuration: 0.35,
  bottomLowerLimitPosition: 0.4,
  bottomUpperLimitPosition: 0.85,
  topLowerLimitPosition: 1.25,
  topUpperLimitPosition: 0.85,
  nearZ: 2.3,
  farZ: 4.5,
  fov: 25,
} as const;

export type PjskCameraPreset = "default" | "capture";
export type PjskCameraProfile = "official-default" | "full-body" | "legacy-cloud";

export type RuntimeCameraDebug = {
  preset: PjskCameraPreset;
  profile: PjskCameraProfile | null;
  characterRootYawDegrees: number;
  costumeShopState: {
    cameraRootYawDegrees: number;
    zoomValue: number;
    zoomMoveValue: number;
    zoomRatio: number;
    localCameraPosition: { x: number; y: number; z: number };
    localCameraRotationYDegrees: number;
  } | null;
  position: { x: number; y: number; z: number };
  target: { x: number; y: number; z: number };
  offset: { x: number; y: number; z: number };
  distance: number;
  polarDegrees: number;
  azimuthDegrees: number;
  fovDegrees: number;
  aspect: number;
  zoom: number;
  minPolarDegrees: number;
  maxPolarDegrees: number;
  masterCharacterHeightMeters: number;
  characterModelScaleMeters: number;
};

type CostumeShopCameraPose = {
  target: THREE.Vector3;
  position: THREE.Vector3;
  fov: number;
  costumeShopState: {
    cameraRootYawDegrees: number;
    zoomValue: number;
    zoomMoveValue: number;
    zoomRatio: number;
    localCameraPosition: THREE.Vector3;
    localCameraRotationYDegrees: number;
  } | null;
};

export function getDefaultCameraPose(
  characterModelScale: number
): CostumeShopCameraPose {
  const target = DEFAULT_TARGET_SCALE.clone().multiplyScalar(characterModelScale);
  return {
    target,
    position: target.clone().add(DEFAULT_OFFSET_SCALE.clone().multiplyScalar(characterModelScale)),
    fov: DEFAULT_FOV,
    costumeShopState: null,
  };
}

export function getCostumeShopCameraPose(
  profile: PjskCameraProfile,
  cameraRootYawDegrees = 0,
  characterHeightMeters = 1.6
): CostumeShopCameraPose {
  const finiteCameraRootYawDegrees = Number.isFinite(cameraRootYawDegrees)
    ? cameraRootYawDegrees
    : 0;
  if (profile === "legacy-cloud") {
    const target = new THREE.Vector3(0, LEGACY_CLOUD_CAPTURE_CENTER_Y, 0);
    const offset = new THREE.Vector3(0, 0, LEGACY_CLOUD_CAPTURE_DISTANCE)
      .applyAxisAngle(
        new THREE.Vector3(0, 1, 0),
        THREE.MathUtils.degToRad(finiteCameraRootYawDegrees)
      );
    return {
      target,
      position: target.clone().add(offset),
      fov: COSTUME_SHOP_CAMERA.fov,
      costumeShopState: null,
    };
  }
  const state = profile === "official-default"
    ? {
        cameraRootYawDegrees: finiteCameraRootYawDegrees,
        zoomValue: 0,
        zoomMoveValue: 1,
      }
    : {
        cameraRootYawDegrees: finiteCameraRootYawDegrees,
        zoomValue: COSTUME_SHOP_CAMERA.zoomDuration,
        zoomMoveValue: 0,
      };
  const zoomValue = THREE.MathUtils.clamp(
    state.zoomValue,
    0,
    COSTUME_SHOP_CAMERA.zoomDuration
  );
  const zoomRatio = COSTUME_SHOP_CAMERA.zoomDuration > 0
    ? zoomValue / COSTUME_SHOP_CAMERA.zoomDuration
    : 0;
  const bottomY = THREE.MathUtils.lerp(
    COSTUME_SHOP_CAMERA.bottomLowerLimitPosition,
    COSTUME_SHOP_CAMERA.bottomUpperLimitPosition,
    zoomRatio
  );
  const topY = THREE.MathUtils.lerp(
    COSTUME_SHOP_CAMERA.topLowerLimitPosition,
    COSTUME_SHOP_CAMERA.topUpperLimitPosition,
    zoomRatio
  );
  const zoomMoveValue = THREE.MathUtils.clamp(state.zoomMoveValue, 0, 1);
  const y = profile === "full-body"
    ? FULL_BODY_CAPTURE_CENTER_Y
    : THREE.MathUtils.lerp(bottomY, topY, zoomMoveValue);
  const z = THREE.MathUtils.lerp(
    COSTUME_SHOP_CAMERA.nearZ,
    COSTUME_SHOP_CAMERA.farZ,
    zoomRatio
  );
  const rotationY = THREE.MathUtils.degToRad(state.cameraRootYawDegrees);
  const localCameraPosition = new THREE.Vector3(0, y, z);
  return {
    target: new THREE.Vector3(0, y, 0),
    position: localCameraPosition.clone()
      .applyAxisAngle(new THREE.Vector3(0, 1, 0), rotationY),
    fov: COSTUME_SHOP_CAMERA.fov,
    costumeShopState: {
      cameraRootYawDegrees: state.cameraRootYawDegrees,
      zoomValue,
      zoomMoveValue,
      zoomRatio,
      localCameraPosition,
      localCameraRotationYDegrees: 180,
    },
  };
}

export function shiftCameraPoseRight(
  position: THREE.Vector3,
  target: THREE.Vector3,
  amount: number,
  characterModelScale: number
) {
  const forward = target.clone().sub(position).normalize();
  const right = new THREE.Vector3()
    .crossVectors(forward, new THREE.Vector3(0, 1, 0))
    .normalize();
  const shift = right.multiplyScalar(
    CAPTURE_LATERAL_SHIFT_SCALE * amount * characterModelScale
  );
  return {
    target: target.clone().add(shift),
    position: position.clone().add(shift),
  };
}

/**
 * Host-driven framing on top of a profile pose: `zoom` scales the camera
 * distance (1 = the profile's own distance, 2 = twice as close) and
 * `heightOffset` lifts target and camera together, in metres. The character
 * never moves; this mirrors the CostumeShop pinch/drag which dollies and
 * slides CameraRoot's local camera.
 */
export type CostumeShopViewFraming = {
  zoom: number;
  heightOffset: number;
};

export const COSTUME_SHOP_VIEW_FRAMING_LIMITS = {
  minZoom: 0.5,
  maxZoom: 3,
  minHeightOffset: -0.5,
  maxHeightOffset: 0.8,
} as const;

export const COSTUME_SHOP_VIEW_FRAMING_DEFAULT: Readonly<CostumeShopViewFraming> = {
  zoom: 1,
  heightOffset: 0,
};

export function clampCostumeShopViewFraming(
  framing: Partial<CostumeShopViewFraming>
): CostumeShopViewFraming {
  const limits = COSTUME_SHOP_VIEW_FRAMING_LIMITS;
  const zoom = Number.isFinite(framing.zoom)
    ? (framing.zoom as number)
    : COSTUME_SHOP_VIEW_FRAMING_DEFAULT.zoom;
  const heightOffset = Number.isFinite(framing.heightOffset)
    ? (framing.heightOffset as number)
    : COSTUME_SHOP_VIEW_FRAMING_DEFAULT.heightOffset;
  return {
    zoom: THREE.MathUtils.clamp(zoom, limits.minZoom, limits.maxZoom),
    heightOffset: THREE.MathUtils.clamp(
      heightOffset,
      limits.minHeightOffset,
      limits.maxHeightOffset
    ),
  };
}

export function applyCostumeShopViewFraming(
  pose: { target: THREE.Vector3; position: THREE.Vector3 },
  framing: CostumeShopViewFraming
) {
  const { zoom, heightOffset } = clampCostumeShopViewFraming(framing);
  const target = pose.target.clone();
  target.y += heightOffset;
  const offset = pose.position.clone().sub(pose.target).divideScalar(zoom);
  return {
    target,
    position: target.clone().add(offset),
  };
}
