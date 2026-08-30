using UnityEngine;

namespace InterrogationRoom
{
    //What is left of the room's screens once they are made of prefabs rather than of code: emptying a
    //list before it is filled again. The cards, the titles, the rows, the frames and every colour and
    //size in them are objects in the scene now, so there is nothing here to build them with.
    public static class PanelUI
    {
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
