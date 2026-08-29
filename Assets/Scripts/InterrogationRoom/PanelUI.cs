using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //The four screens of the interrogation room are all the same shape - a dark card with a title, a
    //close button and a column of things to press - so the shape is built once here and each screen
    //fills it with its own contents. The same bargain the refill station makes with its panel: what is
    //wired up in the editor is used as it is, and what is left empty is put together from scratch, so
    //nothing has to exist in the scene for the room to work.
    public static class PanelUI
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.09f, 0.96f);
        public static readonly Color EntryColor = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color EntrySelectedColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        public static readonly Color TextColor = new Color(0.94f, 0.93f, 0.9f, 1f);
        public static readonly Color DimTextColor = new Color(0.94f, 0.93f, 0.9f, 0.55f);
        public static readonly Color HighlightColor = new Color(1f, 0.85f, 0.4f, 1f);

        //A screen the player opens over the room: dark card, centred, sized as a fraction of whatever
        //canvas it is dropped into so it sits the same at any resolution.
        public static GameObject CreatePanel(Transform parent, string name, Vector2 anchorSize)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            panel.layer = parent != null ? parent.gameObject.layer : panel.layer;
            Vector2 margin = (Vector2.one - anchorSize) * 0.5f;
            rect.anchorMin = margin;
            rect.anchorMax = Vector2.one - margin;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image background = panel.GetComponent<Image>();
            background.color = PanelColor;
            //the card eats clicks, so the room behind it cannot be poked through an open screen
            background.raycastTarget = true;
            return panel;
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            label.layer = parent != null ? parent.gameObject.layer : label.layer;
            TextMeshProUGUI textMesh = label.GetComponent<TextMeshProUGUI>();
            textMesh.text = text;
            textMesh.fontSize = fontSize;
            textMesh.alignment = alignment;
            textMesh.color = TextColor;
            textMesh.raycastTarget = false;
            textMesh.textWrappingMode = TextWrappingModes.Normal;
            return textMesh;
        }

        //title across the top of a card, kept clear of the close button in the corner beside it
        public static TextMeshProUGUI CreateHeader(Transform parent, string title, float height)
        {
            TextMeshProUGUI header = CreateLabel(parent, "Title", title, 34f, TextAlignmentOptions.Left);
            RectTransform rect = (RectTransform)header.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(28f, -height);
            rect.offsetMax = new Vector2(-96f, 0f);
            return header;
        }

        public static Button CreateCloseButton(Transform parent, string label)
        {
            Button button = CreateButton(parent, "Close", label, 28f);
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.alignment = TextAlignmentOptions.Center;
            }
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -16f);
            rect.sizeDelta = new Vector2(56f, 56f);
            return button;
        }

        //One pressable row. Built rather than prefabbed so a screen can list however many suspects,
        //clues or templates the scene happens to hold without a prefab existing for each.
        public static Button CreateButton(Transform parent, string name, string label, float fontSize)
        {
            GameObject entry = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = (RectTransform)entry.transform;
            rect.SetParent(parent, false);
            entry.layer = parent != null ? parent.gameObject.layer : entry.layer;
            Image background = entry.GetComponent<Image>();
            background.color = EntryColor;

            TextMeshProUGUI text = CreateLabel(rect, "Label", label, fontSize, TextAlignmentOptions.Left);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);

            Button button = entry.GetComponent<Button>();
            button.targetGraphic = background;
            //the row tints its own faint background, so the multipliers are what read as hover and press
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.8f, 1.8f, 1.8f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            return button;
        }

        //The column a screen stacks its rows in, inset under the title, and scrollable.
        //
        //It has to scroll: the notebook lists every clue ever written and the case file pages are as
        //long as whatever was typed into them, so a fixed column would quietly hide the bottom of both
        //with nothing on screen to say it had. Returns the content the rows go into, not the frame.
        public static RectTransform CreateColumn(Transform parent, string name, float headerHeight, float spacing)
        {
            GameObject viewport = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.SetParent(parent, false);
            viewport.layer = parent != null ? parent.gameObject.layer : viewport.layer;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(28f, 28f);
            viewportRect.offsetMax = new Vector2(-28f, -headerHeight);

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewportRect, false);
            content.layer = viewport.layer;
            //pinned to the top and grown downwards, which is the direction a list of rows reads in
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            //the column is as tall as what is in it, which is what gives the scroll something to move
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            //the frame masks itself, so it doubles as the viewport rather than needing a third object
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return contentRect;
        }

        //rows are laid out by the column, so their height is asked for rather than set
        public static void SetEntryHeight(Component entry, float height)
        {
            LayoutElement element = entry.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = entry.gameObject.AddComponent<LayoutElement>();
            }
            element.minHeight = height;
            element.preferredHeight = height;
        }

        //The little name that rides next to the cursor. It is a pill that hugs whatever is written in it
        //rather than a fixed box, so a short name does not sit in a wide empty plate, and neither it nor
        //its text takes raycasts - a label that ate the cursor would end the hover that summoned it the
        //moment it appeared. Returns the text; the pill is its parent.
        public static TextMeshProUGUI CreateHoverLabel(Transform parent, string name)
        {
            GameObject pill = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform rect = (RectTransform)pill.transform;
            rect.SetParent(parent, false);
            pill.layer = parent != null ? parent.gameObject.layer : pill.layer;
            //anchored to the middle of the canvas and grown up and to the right of wherever it is put,
            //which is what makes the anchored position the cursor's own corner of it
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;

            Image background = pill.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);
            background.raycastTarget = false;

            HorizontalLayoutGroup layout = pill.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = pill.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI text = CreateLabel(rect, "Text", string.Empty, 24f, TextAlignmentOptions.Left);
            //the pill is sized from this, so it must not wrap: a wrapping name would be measured against
            //a width that does not exist yet and collapse the pill to a sliver
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                //Destroy does not take effect until the end of the frame, and the rows for the new
                //contents are added immediately after this returns. Left parented, the old rows would
                //be laid out alongside the new ones for a frame, which reads as the screen flinching
                //every time anything is pressed. Unparenting first takes them out of the layout now and
                //leaves only the cleanup to happen later.
                Transform child = parent.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }
    }
}
