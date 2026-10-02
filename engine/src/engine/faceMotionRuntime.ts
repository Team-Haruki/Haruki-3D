import * as THREE from "three";
import type { HeadAssetManifest } from "../data/sampleScene";

export type FaceMotionKeyframe = {
  time: number;
  value: number;
};

export type FaceMotionCurve = {
  curveHash: number;
  keyframes: FaceMotionKeyframe[];
};

export type FaceMotionClip = {
  name: string;
  sampleRate: number;
  duration: number;
  curves: FaceMotionCurve[];
};

export type FaceMotionSet = {
  bundlePath?: string;
  clips: FaceMotionClip[];
};

export type FaceMotionPlaybackSnapshot = {
  activeClipName: string | null;
  queuedLoopClipName: string | null;
  error: string | null;
  currentTime: number;
  mappedMeshCount: number;
  mappedCurveCount: number;
};

export type RuntimeHeadMorphDebug = {
  meshName: string;
  morphTargetCount: number;
  mappedChannelCount: number;
  sampleChannels: string[];
};

type HeadMorphRuntime = {
  mesh: THREE.Mesh;
  curveIndexByHash: Map<number, number>;
  controlledIndices: number[];
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? value as Record<string, unknown> : {};
}

function isMorphMesh(node: THREE.Object3D): node is THREE.Mesh {
  const mesh = node as THREE.Mesh;
  return !!mesh.isMesh && Array.isArray(mesh.morphTargetInfluences);
}

function unityMorphNameHash(name: string) {
  let crc = 0xffffffff;
  for (const byte of new TextEncoder().encode(name)) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
  }
  return (~crc) >>> 0;
}

function sampleFaceCurve(keyframes: FaceMotionKeyframe[], time: number) {
  if (!keyframes.length) {
    return 0;
  }
  if (time <= keyframes[0].time) {
    return keyframes[0].value;
  }
  for (let index = 1; index < keyframes.length; index += 1) {
    const previous = keyframes[index - 1];
    const next = keyframes[index];
    if (time > next.time) {
      continue;
    }
    const span = next.time - previous.time;
    if (span <= 1e-6) {
      return next.value;
    }
    const phase = (time - previous.time) / span;
    return previous.value + (next.value - previous.value) * phase;
  }
  return keyframes[keyframes.length - 1].value;
}

export function readEmbeddedRuntimeFaceMotion(extension: unknown): FaceMotionSet | null {
  const motionPackage = asRecord(
    asRecord(extension).motionPackage ?? asRecord(extension).MotionPackage
  );
  const faceMotion = motionPackage.faceMotion ?? motionPackage.FaceMotion;
  return faceMotion ? faceMotion as FaceMotionSet : null;
}

export class FaceMotionRuntime {
  private motionSet: FaceMotionSet | null = null;
  private clip: FaceMotionClip | null = null;
  private loopClip: FaceMotionClip | null = null;
  private time = 0;
  private error: string | null = null;
  private enabled = true;
  private readonly bindings: HeadMorphRuntime[] = [];

  bind(
    root: THREE.Object3D,
    headAsset: Pick<HeadAssetManifest, "morphChannels" | "morphChannelBindings">
  ): RuntimeHeadMorphDebug[] {
    const manifestChannels = headAsset.morphChannels ?? [];
    const channelBindings = headAsset.morphChannelBindings ?? [];
    const debug: RuntimeHeadMorphDebug[] = [];
    this.bindings.length = 0;

    root.traverse((node) => {
      if (
        node.userData.pjskOutlineShell ||
        node.userData.pjskEyeThroughHairOverlay ||
        node.userData.pjskEyeThroughHairStencilPrepass ||
        !isMorphMesh(node)
      ) {
        return;
      }
      const mesh = node;
      const morphTargetCount = mesh.morphTargetInfluences?.length ?? 0;
      if (!morphTargetCount) {
        return;
      }
      if (
        (!mesh.morphTargetDictionary || !Object.keys(mesh.morphTargetDictionary).length) &&
        manifestChannels.length === morphTargetCount
      ) {
        mesh.morphTargetDictionary = Object.fromEntries(
          manifestChannels.map((channel, index) => [channel, index])
        );
      }

      const dictionary = mesh.morphTargetDictionary ?? {};
      const curveIndexByHash = new Map<number, number>();
      const controlledIndices: number[] = [];
      for (const binding of channelBindings) {
        const index = dictionary[binding.name];
        if (typeof index !== "number") {
          continue;
        }
        curveIndexByHash.set(binding.curveHash, index);
        controlledIndices.push(index);
      }

      // Unity's built blend-shape binding is CRC32(channel name). Older part
      // packages wrote FNV(blendShape.name); keep those aliases while binding
      // original clips by the mesh's actual channel names.
      for (const [name, index] of Object.entries(dictionary)) {
        curveIndexByHash.set(unityMorphNameHash(name), index);
        controlledIndices.push(index);
      }

      mesh.morphTargetInfluences?.fill(0);
      this.bindings.push({
        mesh,
        curveIndexByHash,
        controlledIndices: [...new Set(controlledIndices)],
      });
      const channelNames = Object.entries(dictionary)
        .sort((left, right) => left[1] - right[1])
        .map(([name]) => name);
      debug.push({
        meshName: mesh.name,
        morphTargetCount,
        mappedChannelCount: new Set(curveIndexByHash.values()).size,
        sampleChannels: channelNames.slice(0, 12),
      });
    });
    return debug;
  }

