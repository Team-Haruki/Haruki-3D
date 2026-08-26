# ADR 0001: Separate Base, Costume Shop, and MV runtimes

## Status

Accepted.

## Context

The existing browser engine grew from CostumeShop capture work. Shared
character assembly and playback behavior sat beside CostumeShop camera,
height-rate, lighting, outline, and capture rules. 3DMV, however, is delivered
as the original Unity WebGL/WASM project and has different scene, formation,
timeline, camera, and lighting ownership.

Treating both contexts as modes of one Three.js renderer would make Base depend
on presentation flags and would invite CostumeShop assumptions into MV.

## Decision

Expose three explicit package modules:

- `base` for context-independent package loading, assembly, animation,
  constraints, SpringBone, and browser lifecycle behavior;
- `costume_shop` for the current single-character Three.js preview policy;
- `mv` for hosting one original Unity WebGL/WASM instance and forwarding calls
  to its public bridge.

The default package entry remains an alias of `costume_shop`. Existing capture
internals remain compatible while new consumers can select an explicit module.

## Consequences

- CostumeShop and MV can evolve without a growing matrix of mode flags.
- Base assembly is reusable and cannot choose presentation policy.
- MV integration requires the official Unity loader and build artifacts from
  the host; this package does not translate MV behavior into Three.js.
- Existing default imports keep their current behavior.
