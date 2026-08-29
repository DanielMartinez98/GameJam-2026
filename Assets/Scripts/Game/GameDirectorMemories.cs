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
    [SerializeField] private GameObject conversationUI; // UI element to display the conversation lines
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
    //the victim copy standing in the room for the memory being played, if this one has a victim at all
    private GameObject spawnedVictim;
    // Update is called once per frame
    [SerializeField] private MemoryInfo[] memories; // array of MemoryInfo for each memory in the game
    [SerializeField] private GameObject CharactersParent; // parent object that will hold all the characters in the scene
    [SerializeField] private GameObject Player; // player object that will be used to get the current suspect

    [Header("Food orders")]
    //An order is authored data, not something rolled while the game runs: every quantity below is a
    //constant sitting in the scene, and the same suspect asks for the same thing every play. The
    //component's "Randomise Food Orders" context menu rolls one fresh set into the scene when a new
    //spread is wanted, and the settings under it only steer that roll.
    //holds the food laid out on the board, its stock is all a suspect can actually be served. The same
    //object the refill station calls its board: rolling the orders counts what is on it, and leaving a
    //memory writes down what is left on it so returning can put it back.
    [SerializeField] private GameObject charcuterieFoodParent;
#if UNITY_EDITOR
    //nothing but the roll reads these, and the roll only ever happens in the editor
    [SerializeField] private int minFoodTypesPerSuspect = 1;
    [SerializeField] private int maxFoodTypesPerSuspect = 3;
    [SerializeField] private int maxQuantityPerFood = 3;
