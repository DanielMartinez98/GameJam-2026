using TMPro;
using UnityEngine;

namespace InterrogationRoom
{
    //One line of a report: what it is called, and what it says. "Time of death" over "12 AM".
    //
    //The prefab decides everything about how those two read - which is the quiet one, how far apart
    //they sit, how tall the line stands when the answer is short. All the file does is fill them in.
    public class CaseFinding : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private TextMeshProUGUI value;

        public void Set(string labelText, string valueText)
        {
            if (label != null)
            {
                label.text = labelText;
            }
            if (value != null)
            {
                value.text = valueText;
            }
        }
    }
}
