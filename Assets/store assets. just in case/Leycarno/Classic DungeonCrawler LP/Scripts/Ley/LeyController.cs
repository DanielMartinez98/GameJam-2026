using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scripts.Ley
{
    public class LeyController : LeyABehaviour
    {
        [SerializeField] protected LeyWaypoint startWaypoint;
        [SerializeField] protected LeyDir startDirection;
        [SerializeField] protected Transform waypointContainer;

        [SerializeField] protected bool velocityMoving;
        [SerializeField] protected float movingSpeed = 4;
        [SerializeField] protected float rotationSpeed = 6;

        protected LeyDir CurrentDir { get; private set; }
        protected bool InMotion { get; private set; }
        protected List<LeyWaypoint> Waypoints;

        private Coroutine _coroutine;

        protected virtual void Awake()
        {
            Waypoints = waypointContainer.GetComponentsInChildren<LeyWaypoint>().ToList();
            TeleportTo(startWaypoint ? startWaypoint.TheTransform.position : TheTransform.position, startDirection);
        }

        protected virtual void OnTeleportStart()
        {
            if (!(_coroutine is null))
                StopCoroutine(_coroutine);
        }

        public void TeleportTo(Vector3 targetPosition, LeyDir targetDir)
        {
            OnTeleportStart();

            var t = TheTransform;
            t.position = targetPosition;
            t.localRotation = LeyDirection.GetRotationOf(targetDir);
            CurrentDir = targetDir;

            OnTeleportFinished();
        }

        protected virtual void OnTeleportFinished()
        {
            InMotion = false;
        }

        public void Move(int steps, bool sideways = false)
        {
            if (InMotion)
                return;

            var targetDir = sideways ? LeyDirection.RightOf(CurrentDir) : CurrentDir;
            var nextWaypointPosition = TheTransform.position + LeyDirection.GetDeltaPositionOf(targetDir, steps);

            if (Waypoints.All(wp =>
                wp.TheTransform.position != nextWaypointPosition || wp.IsBlocked))
                return;

            if (sideways)
                Debug.Log("Move sideways " + (steps > 0 ? "right" : "left") + " to " + nextWaypointPosition);
            else
                Debug.Log("Move " + (steps > 0 ? "forwards" : "backwards") + " to " + nextWaypointPosition);

            _coroutine = StartCoroutine(DoMove(nextWaypointPosition));
        }

        private IEnumerator DoMove(Vector3 targetPosition)
        {
            var originPosition = TheTransform.position;

            OnMovingStart();

            var counter = .0f;
            while (counter < 1)
            {
                var curve = Mathf.Abs(counter - .5f);
                counter += movingSpeed * Time.deltaTime * (velocityMoving ? 1f - curve : 1f);
                OnMovingStep(counter, originPosition, targetPosition);
                yield return null;
            }

            TheTransform.position = targetPosition;
            OnMovingFinished();
            yield return null;
        }

        protected virtual void OnMovingStart()
        {
            InMotion = true;
        }

        protected virtual void OnMovingStep(
            float counter,
            Vector3 originPosition,
            Vector3 targetPosition)
        {
            TheTransform.position = Vector3.LerpUnclamped(originPosition, targetPosition, counter);
        }

        protected virtual void OnMovingFinished()
        {
            InMotion = false;
        }

        public void Rotate(bool toTheRight)
        {
            if (InMotion)
                return;
            _coroutine = StartCoroutine(DoRotate(toTheRight));
        }

        private IEnumerator DoRotate(bool toTheRight)
        {
            OnRotationStart();

            Debug.Log("Rotate " + (toTheRight ? "right" : "left"));

            var targetDir = toTheRight ? LeyDirection.RightOf(CurrentDir) : LeyDirection.LeftOf(CurrentDir);
            var originRotation = TheTransform.rotation;
            var targetRotation = LeyDirection.GetRotationOf(targetDir);

            var counter = .0f;
            while (counter < 1)
            {
                var curve = Mathf.Abs(counter - .5f);
                counter += rotationSpeed * Time.deltaTime * (.75f - curve);
                OnRotationStep(counter, originRotation, targetRotation, toTheRight);
                yield return null;
            }

            TheTransform.localRotation = targetRotation;
            CurrentDir = targetDir;
            OnRotationFinished();
            yield return null;
        }

        protected virtual void OnRotationStart()
        {
            InMotion = true;
        }

        protected virtual void OnRotationStep(
            float counter, Quaternion originRotation, Quaternion targetRotation, bool toTheRight)
        {
            TheTransform.localRotation = Quaternion.Lerp(originRotation, targetRotation, counter);
        }

        protected virtual void OnRotationFinished()
        {
            InMotion = false;
        }
    }
}