#endif

    [Header("Leaving the memory")]
    //The interrogation room is both where a memory is chosen and where the player comes back to when
    //they are done with it, whether or not they served everyone.
    [SerializeField] private string interrogationSceneName = "MainScene";
    [SerializeField] private KeyCode leaveMemoryKey = KeyCode.Escape;
    //Where the food on the board comes back from when a memory is resumed. Left empty it is found in
    //the scene, since the refill screen is usually switched off and easy to forget to wire up.
    [SerializeField] private RefillStationMinigame refillStation;
    //the memory being restored, read by StartMemory in place of resetting the orders. Null on a fresh
    //start, which is every entry except coming back to a memory left half served.
    private InterrogationRoom.MemorySnapshot restoringFrom;

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
        //Resuming puts the orders back as they were left; anything else hands them out whole. This has
        //to happen before the suspects are spawned below, because the focus lights are hung on whoever
        //is still owed an order and would otherwise light up people who have already been served.
        if (restoringFrom != null)
        {
            RestoreOrders(memory, restoringFrom);
        }
        else
        {
            ResetOrders(memory);
        }
        ClearFocusLights();
        SpawnVictim(memory);
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

    //Only some memories have one, and the victim is there to be listened to rather than served: no order,
    //no serve prompt, and deliberately no focus light. The lights mark the suspects who still owe an
    //order, so putting one over someone who can never be served would be reading the room wrong.
    private void SpawnVictim(MemoryInfo memory)
    {
        //the previous memory's victim is not tagged "Suspect", so nothing else clears it away
        if (spawnedVictim != null)
        {
            Destroy(spawnedVictim);
            spawnedVictim = null;
        }
        if (memory == null || !memory.hasVictim || memory.victimInfo == null || memory.victimInfo.victim == null)
        {
            return;
        }
        spawnedVictim = Instantiate(memory.victimInfo.victim, memory.victimInfo.spawnCoordinates, Quaternion.identity, CharactersParent.transform);
        spawnedVictim.name = spawnedVictim.name.Replace("(Clone)", "").Trim();
        //sorted against the player and the suspects from where they stand, the same as everyone else
        if (spawnedVictim.GetComponent<CharacterDepthSort>() == null)
        {
            spawnedVictim.AddComponent<CharacterDepthSort>();
        }
    }

    //the victim standing in the room right now, or null in a memory that has none
    public GameObject GetVictim()
    {
        return spawnedVictim;
    }

    //the authored line and coordinates behind that victim, for whoever needs to read it out
    public VictimInfo GetActiveVictimInfo()
    {
        if (activeMemoryIndex < 0 || activeMemoryIndex >= memories.Length)
        {
            return null;
        }
        MemoryInfo memory = memories[activeMemoryIndex];
        return memory != null && memory.hasVictim ? memory.victimInfo : null;
    }

    //Which memory this is comes from the interrogation room, which wrote it down on its way out of its
    //own scene. Nothing written means the dining room was opened on its own - straight from the editor,
    //most likely - and it is left alone for the M key below to drive, exactly as it always was.
    private void Start()
    {
        int requested = InterrogationRoom.CaseFile.TakeRequestedMemory();
        if (requested >= 0)
        {
            //Arriving from the interrogation room, so a memory left half served is picked up where it
            //was left. Choosing a memory from the detective threw that away on the way out, so by the
            //time the request gets here there is only a snapshot to find if resuming was what was meant.
            EnterMemory(requested, true);
        }
    }

    public void Update()
    {
        //if the player presses m the game will start the first memory memory 0 and if pressed again it will start the next memory and so on until the last memory is reached then it will loop back to the first memory
        if (Input.GetKeyDown(KeyCode.M))
        {
            //a debug key for flipping through the memories, so it always deals a fresh one
            EnterMemory(CurrentMemoryIndex, false);
            CurrentMemoryIndex = (CurrentMemoryIndex + 1) % memories.Length;
        }
        //the way back to the interrogation room, which is also the only thing that records how this
        //memory went, so leaving is a real action rather than just a scene change
        if (Input.GetKeyDown(leaveMemoryKey))
        {
            LeaveMemory();
        }
        RefreshSuspectsStatusDisplay();
    }

    //Clears the room and puts a memory in it. Both ways in go through here, so a memory started from
    //the interrogation room is set up exactly the same as one started with the M key.
    public void EnterMemory(int memoryIndex, bool resumeIfSaved)
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
        restoringFrom = resumeIfSaved ? InterrogationRoom.CaseFile.GetSnapshot(memoryIndex) : null;
        StartMemory(memoryIndex);
        if (restoringFrom != null)
        {
            //The orders went back inside StartMemory, because the spawn had to see them. The board and
            //the player are nothing to do with spawning, so they go back out here.
            RestoreBoard(restoringFrom);
            RestorePlayerPosition(restoringFrom);
        }
        restoringFrom = null;
        if (Player != null)
        {
            PlayerControlsDiningRoom controls = Player.GetComponent<PlayerControlsDiningRoom>();
            if (controls != null)
            {
                controls.findSuspects();
            }
        }
    }

    //Everything the player did in this memory that the scene file knows nothing about. Taken at the
    //moment they walk out, which is the only moment it can be taken: the scene is torn down straight
    //after and there is nothing left to read.
    private InterrogationRoom.MemorySnapshot CaptureSnapshot()
    {
        InterrogationRoom.MemorySnapshot snapshot = new InterrogationRoom.MemorySnapshot();
        snapshot.memoryIndex = activeMemoryIndex;
        if (Player != null)
        {
            snapshot.hasPlayerPosition = true;
            snapshot.playerPosition = Player.transform.position;
        }
        CaptureBoard(snapshot);
        CaptureOrders(snapshot);
        return snapshot;
    }

    //What is still on the charcuterie board, and where on it. Food that has been served is not on the
    //board any more - it was carried onto a suspect's plate and retagged - so it never reaches here,
    //which is what makes the board come back depleted by exactly what was handed out.
    private void CaptureBoard(InterrogationRoom.MemorySnapshot snapshot)
    {
        if (charcuterieFoodParent == null)
        {
            return;
        }
        foreach (Transform child in charcuterieFoodParent.transform)
        {
            //the same rule the order roll counts stock by: untagged things are scenery and "Served"
            //food is spoken for
            if (child.CompareTag("Untagged") || child.CompareTag("Served"))
            {
                continue;
            }
            snapshot.board.Add(new InterrogationRoom.MemorySnapshot.BoardItem
            {
                foodTag = child.tag,
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });
        }
    }

    private void CaptureOrders(InterrogationRoom.MemorySnapshot snapshot)
    {
        MemoryInfo memory = GetActiveMemory();
        if (memory == null || memory.suspectSpawnInfos == null)
        {
            return;
        }
        foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
        {
            InterrogationRoom.MemorySnapshot.SuspectState state =
                new InterrogationRoom.MemorySnapshot.SuspectState();
            //an empty entry still takes its place in the list, so the positions keep lining up with the
            //memory's own array on the way back in
            if (suspectInfo != null)
            {
                state.suspectName = suspectInfo.suspect != null ? suspectInfo.suspect.name : null;
                state.isServed = suspectInfo.isServed;
                if (suspectInfo.foodItems != null)
                {
                    foreach (FoodItem foodItem in suspectInfo.foodItems)
                    {
                        if (foodItem != null)
                        {
                            state.foods.Add(new InterrogationRoom.MemorySnapshot.FoodRemaining
                            {
                                foodItemId = foodItem.foodItemId,
                                remaining = foodItem.remaining
                            });
                        }
                    }
                }
            }
            snapshot.suspects.Add(state);
        }
    }

    //The other side of ResetOrders: instead of handing everyone their whole order back, everyone gets
    //back exactly what they were still owed, and whoever had already been served stays served.
    private void RestoreOrders(MemoryInfo memory, InterrogationRoom.MemorySnapshot snapshot)
    {
        if (memory == null || memory.suspectSpawnInfos == null)
        {
            return;
        }
        for (int i = 0; i < memory.suspectSpawnInfos.Length; i++)
        {
            SuspectSpawnInfo suspectInfo = memory.suspectSpawnInfos[i];
            if (suspectInfo == null)
            {
                continue;
            }
            InterrogationRoom.MemorySnapshot.SuspectState state = i < snapshot.suspects.Count
                ? snapshot.suspects[i]
                : null;
            if (state == null)
            {
                //nothing was written down for this one, so it starts the memory owing its whole order
                ResetSuspectOrder(suspectInfo);
                continue;
            }
            suspectInfo.isServed = state.isServed;
            if (suspectInfo.foodItems == null)
            {
                continue;
            }
            foreach (FoodItem foodItem in suspectInfo.foodItems)
            {
                if (foodItem == null)
                {
                    continue;
                }
                //matched by id rather than by position, so a food missing from the snapshot falls back
                //to its authored quantity instead of silently taking the next one's count
                foodItem.remaining = FindRemaining(state, foodItem.foodItemId, foodItem.quantity);
            }
        }
        shownServedCount = -1;
        RefreshSuspectsStatusDisplay();
    }

    private static int FindRemaining(InterrogationRoom.MemorySnapshot.SuspectState state, string foodItemId,
        int fallback)
    {
        foreach (InterrogationRoom.MemorySnapshot.FoodRemaining food in state.foods)
        {
            if (food.foodItemId == foodItemId)
            {
                return food.remaining;
            }
        }
        return fallback;
    }

    //Puts back the board the player walked out carrying. The scene has just laid out its own authored
    //board, so that is cleared first: what the player was actually holding replaces it wholesale rather
    //than being added to it.
    private void RestoreBoard(InterrogationRoom.MemorySnapshot snapshot)
    {
        if (charcuterieFoodParent == null)
        {
            return;
        }
        RefillStationMinigame station = ResolveRefillStation();
        if (station == null)
        {
            Debug.LogWarning("No refill station found, so the charcuterie board cannot be put back as it was left.");
            return;
        }
        Transform board = charcuterieFoodParent.transform;
        for (int i = board.childCount - 1; i >= 0; i--)
        {
            Transform child = board.GetChild(i);
            //Only the food is swapped out, on exactly the rule the capture used. Anything else the
            //board carries was never written down and so cannot be put back, and clearing it here
            //would quietly strip the board of it a little more every time a memory is resumed.
            if (child.CompareTag("Untagged") || child.CompareTag("Served"))
            {
                continue;
            }
            //unparented first: Destroy only takes effect at the end of the frame, and the restored food
            //is going in right now, which would otherwise leave the board double stocked in between
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        foreach (InterrogationRoom.MemorySnapshot.BoardItem item in snapshot.board)
        {
            GameObject foodPrefab = station.FindFoodPrefab(item.foodTag);
            if (foodPrefab == null)
            {
                Debug.LogWarning("No food prefab tagged '" + item.foodTag + "', so that piece cannot be put back on the board.");
                continue;
            }
            GameObject food = Instantiate(foodPrefab, board, false);
            food.transform.localPosition = item.localPosition;
            food.transform.localRotation = item.localRotation;
            food.transform.localScale = item.localScale;
        }
    }

    private RefillStationMinigame ResolveRefillStation()
    {
        if (refillStation == null)
        {
            //the refill screen spends most of its life switched off, so the search has to look at the
            //objects that are not currently active too
            refillStation = FindFirstObjectByType<RefillStationMinigame>(FindObjectsInactive.Include);
        }
        return refillStation;
    }

    private void RestorePlayerPosition(InterrogationRoom.MemorySnapshot snapshot)
    {
        if (Player == null || !snapshot.hasPlayerPosition)
        {
            return;
        }
        Player.transform.position = snapshot.playerPosition;
        //the camera is put wherever the player is on every frame, so it lands on them by itself and
        //there is nothing to move here
    }

    //Serving everyone is what finishes a memory. Walking out on anything less leaves it on the Memory
    //button in the interrogation room to be picked up again, which is the whole reason that button
    //exists, so the count is taken here at the moment the player leaves rather than assumed either way.
    public bool IsActiveMemoryComplete()
    {
        MemoryInfo memory = GetActiveMemory();
        if (memory == null || memory.suspectSpawnInfos == null)
        {
            return false;
        }
        bool anySuspects = false;
        foreach (SuspectSpawnInfo suspectInfo in memory.suspectSpawnInfos)
        {
            if (suspectInfo == null || suspectInfo.suspect == null)
            {
                continue;
            }
            anySuspects = true;
            if (!suspectInfo.isServed)
            {
                return false;
            }
        }
        return anySuspects;
    }

    //Hands the result back to the case file and returns to the interrogation room. Public so an exit
    //button in the dining room's own UI can leave the same way the key does.
    public void LeaveMemory()
    {
        bool complete = IsActiveMemoryComplete();
        //Written down before the result is reported, because reporting a finished memory is what throws
        //the old snapshot away - and taken only for a memory that is actually still in progress, since
        //a finished one is never walked back into.
        if (activeMemoryIndex >= 0 && !complete)
        {
            InterrogationRoom.CaseFile.SaveSnapshot(CaptureSnapshot());
        }
        InterrogationRoom.CaseFile.ReportMemoryLeft(activeMemoryIndex, complete);
        if (string.IsNullOrEmpty(interrogationSceneName))
        {
            Debug.LogError("No interrogation room scene set, so there is nowhere to go back to.");
            return;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(interrogationSceneName);
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
            ResetSuspectOrder(suspectInfo);
        }
        shownServedCount = -1;
        RefreshSuspectsStatusDisplay();
    }

    //one suspect handed their whole order back, unserved
    private static void ResetSuspectOrder(SuspectSpawnInfo suspectInfo)
    {
        if (suspectInfo == null)
        {
            return;
        }
        suspectInfo.isServed = false;
        if (suspectInfo.foodItems == null)
        {
            return;
        }
        foreach (FoodItem foodItem in suspectInfo.foodItems)
        {
            if (foodItem != null)
            {
                foodItem.remaining = foodItem.quantity;
            }
        }
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
        public string conversation0; // conversation lines for the suspect when first aproached
        public string conversation1;
        public string conversation2;
    }
    [System.Serializable]
    public class MemoryInfo
    {
        public SuspectSpawnInfo[] suspectSpawnInfos; // array of SuspectSpawnInfo for each suspect in this memory
        public bool hasVictim; // true if the memory has a victim, false if not
        public VictimInfo victimInfo; // VictimInfo for the victim in this memory
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
    [System.Serializable]
    public class VictimInfo
    {
        public GameObject victim;
        public Vector3 spawnCoordinates; // x,y,z coordinates
        public string conversation0; // conversation lines for the victim when first aproached
    }
}
