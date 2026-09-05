using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterrogationRoom
{
    //A pressable row in one of the room's lists: a sentence in the notebook, a number on the phone, a
    //memory on offer. It is a prefab rather than something built in code, so how it is put together -
    //what the background is, where the writing sits, how tall it stands - is settled in the editor.
    //
    //This is the whole of what a panel needs to know about one: give it its words, hear when it is
    //pressed. Rearrange the prefab however you like and both still work, as long as these two are
    //pointed at something.
    public class PanelEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;

        public Button Button { get { return button; } }

        public TextMeshProUGUI Label { get { return label; } }

        public void SetLabel(string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        //Found rather than demanded. A row dragged together out of a plain Button and a text works
        //without either being wired by hand, and a wired one is left exactly as it was wired.
        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (label == null)
            {
                label = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void Reset()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
