using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//One-shot setup for the charcuterie minigame's trigger based pickup.
//Only the Hand's box is a trigger. The food and the plate get plain solid boxes, which is enough:
//a trigger overlapping a solid collider still raises OnTriggerEnter2D on both sides.
//2D colliders are used on purpose: they ignore z, and the hand sits well in front of the board.
public static class AddFoodTriggerBoxes
{
    private const string FoodPrefabFolder = "Assets/Prefabs/UI Food";

    [MenuItem("Tools/Charcuterie/Add Trigger Boxes")]
    public static void AddTriggerBoxes()
    {
        int prefabs = AddToFoodPrefabs();
        int sceneObjects = AddToOpenScene();
        AssetDatabase.SaveAssets();
        Debug.Log("Trigger boxes: " + prefabs + " food prefabs updated, " + sceneObjects + " scene objects updated.");
    }

    private static int AddToFoodPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { FoodPrefabFolder });
        int updated = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (EnsureBox(root, false))
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                updated++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return updated;
    }

    private static int AddToOpenScene()
    {
        CharcuterieBoardMinigame minigame = Object.FindFirstObjectByType<CharcuterieBoardMinigame>(FindObjectsInactive.Include);
        if (minigame == null)
        {
            Debug.LogWarning("No CharcuterieBoardMinigame in the open scene, skipped the scene half. Open GameScene and run this again.");
            return 0;
        }
        int updated = 0;
        SerializedObject serializedMinigame = new SerializedObject(minigame);
        GameObject hand = serializedMinigame.FindProperty("Hand").objectReferenceValue as GameObject;
        if (hand != null && SetUpHand(hand))
        {
            updated++;
        }

        //loose food sitting on the board. Prefab instances already inherit the box from their prefab,
        //so only the plain scene objects need one of their own.
        GameDirectorMemories director = Object.FindFirstObjectByType<GameDirectorMemories>(FindObjectsInactive.Include);
        if (director != null)
        {
            SerializedObject serializedDirector = new SerializedObject(director);
            GameObject foodParent = serializedDirector.FindProperty("charcuterieFoodParent").objectReferenceValue as GameObject;
            if (foodParent != null)
            {
                foreach (RectTransform child in foodParent.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.gameObject == foodParent || child.CompareTag("Untagged"))
                    {
                        continue;
                    }
                    if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                    {
                        continue;
                    }
                    if (EnsureBox(child.gameObject, false))
                    {
                        updated++;
                    }
                }
            }
        }
        if (updated > 0)
        {
            EditorSceneManager.MarkSceneDirty(minigame.gameObject.scene);
        }
        return updated;
    }

    private static bool SetUpHand(GameObject hand)
    {
        EnsureBox(hand, true);
        //The Hand object is a 10px anchor, so a box matching its rect would be a dot up the wrist.
        //CharcuterieBoardMinigame moves and sizes this box onto the spawned hand's fingers at runtime;
        //this is only so the editor view is not wildly smaller than what actually picks food up.
        BoxCollider2D grabBox = hand.GetComponent<BoxCollider2D>();
        if (grabBox != null)
        {
            grabBox.size = new Vector2(60f, 60f);
        }
        //2D triggers need a Rigidbody2D on at least one side, and the hand is the side that moves.
        //Kinematic so it is driven purely by the minigame and never falls under gravity.
        Rigidbody2D body = hand.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody2D>(hand);
        }
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;
        //THIS is what makes the hand actually notice the food. A kinematic body ignores static and
        //kinematic colliders by default, and the food has no body of its own so it counts as static.
        //Without this the hand overlaps the food and no trigger callback is ever raised.
        body.useFullKinematicContacts = true;
        //a still overlap keeps being reported only while the body is not allowed to sleep
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (hand.GetComponent<HandFoodTrigger>() == null)
        {
            Undo.AddComponent<HandFoodTrigger>(hand);
        }
        EditorUtility.SetDirty(hand);
        return true;
    }

    //the box matches the RectTransform, so what the player sees is what collides
    private static bool EnsureBox(GameObject target, bool isTrigger)
    {
        RectTransform rect = target.transform as RectTransform;
        if (rect == null)
        {
            return false;
        }
        BoxCollider2D box = target.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider2D>(target);
        }
        box.isTrigger = isTrigger;
        box.size = rect.rect.size;
        //rect.center carries the pivot, which is a corner on some of these prefabs
        box.offset = rect.rect.center;
        EditorUtility.SetDirty(target);
        return true;
    }
}
