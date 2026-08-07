# Domain Context

## Runtime Module

A browser rendering context with its own lifecycle and presentation policy.
Runtime modules may share Base capabilities, but they do not inherit each
other's camera, lighting, scene, or interaction rules.

## Base

The context-independent character runtime. It owns converted-package loading,
body/head assembly, animation, constraints, material binding, SpringBone, and
the common browser playback lifecycle. Base does not decide how a character is
framed or how an MV scene is directed.

## Costume Shop

The single-character costume-preview context. It applies the official
CostumeShop camera, character height rate, lighting, outline, post-processing,
and capture behavior on top of Base.

## MV

The 3DMV context. It is the original Unity WebGL/WASM player and owns MV scenes,
formations, timelines, cameras, lights, and live-specific runtime behavior. MV
does not run through the Costume Shop renderer.

## Render Recipe

A complete role-scoped body, head, hair, and optional-head selection consumed
by the Costume Shop character runtime. It is not an MV formation or scene
description.

## Invariants

- Base never depends on Costume Shop or MV.
- Costume Shop and MV may depend on Base, but never on each other.
- A caller chooses one runtime module explicitly; presentation rules are not
  selected by hidden flags inside Base.
- The default package entry remains the Costume Shop kernel for compatibility.
