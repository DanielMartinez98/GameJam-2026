using UnityEngine;

namespace InterrogationRoom
{
    //The shape a page of the file takes: the writing down one side, the photographs down the other.
    //
    //Two columns rather than a row per finding, because a photograph is several lines tall - put beside
    //its own line it would push the next finding a whole frame down the page, and the pictures would
    //end up scattered down the margin instead of stacked where they can be compared. Which side is
    //which, how wide they are and how far apart they sit is the prefab's to decide.
    public class FindingsRow : MonoBehaviour
    {
        [SerializeField] private RectTransform facts;
        [SerializeField] private RectTransform plates;

        public RectTransform Facts { get { return facts; } }

        public RectTransform Plates { get { return plates; } }

        //A page with nothing written on it, or nothing to show, does not keep the empty column open -
        //an empty column still takes its width, and the other one would sit off to one side of a page
        //it has all to itself.
        public void ShowColumns(bool anyWords, bool anyPictures)
        {
            if (facts != null)
            {
                facts.gameObject.SetActive(anyWords);
            }
            if (plates != null)
            {
                plates.gameObject.SetActive(anyPictures);
            }
        }
    }
}
