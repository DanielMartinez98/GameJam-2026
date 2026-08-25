using UnityEngine;

namespace Scripts.Ley
{
    public class LeyTeleport : LeyWaypoint
    {
        [SerializeField] protected LeyWaypoint targetWaypoint;
        [SerializeField] protected LeyDir targetDirection;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out LeyController controller))
            {
                Debug.Log(other.name + " enters the teleport... nothing happens ;-)");
                return;
            }

            if (!targetWaypoint)
            {
                Debug.Log("SimpleController enters the teleport... no target ;-)");
                return;
            }

            var position = targetWaypoint.TheTransform.position;
            Debug.Log(" SimpleController has enter the teleport > " + position);
            controller.TeleportTo(position, targetDirection);
        }
    }
}