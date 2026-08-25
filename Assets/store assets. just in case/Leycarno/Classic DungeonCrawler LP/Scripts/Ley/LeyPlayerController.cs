using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Scripts.Ley
{
    public class LeyPlayerController : LeyController
    {
        [Header("Optional Camera")] [SerializeField]
        protected Transform cameraContainer;

        [SerializeField] protected float cameraMovingTilt = 1;
        [SerializeField] protected float cameraRotationTilt = 1;

        [CanBeNull] private LeyWaypoint _currentWaypoint; // TODO: needed???
        [CanBeNull] private LeyWaypoint _currentWaypointInFront;

        protected override void Awake()
        {
            base.Awake();
            DetectCurrentWaypoints();
        }

        private void DetectCurrentWaypoints()
        {
            _currentWaypoint = Waypoints.FirstOrDefault(waypoint => waypoint.IsAtPosition(TheTransform.position));
            _currentWaypointInFront = Waypoints.FirstOrDefault(waypoint =>
                waypoint.IsAtPosition(TheTransform.position + LeyDirection.GetDeltaPositionOf(CurrentDir, 1)));
        }

        protected override void OnTeleportStart()
        {
            base.OnTeleportStart();
            DoDeactivateTrigger();
        }
        
        protected override void OnTeleportFinished()
        {
            base.OnTeleportFinished();
            ResetCam();
            DetectCurrentWaypoints();
            DoActivateTrigger();
        }

        protected override void OnMovingStart()
        {
            base.OnMovingStart();
            DoDeactivateTrigger();
        }

        protected override void OnMovingStep(
            float counter, 
            Vector3 originPosition,
            Vector3 targetPosition)
        {
            base.OnMovingStep(counter, originPosition, targetPosition);

            if (!cameraContainer)
                return;

            var curve = Mathf.Abs(counter - .5f);
            
            var tiltFactor =
                Math.Abs(originPosition.z - targetPosition.z) > .1f 
                ? originPosition.z - targetPosition.z
                : originPosition.x - targetPosition.x;
            
            TiltCamera(curve, -cameraMovingTilt * tiltFactor, 
                Vector3.left * tiltFactor);
        }

        protected override void OnMovingFinished()
        {
            base.OnMovingFinished();
            ResetCam();
            DetectCurrentWaypoints();
            DoActivateTrigger();
        }

        protected override void OnRotationStart()
        {
            base.OnRotationStart();
            DoDeactivateTrigger();
        }

        protected override void OnRotationStep(
            float counter, Quaternion originRotation, Quaternion targetRotation, bool toTheRight)
        {
            base.OnRotationStep(counter, originRotation, targetRotation, toTheRight);

            if (!cameraContainer)
                return;

            var curve = Mathf.Abs(counter - .5f);
            TiltCamera(curve, cameraRotationTilt * (toTheRight ? -1f : 1f), Vector3.forward);
        }

        protected override void OnRotationFinished()
        {
            base.OnRotationFinished();
            ResetCam();
            DetectCurrentWaypoints();
            DoActivateTrigger();
        }

        private void ResetCam()
        {
            cameraContainer.localPosition = Vector3.zero;
            cameraContainer.localRotation = Quaternion.identity;
        }

        private void TiltCamera(float curve, float factor, Vector3 factorVector)
        {
            if (!cameraContainer)
                return;

            var tiltCurve = (.5f - curve) * factor;
            cameraContainer.localRotation = Quaternion.Euler(factorVector * tiltCurve);
            cameraContainer.localPosition = new Vector3(0, Mathf.Abs(tiltCurve) * .02f, 0);
        }
        
        private void DoActivateTrigger()
        {
            // ReSharper disable once Unity.NoNullPropagation
            _currentWaypoint?.ActivateTrigger();
            // ReSharper disable once Unity.NoNullPropagation
            _currentWaypointInFront?.ActivateTrigger(true);
        }
        
        private void DoDeactivateTrigger()
        {
            // ReSharper disable once Unity.NoNullPropagation
            _currentWaypoint?.DeactivateTrigger();
            // ReSharper disable once Unity.NoNullPropagation
            _currentWaypointInFront?.DeactivateTrigger();
        }
    }
}