import * as THREE from "three";
import type { RuntimeNumericArray } from "../runtime/runtimeTypes";
import { buildPrefabNodePathLookup } from "./prefabNodeLookup";
import {
  UnityConstraintRuntime,
  applyUnityRuntimeConstraints,
  type RuntimeConstraintDebug,
  type RuntimeConstraintSetupSource,
} from "./unityConstraintRuntime";
import {
  convertUnityPositionToThree,
  convertUnityQuaternionToThree,
  readUnityQuaternion,
  readUnityVector3,
  type UnityQuaternionLike,
  type UnityVectorLike,
} from "./unityCoordinateConversion";

export type PrefabHeadFollowDebug = {
  active: boolean;
  sourcePath: string | null;
  targetPath: string | null;
  reason: string | null;
  setupVersion?: string;
  sourceScaleCorrection?: {
    characterHeightMeters: number | null;
    scale: number;
    reason: string;
  };
  targetCount?: number;
  targetPaths?: string[];
  mountedHeadRootCount?: number;
  mountedHeadOriginPaths?: string[];
  assemblyCounts?: {
    inputTransforms: number;
    retainedTransforms: number;
    removedTransforms: number;
    capturedCommonRemovedTransforms: number;
    removedAtLeastCapturedCommonCount: boolean;
  };
  positionRoots?: PrefabHeadFollowNodeDebug[];
  keyNodes?: Record<string, PrefabHeadFollowNodeDebug | null>;
  assemblyDistances?: {
    bodyNeckToFaceNeck: number | null;
    bodyHeadToFaceHead: number | null;
  };
};

export type PrefabHeadFollowNodeDebug = {
  path: string;
  canonicalPath: string;
  parentPath: string | null;
  destroyed: boolean;
  localPosition: { x: number; y: number; z: number };
  localQuaternion: { x: number; y: number; z: number; w: number };
  worldPosition: { x: number; y: number; z: number };
  worldQuaternion: { x: number; y: number; z: number; w: number };
  worldForward: { x: number; y: number; z: number };
};

export type UnityPrefabSourceGraph = {
  root: THREE.Group;
  nodeByPath: Map<string, THREE.Object3D>;
  nodeByPathId: Map<number, THREE.Object3D>;
  ambiguousPaths: Set<string>;
  meshCarrierBindings: Array<{
    source: THREE.Object3D;
    target: THREE.Object3D;
  }>;
  bodyAttach: THREE.Object3D | null;
  bodyAttachPath: string | null;
  headRoot: THREE.Object3D | null;
  headRootPath: string | null;
  headOrigin: THREE.Object3D | null;
  headOriginPath: string | null;
  bodyRootBone: THREE.Object3D | null;
  bodyRootBonePath: string | null;
  headRendererPaths: string[];
  debug: PrefabHeadFollowDebug;
};

export type NativeMeshInstallDiagnostics = {
  meshCount: number;
  boneCount: number;
  skinnedMeshCount: number;
  skinBindings: NativeMeshSkinBindingDiagnostics[];
  error: string | null;
  warnings: string[];
};

export type NativeMeshSkinBindingDiagnostics = {
  meshName: string;
  partKind: string | null;
  rendererTransformPath: string | null;
  rootBonePath: string | null;
  rootBoneResolved: boolean;
  effectiveRootBonePath: string | null;
  effectiveRootBoneResolved: boolean;
  boneCount: number;
  restTranslation: { x: number; y: number; z: number };
  restScale: { x: number; y: number; z: number };
  restMatrixSpread: number;
  restMatrixSpreadBonePath: string | null;
};

type NativeMeshInstallOutcome = {
  installed: boolean;
  skinned: boolean;
  skinBinding?: NativeMeshSkinBindingDiagnostics;
};

export function applyUnityCharacterModelScale(
  graph: UnityPrefabSourceGraph,
  characterModelScale: number
) {
  const scale = THREE.MathUtils.clamp(characterModelScale || 1, 0.5, 2);
  const positionNote = graph.nodeByPath.get("body/Position");
  if (!positionNote) {
    throw new Error("Official CharacterModel PositionNote 'body/Position' was not found.");
  }
  positionNote.scale.setScalar(scale);
  positionNote.updateMatrix();
  graph.root.updateMatrixWorld(true);
  return positionNote;
}

type RuntimePrefabTransformSource = {
  pathId?: number;
  name?: string | null;
  transformPath?: string | null;
  poseRoot?: string | null;
  runtimePartIndex?: number;
  parentPathId?: number | null;
  childPathIds?: number[];
  localPosition?: UnityVectorLike;
  localRotation?: UnityQuaternionLike;
  localScale?: UnityVectorLike;
};

type RuntimePrefabRendererSource = {
  pathId?: number;
  typeName?: string | null;
  transformPathId?: number | null;
  transformPath?: string | null;
};

type RuntimePrefabGraphSource = {
  partKind?: string;
  transforms?: RuntimePrefabTransformSource[];
  renderers?: RuntimePrefabRendererSource[];
};

type RuntimeUnitySetupSource = {
  version?: string | number;
  prefabGraphs?: RuntimePrefabGraphSource[];
  bodyHeadAssembly?: RuntimeUnityBodyHeadAssemblySource;
  constraintSetup?: RuntimeConstraintSetupSource;
};

type RuntimeUnityBodyHeadAssemblySource = {
  version?: string | number;
  sourceKind?: string;
  parentRootPath?: string | null;
  parentAttachPath?: string | null;
  childRootPath?: string | null;
  childOriginPath?: string | null;
  parentingMode?: string;
  coordinateSpace?: string;
  faceRendererName?: string | null;
  combineNodeAName?: string | null;
  combineNodeBName?: string | null;
  childMoveSuffix?: string | null;
  parentCombineNodeAPath?: string | null;
  parentCombineNodeBPath?: string | null;
  childCombineNodeAPath?: string | null;
  childCombineNodeBPath?: string | null;
};

type RuntimeNativeMeshSetSource = {
  version?: string | number;
  meshes?: RuntimeNativeMeshSource[];
  warnings?: string[];
};

type RuntimeNativeMeshSource = {
  partKind?: string;
  meshPath?: string;
  meshName?: string;
  rendererPathId?: number;
  rendererTransformPathId?: number | null;
  rendererTransformPath?: string;
  rootBonePathId?: number | null;
  rootBonePath?: string | null;
  bonePathIds?: number[];
  bonePaths?: string[];
  boneInverseBindMatrices?: RuntimeNumericArray;
  submeshes?: RuntimeNativeSubmeshSource[];
  positions?: RuntimeNumericArray;
  normals?: RuntimeNumericArray;
  tangents?: RuntimeNumericArray;
  uv0?: RuntimeNumericArray;
  uv1?: RuntimeNumericArray;
  uv2?: RuntimeNumericArray;
  colors?: RuntimeNumericArray;
  skinIndices?: RuntimeNumericArray;
  skinWeights?: RuntimeNumericArray;
  morphTargets?: RuntimeNativeMorphTargetSource[];
};

type RuntimeNativeSubmeshSource = {
  slotIndex: number;
  materialKey: string;
  materialFileId?: number;
  materialPathId?: number;
  materialName?: string;
  start?: number;
  count?: number;
  indices?: RuntimeNumericArray;
};

