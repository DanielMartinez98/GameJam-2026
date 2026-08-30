using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //The moment on the way into a memory where the room goes dark and the detective is shown one picture
    //with a passage read underneath it. It is not a screen over the room the way the notebook is: it is
    //the whole view, held until the player presses to go on, and the memory only starts once they do.
    //
    //Everything it needs can be authored in the scene and wired in below, but none of it has to be. Left
    //empty, it builds its own full-screen overlay the first time it plays, so a memory can have a
    //prologue written on it without a single object being placed by hand.
    public class CutsceneDirector : MonoBehaviour
    {
        [Header("The screen")]
        //switched on for the length of the cutscene and off again the instant it is dismissed
        [SerializeField] private GameObject cutsceneRoot;
        //where the memory's picture goes. Kept to its own aspect rather than stretched to the frame, so
        //a tall portrait and a wide establishing shot both read as themselves.
        [SerializeField] private Image imageDisplay;
        //the prologue, read beneath the picture
        [SerializeField] private TextMeshProUGUI prologueLabel;
        //the small line telling the player how to move on
        [SerializeField] private TextMeshProUGUI continueHint;

        [Header("How it plays")]
        //A breath before the press counts, so a cutscene is never skipped by the same click that opened
        //it - the player's finger is often still down from picking the memory.
        [SerializeField] private float minReadSeconds = 0.4f;
        [SerializeField] private KeyCode advanceKey = KeyCode.Space;
        [SerializeField] private string continueHintText = "Click or press Space to continue";
        //what fills the screen behind the picture when the overlay is built here rather than authored
        [SerializeField] private Color backgroundColor = Color.black;
        //The font the built labels are set in. Left empty, the game's own MTO Comic is found and used so
        //the cutscene reads in the same hand as the rest of the interface rather than in TMP's default.
        [SerializeField] private TMP_FontAsset font;

        //what to do once the player presses on. Held for the length of the cutscene and cleared as it is
        //called, so a second press cannot fire the same entry twice.
        private Action onComplete;
        private float shownAt;
        private bool playing;
        //built the first time it is needed and kept, so a second cutscene reuses the same overlay rather
        //than stacking another one on the canvas
        private bool built;

        //The font the built labels read in. Set before the first Play to force a particular face - the
        //menu uses this to hand in MTO, which is not otherwise loaded when the cutscene builds itself.
        public TMP_FontAsset Font
        {
            get => font;
            set => font = value;
        }

        //Puts the picture and its prologue up over everything and holds there. onDone is called once the
        //player presses to go on - for a memory that is when the dining room is loaded.
        public void Play(Sprite image, string prologue, Action onDone)
        {
            EnsureBuilt();
            onComplete = onDone;
            if (imageDisplay != null)
            {
                imageDisplay.sprite = image;
                //a memory with words but no picture shows the words on the bare screen rather than an
                //empty frame where the picture would have been
                imageDisplay.enabled = image != null;
                imageDisplay.preserveAspect = true;
            }
            if (prologueLabel != null)
            {
                prologueLabel.text = prologue;
            }
            if (continueHint != null)
            {
                continueHint.text = continueHintText;
            }
            if (cutsceneRoot != null)
            {
                cutsceneRoot.SetActive(true);
                //drawn last so nothing else on the canvas is left showing through the darkened screen
                cutsceneRoot.transform.SetAsLastSibling();
            }
            shownAt = Time.unscaledTime;
            playing = true;
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }
            if (Time.unscaledTime - shownAt < minReadSeconds)
            {
                return;
            }
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(advanceKey))
            {
                Advance();
            }
        }

        private void Advance()
        {
            playing = false;
            if (cutsceneRoot != null)
            {
                cutsceneRoot.SetActive(false);
            }
            //cleared before it is called, so a callback that itself puts up another cutscene is not
            //undone by this one tidying up after it
            Action done = onComplete;
            onComplete = null;
            if (done != null)
            {
                done();
            }
        }

        //Only builds what is not already wired. A scene that hands over a root, an image and the two
        //labels gets exactly that; a scene that hands over nothing gets the whole overlay made here.
        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;
            if (cutsceneRoot != null)
            {
                //authored: switched off until the first play, like the room's own cards
                cutsceneRoot.SetActive(false);
                return;
            }
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            //A canvas of its own, above the room's, so the cutscene covers the whole view no matter what
            //else is on screen when it plays.
            GameObject canvasObject = new GameObject("Cutscene Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            cutsceneRoot = canvasObject;

            //the darkened screen everything else sits on
            Image background = NewStretchedImage("Background", canvasObject.transform);
            background.color = backgroundColor;

            //the picture, in the upper two-thirds
            GameObject imageObject = new GameObject("Image");
            imageObject.transform.SetParent(canvasObject.transform, false);
            imageDisplay = imageObject.AddComponent<Image>();
            imageDisplay.preserveAspect = true;
            RectTransform imageRect = imageDisplay.rectTransform;
            imageRect.anchorMin = new Vector2(0.15f, 0.4f);
            imageRect.anchorMax = new Vector2(0.85f, 0.93f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            //the prologue, read beneath it
            prologueLabel = NewLabel("Prologue", canvasObject.transform, 34f,
                TextAlignmentOptions.Top, ResolveFont());
            RectTransform prologueRect = prologueLabel.rectTransform;
            prologueRect.anchorMin = new Vector2(0.12f, 0.1f);
            prologueRect.anchorMax = new Vector2(0.88f, 0.37f);
            prologueRect.offsetMin = Vector2.zero;
            prologueRect.offsetMax = Vector2.zero;

            //the line telling the player how to move on
            continueHint = NewLabel("Continue Hint", canvasObject.transform, 24f,
                TextAlignmentOptions.Bottom, ResolveFont());
            continueHint.color = new Color(1f, 1f, 1f, 0.6f);
            RectTransform hintRect = continueHint.rectTransform;
            hintRect.anchorMin = new Vector2(0.1f, 0.03f);
            hintRect.anchorMax = new Vector2(0.9f, 0.09f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            canvasObject.SetActive(false);
        }

        private static Image NewStretchedImage(string label, Transform parent)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static TextMeshProUGUI NewLabel(string label, Transform parent, float fontSize,
            TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.color = Color.white;
            //TextMeshProUGUI throws without a font asset, and one built here has none of its own, so the
            //resolved font is put on it. A label wired up in the scene already carries its own.
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }
            return text;
        }

        //The font the built labels read in: the one wired up if there is one, otherwise the game's own
        //MTO Comic picked out by name from whatever fonts are loaded, so the cutscene matches the rest
        //of the interface. TMP's default is the last resort, only reached if MTO is nowhere in memory.
        private TMP_FontAsset ResolveFont()
        {
            if (font != null)
            {
                return font;
            }
            TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (TMP_FontAsset candidate in loaded)
            {
                if (candidate != null
                    && candidate.name.IndexOf("MTO", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    font = candidate;
                    return candidate;
                }
            }
            return TMP_Settings.defaultFontAsset;
        }
    }
}
