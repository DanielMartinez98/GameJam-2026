using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scripts.Ley
{
    public class LeyWaypoint : LeyABehaviour
    {
        [SerializeField] protected bool blocked;
        private List<LeyAInteractionTarget> _interactionTargets = new List<LeyAInteractionTarget>();

        private void Awake()
        {
            _interactionTargets = TheTransform.GetComponentsInChildren<LeyAInteractionTarget>().ToList();
            if (TheTransform.TryGetComponent<LeyAInteractionTarget>(out var interactionTarget))
                _interactionTargets.Add(interactionTarget);
        }

        public bool IsBlocked
        {
            get => blocked;
            set => blocked = value;
        }

        public void ActivateTrigger(bool isInFront = false)
            => _interactionTargets.ForEach(target => target.ActivateTrigger(isInFront));

        public void DeactivateTrigger()
            => _interactionTargets.ForEach(target => target.DeactivateTrigger());
    }
}