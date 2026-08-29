using System.Collections.Generic;
using UnityEngine;

public class GameDirectorMemories : MonoBehaviour
{
    private int CurrentMemoryIndex = 0;
    //CurrentMemoryIndex is advanced as soon as a memory starts, so it points at the NEXT one.
    //The status display and the suspect lookups need the memory that is actually on screen.
    private int activeMemoryIndex = -1;
    [SerializeField] private GameObject uiSuspectsStatusDisplay;
    [SerializeField] private string suspectsStatusLabel = "Clients Served";
    private TMPro.TextMeshProUGUI suspectsStatusText;
    private int shownServedCount = -1;
    private int shownSuspectCount = -1;
    //Every suspect still owed their order carries a light of their own, so the room reads at a glance
    //as who is left to serve. The field is the template the copies are cut from, and is switched off
    //itself: a template lighting the room would be one spotlight nobody is standing under.
    [SerializeField] private GameObject focusLight;
    //A suspect stands about 17 units tall, so 25 clears their head. The template's Light needs a Range
    //that can cover the drop from up there, or the light hangs above the room lighting nothing.
    [SerializeField] private Vector3 focusLightOffset = new Vector3(0f, 25f, 0f);
    private readonly Dictionary<GameObject, GameObject> focusLights = new Dictionary<GameObject, GameObject>();
    // Update is called once per frame
    [SerializeField] private MemoryInfo[] memories; // array of MemoryInfo for each memory in the game
    [SerializeField] private GameObject CharactersParent; // parent object that will hold all the characters in the scene
    [SerializeField] private GameObject Player; // player object that will be used to get the current suspect

    [Header("Food orders")]
    //An order is authored data, not something rolled while the game runs: every quantity below is a
    //constant sitting in the scene, and the same suspect asks for the same thing every play. The
    //component's "Randomise Food Orders" context menu rolls one fresh set into the scene when a new
    //spread is wanted, and the settings under it only steer that roll.
#if UNITY_EDITOR
    //nothing but the roll reads these, and the roll only ever happens in the editor
    [SerializeField] private GameObject charcuterieFoodParent; // holds the food laid out on the board, its stock is all a suspect can actually be served
    [SerializeField] private int minFoodTypesPerSuspect = 1;
    [SerializeField] private int maxFoodTypesPerSuspect = 3;
    [SerializeField] private int maxQuantityPerFood = 3;
#endif

    private void Awake()
    {
        //the template is only ever copied, never lit in place. A prefab asset dropped into the field
        //has no scene of its own and is already inert, so it is left alone.
        if (focusLight != null && focusLight.scene.IsValid())
        {
            focusLight.SetActive(false);
        }
        if (uiSuspectsStatusDisplay != null)
        {
            suspectsStatusText = uiSuspectsStatusDisplay.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (suspectsStatusText == null)
            {
                Debug.LogWarning("uiSuspectsStatusDisplay has no TextMeshProUGUI to write the served count into.");
            }
        }
    }

    public void SetCurrentMemoryIndex(int index)
    {
        CurrentMemoryIndex = index;
    }
    public void StartMemory(int memoryIndex)
    {
        //spawns all the suspects in the memory in the index.
        if (memoryIndex < 0 || memoryIndex >= memories.Length)
        {
            Debug.LogError("Invalid memory index: " + memoryIndex);
            return;
        }
        activeMemoryIndex = memoryIndex;
        MemoryInfo memory = memories[memoryIndex];
        ResetOrders(memory);
        ClearFocusLights();
        foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
        {
            if (suspectInfo.suspect != null)
            {
                GameObject suspectInstance = Instantiate(suspectInfo.suspect, suspectInfo.spawnCoordinates, Quaternion.identity, CharactersParent.transform);
                // Set the food items served to the suspect
                // You can implement this logic based on your game's requirements
                //remove the (clone) from the name of the suspectInstance
                suspectInstance.name = suspectInstance.name.Replace("(Clone)", "").Trim();
                //sorted against the player, and against each other, from where they stand
                if (suspectInstance.GetComponent<CharacterDepthSort>() == null)
                {
                    suspectInstance.AddComponent<CharacterDepthSort>();
                }
                //a suspect who still owes an order gets lit. ResetOrders has just cleared the flags, so
                //in practice that is every one of them, but the check keeps the light honest either way.
                if (!suspectInfo.isServed)
                {
                    SpawnFocusLight(suspectInstance);
                }
            }
            else
            {
                Debug.LogError("Suspect prefab is null in memory index: " + memoryIndex);
            }
        }
    }

