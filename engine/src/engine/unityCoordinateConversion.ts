import * as THREE from "three";

/**
 * Coordinate contract for this engine.
 *
 * Unity-authored data enters the engine through a single mirror:
 *
 *   Unity / F5 / prefab local or world vector:  ( x, y, z )
 *   Engine / Three vector after import:        ( -x, y, z )
 *
 * Quaternions are mirrored by the matching basis change:
 *
 *   Unity quaternion:  ( x, y, z, w )
 *   Engine quaternion: ( x, -y, -z, w )
 *
 * Rules that keep the runtime sane:
 *
 * 1. Convert serialized Unity positions, directions, and rotations exactly once
 *    at the import boundary.
 * 2. After a Transform/Object3D exists in the engine scene, all runtime math is
 *    engine-space math. Do not convert live Three world/local positions again.
 * 3. When IDA/F5 names a Unity basis vector, such as QuaternionUtility.Left,
 *    convert that named Unity axis with convertUnityAxisToThree before using it
 *    with a Three matrix/quaternion.
 * 4. Never paste raw Unity basis literals like (-1, 0, 0) into runtime code
 *    unless the code is still explicitly operating in unconverted Unity space.
 * 5. AssetStudio ImportedModel geometry, bind matrices, morph deltas, and
 *    ImportedFrame rest transforms are already mirrored before export. Motion
 *    tracks are likewise normalized by Haruki-3D-Exporter. Do not pass either
 *    class of data through these helpers.
 *
 * The trap: Unity "Left" is local -X, but in this engine local -X is mirrored,
 * so Unity Left becomes engine +X. Hardcoding Three (-1, 0, 0) for an F5
 * "Left" silently flips SpringBone angle limits and makes whole cloth/hair
 * groups bend outward.
 */

export type UnityVectorLike = {
  x?: number;
  y?: number;
  z?: number;
  X?: number;
  Y?: number;
  Z?: number;
};

export type UnityQuaternionLike = {
  x?: number;
  y?: number;
  z?: number;
  w?: number;
  X?: number;
  Y?: number;
  Z?: number;
  W?: number;
};

export type UnityAxisName =
  | "right"
  | "left"
  | "up"
  | "down"
  | "forward"
  | "back";

const UNITY_AXIS_DIRECTIONS: Record<UnityAxisName, THREE.Vector3> = {
  right: new THREE.Vector3(1, 0, 0),
  left: new THREE.Vector3(-1, 0, 0),
  up: new THREE.Vector3(0, 1, 0),
  down: new THREE.Vector3(0, -1, 0),
  forward: new THREE.Vector3(0, 0, 1),
  back: new THREE.Vector3(0, 0, -1),
};

export function readUnityVector3(
  value: UnityVectorLike | undefined | null,
  fallback: THREE.Vector3
): THREE.Vector3 {
  if (!value) {
    return fallback.clone();
  }
  const x = readFiniteNumber(value.x ?? value.X);
  const y = readFiniteNumber(value.y ?? value.Y);
  const z = readFiniteNumber(value.z ?? value.Z);
  return x === null || y === null || z === null
    ? fallback.clone()
    : new THREE.Vector3(x, y, z);
}

export function readUnityQuaternion(
  value: UnityQuaternionLike | undefined | null
): THREE.Quaternion {
  if (!value) {
    return new THREE.Quaternion();
  }
  const x = readFiniteNumber(value.x ?? value.X);
  const y = readFiniteNumber(value.y ?? value.Y);
  const z = readFiniteNumber(value.z ?? value.Z);
  const w = readFiniteNumber(value.w ?? value.W);
  return x === null || y === null || z === null || w === null
    ? new THREE.Quaternion()
    : normalizeUnityLocalQuaternion(new THREE.Quaternion(x, y, z, w));
}

/** Match Transform.localRotation's SIMD normalization, including its two sum orders. */
export function normalizeUnityLocalQuaternion(q: THREE.Quaternion): THREE.Quaternion {
  const f = Math.fround;
  const x = f(q.x), y = f(q.y), z = f(q.z), w = f(q.w);
  const xx = f(x * x), yy = f(y * y), zz = f(z * z), ww = f(w * w);
  const xzLengthSq = f(f(xx + yy) + f(zz + ww));
  const ywLengthSq = f(f(yy + zz) + f(ww + xx));
  const xzLength = f(Math.sqrt(xzLengthSq)), ywLength = f(Math.sqrt(ywLengthSq));
  return q.set(xzLengthSq > f(1e-30) ? f(x / xzLength) : 0,
    ywLengthSq > f(1e-30) ? f(y / ywLength) : 0,
    xzLengthSq > f(1e-30) ? f(z / xzLength) : 0,
    ywLengthSq > f(1e-30) ? f(w / ywLength) : 1);
}

