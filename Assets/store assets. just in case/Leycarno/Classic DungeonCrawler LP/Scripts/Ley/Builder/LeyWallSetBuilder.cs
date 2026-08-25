using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scripts.Ley.Builder
{
    public class LeyWallSetBuilder : LeyABehaviour
    {
#if UNITY_EDITOR

        [SerializeField] [Range(0, 8)] protected int wallNorth;
        [SerializeField] [Range(0, 8)] protected int wallEast;
        [SerializeField] [Range(0, 8)] protected int wallSouth;
        [SerializeField] [Range(0, 8)] protected int wallWest;

        [Space] [SerializeField] protected List<GameObject> wallSetPrefabs;
        [SerializeField] protected GameObject pillarPrefab;

        private readonly Dictionary<LeyDir, GameObject> _currentWalls = new Dictionary<LeyDir, GameObject>();
        private readonly Dictionary<LeyDir, GameObject> _currentPillars = new Dictionary<LeyDir, GameObject>();

        private readonly Dictionary<LeyDir, LeyWallSetBuilder> _otherWallSets =
            new Dictionary<LeyDir, LeyWallSetBuilder>();

        private readonly Dictionary<LeyDir, Vector3> _localWallPositions = new Dictionary<LeyDir, Vector3>()
        {
            { LeyDir.North, new Vector3(0, 0, .5f) },
            { LeyDir.East, new Vector3(.5f, 0, 0) },
            { LeyDir.South, new Vector3(0, 0, -.5f) },
            { LeyDir.West, new Vector3(-.5f, 0, 0) }
        };

        private readonly Dictionary<LeyDir, Vector3> _localPillarPositions = new Dictionary<LeyDir, Vector3>()
        {
            { LeyDir.NorthWest, new Vector3(-.5f, 0, .5f) },
            { LeyDir.NorthEast, new Vector3(.5f, 0, .5f) },
            { LeyDir.SouthEast, new Vector3(.5f, 0, -.5f) },
            { LeyDir.SouthWest, new Vector3(-.5f, 0, -.5f) }
        };

        private void OnValidate() => EditorApplication.delayCall += _OnValidate;

        private void _OnValidate()
        {
            if (Application.isPlaying || this == null)
                return;

            if (!TheTransform.parent)
                return;

            DeleteCurrentParts();
            RefreshCurrentWalls();

            if (!pillarPrefab)
                return;
            RefreshOtherWallSets();
            RefreshPillarsForWalls();
        }

        private void DeleteCurrentParts()
        {
            foreach (var d in TheTransform.GetComponentsInChildren<LeyWall>())
                d.RemovePrefabInstanceFromScene();
            _currentWalls.Clear();
            foreach (var ds in TheTransform.GetComponentsInChildren<LeyPillar>())
                ds.RemovePrefabInstanceFromScene();
            _currentPillars.Clear();
        }

        private void RefreshCurrentWalls()
        {
            RefreshWall(LeyDir.North, wallNorth);
            RefreshWall(LeyDir.East, wallEast);
            RefreshWall(LeyDir.South, wallSouth);
            RefreshWall(LeyDir.West, wallWest);
        }

        private void RefreshWall(LeyDir dir, int variation)
        {
            if (variation < 1 || variation > wallSetPrefabs.Count) return;
            if (!(PrefabUtility.InstantiatePrefab(wallSetPrefabs[variation - 1]) is GameObject prefab)) return;
            prefab.transform.SetParent(TheTransform);
            prefab.transform.localPosition = _localWallPositions[dir];
            prefab.transform.localRotation = LeyDirection.GetRotationOf(dir);
            prefab.name = "wall" + dir;
            _currentWalls.Add(dir, prefab);
        }

        private void RefreshOtherWallSets()
        {
            _otherWallSets.Clear();

            var myP = TheTransform.position;
            var myX = Mathf.RoundToInt(myP.x);
            var myZ = Mathf.RoundToInt(myP.z);

            foreach (Transform t in TheTransform.parent)
            {
                var p = t.position;
                if (Mathf.RoundToInt(Vector3.Distance(myP, p)) != 1)
                    continue;
                if (!t.TryGetComponent<LeyWallSetBuilder>(out var ws))
                    continue;
                var x = Mathf.RoundToInt(p.x);
                var z = Mathf.RoundToInt(p.z);

                if (myX == x && myZ < z) _otherWallSets.Add(LeyDir.North, ws);
                if (myX == x && myZ > z) _otherWallSets.Add(LeyDir.South, ws);
                if (myX < x && myZ == z) _otherWallSets.Add(LeyDir.East, ws);
                if (myX > x && myZ == z) _otherWallSets.Add(LeyDir.West, ws);
                if (myX < x && myZ < z) _otherWallSets.Add(LeyDir.NorthEast, ws);
                if (myX > x && myZ < z) _otherWallSets.Add(LeyDir.NorthWest, ws);
                if (myX > x && myZ > z) _otherWallSets.Add(LeyDir.SouthWest, ws);
                if (myX < x && myZ > z) _otherWallSets.Add(LeyDir.SouthEast, ws);
            }

            // foreach (var ws in _otherWallSets)
            //     Debug.Log(myX + " | " + myZ + "  " + ws.Key + " > " + ws.Value);
        }

        private void RefreshPillarsForWalls()
        {
            Debug.Log("Start refresh pillars");
            foreach (var ws in _currentWalls)
            {
                switch (ws.Key)
                {
                    case LeyDir.North:
                        RefreshPillar(LeyDir.NorthWest);
                        RefreshPillar(LeyDir.NorthEast);
                        break;
                    case LeyDir.East:
                        RefreshPillar(LeyDir.NorthEast);
                        RefreshPillar(LeyDir.SouthEast);
                        break;
                    case LeyDir.South:
                        RefreshPillar(LeyDir.SouthEast);
                        RefreshPillar(LeyDir.SouthWest);
                        break;
                    case LeyDir.West:
                        RefreshPillar(LeyDir.NorthWest);
                        RefreshPillar(LeyDir.SouthWest);
                        break;
                    default:
                        Debug.LogError("WALL CAN NOT HAVE THIS DIRECTION");
                        break;
                }
            }

            Debug.Log("END refresh pillars");
        }

        private void RefreshPillar(LeyDir pillarDir)
        {
            if (_currentPillars.ContainsKey(pillarDir)) return;

            switch (pillarDir)
            {
                case LeyDir.NorthWest:
                    if (IsPillarAlreadyByOther(LeyDir.North, LeyDir.SouthWest)) return;
                    if (IsPillarAlreadyByOther(LeyDir.West, LeyDir.NorthEast)) return;
                    if (IsPillarAlreadyByOther(LeyDir.NorthWest, LeyDir.SouthEast)) return;
                    break;
                case LeyDir.NorthEast:
                    if (IsPillarAlreadyByOther(LeyDir.North, LeyDir.SouthEast)) return;
                    if (IsPillarAlreadyByOther(LeyDir.East, LeyDir.NorthWest)) return;
                    if (IsPillarAlreadyByOther(LeyDir.NorthEast, LeyDir.SouthWest)) return;
                    break;
                case LeyDir.SouthEast:
                    if (IsPillarAlreadyByOther(LeyDir.South, LeyDir.NorthEast)) return;
                    if (IsPillarAlreadyByOther(LeyDir.East, LeyDir.SouthWest)) return;
                    if (IsPillarAlreadyByOther(LeyDir.SouthEast, LeyDir.NorthWest)) return;
                    break;
                case LeyDir.SouthWest:
                    if (IsPillarAlreadyByOther(LeyDir.South, LeyDir.NorthWest)) return;
                    if (IsPillarAlreadyByOther(LeyDir.West, LeyDir.SouthEast)) return;
                    if (IsPillarAlreadyByOther(LeyDir.SouthWest, LeyDir.NorthEast)) return;
                    break;
                default:
                    Debug.LogError("PILLAR CAN NOT HAVE THIS DIRECTION");
                    break;
            }

            if (!(PrefabUtility.InstantiatePrefab(pillarPrefab) is GameObject prefab)) return;
            prefab.transform.SetParent(TheTransform);
            prefab.transform.localPosition = _localPillarPositions[pillarDir];
            prefab.name = "pillar" + pillarDir;
            _currentPillars.Add(pillarDir, prefab);
        }

        private bool IsPillarAlreadyByOther(LeyDir wsDir, LeyDir pillarDir) =>
            _otherWallSets.ContainsKey(wsDir)
            && _otherWallSets[wsDir]._currentPillars.ContainsKey(pillarDir);

#endif
    }
}