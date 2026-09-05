using UnityEngine;
using UnityEngine.UI;

// Optional driver for the "GameJam/2D Drop Shadow" shader. Put it on a
// SpriteRenderer (or a UI Image) that uses one of the drop shadow materials and
// the shadow direction is computed from a light source instead of being baked
// into the material.
//
// Sprites are driven through a MaterialPropertyBlock, so every object can have
// its own shadow while still sharing one material. UI elements get their own
// material instance at runtime (CanvasRenderer does not support property blocks).
[ExecuteAlways]
[DisallowMultipleComponent]
public class DropShadow2D : MonoBehaviour
{
    [Header("Direction")]
    [Tooltip("Optional. The shadow falls away from this transform. Leave empty to fall back to the nearest Simple Light 2D, then to Light Angle.")]
    [SerializeField] private Transform lightSource;
    [Tooltip("With no Light Source set, take the direction from the nearest SimpleLight2D in the scene, so shading and shadow agree.")]
    [SerializeField] private bool useNearestSimpleLight = true;
    [Tooltip("Degrees. Where the light sits when there is no Light Source: 90 = straight above, 135 = upper left.")]
    [SerializeField] private float lightAngle = 115f;
    [Tooltip("How far the shadow is pushed away, in world units (sprites) or canvas units (UI).")]
    [SerializeField] private float distance = 0.5f;

    [Header("Shape")]
    [Tooltip("Vertical squash. 1 = a plain drop shadow, ~0.4 = a shadow lying on the floor.")]
    [SerializeField, Range(0.01f, 2f)] private float squash = 0.45f;
    [Tooltip("Squash the shadow around the bottom of the sprite. Works out the value from the sprite bounds, so it is right whatever the pivot is.")]
    [SerializeField] private bool anchorToSpriteBottom = true;
    [Tooltip("Local Y the shadow is squashed around, used when Anchor To Sprite Bottom is off.")]
    [SerializeField] private float anchorY = 0f;
    [Tooltip("Lean the shadow away from the light. 0 disables leaning.")]
    [SerializeField, Range(0f, 3f)] private float skewAmount = 1f;

    [Header("Look")]
    [SerializeField] private Color shadowColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float strength = 0.45f;
    [SerializeField, Range(0f, 16f)] private float softness = 4f;

    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    private static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");
    private static readonly int ShadowScaleId = Shader.PropertyToID("_ShadowScale");
    private static readonly int ShadowSkewId = Shader.PropertyToID("_ShadowSkew");
    private static readonly int ShadowAnchorYId = Shader.PropertyToID("_ShadowAnchorY");
    private static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");

    private SpriteRenderer spriteRenderer;
    private Graphic graphic;
    private MaterialPropertyBlock block;
    private Material uiMaterial;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        graphic = GetComponent<Graphic>();
        Apply();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            Apply();
        }
    }

    private void LateUpdate()
    {
        // Only worth re-applying every frame while a light can move.
        if (lightSource != null || useNearestSimpleLight)
        {
            Apply();
        }
    }

    private void OnDestroy()
    {
        if (uiMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(uiMaterial);
            }
            else
            {
                DestroyImmediate(uiMaterial);
            }
        }
    }

    /// <summary>Pushes the current settings into the renderer. Safe to call from other scripts.</summary>
    public void Apply()
    {
        Transform source = lightSource;
        if (source == null && useNearestSimpleLight)
        {
            SimpleLight2D nearest = SimpleLight2D.FindNearest(transform.position);
            if (nearest != null)
            {
                source = nearest.transform;
            }
        }

        Vector2 shadowDir;
        if (source != null)
        {
            shadowDir = (Vector2)(transform.position - source.position);
        }
        else
        {
            float radians = lightAngle * Mathf.Deg2Rad;
            shadowDir = -new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        shadowDir = shadowDir.sqrMagnitude > 1e-6f ? shadowDir.normalized : Vector2.down;

        // The shader displaces vertices in local space, so undo the object scale
        // to keep "distance" meaningful in world units.
        Vector3 scale = transform.lossyScale;
        Vector2 offset = shadowDir * distance;
        offset.x /= Mathf.Approximately(scale.x, 0f) ? 1f : scale.x;
        offset.y /= Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;

        float skew = Mathf.Clamp(shadowDir.x * skewAmount, -3f, 3f);

        float anchor = anchorY;
        if (anchorToSpriteBottom && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // Sprite bounds are in local units with the pivot already applied, so
            // this lands on the sprite's feet whatever the pivot is set to.
            anchor = spriteRenderer.sprite.bounds.min.y;
        }

        if (spriteRenderer != null)
        {
            block ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            Write(block, offset, skew, anchor);
            spriteRenderer.SetPropertyBlock(block);
        }
        else if (graphic != null && Application.isPlaying)
        {
            if (uiMaterial == null && graphic.material != null)
            {
                uiMaterial = new Material(graphic.material) { name = graphic.material.name + " (Shadow Instance)" };
                graphic.material = uiMaterial;
            }

            if (uiMaterial != null)
            {
                Write(uiMaterial, offset, skew, anchor);
            }
        }
    }

    private void Write(MaterialPropertyBlock target, Vector2 offset, float skew, float anchor)
    {
        target.SetColor(ShadowColorId, shadowColor);
        target.SetFloat(ShadowStrengthId, strength);
        target.SetVector(ShadowOffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
        target.SetVector(ShadowScaleId, new Vector4(1f, squash, 0f, 0f));
        target.SetFloat(ShadowSkewId, skew);
        target.SetFloat(ShadowAnchorYId, anchor);
        target.SetFloat(ShadowSoftnessId, softness);
    }

    private void Write(Material target, Vector2 offset, float skew, float anchor)
    {
        target.SetColor(ShadowColorId, shadowColor);
        target.SetFloat(ShadowStrengthId, strength);
        target.SetVector(ShadowOffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
        target.SetVector(ShadowScaleId, new Vector4(1f, squash, 0f, 0f));
        target.SetFloat(ShadowSkewId, skew);
        target.SetFloat(ShadowAnchorYId, anchor);
        target.SetFloat(ShadowSoftnessId, softness);
    }
}
