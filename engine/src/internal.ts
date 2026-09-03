export {
  decodeRuntimeMessagePackBrotli,
} from "./runtime/runtimeMessagePackDecoder";
export {
  type HarukiCameraControls,
  type HarukiCameraControlsFactory,
  type AnimationPlaybackSnapshot,
  type BodyAnimationKind,
  type BodyAnimationSelection,
  type BodyDebugMode,
  type CompositionMode,
  type CompositionStatus,
  type FaceMotionClip,
  type FaceMotionCurve,
  type FaceMotionKeyframe,
  type FaceMotionPlaybackSnapshot,
  type FaceMotionSet,
  type HairShadowMode,
  type Haruki3DEngineOptions,
  type HarukiEngineSnapshots,
  type HarukiRuntimePackageRequest,
  type HarukiRenderResult,
  type PartImportSnapshot,
  type PjskCameraProfile,
  type PjskCameraPreset,
  type PjskPresentationMode,
  type ProjectedShadowSettings,
  type ProjectedShadowSettingsInput,
  type PjskViewerEngineOptions,
  type PjskEngineOptions,
  type RenderIsolationMode,
  type RuntimeCameraDebug,
  type RuntimeDebugSnapshot,
  type SpringBoneRuntimeSnapshot,
  readCharacterHairMaterialController,
  defaultProjectedShadowSettings,
} from "./engine/Haruki3DEngine";
export {
  Haruki3DEngine,
  Haruki3DEngineCore,
  type Haruki3DCaptureEngineOptions,
} from "./capture/Haruki3DCaptureEngine";
export type { RuntimeCombinedCharacterAsset } from "./runtime/runtimeTypes";
export {
  HarukiCaptureAdapter,
} from "./capture/captureAdapter";
export type {
  HarukiCaptureRolePartsRequest,
  HarukiCaptureRolePartsResult,
  HarukiPrepareCaptureFrameRequest,
} from "./capture/captureTypes";
export {
  loadRuntimePackageFromBaseUrl,
  resolveRuntimePackageUrl,
  type RuntimePackageLoadOptions,
  type RuntimePackageLoadResult,
} from "./runtime/runtimePackageLoader";
export {
  CustomWardrobeController,
  type CustomWardrobeControllerOptions,
} from "./parts/customWardrobeController";
export {
  officialColliderFlagPrefixes,
  runtimePartComposerInternals,
  type CustomPartSelection,
  type HeadHairCompatibility,
  type PartPackageSet,
  type PartRegistryEntry,
  type PartRuntimePackage,
  type RoleRuntimePackage,
  type RuntimeRoleCatalog,
  type RuntimeRoleCatalogEntry,
  type RuntimePartType,
} from "./parts/runtimePartComposer";
export {
  previewLightDefaults,
  sekaiCostumeShopControllerDefaults,
  sekaiCostumeShopDirectionalLightDirection,
  sekaiCostumeShopDirectionalLightRotationDegrees,
  sekaiCostumeShopRimLightDirection,
  type BodyAssetManifest,
  type HeadAssetManifest,
  type PreviewLightState,
  type Vec3,
} from "./data/sampleScene";
export {
  evaluateSekaiBaseShadow,
  evaluateSekaiFaceShadow,
  evaluateSekaiSkinColor,
  type SekaiBaseShadowInput,
  type SekaiFaceShadowInput,
  type SekaiSkinColorInput,
} from "./materials/sekaiCharacterLighting";
export {
  normalizeHarukiRenderRecipe,
  type HarukiRenderRecipe,
  type HarukiRuntimeRenderRecipe,
  type NormalizedHarukiRenderRecipe,
} from "./kernel/renderRecipe";
export {
  createHaruki3DKernel,
  createHaruki3DKernelRuntime,
  type Haruki3DKernel,
  type Haruki3DKernelOptions,
} from "./costume_shop/CostumeShopKernel";
export {
  COSTUME_SHOP_VIEW_FRAMING_DEFAULT,
  COSTUME_SHOP_VIEW_FRAMING_LIMITS,
  applyCostumeShopViewFraming,
  clampCostumeShopViewFraming,
  getCostumeShopCameraPose,
  getDefaultCameraPose,
  shiftCameraPoseRight,
} from "./costume_shop/cameraPolicy";
export { createCaptureBackgroundTexture } from "./engine/captureBackground";
export { CharacterProjectedShadowController } from "./engine/projectedShadow";
export {
  createSmoothedLoopClip,
  decodeUnityMotionClips,
  inferBodyAnimationKind,
  makeAnimationTrackDebug,
  prepareRuntimeAnimationClip,
  retargetUnityPrefabAnimationClip,
} from "./engine/runtimeMotion";
export {
  AnimationPlaybackRuntime,
  type AnimationPlaybackContext,
  type AnimationPlaybackPosition,
} from "./engine/animationPlaybackRuntime";
export {
  FaceMotionRuntime,
  readEmbeddedRuntimeFaceMotion,
} from "./engine/faceMotionRuntime";
export {
  CharacterLightingRuntime,
  bodyDebugModeToUniform,
  characterLightingRuntimeInternals,
  evaluateSekaiOutlineFovFactor,
  faceSdfDebugModeToUniform,
  sekaiCostumeShopOutlineSettings,
} from "./engine/characterLightingRuntime";
export {
  resolveSekaiPreviewPixelRatio,
  sekaiPreviewPostProcessDefaults,
} from "./engine/sekaiPreviewPostProcessor";
export {
  createSekaiOutlineMaterial,
  evaluateSekaiOutlineColor,
  isSekaiOutlinePassEnabled,
  sekaiCostumeShopOutlineControllerDefaults,
} from "./engine/sekaiOutlineRuntime";
export {
  applyRawMaterialTextureTransform,
  readRawMaterialBoolean,
  readRawMaterialColor,
  readRawMaterialFloat,
  readRawMaterialTexture,
} from "./engine/rawMaterialRuntime";
export {
  applyUnityCharacterModelScale,
  buildUnityPrefabSourceGraph,
  createUnityPrefabConstraintRuntime,
  installUnityRuntimeNativeMeshes,
  makeUnityPrefabHeadFollowDebugSnapshot,
  syncUnityPrefabSourceGraph,
  unityPrefabRuntimeInternals,
} from "./engine/unityPrefabRuntime";
export {
  resolveCostumeShopHeightRate,
  resolveCostumeShopModelScale,
} from "./costume_shop/heightPolicy";
export {
  UnityPrefabSpringRuntime,
  computeUtjAverageChildTailPosition,
  unityPrefabSpringRuntimeInternals,
  type SpringTimelineControl,
} from "./engine/unityPrefabSpringRuntimeAdapter";
export { SekaiExtraBoneRuntime } from "./engine/sekaiExtraBoneRuntime";
export {
  UtjColliderStatus,
  applyUtjCollisionVelocityResponse,
  applyUtjLengthLimits,
  cacheUtjSpringBonePosition,
  checkCapsuleCollisionAndReact,
  checkCollisionWithAlignedPlaneAndReact,
  checkLocalCylinderCollisionAndReact,
  checkLocalCapsuleCollisionAndReact,
  checkLocalYAxisCapsuleCollisionAndReact,
  checkLocalSphereCollisionAndReact,
  checkPanelCollisionAndReact,
  checkSphereCollisionAndReact,
  checkUtjCollisions,
  checkUtjGroundCollision,
  computeAnimatedTipPosition,
  computeNewTailPosition,
  computeSphereIntersectionCircle,
  computeUtjLocalRotation,
  computeUtjWorldRotation,
  constrainUtjAngleLimit,
  createUtjSpringBoneState,
  enforceSpringLength,
  fixBoneLength,
  updateUtjSpring,
} from "./engine/utjSpringBoneRuntime";
export {
  UnityConstraintRuntime,
  applyUnityRuntimeConstraints,
} from "./engine/unityConstraintRuntime";
export {
  applyRawMaterialShaderUniforms,
  bindBodyRuntimeMaterials,
  cloneBodyShaderMaterial,
  configureSekaiBaseStencilClear,
  configureSekaiEyelashPass,
  configureSekaiFaceLayerStencilPrepass,
  configureSekaiHairStencil,
  createGroupedLayerMesh,
  createSekaiThroughHairOverlayMesh,
  extractMaterialColorMap,
  getHeadLayerRenderOrder,
  getSekaiPreviewRimDirection,
  loadRuntimeTexture,
  normalizeMeshSlotName,
  sortHeadMeshGroupsByMaterialKind,
  syncReplacementTextureFromOriginal,
  tuneLightingForPreview,
  updateSekaiEyelashPassView,
  type RuntimeMaterialDebug,
} from "./engine/characterMaterialRuntime";
export {
  bindHeadRuntimeMaterials,
  headMaterialRuntimeInternals,
  type CharacterEyeMaterialController,
} from "./engine/headMaterialRuntime";
export { createSekaiBodyMaterial } from "./materials/sekaiBodyMaterial";
export { createSekaiFaceMaterial } from "./materials/sekaiFaceMaterial";
export { createSekaiLayerMaterial } from "./materials/sekaiCharacterShader";