export function convertUnityPositionToThree(value: THREE.Vector3): THREE.Vector3 {
  return new THREE.Vector3(-value.x, value.y, value.z);
}

export function convertUnityDirectionToThree(value: THREE.Vector3): THREE.Vector3 {
  return convertUnityPositionToThree(value);
}

export function convertUnityQuaternionToThree(value: THREE.Quaternion): THREE.Quaternion {
  return new THREE.Quaternion(value.x, -value.y, -value.z, value.w);
}

export function convertUnityAxisToThree(axis: UnityAxisName): THREE.Vector3 {
  return convertUnityDirectionToThree(UNITY_AXIS_DIRECTIONS[axis]);
}

/** Native rotation composition includes ancestor scale signs, without matrix decomposition. */
export function getUnityWorldQuaternion(
  node: THREE.Object3D | null,
  target: THREE.Quaternion
): THREE.Quaternion {
  const f = Math.fround;
  target.identity();
  for (let current = node; current; current = current.parent) {
    const x = f(current.quaternion.x), y = f(current.quaternion.y);
    const z = f(current.quaternion.z), w = f(current.quaternion.w);
    const sx = current.scale.x < 0 ? -1 : 1, sy = current.scale.y < 0 ? -1 : 1;
    const sz = current.scale.z < 0 ? -1 : 1;
    const a = target.x * sy * sz, b = target.y * sx * sz, c = target.z * sx * sy, d = target.w;
    target.set(
      f(f(f(f(y * c) - f(z * b)) + f(w * a)) + f(x * d)),
      f(f(f(f(z * a) - f(x * c)) + f(w * b)) + f(y * d)),
      f(f(f(f(w * c) - f(y * a)) + f(z * d)) + f(x * b)),
      f(f(f(f(w * d) - f(x * a)) - f(z * c)) - f(y * b))
    );
  }
  return target;
}

const unityDirectionRotation = new THREE.Quaternion();

/** Unity Transform.position evaluates the local TRS chain in float32. */
export function getUnityWorldPosition(node: THREE.Object3D, target: THREE.Vector3): THREE.Vector3 {
  const f = Math.fround;
  target.set(f(node.position.x), f(node.position.y), f(node.position.z));
  for (let parent = node.parent; parent; parent = parent.parent) {
    target.set(f(target.x * f(parent.scale.x)), f(target.y * f(parent.scale.y)), f(target.z * f(parent.scale.z)));
    rotateUnityPositionFloat32(target, parent.quaternion);
    target.set(f(target.x + f(parent.position.x)), f(target.y + f(parent.position.y)), f(target.z + f(parent.position.z)));
  }
  return target;
}

export function inverseUnityTransformPoint(node: THREE.Object3D, point: THREE.Vector3): THREE.Vector3 {
  const chain: THREE.Object3D[] = [];
  for (let current: THREE.Object3D | null = node; current; current = current.parent) chain.push(current);
  const f = Math.fround;
  point.set(f(point.x), f(point.y), f(point.z));
  for (let i = chain.length - 1; i >= 0; i--) {
    const current = chain[i];
    point.set(f(point.x - f(current.position.x)), f(point.y - f(current.position.y)), f(point.z - f(current.position.z)));
    rotateUnityPositionFloat32(point, current.quaternion, true);
    point.set(f(point.x * unityInverseScaleFloat32(current.scale.x)),
      f(point.y * unityInverseScaleFloat32(current.scale.y)), f(point.z * unityInverseScaleFloat32(current.scale.z)));
  }
  return point;
}

function unityInverseScaleFloat32(value: number): number {
  const f = Math.fround, scale = f(value), absolute = Math.abs(scale);
  if (absolute < f(1e-9)) return 0;
  if (!Number.isFinite(scale)) return 1 / scale;
  // Unity's SSE inverse transform uses RCPPS plus two Newton steps. Recreate
  // its 11-bit input bins and 12-bit fractional reciprocal estimate without a
  // CPU-specific dependency/table; direct division loses the reciprocal rounding.
  const exponent = 2 ** Math.floor(Math.log2(absolute));
  const bin = Math.floor((absolute / exponent - 1) * 2048);
  const seed = f(Math.sign(scale) * Math.round(8192 / (1 + (bin + 0.5) / 2048)) / 8192 / exponent);
  let inverse = f(seed * f(2.000000476837158 - f(seed * scale)));
  inverse = f(inverse * f(2 - f(inverse * scale)));
  return Number.isNaN(inverse) ? seed : inverse;
}

