using UnityEngine;
using UnityEngine.EventSystems;

namespace InterrogationRoom
{
    //Sits on one of the things in the room and says what it is called when the cursor is over it. The
    //name is handed in by the director rather than read off the GameObject, because the objects are
    //named for the scene tree - "Case Information", "PlayerCharacter" - and the player should be shown
    //what the thing is, not what it was filed under.
    //
    //It only reports; where the name goes and what it looks like is the director's business.
    public class RoomHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private InterrogationRoomDirector director;
        private string displayName;

        public void Bind(InterrogationRoomDirector owner, string name)
        {
            director = owner;
            displayName = name;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (director != null)
            {
                director.ShowHoverName(this, displayName);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (director != null)
            {
                director.HideHoverName(this);
            }
        }

        //Leaving the room by any route other than the cursor moving off - the object being switched off,
        //the scene being left - never fires an exit, so the name would be left hanging over an object
        //that is no longer there.
        private void OnDisable()
        {
            if (director != null)
            {
                director.HideHoverName(this);
            }
        }
    }
}
