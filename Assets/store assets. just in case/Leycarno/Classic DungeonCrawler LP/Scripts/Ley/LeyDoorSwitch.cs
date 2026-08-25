using UnityEngine;

namespace Scripts.Ley
{
    public class LeyDoorSwitch : LeyAInteractionTarget
    {
        [SerializeField][HideInInspector] protected LeyDoor affectedDoor;

        public void SetDoor(LeyDoor leyDoor)
        {
            affectedDoor = leyDoor;
        }

        public void OnClick()
        {
            if (isTriggerAble && affectedDoor)
                affectedDoor.OnClickDoorKnob();
        }
    }
}