function rotateUnityPositionFloat32(target: THREE.Vector3, rotation: THREE.Quaternion, inverse = false): void {
  const f = Math.fround;
  const x = target.x, y = target.y, z = target.z;
  const sign = inverse ? -1 : 1;
  const a = sign * f(rotation.x), b = sign * f(rotation.y), c = sign * f(rotation.z), d = f(rotation.w);
  // Native GetPosition factors the diagonal as v + rotationDelta(v).
  // Forming a rounded (1 + diagonalDelta) matrix first changes the result.
  const xx = f(f(-2 * b * b) - f(2 * c * c));
  const xy = f(f(-2 * c * d) - f(-2 * a * b));
  const xz = f(f(2 * a * c) - f(-2 * b * d));
  const yx = f(f(2 * b * a) - f(-2 * c * d));
  const yy = f(f(-2 * c * c) - f(2 * a * a));
  const yz = f(f(-2 * a * d) - f(-2 * b * c));
  const zx = f(f(-2 * b * d) - f(-2 * c * a));
  const zy = f(f(2 * c * b) - f(-2 * a * d));
  const zz = f(f(-2 * a * a) - f(2 * b * b));
  target.set(
    f(f(f(xx * x) + x) + f(f(xy * y) + f(xz * z))),
    f(f(f(yx * x) + y) + f(f(yy * y) + f(yz * z))),
    f(f(f(zx * x) + z) + f(f(zy * y) + f(zz * z)))
  );
}

/** Already-imported direction follows Unity world orientation without scale magnitudes. */
export function transformUnityDirectionToWorld(
  node: THREE.Object3D,
  direction: THREE.Vector3
): THREE.Vector3 {
  return direction.copy(rotateUnityVectorFloat32(direction, getUnityWorldQuaternion(node, unityDirectionRotation)));
}

// Unity's managed Quaternion * Vector3 stores these products in float locals
// before evaluating each output component. The optimized Three.js formula has
// different rounding near FromToRotation's discontinuous identity boundary.
export function rotateUnityVectorFloat32(v: THREE.Vector3, q: THREE.Quaternion): THREE.Vector3 {
  const f = Math.fround;
  const x = f(q.x * 2), y = f(q.y * 2), z = f(q.z * 2);
  const xx = f(q.x * x), yy = f(q.y * y), zz = f(q.z * z);
  const xy = f(q.x * y), xz = f(q.x * z), yz = f(q.y * z);
  const wx = f(q.w * x), wy = f(q.w * y), wz = f(q.w * z);
  return new THREE.Vector3(
    f((1 - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z),
    f((xy + wz) * v.x + (1 - (xx + zz)) * v.y + (yz - wx) * v.z),
    f((xz - wy) * v.x + (yz + wx) * v.y + (1 - (xx + yy)) * v.z)
  );
}


export function lerpUnityQuaternionFloat32(
  from: THREE.Quaternion,
  to: THREE.Quaternion,
  t: number
): THREE.Quaternion {
  const f = Math.fround;
  const amount = f(THREE.MathUtils.clamp(t, 0, 1));
  const fromX = f(from.x), fromY = f(from.y), fromZ = f(from.z), fromW = f(from.w);
  let toX = f(to.x);
  let toY = f(to.y);
  let toZ = f(to.z);
  let toW = f(to.w);
  const dot = f(f(f(f(fromX * toX) + f(fromY * toY)) + f(fromZ * toZ)) + f(fromW * toW));
  if (dot < 0) {
    toX = -toX;
    toY = -toY;
    toZ = -toZ;
    toW = -toW;
  }
  // Even t=1 evaluates the subtraction and addition in native Quaternion.Lerp;
  // returning/normalizing `to` directly loses the cancellation rounding.
  const x = f(fromX + f(f(toX - fromX) * amount));
  const y = f(fromY + f(f(toY - fromY) * amount));
  const z = f(fromZ + f(f(toZ - fromZ) * amount));
  const w = f(fromW + f(f(toW - fromW) * amount));
  const length = f(Math.sqrt(f(f(f(f(x * x) + f(y * y)) + f(z * z)) + f(w * w))));
  return length > 0
    ? new THREE.Quaternion(f(x / length), f(y / length), f(z / length), f(w / length))
    : new THREE.Quaternion();
}


function readFiniteNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}
