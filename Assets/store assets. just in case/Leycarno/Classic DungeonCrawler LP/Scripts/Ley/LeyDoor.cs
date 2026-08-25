using System;
using System.Collections;
using UnityEngine;

namespace Scripts.Ley
{
    public enum LeyDoorType
    {
        UpAndDown,
        DoubleDoor,
        ForcepsDoor
    }

    public enum LeyDoorState
    {
        Closed,
        Opening,
        Closing,
        Open
    }

    public class LeyDoor : LeyABehaviour
    {
        [SerializeField] protected LeyDoorType doorType;
        [SerializeField] protected float speed = 1f;

        [Space] [SerializeField] protected Transform doorPartA;

        [Header("optional for DoorTypes 'DoubleDoor' and 'ForcepsDoor'")] [SerializeField]
        protected Transform doorPartB;

        private void Awake()
        {
            _waypoint = GetComponentInParent<LeyWaypoint>();
            if (_waypoint)
                _waypoint.IsBlocked = !LeyDoorState.Open.Equals(State);
        }

        public LeyDoorState State { get; private set; }

        private LeyWaypoint _waypoint;
        private Coroutine _coroutine;
        private readonly Vector3 _closedPosition = Vector3.zero;
        private readonly Vector3 _openPosition = new Vector3(0, .8f, 0);
        private readonly Vector3 _openPositionLeft = new Vector3(-.55f, 0, 0);
        private readonly Vector3 _openPositionRight = new Vector3(.55f, 0, 0);
        private readonly Vector3 _openPositionTop = new Vector3(0, .5f, 0);
        private readonly Vector3 _openPositionBottom = new Vector3(0, -.5f, 0);

        public void OnClickDoorKnob()
        {
            if (!(_coroutine is null))
                StopCoroutine(_coroutine);

            switch (State)
            {
                case LeyDoorState.Closed:
                case LeyDoorState.Closing:
                    State = LeyDoorState.Opening;
                    break;
                case LeyDoorState.Open:
                case LeyDoorState.Opening:
                    State = LeyDoorState.Closing;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (_waypoint)
                _waypoint.IsBlocked = true;

            var opening = LeyDoorState.Opening.Equals(State);

            switch (doorType)
            {
                case LeyDoorType.UpAndDown:
                    _coroutine = StartCoroutine(DoMoveUpOrDown(opening));
                    break;
                case LeyDoorType.DoubleDoor:
                    _coroutine = StartCoroutine(DoMoveDoubleAndForcepsDoor(opening,
                        _openPositionLeft, _openPositionRight));
                    break;
                case LeyDoorType.ForcepsDoor:
                    _coroutine = StartCoroutine(DoMoveDoubleAndForcepsDoor(opening,
                        _openPositionTop, _openPositionBottom));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private IEnumerator DoMoveUpOrDown(bool opening)
        {
            var currentPosition = doorPartA.transform.localPosition;
            var targetPosition = opening ? _openPosition : _closedPosition;
            var magnitude = Mathf.Abs(currentPosition.magnitude - targetPosition.magnitude);

            var counter = .0f;
            while (magnitude > 0 && counter < 1)
            {
                counter += speed * Time.deltaTime / magnitude;
                doorPartA.transform.localPosition = Vector3.LerpUnclamped(currentPosition, targetPosition, counter);
                yield return null;
            }

            doorPartA.transform.localPosition = targetPosition;
            State = opening ? LeyDoorState.Open : LeyDoorState.Closed;

            if (_waypoint)
                _waypoint.IsBlocked = !LeyDoorState.Open.Equals(State);

            yield return null;
        }

        private IEnumerator DoMoveDoubleAndForcepsDoor(bool opening,
            Vector3 openTargetPositionA, Vector3 openTargetPositionB)
        {
            if (!doorPartA || !doorPartB)
                yield return null;

            var currentPositionA = doorPartA.localPosition;
            var currentPositionB = doorPartB.localPosition;

            var targetPositionA = opening ? openTargetPositionA : _closedPosition;
            var targetPositionB = opening ? openTargetPositionB : _closedPosition;
            var magnitude = Mathf.Abs(currentPositionA.magnitude - targetPositionA.magnitude);

            var counter = .0f;
            while (magnitude > 0 && counter < 1)
            {
                counter += speed * Time.deltaTime / magnitude;
                doorPartA.localPosition = Vector3.LerpUnclamped(currentPositionA, targetPositionA, counter);
                doorPartB.localPosition = Vector3.LerpUnclamped(currentPositionB, targetPositionB, counter);
                yield return null;
            }

            doorPartA.localPosition = targetPositionA;
            doorPartB.localPosition = targetPositionB;
            State = opening ? LeyDoorState.Open : LeyDoorState.Closed;

            if (_waypoint)
                _waypoint.IsBlocked = !LeyDoorState.Open.Equals(State);

            yield return null;
        }
    }
}