# PJSK Character Shading Evidence Boundary

Updated: 2026-07-25

This document records what the Engine may reproduce as official behavior. It
separates CostumeShop runtime evidence from Clausekai's public 3DMV scene so
scene-specific values are never mixed.

## Sources

- JP 6.6.2 coherent CostumeShop Frida frame:
  `pjsk-frida-662-costume-frame-lighting-20260724-round9.jsonl`
- Final JP 6.6.2 driver capture:
  `/data/xy/pjsk-662-reverse-evidence-final-20260724.tar.zst`
  (SHA-256 `52788e890fff37ebd72b07a64ed3e92978e6a401da41a65fcbe673df70a011a2`)
- JP 6.5.5/6.6.2 IDA and metadata notes in
  `pjsk-662-reverse-evidence-20260724`
- Clausekai WebGL Toon-v3 shader capture:
  `clausekai-preview-20260718/analysis/webgl-shaders`
- Fresh 2026-07-24 Clausekai material/outline capture:
  `/tmp/clausekai-full-capture-20260724`
- Exported runtime material properties and role catalogs from the mounted
  five-region package.

Clausekai provides shader formulas and render-pass behavior. Its MV0110 light
values are not CostumeShop defaults.

## Implemented official path

The body Toon and face/hair Toon-v3 paths share these character-color stages:

1. Generate the common C/S/H coordinate from `_MainTex_ST`, sample FaceSDF
   directly from UV1, and restore Gamma-space character values after the GPU's
   sRGB decode.
2. Apply the exact `_UseLambert` and `_UseValueTex` base-band formula.
3. Apply the exact FadeOut/Spread `_ShadowWidth` band.
4. Merge FaceSDF with the base band only on FaceSDF-capable face materials.
5. Reconstruct the three-band skin color from the shaded C/S red channel and
   use `H.r >= 0.5` as the official binary skin selector.
6. Apply global shadow color to the normal shadow branch and to the two shadow
   skin colors exactly where the GPU variant does.
7. Apply the captured rim and specular formulas using H.a and vertex color G.
8. Apply character ambient and parts ambient.
9. Render the official material outline shell using vertex color R and the
   captured global outline blend.

The coherent CostumeShop world-space light vectors are used directly after
the Engine's mirrored-X coordinate conversion. Reconstructing them from the
child light's local Euler rotation is incorrect because the light lives below
`CameraRoot`.

The post-combine CostumeShop material capture is authoritative:

```text
Shader globals: _FinalSat=0, _Brightness=0, _HighlightRolloff=0
Active face/body/hair/accessory materials: all three properties absent
```

The Engine therefore has no final saturation/brightness/highlight-rolloff
stage. The old `0.95 / 1 / 0.8` values came from Clausekai's 3DMV draw and
were removed rather than translated into zero-valued uniforms, because
executing that unrelated formula with zero values would produce another
incorrect image.

The final fragment programs also remove the old inferred skin mask and
shadow-strength ramp. The official operation is:

```text
skinValue = mix(C.r, mix(C.r, S.r, _ShadowTexWeight), shadowBand)
mid        = globalShadow * shadow1Skin
dark       = globalShadow * shadow2Skin
upper      = mix(mid, defaultSkin, saturate(2 * skinValue - 1))
skin       = mix(dark, upper, saturate(2 * skinValue))
color      = H.r >= 0.5 ? HSVC(skin) : normalShadedColor
```

The driver-final Toon-v3 hair vertex variant also confirms that hair shadow is
a lighting-normal operation, not an extra texture or projected decal:

```text
radial = normalize(worldPosition - _HeadPosition)
lightingNormal = normalize(
  worldNormal + _HeadNormalBlend * (radial - worldNormal)
)
```

`_HAIR_SHADOW` gates this branch. `_HeadNormalBlend` is a serialized material
property; the structurally validated Base UBO contains `0.7`.
`_HeadPosition` remains the animated `Head.TransformPoint(controller.offset)`
value. The outline pass continues to use its ordinary or packed second normal
and does not inherit this base-pass normal replacement.

## Exact outline state

The final CostumeShop Vulkan capture confirms:

```text
_SekaiOutlineWidth = (0.0004, 0.0095)
_OutlineColor = (0.52, 0.47, 0.55, 1)
_SekaiCharacterOutlineColorArray[active] = (0, 0, 0, 1)
_SekaiCharacterOutlineBlendingArray[active] = 0.5
```

The main outline pass extrudes along the normal using vertex color R. The hair
second-normal pass reconstructs its alternate normal from exported UV data.
The purple-gray material `_OutlineColor` is preserved as exported data, but it
is not used as a flat-shell fallback for character materials. The driver-final
outline fragment still samples and shades C/S/H before applying the active
formation's global outline color/blending. The Engine therefore reuses the
source Toon fragment/uniforms for body, face, hair and accessory outline
passes; a solid purple-gray `MeshBasicMaterial` would be the wrong path.

The final global-outline blend is a Gamma-space operation. The game runs a
Unity Gamma pipeline (the CostumeShop preview renders into a 1024x1024
Gamma-space target), and the driver-final `0090` fragment computes
`mix(outlineColorArray[i].rgb * a, shadedColor, blendingArray[i])` directly on
the gamma-form values with no EOTF anywhere in the pass. The browser Toon
kernel keeps its intermediate character colors in the same sRGB-shaped domain,
so the outline hook blends that shaded result as-is with the controller color.
Pixel evidence (2026-07-27): official reference hair/skin boundary lines
measure ~(84,54,46) ≈ gamma `mix(black, shadow1(176,102,83), 0.5)` = (88,51,41),
while a linear-light mix predicts (128,73,59) — a full tier brighter than the
official preview. An earlier "dirty blue-gray" appearance once attributed to
Gamma-space mixing was actually the FLIP_SIDED ramp inversion (shell ramp
normals negated), fixed separately; with the ramp fixed, Gamma-space mixing
reproduces the official line colors to within ±1/255. Clausekai also contains
a simpler `MainTex * _OutlineColor` outline fragment; that is evidence for its
captured scene/path, not a replacement for the CostumeShop Toon outline
variant identified by `0089/0090/0091`.

