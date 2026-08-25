using System;
using UnityEngine;

namespace Scripts.Ley
{
    public enum LeyDir
    {
        North,
        East,
        South,
        West,
        
        NorthWest,
        NorthEast,
        SouthEast,
        SouthWest
    }

    public static class LeyDirection
    {
        public static LeyDir RightOf(LeyDir dir)
        {
            switch (dir)
            {
                case LeyDir.North: return LeyDir.East;
                case LeyDir.West: return LeyDir.North;
                case LeyDir.South: return LeyDir.West;
                case LeyDir.East: return LeyDir.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static LeyDir LeftOf(LeyDir dir)
        {
            switch (dir)
            {
                case LeyDir.North: return LeyDir.West;
                case LeyDir.West: return LeyDir.South;
                case LeyDir.South: return LeyDir.East;
                case LeyDir.East: return LeyDir.North;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static Quaternion GetRotationOf(LeyDir dir)
        {
            switch (dir)
            {
                case LeyDir.North: return Quaternion.Euler(0, 0, 0);
                case LeyDir.West: return Quaternion.Euler(0, -90, 0);
                case LeyDir.South: return Quaternion.Euler(0, 180, 0);
                case LeyDir.East: return Quaternion.Euler(0, 90, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static Vector3 GetDeltaPositionOf(LeyDir dir, int steps)
        {
            switch (dir)
            {
                case LeyDir.North: return new Vector3(0, 0, steps);
                case LeyDir.South: return new Vector3(0, 0, -steps);
                case LeyDir.West: return new Vector3(-steps, 0, 0);
                case LeyDir.East: return new Vector3(steps, 0, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static LeyDir Invert(LeyDir dir)
        {
            switch (dir)
            {
                case LeyDir.North: return LeyDir.South;
                case LeyDir.West: return LeyDir.East;
                case LeyDir.South: return LeyDir.North;
                case LeyDir.East: return LeyDir.West;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static bool IsInFrontOf(Vector3 currentPosition, LeyDir dir, Vector3 targetPosition)
        {
            var cpX = Mathf.CeilToInt(currentPosition.x);
            var cpY = Mathf.CeilToInt(currentPosition.z);
            var tpX = Mathf.CeilToInt(targetPosition.x);
            var tpY = Mathf.CeilToInt(targetPosition.z);

            switch (dir)
            {
                case LeyDir.North: return cpX == tpX && cpY + 1 == tpY;
                case LeyDir.West: return cpX - 1 == tpX && cpY == tpY;
                case LeyDir.South: return cpX == tpX && cpY - 1 == tpY;
                case LeyDir.East: return cpX + 1 == tpX && cpY == tpY;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static bool IsInBackOf(Vector3 currentPosition, LeyDir dir, Vector3 targetPosition)
        {
            var cpX = Mathf.CeilToInt(currentPosition.x);
            var cpY = Mathf.CeilToInt(currentPosition.z);
            var tpX = Mathf.CeilToInt(targetPosition.x);
            var tpY = Mathf.CeilToInt(targetPosition.z);

            switch (dir)
            {
                case LeyDir.North: return cpX == tpX && cpY - 1 == tpY;
                case LeyDir.West: return cpX + 1 == tpX && cpY == tpY;
                case LeyDir.South: return cpX == tpX && cpY + 1 == tpY;
                case LeyDir.East: return cpX - 1 == tpX && cpY == tpY;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static bool IsInRightOf(Vector3 currentPosition, LeyDir dir, Vector3 targetPosition)
        {
            var cpX = Mathf.CeilToInt(currentPosition.x);
            var cpY = Mathf.CeilToInt(currentPosition.z);
            var tpX = Mathf.CeilToInt(targetPosition.x);
            var tpY = Mathf.CeilToInt(targetPosition.z);

            switch (dir)
            {
                case LeyDir.North: return cpX + 1 == tpX && cpY == tpY;
                case LeyDir.West: return cpX == tpX && cpY + 1 == tpY;
                case LeyDir.South: return cpX - 1 == tpX && cpY == tpY;
                case LeyDir.East: return cpX == tpX && cpY - 1 == tpY;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public static bool IsInLeftOf(Vector3 currentPosition, LeyDir dir, Vector3 targetPosition)
        {
            var cpX = Mathf.CeilToInt(currentPosition.x);
            var cpY = Mathf.CeilToInt(currentPosition.z);
            var tpX = Mathf.CeilToInt(targetPosition.x);
            var tpY = Mathf.CeilToInt(targetPosition.z);

            switch (dir)
            {
                case LeyDir.North: return cpX - 1 == tpX && cpY == tpY;
                case LeyDir.West: return cpX == tpX && cpY - 1 == tpY;
                case LeyDir.South: return cpX + 1 == tpX && cpY == tpY;
                case LeyDir.East: return cpX == tpX && cpY + 1 == tpY;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }
    }
}