using UnityEngine;
using UnityEngine.UI;

//The countdown ring that sits on a conversation panel whenever the game is making the player wait: the
//greeting that holds back the serve prompt, the last word after serving, the beat the finished board is
//held for. A panel of text with nothing moving on it reads as the game having hung, and a ring that
//empties says "this is going somewhere" without adding another line to read.
//Built in code, sprite and all, so a timer needs nothing wired up in the scene to appear, and shared
//from here so the two screens that show one cannot drift apart.
public static class ConversationTimerRing
{
    private static readonly Color DefaultColour = new Color(1f, 0.85f, 0.4f, 0.95f);
    private const float DefaultSize = 54f;
    //one texture, shared by every ring that ever needs it
    private static Sprite sprite;

    //Centred on the panel's top left corner: anchored to the corner, and pivoted from its own middle, so
    //the corner lands in the middle of the ring rather than at its edge.
    public static Image Create(GameObject panel, string name)
    {
        if (panel == null)
        {
            return null;
        }
        if (sprite == null)
        {
            sprite = BuildSprite();
        }
        GameObject ring = new GameObject(name, typeof(RectTransform), typeof(Image));
        ring.layer = panel.layer;
        RectTransform rect = (RectTransform)ring.transform;
        rect.SetParent(panel.transform, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = ring.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        //a radial wipe from the top, so what is left of the ring is what is left of the wait
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        ring.SetActive(false);
        return image;
    }

    //Size, placement and colour come straight off inspector fields, and a field added to a component
    //already sitting in a scene can come back as zero rather than as the value written on it. A
    //zero-sized ring and an all-zero colour are both invisible ones, so each falls back rather than
    //vanishing; anything actually set in the inspector is used as it stands.
    public static void ApplyStyle(Image ring, float size, Vector2 nudge, Color colour)
    {
        if (ring == null)
        {
            return;
        }
        float side = size > 0f ? size : DefaultSize;
        ring.rectTransform.sizeDelta = new Vector2(side, side);
        //zero is the corner itself, which is where it is meant to sit
        ring.rectTransform.anchoredPosition = nudge;
        bool unset = colour.r == 0f && colour.g == 0f && colour.b == 0f && colour.a == 0f;
        ring.color = unset ? DefaultColour : colour;
    }

    //Shows what is left of the wait, and takes the ring off screen outright when there is none, so it
    //never sits there full and still implying a wait that is not running.
    public static void ShowRemaining(Image ring, float remaining, float duration)
    {
        if (ring == null)
        {
            return;
        }
        bool waiting = remaining > 0f && duration > 0f;
        if (ring.gameObject.activeSelf != waiting)
        {
            ring.gameObject.SetActive(waiting);
        }
        if (waiting)
        {
            ring.fillAmount = Mathf.Clamp01(remaining / duration);
        }
    }

    //Drawn here rather than pulled from an asset, so no sprite has to exist or be assigned for a timer
    //to show up. The band is faded across a pixel at both edges to keep it from coming out jagged.
    private static Sprite BuildSprite()
    {
        const int size = 128;
        const float outerRadius = 0.5f;
        const float innerRadius = 0.34f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        float centre = (size - 1) * 0.5f;
        float edge = 1.5f / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / size;
                float dy = (y - centre) / size;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((outerRadius - distance) / edge)
                            * Mathf.Clamp01((distance - innerRadius) / edge);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