  setMotion(
    data: FaceMotionSet | null,
    preferredClipName: string | null,
    preferredLoopClipName: string | null
  ) {
    this.motionSet = data;
    this.error = null;
    this.time = 0;
    this.clip = null;
    this.loopClip = null;
    if (!data?.clips.length) {
      this.clearInfluences();
      return;
    }
    const primaryCandidates = preferredClipName
      ? data.clips.filter((candidate) => candidate.name === preferredClipName)
      : data.clips;
    if (primaryCandidates.length !== 1) {
      throw new Error(
        `Expected exactly one face motion '${preferredClipName ?? "<only clip>"}', ` +
        `found ${primaryCandidates.length}.`
      );
    }
    this.clip = primaryCandidates[0]!;
    if (preferredLoopClipName && preferredLoopClipName !== this.clip.name) {
      const loopCandidates = data.clips.filter(
        (candidate) => candidate.name === preferredLoopClipName
      );
      if (loopCandidates.length !== 1) {
        throw new Error(
          `Expected exactly one face loop '${preferredLoopClipName}', ` +
          `found ${loopCandidates.length}.`
        );
      }
      this.loopClip = loopCandidates[0]!;
    }
    this.applyCurrent();
  }

  hasMotion() {
    return this.motionSet !== null;
  }

  isEnabled() {
    return this.enabled;
  }

  setEnabled(enabled: boolean) {
    this.enabled = enabled;
    if (enabled) {
      this.applyCurrent();
    } else {
      this.clearInfluences();
    }
  }

  step(delta: number, speed: number, paused = false) {
    if (paused || !this.enabled || !this.clip || this.bindings.length === 0) {
      return;
    }
    this.time += delta * speed;
    const duration = this.clip.duration;
    if (duration > 0 && this.time > duration) {
      if (this.loopClip) {
        const loopTime = this.time - duration;
        this.clip = this.loopClip;
        this.loopClip = null;
        this.time = this.clip.duration > 0 ? loopTime % this.clip.duration : 0;
      } else {
        this.time %= duration;
      }
    }
    this.applyCurrent();
  }

  seek(time: number) {
    this.time = time;
    this.applyCurrent();
  }

  promoteLoop() {
    if (!this.loopClip) {
      return;
    }
    this.clip = this.loopClip;
    this.loopClip = null;
    this.time = 0;
    this.applyCurrent();
  }

  applyCurrent() {
    if (!this.enabled || !this.clip) {
      return;
    }
    for (const binding of this.bindings) {
      const influences = binding.mesh.morphTargetInfluences;
      if (!influences) {
        continue;
      }
      for (const index of binding.controlledIndices) {
        influences[index] = 0;
      }
      for (const curve of this.clip.curves) {
        const index = binding.curveIndexByHash.get(curve.curveHash);
        if (index !== undefined) {
          influences[index] = sampleFaceCurve(curve.keyframes, this.time) / 100;
        }
      }
    }
  }

  release(options: { preserveMotion?: boolean } = {}) {
    this.clearInfluences();
    this.bindings.length = 0;
    if (!options.preserveMotion) {
      this.setMotion(null, null, null);
    }
  }

  getSnapshot(): FaceMotionPlaybackSnapshot {
    return {
      activeClipName: this.clip?.name ?? null,
      queuedLoopClipName: this.loopClip?.name ?? null,
      error: this.error,
      currentTime: this.time,
      mappedMeshCount: this.bindings.length,
      mappedCurveCount: this.bindings.reduce(
        (sum, binding) => sum + new Set(binding.curveIndexByHash.values()).size,
        0
      ),
    };
  }

  private clearInfluences() {
    for (const binding of this.bindings) {
      const influences = binding.mesh.morphTargetInfluences;
      if (!influences) {
        continue;
      }
      for (const index of binding.controlledIndices) {
        influences[index] = 0;
      }
    }
  }
}