    public void Update()
    {
        //if the player presses m the game will start the first memory memory 0 and if pressed again it will start the next memory and so on until the last memory is reached then it will loop back to the first memory
        if (Input.GetKeyDown(KeyCode.M))
        {
            //delete all the suspects in the scene before spawning the new ones using the tag "Suspect"
            try
            {
                GameObject[] existingSuspects = GameObject.FindGameObjectsWithTag("Suspect");
                foreach (GameObject suspect in existingSuspects)
                {
                    Destroy(suspect);
                }
            }
            catch
            {
                Debug.LogWarning("No suspects found to destroy.");
            }
            StartMemory(CurrentMemoryIndex);
            Player.GetComponent<PlayerControlsDiningRoom>().findSuspects();
            CurrentMemoryIndex = (CurrentMemoryIndex + 1) % memories.Length;
        }
        RefreshSuspectsStatusDisplay();
    }

    //The authored quantity is the order itself and is never written to; serving counts down a separate
    //run time copy of it. Starting a memory hands every suspect their whole order back, so the same
    //memory played twice asks for the same food both times.
    private void ResetOrders(MemoryInfo memory)
    {
        if (memory == null || memory.suspectSpawnInfos == null)
        {
            return;
        }
        foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
        {
            if (suspectInfo == null)
            {
                continue;
            }
            suspectInfo.isServed = false;
            if (suspectInfo.foodItems == null)
            {
                continue;
            }
            foreach (FoodItem foodItem in suspectInfo.foodItems)
            {
                if (foodItem != null)
                {
                    foodItem.remaining = foodItem.quantity;
                }
            }
        }
        shownServedCount = -1;
        RefreshSuspectsStatusDisplay();
    }

#if UNITY_EDITOR
    //Rolls one fresh set of orders straight into the scene, where they then sit as constants. Editor
    //only on purpose: this is how the numbers are authored, the game itself only ever reads them.
    //Orders are drawn from the food actually laid out on the board, and the board is laid out once and
    //never restocked, so every suspect of every memory shares that one stock between them. Asking for
    //food that is not there would leave a suspect unservable for the rest of the game.
    [ContextMenu("Randomise Food Orders")]
    public void RandomizeFoodOrders()
    {
        if (memories == null)
        {
            return;
        }
        UnityEditor.Undo.RecordObject(this, "Randomise Food Orders");
        Dictionary<string, int> stock = CountBoardStock();
        List<SuspectSpawnInfo> suspects = new List<SuspectSpawnInfo>();
        foreach (MemoryInfo memory in memories)
        {
            if (memory == null || memory.suspectSpawnInfos == null)
            {
                continue;
            }
            foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
            {
                if (suspectInfo != null && suspectInfo.foodItems != null)
                {
                    suspects.Add(suspectInfo);
                }
            }
        }
        int remainingStock = 0;
        foreach (int count in stock.Values)
        {
            remainingStock += count;
        }
        for (int i = 0; i < suspects.Count; i++)
        {
            SuspectSpawnInfo suspectInfo = suspects[i];
            //a re-rolled order has not been served yet
            suspectInfo.isServed = false;
            foreach (FoodItem foodItem in suspectInfo.foodItems)
            {
                if (foodItem != null)
                {
                    foodItem.quantity = 0;
                }
            }
            //fair share of what is left, so the last suspect is not handed an empty board
            int budget = Mathf.Max(1, remainingStock / (suspects.Count - i));
            remainingStock -= FillOrder(suspectInfo, stock, budget);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private int FillOrder(SuspectSpawnInfo suspectInfo, Dictionary<string, int> stock, int budget)
    {
        //the foods this suspect can be asked for: listed on the suspect and still on the board
        List<FoodItem> candidates = new List<FoodItem>();
        foreach (FoodItem foodItem in suspectInfo.foodItems)
        {
            if (foodItem == null || string.IsNullOrEmpty(foodItem.foodItemId))
            {
                continue;
            }
            int available;
            if (stock.TryGetValue(foodItem.foodItemId, out available) && available > 0)
            {
                candidates.Add(foodItem);
            }
        }
        if (candidates.Count == 0 || budget <= 0)
        {
            return 0;
        }
        //shuffle so the order is not always the first few foods in the array
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            FoodItem swap = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = swap;
        }
        int wantedTypes = Mathf.Clamp(Random.Range(minFoodTypesPerSuspect, maxFoodTypesPerSuspect + 1), 1, candidates.Count);
        int ordered = 0;
        for (int i = 0; i < wantedTypes && ordered < budget; i++)
        {
            FoodItem foodItem = candidates[i];
            int available = Mathf.Min(stock[foodItem.foodItemId], maxQuantityPerFood, budget - ordered);
            if (available <= 0)
            {
                continue;
            }
            int quantity = Random.Range(1, available + 1);
            foodItem.quantity = quantity;
            stock[foodItem.foodItemId] -= quantity;
            ordered += quantity;
        }
        return ordered;
    }

    //counts the food laid out on the board, inactive included since the board is closed most of the
    //time. Anything already carrying the "Served" tag is spoken for and does not go back into the pot.
    private Dictionary<string, int> CountBoardStock()
    {
        Dictionary<string, int> stock = new Dictionary<string, int>();
        if (charcuterieFoodParent == null)
        {
            Debug.LogWarning("charcuterieFoodParent is not set, food orders cannot be matched to the board.");
            return stock;
        }
        foreach (Transform child in charcuterieFoodParent.GetComponentsInChildren<Transform>(true))
        {
            if (child == charcuterieFoodParent.transform || child.CompareTag("Untagged") || child.CompareTag("Served"))
            {
                continue;
            }
            int count;
            stock.TryGetValue(child.tag, out count);
            stock[child.tag] = count + 1;
        }
        return stock;
    }
#endif

    //writes "<label> served/total" for the memory on screen. Only touches the text when a
    //count actually changed, so this is cheap enough to poll and cannot miss a change.
    public void RefreshSuspectsStatusDisplay()
    {
        if (suspectsStatusText == null)
        {
            return;
        }
        int servedCount = 0;
        int suspectCount = 0;
        MemoryInfo memory = GetActiveMemory();
        if (memory != null && memory.suspectSpawnInfos != null)
        {
            foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
            {
                if (suspectInfo == null || suspectInfo.suspect == null)
                {
                    continue;
                }
                suspectCount++;
                if (suspectInfo.isServed)
                {
                    servedCount++;
                }
            }
        }
        if (servedCount == shownServedCount && suspectCount == shownSuspectCount)
        {
            return;
        }
        shownServedCount = servedCount;
        shownSuspectCount = suspectCount;
        suspectsStatusText.text = suspectsStatusLabel + "\n\n" + servedCount + "/" + suspectCount;
    }

    private MemoryInfo GetActiveMemory()
    {
        if (memories == null || activeMemoryIndex < 0 || activeMemoryIndex >= memories.Length)
        {
            return null;
        }
        return memories[activeMemoryIndex];
    }

    // Spawned suspects are Instantiate clones (name has "(Clone)" stripped),
    // so they are matched back to their SuspectSpawnInfo by prefab name.
    private SuspectSpawnInfo FindSuspectSpawnInfo(GameObject suspect)
    {
        if (suspect == null || memories == null)
        {
            return null;
        }
        //every memory reuses the same suspect prefabs, so a plain scan would always hand back
        //memory 0's entry and mark the wrong one served. The memory on screen wins.
        SuspectSpawnInfo activeMatch = FindSuspectSpawnInfoIn(GetActiveMemory(), suspect);
        if (activeMatch != null)
        {
            return activeMatch;
        }
        foreach (MemoryInfo memory in memories)
        {
            SuspectSpawnInfo match = FindSuspectSpawnInfoIn(memory, suspect);
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }

    private SuspectSpawnInfo FindSuspectSpawnInfoIn(MemoryInfo memory, GameObject suspect)
    {
        if (memory == null || memory.suspectSpawnInfos == null)
        {
            return null;
        }
        foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
        {
            if (suspectInfo != null && suspectInfo.suspect != null && suspectInfo.suspect.name == suspect.name)
            {
                return suspectInfo;
            }
        }
        return null;
    }

    //The light is hung on the suspect it belongs to, so it stays over their head and is destroyed along
    //with them when the room is cleared for the next memory. It is placed in world space first and then
    //re-parented keeping that placement, so the offset means the same thing whatever transform the
    //suspect prefab happens to arrive with.
    private void SpawnFocusLight(GameObject suspect)
    {
        if (focusLight == null || suspect == null || focusLights.ContainsKey(suspect))
        {
            return;
        }
        GameObject spawnedLight = Instantiate(focusLight, suspect.transform.position + focusLightOffset, focusLight.transform.rotation);
        spawnedLight.name = "Focus Lighting - " + suspect.name;
        spawnedLight.transform.SetParent(suspect.transform, true);
        //the template is switched off, so the copy comes out of it switched off too
        spawnedLight.SetActive(true);
        focusLights[suspect] = spawnedLight;
    }

    private void RemoveFocusLight(GameObject suspect)
    {
        if (suspect == null)
        {
            return;
        }
        GameObject spawnedLight;
        if (focusLights.TryGetValue(suspect, out spawnedLight))
        {
            focusLights.Remove(suspect);
            if (spawnedLight != null)
            {
                Destroy(spawnedLight);
            }
        }
    }

    //Starting a memory clears the room, and the lights are children of the suspects being cleared, so
    //this is mostly dropping bookkeeping that is about to point at destroyed objects. It still destroys
    //what it holds, in case a light ever outlives the suspect it was hung on.
    private void ClearFocusLights()
    {
        foreach (GameObject spawnedLight in focusLights.Values)
        {
            if (spawnedLight != null)
            {
                Destroy(spawnedLight);
            }
        }
        focusLights.Clear();
    }

    public SuspectSpawnInfo GetSuspectSpawnInfo(GameObject suspect)
    {
        return FindSuspectSpawnInfo(suspect);
    }

    public GameObject GetSuspectHand(GameObject suspect)
    {
        SuspectSpawnInfo suspectInfo = FindSuspectSpawnInfo(suspect);
        return suspectInfo != null ? suspectInfo.hand : null;
    }

    public void SetSuspectServed(GameObject suspect, bool served)
    {
        SuspectSpawnInfo suspectInfo = FindSuspectSpawnInfo(suspect);
        if (suspectInfo != null)
        {
            suspectInfo.isServed = served;
            //the light means "still waiting", so it goes out the moment the order is filled
            if (served)
            {
                RemoveFocusLight(suspect);
            }
            else
            {
                SpawnFocusLight(suspect);
            }
            RefreshSuspectsStatusDisplay();
        }
    }

    public bool IsSuspectServed(GameObject suspect)
    {
        SuspectSpawnInfo suspectInfo = FindSuspectSpawnInfo(suspect);
        return suspectInfo != null && suspectInfo.isServed;
    }
    //Class that will store the Suspect, spawns on which x,y,z coordinates,
    //and an 13 index int array that will store how many of each food item is served to that suspect on that memory,
    //0 is not served, 1+ is the amount of that food served to that suspect on that mission
    //Also a bool to store if the suspect has been served or not set to false by default

    [System.Serializable]
    public class SuspectSpawnInfo
    {
        public GameObject suspect;
        public GameObject hand; // hand object for the suspect
        public Vector3 spawnCoordinates; // x,y,z coordinates
        public FoodItem[] foodItems; // array of FoodItem for each food item served to the suspect
        public bool isServed; // true if the suspect has been served, false if not
        public string conversation1;
        public string conversation2;
    }
    [System.Serializable]
    public class MemoryInfo
    {
        public SuspectSpawnInfo[] suspectSpawnInfos; // array of SuspectSpawnInfo for each suspect in this memory
    }
    [System.Serializable]
    public class FoodItem
    {
        public string foodItemId; // unique identifier for the food item
        public int quantity; // how many of this food the suspect orders, authored in the scene and constant from there on
        //a sprite for the food item that will be displayed on the UI
        public Sprite foodItemSprite; // sprite for the food item
        //how many are still owed this run. Serving counts this down and leaves quantity untouched, so
        //the order sitting in the scene is still there to be handed back when the memory starts again.
        [System.NonSerialized] public int remaining;
    }
}