type RuntimeNativeMorphTargetSource = {
  name?: string;
  indices?: RuntimeNumericArray;
  positionDeltas?: RuntimeNumericArray;
  normalDeltas?: RuntimeNumericArray;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? value as Record<string, unknown> : {};
}

function readRuntimeNumber(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function readRuntimeUnitySetup0414(extension: unknown): RuntimeUnitySetupSource | null {
  const payload = asRecord(extension);
  const springBone = asRecord(payload.pjskSpringBone ?? payload.PjskSpringBone);
  const setup = asRecord(
    payload.runtimeUnitySetup ?? payload.RuntimeUnitySetup ??
      springBone.runtimeUnitySetup ?? springBone.RuntimeUnitySetup
  ) as RuntimeUnitySetupSource;
  const version = setup.version;
  return version === "0414" || version === 414 ? setup : null;
}

function readRuntimeNativeMeshSet0414(extension: unknown): RuntimeNativeMeshSetSource | null {
  const payload = asRecord(extension);
  const nativeMeshes = asRecord(
    payload.nativeMeshes ?? payload.NativeMeshes
  ) as RuntimeNativeMeshSetSource;
  const version = nativeMeshes.version;
  return version === "0414" || version === 414 ? nativeMeshes : null;
}

function resolvePrefabGraphNode(
  nodeByPath: ReadonlyMap<string, THREE.Object3D>,
  candidates: readonly (string | null | undefined)[]
) {
  for (const candidate of candidates) {
    if (!candidate) {
      continue;
    }
    const node = nodeByPath.get(candidate);
    if (node) {
      return { path: candidate, node };
    }
  }
  return null;
}

function isModelCombineSetupAssembly(
  assembly: RuntimeUnityBodyHeadAssemblySource | undefined
): assembly is RuntimeUnityBodyHeadAssemblySource {
  return assembly?.parentingMode === "model_combine_setup";
}

function setParentKeepingLocal(child: THREE.Object3D, parent: THREE.Object3D) {
  if (child.parent) {
    child.parent.remove(child);
  }
  parent.add(child);
  child.updateMatrix();
}

function drainChildrenKeepingLocal(
  sourceParent: THREE.Object3D,
  destParent: THREE.Object3D
) {
  while (sourceParent.children.length > 0) {
    setParentKeepingLocal(sourceParent.children[0], destParent);
  }
}

function moveFaceRendererTransforms(
  nodeByPath: Map<string, THREE.Object3D>,
  rendererPaths: readonly string[],
  destParent: THREE.Object3D,
) {
  const movedPaths: string[] = [];
  const movedNodes = new Set<THREE.Object3D>();
  for (const rendererPath of rendererPaths) {
    const node = nodeByPath.get(rendererPath);
    if (node && !movedNodes.has(node)) {
      setParentKeepingLocal(node, destParent);
      movedNodes.add(node);
      movedPaths.push(rendererPath);
    }
  }
  return movedPaths;
}

function detachRuntimeSubtree(
  node: THREE.Object3D,
  nodeByPath: Map<string, THREE.Object3D>,
  nodeByPathId: Map<number, THREE.Object3D>
) {
  if (node.parent) {
    node.parent.remove(node);
  }
  const detached = new Set<THREE.Object3D>();
  node.traverse((child) => {
    child.userData.pjskModelCombineDestroyed = true;
    detached.add(child);
  });
  for (const [path, candidate] of nodeByPath.entries()) {
    if (detached.has(candidate)) {
      nodeByPath.delete(path);
    }
  }
  for (const [pathId, candidate] of nodeByPathId.entries()) {
    if (detached.has(candidate)) {
      nodeByPathId.delete(pathId);
    }
  }
}

function replacePathIdNodeReferences(
  nodeByPathId: Map<number, THREE.Object3D>,
  source: THREE.Object3D,
  replacement: THREE.Object3D
) {
  for (const [pathId, candidate] of nodeByPathId.entries()) {
    if (candidate === source) {
      nodeByPathId.set(pathId, replacement);
    }
  }
}

function applyOfficialModelCombineSetup(
  root: THREE.Group,
  nodeByPath: Map<string, THREE.Object3D>,
  nodeByPathId: Map<number, THREE.Object3D>,
  assembly: RuntimeUnityBodyHeadAssemblySource,
  headRendererPaths: readonly string[]
) {
  const childMoveSuffix = assembly.childMoveSuffix ?? "_target";
  const parentRootPath = assembly.parentRootPath;
  const childRootPath = assembly.childRootPath;
  const bodyNodeA = resolvePrefabGraphNode(nodeByPath, [
    assembly.parentCombineNodeAPath ?? assembly.parentAttachPath,
  ]);
  const bodyNodeB = resolvePrefabGraphNode(nodeByPath, [
    assembly.parentCombineNodeBPath,
  ]);
  const faceNodeA = resolvePrefabGraphNode(nodeByPath, [
    assembly.childCombineNodeAPath ?? assembly.childOriginPath,
  ]);
  const faceNodeB = resolvePrefabGraphNode(nodeByPath, [
    assembly.childCombineNodeBPath,
  ]);
  const childRoot = resolvePrefabGraphNode(nodeByPath, [childRootPath]);

  if (
    !parentRootPath ||
    !childRootPath ||
    !bodyNodeA ||
    !bodyNodeB ||
    !faceNodeA ||
    !faceNodeB ||
    !childRoot
  ) {
    throw new Error("Official model_combine_setup paths were not fully resolved.");
  }

  drainChildrenKeepingLocal(bodyNodeB.node, faceNodeB.node);

  const bodyNodeAParent = bodyNodeA.node.parent;
  const faceNodeAParent = faceNodeA.node.parent;
  if (bodyNodeAParent && faceNodeAParent) {
    // Official call order is sourceParent, destinationParent:
    // MoveSuffixChildren(bodyNeck.parent, faceNeck.parent, "_target").
    // These body-side helper nodes are moved into the temporary face lower
    // chain and disappear when that temporary prefab wrapper is destroyed.
    for (const child of [...bodyNodeAParent.children]) {
      if (child.name.endsWith(childMoveSuffix)) {
        setParentKeepingLocal(child, faceNodeAParent);
      }
    }
    const movedRendererPaths = moveFaceRendererTransforms(
      nodeByPath,
      headRendererPaths,
      nodeByPath.get(parentRootPath) ?? bodyNodeAParent,
    );
    const missingRendererPaths = headRendererPaths.filter(
      (path) => !movedRendererPaths.includes(path)
    );
    if (missingRendererPaths.length > 0) {
      throw new Error(
        `Official model_combine_setup head renderers were not moved: ${missingRendererPaths.join(", ")}.`
      );
    }
    const requiredFaceRendererPath =
      `${childRootPath}/${assembly.faceRendererName ?? "Face"}`;
    if (!movedRendererPaths.includes(requiredFaceRendererPath)) {
      throw new Error(
        `Official model_combine_setup face renderer '${requiredFaceRendererPath}' was not moved.`
      );
    }
    setParentKeepingLocal(faceNodeA.node, bodyNodeAParent);
  }

  faceNodeA.node.position.copy(bodyNodeA.node.position);
  faceNodeA.node.quaternion.copy(bodyNodeA.node.quaternion);
  faceNodeA.node.scale.copy(bodyNodeA.node.scale);
  faceNodeA.node.updateMatrix();
  faceNodeB.node.position.copy(bodyNodeB.node.position);
  faceNodeB.node.quaternion.copy(bodyNodeB.node.quaternion);
  faceNodeB.node.scale.copy(bodyNodeB.node.scale);
  faceNodeB.node.updateMatrix();

  // Unity patches the body renderer's Neck/Head bone slots to the retained
  // face Neck/Head before destroying the body-side duplicates. Keep the
  // original body PathIDs as aliases to those retained nodes so native mesh
  // bindings reproduce the same object-reference replacement.
  replacePathIdNodeReferences(nodeByPathId, bodyNodeA.node, faceNodeA.node);
  replacePathIdNodeReferences(nodeByPathId, bodyNodeB.node, faceNodeB.node);

  detachRuntimeSubtree(bodyNodeB.node, nodeByPath, nodeByPathId);
  detachRuntimeSubtree(bodyNodeA.node, nodeByPath, nodeByPathId);
  // The instantiated face prefab is only an assembly input. Its wrapper,
  // control nodes, and duplicate Position -> Chest_const chain do not survive
  // ModelCombineSetup; Face/Neck/Head and its SkinnedMeshRenderers were
  // extracted above. Static renderers remain in the wrapper and are destroyed.
  detachRuntimeSubtree(childRoot.node, nodeByPath, nodeByPathId);

  nodeByPath.set(bodyNodeA.path, faceNodeA.node);
  nodeByPath.set(bodyNodeB.path, faceNodeB.node);
  if (assembly.parentAttachPath) {
    nodeByPath.set(assembly.parentAttachPath, faceNodeA.node);
  }
  if (assembly.parentCombineNodeBPath) {
    nodeByPath.set(assembly.parentCombineNodeBPath, faceNodeB.node);
  }

  root.updateMatrixWorld(true);

  return { bodyNodeA, bodyNodeB, faceNodeA, faceNodeB };
}

function collectOfficialHeadRendererPaths(
  extension: unknown,
  childRootPath: string
) {
  return [...new Set(
    (readRuntimeUnitySetup0414(extension)?.prefabGraphs ?? [])
      .flatMap((graph) => graph.renderers ?? [])
      .filter((renderer) => renderer.typeName === "SkinnedMeshRenderer")
      .map((renderer) => renderer.transformPath)
      .filter((path): path is string => Boolean(
        path && path.startsWith(`${childRootPath}/`)
      ))
  )];
}

function isDestroyedStaticFaceRenderer(
  extension: unknown,
  source: RuntimeNativeMeshSource
) {
  const setup = readRuntimeUnitySetup0414(extension);
  const childRootPath = setup?.bodyHeadAssembly?.childRootPath;
  const transformPath = source.rendererTransformPath;
  if (
    !childRootPath ||
    !transformPath?.startsWith(`${childRootPath}/`)
  ) {
    return false;
  }
  const matchingRenderers = (setup.prefabGraphs ?? [])
    .flatMap((graph) => graph.renderers ?? [])
    .filter((renderer) => renderer.transformPath === transformPath);
  return matchingRenderers.length > 0 && matchingRenderers.every(
    (renderer) => renderer.typeName !== "SkinnedMeshRenderer"
  );
}

function resolveOfficialBodyRootBone(
  extension: unknown,
  parentRootPath: string
) {
  const bodyMesh = (readRuntimeNativeMeshSet0414(extension)?.meshes ?? [])
    .find((mesh) =>
      mesh.rendererTransformPath?.startsWith(`${parentRootPath}/`) &&
      typeof mesh.rootBonePath === "string"
    );
  return bodyMesh?.rootBonePath ?? null;
}

function resolvePrefabInstanceRoot(
  source: RuntimePrefabTransformSource,
  sourceByPathId: ReadonlyMap<number, RuntimePrefabTransformSource>
) {
  let current = source;
  const visited = new Set<number>();
  while (typeof current.parentPathId === "number") {
    if (typeof current.pathId === "number" && !visited.add(current.pathId)) {
      throw new Error(`Runtime prefab graph contains a parent cycle at PathID ${current.pathId}.`);
    }
    const parent = sourceByPathId.get(current.parentPathId);
    if (!parent) {
      break;
    }
    current = parent;
  }
  return current;
}

function prefabInstanceKey(source: RuntimePrefabTransformSource) {
  const topLevelPath = source.transformPath?.split("/")[0];
  return topLevelPath
    ? `${source.runtimePartIndex ?? -1}:${topLevelPath}`
    : null;
}

function resolvePreferredPrefabRoots(
  extension: unknown,
  sourceByPathId: ReadonlyMap<number, RuntimePrefabTransformSource>
) {
  const preferredRootByKey = new Map<string, number>();
  for (const mesh of readRuntimeNativeMeshSet0414(extension)?.meshes ?? []) {
    if (typeof mesh.rendererTransformPathId !== "number") {
      continue;
    }
    const renderer = sourceByPathId.get(mesh.rendererTransformPathId);
    if (!renderer) {
      continue;
    }
    const root = resolvePrefabInstanceRoot(renderer, sourceByPathId);
    const key = prefabInstanceKey(renderer);
    if (!key || typeof root.pathId !== "number") {
      continue;
    }
    const previous = preferredRootByKey.get(key);
    if (previous !== undefined && previous !== root.pathId) {
      throw new Error(
        `Runtime native meshes reference multiple Unity prefab instances for '${key}' (${previous}, ${root.pathId}).`
      );
    }
    preferredRootByKey.set(key, root.pathId);
  }
  return preferredRootByKey;
}

export function buildUnityPrefabSourceGraph(
  extension: unknown,
  meshCarrierRoot?: THREE.Object3D | null
): UnityPrefabSourceGraph | null {
  const setup = readRuntimeUnitySetup0414(extension);
  if (!setup?.prefabGraphs?.length) {
    return null;
  }

  const root = new THREE.Group();
  root.name = "UnityPrefabSourceRoot";
  root.userData.pjskUnityPrefabSourceGraph = true;
  const sourceScaleCorrection = resolveUnityPrefabSourceScaleCorrection(extension);
  root.scale.setScalar(sourceScaleCorrection.scale);
  root.userData.pjskSourceScaleCorrection = sourceScaleCorrection;
  const { sourceByPathId, pathCounts } = indexPrefabTransformSources(setup);
  const ambiguousPaths = new Set(
    [...pathCounts.entries()]
      .filter(([, count]) => count > 1)
      .map(([path]) => path)
  );
  const preferredRootByKey = resolvePreferredPrefabRoots(extension, sourceByPathId);
  const { nodeByPathId, nodeByPath } = buildPrefabTransformNodes(
    setup, sourceByPathId, preferredRootByKey);
  attachPrefabTransformNodes(root, nodeByPathId, sourceByPathId);

  root.updateMatrixWorld(true);
  const inputTransformCount = countRuntimeTransforms(root);
  const assembly = setup.bodyHeadAssembly;
  if (!isModelCombineSetupAssembly(assembly)) {
    throw new Error("Runtime package must provide the official model_combine_setup body/head assembly.");
  }
  const bodyAttach = resolvePrefabGraphNode(nodeByPath, [assembly.parentAttachPath]);
  const headRoot = resolvePrefabGraphNode(nodeByPath, [assembly.childRootPath]);
  const headOrigin = resolvePrefabGraphNode(nodeByPath, [assembly.childOriginPath]);
  if (!bodyAttach || !headRoot || !headOrigin) {
    throw new Error("Official model_combine_setup body/head roots were not fully resolved.");
  }
  const headRendererPaths = collectOfficialHeadRendererPaths(
    extension,
    headRoot.path
  );
  const modelCombine = applyOfficialModelCombineSetup(
    root,
    nodeByPath,
    nodeByPathId,
    assembly,
    headRendererPaths
  );
  const bodyRootBonePath = resolveOfficialBodyRootBone(extension, bodyAttach.path.split("/")[0]!);
  const bodyRootBone = bodyRootBonePath
    ? nodeByPath.get(bodyRootBonePath) ?? null
    : null;
  const retainedTransformCount = countRuntimeTransforms(root);
  const removedTransformCount = inputTransformCount - retainedTransformCount;

  const meshCarrierBindings = buildMeshCarrierBindings(nodeByPath, meshCarrierRoot);

  const debug: PrefabHeadFollowDebug = {
    active: true,
    sourcePath: modelCombine.bodyNodeA.path,
    targetPath: modelCombine.faceNodeA.path,
    reason: null,
    setupVersion: String(setup.version ?? ""),
    sourceScaleCorrection,
    mountedHeadRootCount: 1,
    mountedHeadOriginPaths: [modelCombine.faceNodeA.path],
    assemblyCounts: {
      inputTransforms: inputTransformCount,
      retainedTransforms: retainedTransformCount,
      removedTransforms: removedTransformCount,
      capturedCommonRemovedTransforms: 14,
      removedAtLeastCapturedCommonCount: removedTransformCount >= 14,
    },
    targetCount: meshCarrierBindings.length,
    targetPaths: meshCarrierBindings.slice(0, 24).map((binding) =>
      String(binding.source.userData.pjskTransformPath ?? binding.source.name)
    ),
    keyNodes: {
      runtimeMount: null,
      modelCombineBodyNeck: makePrefabNodeDebug(modelCombine.bodyNodeA.node, root),
      modelCombineFaceNeck: makePrefabNodeDebug(modelCombine.faceNodeA.node, root),
    },
  };

  return {
    root,
    nodeByPath,
    nodeByPathId,
    ambiguousPaths,
    meshCarrierBindings,
    bodyAttach: modelCombine.faceNodeA.node,
    bodyAttachPath: bodyAttach.path,
    headRoot: modelCombine.faceNodeA.node,
    headRootPath: modelCombine.faceNodeA.path,
    headOrigin: modelCombine.faceNodeA.node,
    headOriginPath: modelCombine.faceNodeA.path,
    bodyRootBone,
    bodyRootBonePath,
    headRendererPaths,
    debug,
  };
}

function indexPrefabTransformSources(setup: RuntimeUnitySetupSource) {
  const sourceByPathId = new Map<number, RuntimePrefabTransformSource>();
  const pathCounts = new Map<string, number>();
  for (const graph of setup.prefabGraphs ?? []) {
    for (const transform of graph.transforms ?? []) {
      if (typeof transform.pathId !== "number" || !transform.transformPath) {
        continue;
      }
      sourceByPathId.set(transform.pathId, transform);
      pathCounts.set(transform.transformPath, (pathCounts.get(transform.transformPath) ?? 0) + 1);
    }
  }
  return { sourceByPathId, pathCounts };
}

function buildPrefabTransformNodes(
  setup: RuntimeUnitySetupSource,
  sourceByPathId: ReadonlyMap<number, RuntimePrefabTransformSource>,
  preferredRootByKey: ReadonlyMap<string, number>
) {
  const nodeByPathId = new Map<number, THREE.Object3D>();
  const nodeByPath = new Map<string, THREE.Object3D>();
  for (const graph of setup.prefabGraphs ?? []) {
    for (const transform of graph.transforms ?? []) {
      addPrefabTransformNode(
        transform, sourceByPathId, preferredRootByKey, nodeByPathId, nodeByPath);
    }
  }
  return { nodeByPathId, nodeByPath };
}

function addPrefabTransformNode(
  transform: RuntimePrefabTransformSource,
  sourceByPathId: ReadonlyMap<number, RuntimePrefabTransformSource>,
  preferredRootByKey: ReadonlyMap<string, number>,
  nodeByPathId: Map<number, THREE.Object3D>,
  nodeByPath: Map<string, THREE.Object3D>
) {
  if (typeof transform.pathId !== "number" || !transform.transformPath) {
    return;
  }
  const node = new THREE.Object3D();
  node.name = transform.name ?? transform.transformPath.split("/").pop() ?? `path_${transform.pathId}`;
  node.userData.pjskTransformPath = transform.transformPath;
  node.userData.pjskRuntimePartIndex = transform.runtimePartIndex;
  node.userData.pjskPoseRoot = transform.poseRoot ?? null;
  node.position.copy(convertUnityPositionToThree(
    readUnityVector3(transform.localPosition, new THREE.Vector3())));
  node.quaternion.copy(convertUnityQuaternionToThree(readUnityQuaternion(transform.localRotation)));
  node.scale.copy(readUnityVector3(transform.localScale, new THREE.Vector3(1, 1, 1)));
  node.updateMatrix();
  nodeByPathId.set(transform.pathId, node);
  const rootSource = resolvePrefabInstanceRoot(transform, sourceByPathId);
  const preferredRoot = preferredRootByKey.get(prefabInstanceKey(transform) ?? "");
  if (preferredRoot === undefined || preferredRoot === rootSource.pathId || !nodeByPath.has(transform.transformPath)) {
    nodeByPath.set(transform.transformPath, node);
  }
}

function attachPrefabTransformNodes(
  root: THREE.Object3D,
  nodeByPathId: ReadonlyMap<number, THREE.Object3D>,
  sourceByPathId: ReadonlyMap<number, RuntimePrefabTransformSource>
) {
  for (const [pathId, node] of nodeByPathId.entries()) {
    const parentPathId = sourceByPathId.get(pathId)?.parentPathId;
    const parent = typeof parentPathId === "number" ? nodeByPathId.get(parentPathId) : null;
    (parent ?? root).add(node);
  }
}

function buildMeshCarrierBindings(
  nodeByPath: ReadonlyMap<string, THREE.Object3D>,
  meshCarrierRoot?: THREE.Object3D | null
): UnityPrefabSourceGraph["meshCarrierBindings"] {
  if (!meshCarrierRoot) {
    return [];
  }
  const bindings: UnityPrefabSourceGraph["meshCarrierBindings"] = [];
  const carrierNodeByPath = buildPrefabNodePathLookup(meshCarrierRoot);
  for (const [path, source] of nodeByPath.entries()) {
    const target = carrierNodeByPath.get(path);
    if (target) {
      bindings.push({ source, target });
    }
  }
  return bindings;
}

function countRuntimeTransforms(root: THREE.Object3D) {
  let count = 0;
  root.traverse((node) => {
    if (node !== root) {
      count += 1;
    }
  });
  return count;
}

function resolveUnityPrefabSourceScaleCorrection(extension: unknown) {
  const payload = asRecord(extension);
  const character = asRecord(payload.character ?? payload.Character);
  const bodyManifest = asRecord(payload.bodyManifest ?? payload.BodyManifest);
  const characterHeightMeters = readRuntimeNumber(
    character.characterHeightMeters ??
      character.CharacterHeightMeters ??
      bodyManifest.CharacterHeightMeters ??
      bodyManifest.characterHeightMeters
  );
  return {
    characterHeightMeters,
    scale: 1,
    reason: "presentation-module-applies-position-scale",
  };
}

export function installUnityRuntimeNativeMeshes(
  graph: UnityPrefabSourceGraph,
  extension: unknown
): NativeMeshInstallDiagnostics {
  const nativeMeshes = readRuntimeNativeMeshSet0414(extension);
  const meshes = nativeMeshes?.meshes ?? [];
  if (!nativeMeshes || meshes.length === 0) {
    return {
      meshCount: 0,
      boneCount: graph.nodeByPath.size,
      skinnedMeshCount: 0,
      skinBindings: [],
      error: "Unity runtime nativeMeshes version 0414 is missing or empty.",
      warnings: nativeMeshes?.warnings ?? [],
    };
  }

  let meshCount = 0;
  let skinnedMeshCount = 0;
  const skinBindings: NativeMeshSkinBindingDiagnostics[] = [];
  const warnings = [...(nativeMeshes.warnings ?? [])];
  const fatalErrors: string[] = [];
  graph.root.updateMatrixWorld(true);

  for (const source of meshes) {
    if (isDestroyedStaticFaceRenderer(extension, source)) {
      continue;
    }
    const outcome = installUnityRuntimeNativeMeshSource(
      graph, source, warnings, fatalErrors);
    meshCount += outcome.installed ? 1 : 0;
    skinnedMeshCount += outcome.skinned ? 1 : 0;
    if (outcome.skinBinding) {
      skinBindings.push(outcome.skinBinding);
    }
  }

  graph.root.updateMatrixWorld(true);
  return {
    meshCount,
    boneCount: graph.nodeByPath.size,
    skinnedMeshCount,
    skinBindings,
    error: fatalErrors.length > 0
      ? fatalErrors.join(" ")
      : meshCount > 0
        ? null
        : "Unity runtime nativeMeshes did not produce any renderable mesh.",
    warnings,
  };
}

function installUnityRuntimeNativeMeshSource(
  graph: UnityPrefabSourceGraph,
  source: RuntimeNativeMeshSource,
  warnings: string[],
  fatalErrors: string[]
): NativeMeshInstallOutcome {
  const bonePaths = source.bonePaths ?? [];
  const bonePathIds = source.bonePathIds ?? [];
  if (!validateNativeMeshBindingSource(graph, source, bonePaths, bonePathIds, warnings, fatalErrors)) {
    return { installed: false, skinned: false };
  }
  const parent = resolveNativeMeshParent(graph, source, warnings, fatalErrors);
  if (!parent) {
    return { installed: false, skinned: false };
  }
  const geometry = buildUnityRuntimeNativeGeometry(source);
  if (!geometry) {
    warnings.push(`Native mesh '${nativeMeshLabel(source)}' skipped: invalid geometry payload.`);
    return { installed: false, skinned: false };
  }
  const meshMaterials = buildNativeMeshMaterials(source, geometry);
  const meshName = source.meshName ?? source.meshPath?.split("/").pop() ?? "UnityNativeMesh";
  const bones = resolveNativeMeshBones(graph, bonePaths, bonePathIds);
  if (bonePaths.length > 0 && bones.length !== bonePaths.length) {
    const error = `Native mesh '${source.meshPath ?? meshName}' skipped: ${bonePaths.length - bones.length} skin bones were unresolved.`;
    warnings.push(error);
    fatalErrors.push(error);
    geometry.dispose();
    return { installed: false, skinned: false };
  }
  const mesh = bonePaths.length > 0
    ? new THREE.SkinnedMesh(geometry, meshMaterials)
    : new THREE.Mesh(geometry, meshMaterials);
  prepareAndMountNativeMesh(mesh, meshName, source, parent);
  const skinBinding = mesh instanceof THREE.SkinnedMesh
    ? bindUnityRuntimeNativeSkin(graph, source, mesh, bones, meshName, warnings)
    : undefined;
  return { installed: true, skinned: mesh instanceof THREE.SkinnedMesh, skinBinding };
}

function nativeMeshLabel(source: RuntimeNativeMeshSource) {
  return source.meshPath ?? source.meshName ?? "<unnamed>";
}

function validateNativeMeshBindingSource(
  graph: UnityPrefabSourceGraph,
  source: RuntimeNativeMeshSource,
  bonePaths: string[],
  bonePathIds: number[],
  warnings: string[],
  fatalErrors: string[]
) {
  const ambiguousLegacyPaths = [
    ...(typeof source.rendererTransformPathId !== "number" ? [source.rendererTransformPath] : []),
    ...(bonePathIds.length === 0 ? bonePaths : []),
    ...(typeof source.rootBonePathId !== "number" ? [source.rootBonePath] : []),
  ].filter((path): path is string => Boolean(path && graph.ambiguousPaths.has(path)));
  if (ambiguousLegacyPaths.length > 0) {
    const error = `Native mesh '${nativeMeshLabel(source)}' has an ambiguous legacy PathID-less skin binding (${[...new Set(ambiguousLegacyPaths)].join(", ")}); regenerate it with a current Haruki-3D-Exporter.`;
    warnings.push(error);
    fatalErrors.push(error);
    return false;
  }
  if (bonePathIds.length > 0 && bonePathIds.length !== bonePaths.length) {
    const error = `Native mesh '${nativeMeshLabel(source)}' has ${bonePaths.length} bone paths but ${bonePathIds.length} bone PathIDs; regenerate it with a current Haruki-3D-Exporter.`;
    warnings.push(error);
    fatalErrors.push(error);
    return false;
  }
  return true;
}

function resolveNativeMeshParent(
  graph: UnityPrefabSourceGraph,
  source: RuntimeNativeMeshSource,
  warnings: string[],
  fatalErrors: string[]
) {
  const targetPath = source.rendererTransformPath;
  const parent = typeof source.rendererTransformPathId === "number"
    ? graph.nodeByPathId.get(source.rendererTransformPathId)
    : targetPath ? graph.nodeByPath.get(targetPath) : null;
  if (parent) {
    return parent;
  }
  const error = `Native mesh '${nativeMeshLabel(source)}' skipped: renderer transform '${targetPath ?? "<null>"}' was not found.`;
  warnings.push(error);
  if (typeof source.rendererTransformPathId === "number") {
    fatalErrors.push(error);
  }
  return null;
}

function buildNativeMeshMaterials(
  source: RuntimeNativeMeshSource,
  geometry: THREE.BufferGeometry
): THREE.Material[] {
  const materials = (source.submeshes ?? []).map((submesh) => {
    if (!submesh.materialKey || typeof submesh.slotIndex !== "number") {
      throw new Error(
        `Native mesh '${nativeMeshLabel(source)}' has a submesh without material identity; regenerate it with Haruki-3D-Exporter materialKey runtime support.`
      );
    }
    const material = new THREE.MeshBasicMaterial({ color: 0xffffff, vertexColors: geometry.hasAttribute("color") });
    material.name = submesh.materialName ?? source.meshName ?? source.meshPath ?? "native_material";
    material.userData.pjskMaterialKey = submesh.materialKey;
    material.userData.pjskMaterialSlotIndex = submesh.slotIndex;
    return material;
  });
  return materials.length > 0 ? materials : [new THREE.MeshBasicMaterial({ color: 0xffffff })];
}

function resolveNativeMeshBones(
  graph: UnityPrefabSourceGraph,
  bonePaths: string[],
  bonePathIds: number[]
) {
  return bonePaths
    .map((path, index) => bonePathIds.length > 0
      ? graph.nodeByPathId.get(bonePathIds[index]!)
      : graph.nodeByPath.get(path))
    .filter((node): node is THREE.Object3D => Boolean(node));
}

function prepareAndMountNativeMesh(
  mesh: THREE.Mesh | THREE.SkinnedMesh,
  meshName: string,
  source: RuntimeNativeMeshSource,
  parent: THREE.Object3D
) {
  mesh.name = meshName;
  mesh.userData.pjskNativeUnityMesh = true;
  mesh.userData.pjskPartKind = source.partKind ?? null;
  mesh.userData.pjskRendererPathId = source.rendererPathId ?? null;
  mesh.frustumCulled = false;
  parent.add(mesh);
}

function bindUnityRuntimeNativeSkin(
  graph: UnityPrefabSourceGraph,
  source: RuntimeNativeMeshSource,
  mesh: THREE.SkinnedMesh,
  skeletonBones: THREE.Object3D[],
  meshName: string,
  warnings: string[]
): NativeMeshSkinBindingDiagnostics {
  graph.root.updateMatrixWorld(true);
  mesh.updateMatrixWorld(true);
  const inverseBindMatrices = buildUnityRuntimeBoneInverseBindMatrices(
    source,
    skeletonBones.length,
    warnings
  );
  const rendererBindMatrix = mesh.matrixWorld.clone();
  convertUnityBindMatricesToThree(inverseBindMatrices, rendererBindMatrix);
  const skeleton = new THREE.Skeleton(
    skeletonBones as unknown as THREE.Bone[],
    inverseBindMatrices.length > 0 ? inverseBindMatrices : undefined
  );
  if (inverseBindMatrices.length === 0) {
    skeleton.calculateInverses();
  }
  mesh.bind(skeleton, rendererBindMatrix);
  return buildNativeSkinBindingDiagnostics(graph, source, meshName, skeletonBones, skeleton);
}

function convertUnityBindMatricesToThree(
  inverseBindMatrices: THREE.Matrix4[],
  rendererBindMatrix: THREE.Matrix4
) {
  if (inverseBindMatrices.length === 0) {
    return;
  }
  const rendererBindMatrixInverse = rendererBindMatrix.clone().invert();
  for (const inverseBindMatrix of inverseBindMatrices) {
    inverseBindMatrix.multiply(rendererBindMatrixInverse);
  }
}

function buildNativeSkinBindingDiagnostics(
  graph: UnityPrefabSourceGraph,
  source: RuntimeNativeMeshSource,
  meshName: string,
  skeletonBones: THREE.Object3D[],
  skeleton: THREE.Skeleton
): NativeMeshSkinBindingDiagnostics {
  const targetPath = source.rendererTransformPath;
  const isHeadRenderer = Boolean(targetPath && graph.headRendererPaths.includes(targetPath));
  const rootBoneResolved = resolveNativeRootBoneStatus(graph, source);
  return {
    meshName,
    partKind: source.partKind ?? null,
    rendererTransformPath: targetPath ?? null,
    rootBonePath: source.rootBonePath ?? null,
    rootBoneResolved,
    effectiveRootBonePath: isHeadRenderer ? graph.bodyRootBonePath : source.rootBonePath ?? null,
    effectiveRootBoneResolved: isHeadRenderer ? Boolean(graph.bodyRootBone) : rootBoneResolved,
    boneCount: skeletonBones.length,
    ...makeSkinRestTransform(skeletonBones[0]!, skeleton.boneInverses[0]!),
    ...measureSkinRestMatrixSpread(skeletonBones, skeleton.boneInverses),
  };
}

function resolveNativeRootBoneStatus(
  graph: UnityPrefabSourceGraph,
  source: RuntimeNativeMeshSource
) {
  if (typeof source.rootBonePathId === "number") {
    return graph.nodeByPathId.has(source.rootBonePathId);
  }
  return source.rootBonePath ? graph.nodeByPath.has(source.rootBonePath) : false;
}

function makeSkinRestTransform(
  bone: THREE.Object3D,
  inverseBindMatrix: THREE.Matrix4
) {
  const matrix = new THREE.Matrix4()
    .multiplyMatrices(bone.matrixWorld, inverseBindMatrix);
  const translation = new THREE.Vector3();
  const rotation = new THREE.Quaternion();
  const scale = new THREE.Vector3();
  matrix.decompose(translation, rotation, scale);
  return {
    restTranslation: vectorDebugSnapshot(translation),
    restScale: vectorDebugSnapshot(scale),
  };
}

function measureSkinRestMatrixSpread(
  bones: readonly THREE.Object3D[],
  inverseBindMatrices: readonly THREE.Matrix4[]
) {
  if (bones.length < 2 || inverseBindMatrices.length !== bones.length) {
    return {
      restMatrixSpread: 0,
      restMatrixSpreadBonePath: null,
    };
  }
  const reference = new THREE.Matrix4()
    .multiplyMatrices(bones[0]!.matrixWorld, inverseBindMatrices[0]!);
  const candidate = new THREE.Matrix4();
  let maxDifference = 0;
  let maxDifferenceBonePath: string | null = null;
  for (let index = 1; index < bones.length; index += 1) {
    candidate.multiplyMatrices(bones[index]!.matrixWorld, inverseBindMatrices[index]!);
    for (let element = 0; element < 16; element += 1) {
      const difference = Math.abs(
        candidate.elements[element]! - reference.elements[element]!
      );
      if (difference > maxDifference) {
        maxDifference = difference;
        maxDifferenceBonePath = String(
          bones[index]!.userData.pjskTransformPath ?? bones[index]!.name
        );
      }
    }
  }
  const restMatrixSpread = Number(maxDifference.toFixed(6));
  return {
    restMatrixSpread,
    restMatrixSpreadBonePath:
      restMatrixSpread > 0 ? maxDifferenceBonePath : null,
  };
}

function buildUnityRuntimeBoneInverseBindMatrices(
  source: RuntimeNativeMeshSource,
  boneCount: number,
  warnings: string[]
) {
  const values = source.boneInverseBindMatrices ?? [];
  if (boneCount === 0 || values.length === 0) {
    return [];
  }
  if (values.length !== boneCount * 16) {
    warnings.push(`Native mesh '${source.meshPath ?? source.meshName ?? "<unnamed>"}' has ${values.length} inverse bind matrix floats for ${boneCount} bones; expected ${boneCount * 16}.`);
    return [];
  }

  const matrices: THREE.Matrix4[] = [];
  for (let offset = 0; offset < values.length; offset += 16) {
    matrices.push(new THREE.Matrix4().fromArray(values, offset));
  }
  return matrices;
}

function buildUnityRuntimeNativeGeometry(source: RuntimeNativeMeshSource) {
  const positions = source.positions ?? [];
  if (positions.length === 0 || positions.length % 3 !== 0) {
    return null;
  }
  const vertexCount = positions.length / 3;
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
  addNativeGeometryAttributes(geometry, source, vertexCount);
  addNativeGeometryIndices(geometry, source.submeshes ?? []);
  addNativeGeometryMorphTargets(geometry, source.morphTargets ?? [], vertexCount);

  geometry.computeBoundingSphere();
  return geometry;
}

function addNativeGeometryAttributes(
  geometry: THREE.BufferGeometry,
  source: RuntimeNativeMeshSource,
  vertexCount: number
) {
  addFloatGeometryAttribute(geometry, "normal", source.normals, 3, vertexCount);
  if (source.tangents?.length === vertexCount * 4) {
    geometry.setAttribute("tangent", new THREE.Float32BufferAttribute(source.tangents, 4));
  }
  addFloatGeometryAttribute(geometry, "uv", source.uv0, 2, vertexCount);
  addFloatGeometryAttribute(geometry, "uv1", source.uv1, 2, vertexCount);
  if (source.uv2?.length === vertexCount * 2) {
    geometry.setAttribute("uv2", new THREE.Float32BufferAttribute(source.uv2, 2));
  }
  addFloatGeometryAttribute(geometry, "color", source.colors, 4, vertexCount);
  addUint16GeometryAttribute(geometry, "skinIndex", source.skinIndices, 4, vertexCount);
  addFloatGeometryAttribute(geometry, "skinWeight", source.skinWeights, 4, vertexCount);
}

function addFloatGeometryAttribute(
  geometry: THREE.BufferGeometry,
  name: string,
  values: RuntimeNumericArray | undefined,
  itemSize: number,
  vertexCount: number
) {
  if (values?.length === vertexCount * itemSize) {
    geometry.setAttribute(name, new THREE.Float32BufferAttribute(values, itemSize));
  }
}

function addUint16GeometryAttribute(
  geometry: THREE.BufferGeometry,
  name: string,
  values: RuntimeNumericArray | undefined,
  itemSize: number,
  vertexCount: number
) {
  if (values?.length === vertexCount * itemSize) {
    geometry.setAttribute(name, new THREE.Uint16BufferAttribute(values, itemSize));
  }
}

function addNativeGeometryIndices(
  geometry: THREE.BufferGeometry,
  submeshes: RuntimeNativeSubmeshSource[]
) {
  const allIndices: number[] = [];
  for (const submesh of submeshes) {
    const start = allIndices.length;
    const indices = submesh.indices ?? [];
    allIndices.push(...indices);
    geometry.addGroup(start, indices.length, geometry.groups.length);
  }
  if (allIndices.length > 0) {
    geometry.setIndex(allIndices);
  }
}

function addNativeGeometryMorphTargets(
  geometry: THREE.BufferGeometry,
  targets: RuntimeNativeMorphTargetSource[],
  vertexCount: number
) {
  const positions: THREE.BufferAttribute[] = [];
  const normals: THREE.BufferAttribute[] = [];
  for (const target of targets) {
    addNativeGeometryMorphTarget(target, vertexCount, positions, normals);
  }
  if (positions.length > 0) {
    geometry.morphAttributes.position = positions;
    geometry.morphTargetsRelative = true;
  }
  if (normals.length === positions.length && normals.length > 0) {
    geometry.morphAttributes.normal = normals;
  }
}

function addNativeGeometryMorphTarget(
  target: RuntimeNativeMorphTargetSource,
  vertexCount: number,
  positions: THREE.BufferAttribute[],
  normals: THREE.BufferAttribute[]
) {
  const indices = target.indices ?? [];
  const positionDeltas = target.positionDeltas ?? [];
  if (indices.length === 0 || positionDeltas.length !== indices.length * 3) {
    return;
  }
  const positionArray = new Float32Array(vertexCount * 3);
  const normalArray = target.normalDeltas?.length === indices.length * 3
    ? new Float32Array(vertexCount * 3)
    : null;
  for (let index = 0; index < indices.length; index += 1) {
    copyNativeMorphDelta(indices[index], index, vertexCount, positionDeltas, positionArray);
    if (normalArray && target.normalDeltas) {
      copyNativeMorphDelta(indices[index], index, vertexCount, target.normalDeltas, normalArray);
    }
  }
  const positionAttribute = new THREE.BufferAttribute(positionArray, 3);
  positionAttribute.name = target.name ?? `morph_${positions.length}`;
  positions.push(positionAttribute);
  if (normalArray) {
    const normalAttribute = new THREE.BufferAttribute(normalArray, 3);
    normalAttribute.name = positionAttribute.name;
    normals.push(normalAttribute);
  }
}

function copyNativeMorphDelta(
  vertexIndex: number | undefined,
  deltaIndex: number,
  vertexCount: number,
  deltas: RuntimeNumericArray,
  output: Float32Array
) {
  if (!Number.isInteger(vertexIndex) || vertexIndex! < 0 || vertexIndex! >= vertexCount) {
    return;
  }
  output[vertexIndex! * 3] = deltas[deltaIndex * 3] ?? 0;
  output[vertexIndex! * 3 + 1] = deltas[deltaIndex * 3 + 1] ?? 0;
  output[vertexIndex! * 3 + 2] = deltas[deltaIndex * 3 + 2] ?? 0;
}

export function syncUnityPrefabSourceGraph(
  graph: UnityPrefabSourceGraph,
  extension: unknown,
  characterModelScale: number,
  constraintRuntime?: { update(): RuntimeConstraintDebug } | null
): RuntimeConstraintDebug | null {
  graph.root.updateMatrixWorld(true);
  const diagnostics = constraintRuntime
    ? constraintRuntime.update()
    : applyUnityRuntimeConstraints(
      graph,
      readRuntimeUnitySetup0414(extension)?.constraintSetup,
      characterModelScale
    );

  for (const binding of graph.meshCarrierBindings) {
    binding.target.position.copy(binding.source.position);
    binding.target.quaternion.copy(binding.source.quaternion);
    binding.target.scale.copy(binding.source.scale);
    binding.target.updateMatrix();
  }
  graph.root.updateMatrixWorld(true);
  return diagnostics;
}

export function createUnityPrefabConstraintRuntime(
  graph: UnityPrefabSourceGraph,
  extension: unknown,
  characterModelScale: number
) {
  const setup = readRuntimeUnitySetup0414(extension)?.constraintSetup;
  return setup
    ? new UnityConstraintRuntime(graph, setup, characterModelScale)
    : null;
}

export function makeUnityPrefabHeadFollowDebugSnapshot(
  graph: UnityPrefabSourceGraph | null,
  extension: unknown,
  fallback: PrefabHeadFollowDebug
): PrefabHeadFollowDebug {
  const base: PrefabHeadFollowDebug = {
    ...(graph?.debug ?? fallback),
    setupVersion: readRuntimeUnitySetupVersion(extension),
  };
  if (!graph) {
    return base;
  }
  const root = graph.root;
  root.updateMatrixWorld(true);
  const assembly = readRuntimeUnitySetup0414(extension)?.bodyHeadAssembly;
  const liveNodeByPath = buildPrefabNodePathLookup(root);
  const resolveKeyNode = (
    candidates: readonly string[]
  ): PrefabHeadFollowNodeDebug | null => {
    const resolved = resolvePrefabGraphNode(graph.nodeByPath, candidates)
      ?? resolvePrefabNodeCandidate(liveNodeByPath, candidates);
    return resolved ? makePrefabNodeDebug(resolved.node, root) : null;
  };
  const bodyNeck = resolveKeyNode([
    assembly?.parentCombineNodeAPath ?? "",
    graph.debug.sourcePath ?? "",
    "body/Position/PositionOffset/Hip/Waist/Spine/Chest/Neck",
    "body/Position/Hip/Waist/Spine/Chest/Neck",
  ]);
  const bodyHead = resolveKeyNode([
    assembly?.parentCombineNodeBPath ?? "",
    graph.debug.sourcePath ? `${graph.debug.sourcePath}/Head` : "",
    "body/Position/PositionOffset/Hip/Waist/Spine/Chest/Neck/Head",
    "body/Position/Hip/Waist/Spine/Chest/Neck/Head",
  ]);
  const facePosition = resolveKeyNode(["face/Position"]);
  const faceNeck = resolveKeyNode([
    assembly?.childCombineNodeAPath ?? "",
    graph.debug.targetPath ?? "",
    "face/Position/Hip/Waist/Spine/Chest/Neck",
  ]);
  const faceHead = resolveKeyNode([
    assembly?.childCombineNodeBPath ?? "",
    graph.debug.targetPath ? `${graph.debug.targetPath}/Head` : "",
    "face/Position/Hip/Waist/Spine/Chest/Neck/Head",
  ]);
  const meshContainerPosition = resolveKeyNode([
    "mdl_chr_IDL_A_00/Position",
    "mdl_chr_IDL_A_00/Position_4",
  ]);
  return {
    ...base,
    positionRoots: collectPrefabPositionRootDebug(root),
    assemblyDistances: {
      // The official combine replaces the body slots with the face nodes.
      // Measuring the two public path aliases would compare one object to
      // itself and falsely report a meaningful zero-distance check.
      bodyNeckToFaceNeck: null,
      bodyHeadToFaceHead: null,
    },
    keyNodes: {
      ...(base.keyNodes ?? {}),
      bodyNeck,
      bodyHead,
      facePosition,
      faceNeck,
      faceHead,
      meshContainerPosition,
    },
  };
}

function readRuntimeUnitySetupVersion(extension: unknown) {
  const payload = asRecord(extension);
  const springBone = asRecord(payload.pjskSpringBone ?? payload.PjskSpringBone);
  const setup = asRecord(
    payload.runtimeUnitySetup ?? payload.RuntimeUnitySetup ??
      springBone.runtimeUnitySetup ?? springBone.RuntimeUnitySetup
  );
  return String(setup.version ?? setup.Version ?? "");
}

function resolvePrefabNodeCandidate(
  nodeByPath: ReadonlyMap<string, THREE.Object3D>,
  candidates: readonly string[]
) {
  for (const path of candidates) {
    const node = nodeByPath.get(path);
    if (node) {
      return { node, path };
    }
  }
  return null;
}

function stripThreeDuplicateSuffix(name: string) {
  return name.replace(/_\d+$/, "");
}

function buildObjectPath(
  node: THREE.Object3D,
  root: THREE.Object3D,
  canonical = false
) {
  const segments: string[] = [];
  let current: THREE.Object3D | null = node;
  while (current && current !== root) {
    if (current.name) {
      segments.push(canonical ? stripThreeDuplicateSuffix(current.name) : current.name);
    }
    current = current.parent;
  }
  return segments.reverse().join("/");
}

function vectorDebugSnapshot(vector: THREE.Vector3) {
  return {
    x: Number(vector.x.toFixed(5)),
    y: Number(vector.y.toFixed(5)),
    z: Number(vector.z.toFixed(5)),
  };
}

function quaternionDebugSnapshot(quaternion: THREE.Quaternion) {
  return {
    x: Number(quaternion.x.toFixed(5)),
    y: Number(quaternion.y.toFixed(5)),
    z: Number(quaternion.z.toFixed(5)),
    w: Number(quaternion.w.toFixed(5)),
  };
}

function makePrefabNodeDebug(
  node: THREE.Object3D,
  root: THREE.Object3D
): PrefabHeadFollowNodeDebug {
  node.updateMatrixWorld(true);
  const worldPosition = new THREE.Vector3();
  const worldQuaternion = new THREE.Quaternion();
  const worldForward = new THREE.Vector3(0, 0, 1);
  node.getWorldPosition(worldPosition);
  node.getWorldQuaternion(worldQuaternion);
  worldForward.applyQuaternion(worldQuaternion).normalize();
  return {
    path: buildObjectPath(node, root),
    canonicalPath: buildObjectPath(node, root, true),
    parentPath: node.parent && node.parent !== root
      ? buildObjectPath(node.parent, root)
      : null,
    destroyed: node.userData.pjskModelCombineDestroyed === true,
    localPosition: vectorDebugSnapshot(node.position),
    localQuaternion: quaternionDebugSnapshot(node.quaternion),
    worldPosition: vectorDebugSnapshot(worldPosition),
    worldQuaternion: quaternionDebugSnapshot(worldQuaternion),
    worldForward: vectorDebugSnapshot(worldForward),
  };
}

function collectPrefabPositionRootDebug(root: THREE.Object3D) {
  const nodes: PrefabHeadFollowNodeDebug[] = [];
  const seen = new Set<THREE.Object3D>();
  root.updateMatrixWorld(true);
  root.traverse((node) => {
    if (node === root || !node.name || seen.has(node)) {
      return;
    }
    const canonicalPath = buildObjectPath(node, root, true);
    const isHeadFollowTarget = canonicalPath === "face/Position";
    const isBodyPosition = canonicalPath === "body/Position";
    const isMeshContainerPosition =
      canonicalPath.endsWith("/Position") &&
      canonicalPath.split("/").some((segment) => segment.startsWith("mdl_chr_"));
    if (!isHeadFollowTarget && !isBodyPosition && !isMeshContainerPosition) {
      return;
    }
    seen.add(node);
    nodes.push(makePrefabNodeDebug(node, root));
  });
  return nodes;
}
