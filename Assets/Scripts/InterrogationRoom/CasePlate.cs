using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //A photograph clipped to the file: a caption, and under it the picture in its frame.
    //
    //The frame, its border, the mat behind the picture and how big the whole plate is are all the
    //prefab's business. The one thing that cannot be settled there is how a particular picture sits in
    //it, because that depends on the picture: a knife is square and is shown whole, and a suspect is
    //three times taller than the frame and has to be filled in and cut off, or their face ends up ten
    //pixels across. That choice is the fitter's, and it is made here because only here is the sprite
    //known.
    public class CasePlate : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI caption;
        [SerializeField] private Image picture;
        //Sizes the picture against the frame. Left empty, the picture is left exactly as the prefab
        //has it and nothing below happens.
        [SerializeField] private AspectRatioFitter fitter;

        public void Set(string captionText, Sprite image, bool crop)
        {
            if (caption != null)
            {
                caption.text = captionText;
                //a picture that says what it is under its own caption does not need an empty line
                //held open above it
                caption.gameObject.SetActive(!string.IsNullOrEmpty(captionText));
            }
            if (picture == null)
            {
                return;
            }
            picture.sprite = image;
            picture.enabled = image != null;
            if (fitter == null || image == null)
            {
                return;
            }
            //the fitter is doing the aspect now, so the image must not also be doing it
            picture.preserveAspect = false;
            Rect slice = image.rect;
            fitter.aspectRatio = slice.width / Mathf.Max(1f, slice.height);
            //Enveloping fills the frame and lets the rest hang past the bottom, where the mat's mask
            //cuts it off. Hung from its top edge, what survives on a standing figure is the head.
            fitter.aspectMode = crop
                ? AspectRatioFitter.AspectMode.EnvelopeParent
                : AspectRatioFitter.AspectMode.FitInParent;
            RectTransform rect = (RectTransform)picture.transform;
            rect.pivot = new Vector2(0.5f, crop ? 1f : 0.5f);
        }
    }
}
