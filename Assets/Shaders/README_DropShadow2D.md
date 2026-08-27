# 2D Drop Shadow

`GameJam/2D Drop Shadow` — one shader that draws a soft shadow behind a sprite or a
UI element. Two ready-made materials live in `Assets/Materials/`.

## How it works

The shader has two passes. The first draws the sprite's silhouette with the vertices
displaced, squashed and skewed in local space, tinted with the shadow colour; the
second draws the sprite normally on top. Because the shadow is real geometry rather
than an offset texture lookup, it is never clipped by the sprite quad and can fall
far outside the sprite's bounds.

## Sprites

1. Select the `SpriteRenderer` and set its Material to `M_Sprite_DropShadow`.
2. `Sprite Renderer Mode` must stay **on** in that material — it is what makes the
   SpriteRenderer's Color tint and Flip X/Y reach the shader.
3. `Shadow Anchor Y` is the local Y the shadow is squashed towards. If the sprite's
   pivot is at the feet, leave it at 0. If the pivot is Center, set it to minus half
   the sprite height in world units.

### Already applied

`M_Sprite_DropShadow` is assigned to Chief, Pianist, Widow and Player Character. Those
four sprites are all 2124 px tall at 100 PPU with a Center pivot, so the material is
tuned to that: `Shadow Anchor Y = -10.62` (half of 21.24 units — the sprite's feet),
squashed to 35%, leaning left, 50% opacity. Any sprite of a different height needs its
own anchor value, or the `DropShadow2D` component below, which works it out per object.

## UI (Image / RawImage)

1. Select the `Image` and set its Material to `M_UI_DropShadow`.
2. Leave `Sprite Renderer Mode` **off**. `ZTest` is set to Always, which is what UI
   wants.
3. `Shadow Offset` is in canvas units here, not world units — 6 px, not 0.06.

`Mask`, `RectMask2D` and `CanvasGroup` alpha all work: the stencil and `_ClipRect`
plumbing from Unity's default UI shader is in both passes, and the shadow fades with
the graphic's own alpha.

TextMeshPro text is **not** covered — TMP has its own shader family, and it already
ships a Drop Shadow / Underlay setting on its materials.

## Which renderer this project uses

`UniversalRP.asset` now defaults to `UniversalRenderer3D.asset` (the Universal, 3D
renderer). `Renderer2D.asset` is still in the list at index 1, so setting
`Default Renderer` back to it returns the project to 2D rendering.

This matters to the shader because the two renderers draw different passes:

| Renderer | Passes it submits |
| --- | --- |
| Universal (3D) | `SRPDefaultUnlit`, `UniversalForward`, `UniversalForwardOnly` |
| 2D | `SRPDefaultUnlit`, `Universal2D` |

The shadow pass is `SRPDefaultUnlit`, which both renderers draw, and always at index 0
so the shadow lands behind the sprite either way. The sprite itself is drawn by a
`Universal2D` pass and an identical `UniversalForward` pass, so exactly one of them
runs under either renderer and the shader works in both.

The 3D renderer is what makes Directional Lights work on the room: URP/Lit does all of
its lighting in `UniversalForward`, and the 2D renderer never draws that pass - under
it, `Lit.shader` falls back to a `Universal2D` pass that returns base map times base
colour, no lighting at all. That is why the walls and floor looked fully lit no matter
what lights were in the scene.

Under the 3D renderer the `Lights2D` lighting mode renders black - there is no 2D
lighting pipeline to sample. Use `None` or `Simple`.

## Cast shadows (the real ones)

Under the 3D renderer the shader has a `ShadowCaster` pass, so the characters cast
genuine shadows onto the floor and walls from the Directional Light. The pass clips on
the sprite's alpha (`Cast Shadow Alpha Cutoff`, default 0.5), so the shadow is the shape
of the character, not of the quad.

Three things this depends on:

1. **The SpriteRenderer's Cast Shadows must be On.** It defaults to Off on every
   SpriteRenderer. Already set on the four character prefabs; any new sprite needs it.
2. **The light's angle matters more than usual.** These sprites are billboards tilted
   25 degrees, so they are nearly vertical planes. A light pointing straight down casts
   a thin sliver; a light at roughly 30-50 degrees elevation casts a long, readable,
   character-shaped shadow. Rotate the Directional Light, do not just move it.
3. **Shadow distance** is now 150 with 2 cascades in `UniversalRP.asset` (it was 50,
   which cut off well inside this room), and soft shadows are on.

The old fake drop shadow is still in the material but turned off - `Shadow Strength` is
0. Dial it back up a little if the real shadow reads as detached and you want some
contact darkening under the feet; the two stack fine.

Characters now receive shadows too, via the Scene3D lighting mode below - a character
standing in the room's shadow goes dark.

## Lighting

The `Lighting Mode` dropdown on the material picks one of four paths.

**None** — flat, unlit. What UI normally wants.

**Simple** — the shader's own lighting: ambient plus up to 8 point lights, evaluated
per pixel in world space. Drag `Assets/Prefabs/Lighting/Simple Light 2D.prefab` into the
scene and move it; every material in Simple mode picks it up through global shader
arrays, so one shared material still sees every light. Colour, intensity, range and
falloff are on the component, and the light draws a wire sphere when selected.

It works under either renderer and on Canvas UI, and with no lights placed sprites
render at the material's `Ambient` colour rather than going black. `DropShadow2D` finds
the nearest `SimpleLight2D` on its own, so the shadow direction and the shading come
from the same light without wiring anything up.

**Scene3D** — real URP lighting, and what the character material uses now. The sprite is
lit by the actual Directional Light and any Point/Spot lights in the scene, with the
ambient probe and shadow attenuation, so a character standing in the room's shadow goes
dark. Forward and Forward+ light loops are both handled; light cookies are not.

Two knobs matter here, because a sprite has no real surface to shade:

- `Sprite Roundness` bends the quad's normal across the sprite's width to fake a
  cylinder, so the character catches the light down one side instead of shading dead
  flat. 0 is a flat plane, 0.6 is a gentle roll.
- `Directional Shading` blends between flat (the sprite just takes the light's colour
  and its shadowing) and a full N·L off that faked normal.

This mode only works under the Universal (3D) renderer - the `UniversalForward` pass is
the only place URP's lighting exists. Under the 2D renderer it falls back to unlit.
It ignores `SimpleLight2D`; use real Unity lights instead.

**Lights2D** — the Unity-native path: the sprite samples URP's 2D light textures, so
real `Light2D` objects light it with proper falloff, colour and blend styles. Two things
to know before switching to it:

- **A scene with no `Light2D` renders every sprite in this mode pure black.** Drag in
  `Assets/Prefabs/Lighting/Global Light 2D.prefab` first.
- `Light2D` does not affect 3D meshes, so the floor, walls and table would stay flat
  while the characters react to the lights.

There is no `NormalsRendering` pass, so `Light2D`'s normal-map option does nothing here.
Shape-light mask filtering works through `Light Mask`.

The shadow pass is never lit, in any mode — a shadow that brightened near a lamp would
be wrong.

## Properties

| Property | What it does |
| --- | --- |
| `Shadow Color` / `Shadow Strength` | Colour and opacity of the shadow. |
| `Shadow Offset` | Displacement in local units (world units for sprites, canvas units for UI). |
| `Shadow Scale` | XY squash. `(1, 0.45)` reads as a shadow on the ground; `(1, 1)` as a flat drop shadow. |
| `Shadow Skew X` | Leans the shadow sideways, as if the light were off to one side. |
| `Shadow Anchor Y` | The local Y that the squash and skew pivot around. |
| `Shadow Softness` | Blur radius in texels. A 9-tap ring — keep it at 4 or below or the ring starts to show. |
| `Sprite UV Rect` | Only needed when the sprite is part of a sheet/atlas: set it to the sprite's UV rect so the blur cannot bleed in neighbouring sprites. |

## Driving the shadow from a light

`Assets/Scripts/Rendering/DropShadow2D.cs` is optional. Add it to a sprite and give it
a `Light Source` transform, and the shadow direction, lean and length are recomputed
each frame from that transform's position. With no light source it uses a fixed
`Light Angle`. `Anchor To Sprite Bottom` reads the sprite's bounds, so the shadow sits
at the feet whatever the pivot and whatever the sprite's height — that is the reason to
use the component rather than hand-tuning `Shadow Anchor Y` per material. On sprites it writes through a `MaterialPropertyBlock`, so one shared
material still gives every object its own shadow. On UI it only runs in Play mode
(a `CanvasRenderer` cannot take property blocks, so it clones the material).

## Notes

- The shadow's blur can only spread into the sprite quad, so leave a few transparent
  pixels of padding in the sprite if you want a very soft edge.
- The project uses the URP 2D Renderer with no `Light2D` in the scene, so this shader
  is unlit — it does not react to 2D lights. If you later add `Light2D`, real 2D
  shadows come from the `ShadowCaster2D` component plus a lit sprite material; this
  shader is the cheap stylised alternative and the only option for Canvas UI, which
  2D lights never touch.