The captured variants are:

```text
0082.vert + 0083.frag = Toon base
0082.vert + 0084.frag = Toon-v3 FaceSDF
0089.vert + 0090.frag = outline with mesh normal
0091.vert + 0090.frag = outline with reconstructed second normal
```

Both outline variants use continuous `COLOR.r` width scaling. `_OutlineOffset`
is a separate clip-space displacement scaled by `COLOR.b`:

```text
clipPosition += projectedCameraOrigin * (-0.01 * _OutlineOffset) * COLOR.b
```

`COLOR.g` is independent of the shell: it masks the fully evaluated Rim RGB
contribution in the Toon base/outline fragment. The captured renderer/material
inventory enables Outline for face skin, eyebrow and eyelash, but not for eye
or eye highlight. Browser eyebrow/eyelash shells therefore use their own
textures and raw values with the Toon-v3 outline fragment instead of reusing
their transparent base-layer shader.

The effective fixed state is front-face culling, depth write on, blending off,
and ShaderLab `Less` (Vulkan reversed-Z `Greater`). The browser shell uses the
matching Three.js state.

## Exported-material coverage audit

A 2026-07-24 scan decoded all 18,327 JP source-part packages and all 38,893
material slots without errors. The Toon-v3 inputs present in those materials
are covered as follows:

- C/S/H textures, `_ShadowTexWeight`, `_ShadowWidth`, `_FadeMode`,
  `_UseLambert`, `_UseFaceSDF`, and `_HAIR_SHADOW` feed the shared character
  shading path.
- `_HueSinAngle`, `_HueCosAngle`, `_Saturation`, `_Value`, and `_Contrast`
  feed the exact material HSVC path.
- `_DefaultSkinColor`, `_Shadow1SkinColor`, `_Shadow2SkinColor`, and
  `_PartsAmbientColor` are preserved. `CharacterModel.SetupSkin` is mirrored
  by applying the role catalog's three master colors after composition. The
  H texture selects skin pixels; material-name heuristics do not.
- `_SpecularPower`, `_RimThreshold`, `_OutlineWidth`, `_OutlineOffset`,
  `_OutlineL`, and `_UseOutlineSecondNormal` are preserved. Only the
  evidence-backed operations are enabled.
- `_FaceShadowTex`, `_RangeLimit`, `_HeadDotDirectionalLightValues`,
  `_HeadPosition`, and `_HeadNormalBlend` are preserved for the face/hair
  runtime paths. FaceSDF
  selects the mirrored sample directly from the sign of the captured head-dot
  X value; the removed `_FaceSdfMirror` and `_FaceSdfBias` knobs were not
  present in the captured variant.
- Eye, eyelash, eyebrow, stencil, atlas, tint, emission, and distortion
  properties remain in their dedicated layer/pass implementations rather than
  being mixed into the body shader.

The scan found no serialized `_FaceSphereShadow*`, `_FinalSat`,
`_Brightness`, `_HighlightRolloff`, `_FaceSkinShadowStrength`,
`_SkinMaskMode`, or `_UseSkinColor` override. It also confirmed that
`_IsAccessory` is serialized and written by `CharacterModel`, but no use of
that flag survived in the captured Toon-v3 pixel/vertex programs. It therefore
remains metadata instead of driving an invented shader branch.

## Ground/contact shadow

`CharacterShadowController` creates one cross shadow and one directional
shadow for each of `Left_Toe` and `Right_Toe`.

- Static CostumeShop setup starts with cross shadows active and directional
  shadows inactive.
- Cross shadow floor offset is `0.015`, fade height is `0.2`.
- Directional shadow floor offset is `0.01`, fade height is `0.2`.
- The two modes are mutually exclusive.

The Engine follows that default and no longer invents a root/hip shadow when
either toe cannot be resolved.

## Deliberately disabled

- No synthetic neck, chin, jaw, or back-head ellipse.
- No forced Lambert keyword.
- No FaceSphere contribution unless real non-zero CostumeShop material
  properties are captured.
- No RCAS, FSR, SMAA, tone-mapping, or other post-process in the base kernel.
- No 3DMV fog, global spotlight, reflection cube, or MRT auxiliary output in
  the CostumeShop preview path.

`CharacterModel` does not expose a dedicated neck/chin shadow controller.
Those visible regions must come from the exported mesh normals, C/S/H/SDF
textures, skin palette, and the official Toon-v3 stages above. Missing evidence
must remain a documented gap rather than becoming a new heuristic.

## Remaining evidence gaps

- `_RimEmission` is bound as zero in the coherent CostumeShop frame, so its
  non-zero pixel formula is not needed by the current preview.
- Clausekai MV0110 binds `_FaceSphereShadowWeight=0`; the CostumeShop capture
  also contains no non-zero FaceSphere write. The branch formula is known but
  its intended non-zero material configuration is not.
- The validated 96-index Base/Outline UBO pair is intentionally not assigned
  to a Unity renderer/material name. This does not block the shared formulas,
  fixed states, matrices, light values, or outline implementation.
- Vulkan writes two auxiliary GBuffer targets in addition to color. The
  browser preview consumes only the final color result, so reproducing those
  unused attachments is outside the Engine kernel.
