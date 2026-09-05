using System.Collections.Generic;
using UnityEngine;

// A light for the "Simple" lighting mode of the GameJam/2D Drop Shadow shader.
//
// This is not a URP Light2D and does not need the 2D lighting pipeline: every
// enabled instance is pushed into global shader arrays, and the shader shades each
// pixel from them in world space. That makes it work with a perspective camera,
// alongside 3D geometry, and on Canvas UI - none of which URP's Light2D handles.
//
// Drop the "Simple Light 2D" prefab into the scene and move it around.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SimpleLight2D : MonoBehaviour
{
    // Must match MAX_SIMPLE_LIGHTS in DropShadow2D.shader.
    public const int MaxLights = 8;

    [SerializeField] private Color color = new Color(1f, 0.93f, 0.78f, 1f);
    [SerializeField] private float intensity = 1.5f;
    [Tooltip("World units. Nothing past this distance is lit by this light.")]
    [SerializeField] private float range = 20f;
    [Tooltip("Higher values make a tighter pool of light. 1 is a straight linear falloff.")]
    [SerializeField, Range(0.25f, 8f)] private float falloff = 2f;

    private static readonly List<SimpleLight2D> Lights = new List<SimpleLight2D>();
    private static readonly Vector4[] Positions = new Vector4[MaxLights];
    private static readonly Vector4[] Colors = new Vector4[MaxLights];
    private static readonly int PositionsId = Shader.PropertyToID("_SimpleLightPositions");
    private static readonly int ColorsId = Shader.PropertyToID("_SimpleLightColors");
    private static readonly int CountId = Shader.PropertyToID("_SimpleLightCount");
    private static int lastPushedFrame = -1;

    public float Range => range;
    public float Intensity => intensity;

    private void OnEnable()
    {
        if (!Lights.Contains(this))
        {
            Lights.Add(this);
        }

        lastPushedFrame = -1;
    }

    private void OnDisable()
    {
        Lights.Remove(this);
        lastPushedFrame = -1;

        // Push straight away: if that was the last light, no LateUpdate will run
        // again to clear the arrays.
        Push();
    }

    private void OnValidate()
    {
        lastPushedFrame = -1;
    }

    private void LateUpdate()
    {
        // One push per frame covers every light, whichever instance gets there first.
        if (Application.isPlaying && lastPushedFrame == Time.frameCount)
        {
            return;
        }

        lastPushedFrame = Time.frameCount;
        Push();
    }

    /// <summary>Nearest enabled light to a point, or null when there are none.</summary>
    public static SimpleLight2D FindNearest(Vector3 position)
    {
        SimpleLight2D nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < Lights.Count; i++)
        {
            SimpleLight2D light = Lights[i];
            if (light == null)
            {
                continue;
            }

            float distance = (light.transform.position - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = light;
            }
        }

        return nearest;
    }

    private static void Push()
    {
        int count = Mathf.Min(Lights.Count, MaxLights);

        for (int i = 0; i < MaxLights; i++)
        {
            if (i < count && Lights[i] != null)
            {
                SimpleLight2D light = Lights[i];
                Vector3 position = light.transform.position;
                Color scaled = light.color * light.intensity;

                Positions[i] = new Vector4(position.x, position.y, position.z, Mathf.Max(light.range, 0.0001f));
                Colors[i] = new Vector4(scaled.r, scaled.g, scaled.b, light.falloff);
            }
            else
            {
                Positions[i] = Vector4.zero;
                Colors[i] = Vector4.zero;
            }
        }

        Shader.SetGlobalVectorArray(PositionsId, Positions);
        Shader.SetGlobalVectorArray(ColorsId, Colors);
        Shader.SetGlobalInt(CountId, count);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
