using System;
using UnityEngine;

namespace Scripts.Ley
{
    /**
     * base class for targets, 'isTriggerAble', when in Range (same of waypoint in front of the player) 
     * Also the base class for targets of 'LeyInTriggerRange'
     */
    public abstract class LeyAInteractionTarget : LeyABehaviour
    {
        [SerializeField] protected bool isEnabledOnCurrentWaypoint;
        [SerializeField] protected bool isEnabledOnWaypointInFront;
        [SerializeField] [HideInInspector] protected bool isTriggerAble;

        private bool _isInRange;

        public void ActivateTrigger(bool isInFront) =>
            isTriggerAble = _isInRange && (isInFront && isEnabledOnWaypointInFront || !isInFront && isEnabledOnCurrentWaypoint);

        public void DeactivateTrigger() => isTriggerAble = false;

        private void OnTriggerEnter(Collider other)
        {
            _isInRange = other.TryGetComponent(out LeyTriggerRange x);
            Debug.Log("OnTriggerEnter " + _isInRange);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out LeyTriggerRange x))
                _isInRange = false;
            Debug.Log("OnTriggerExit " + _isInRange);
        }
    }
}