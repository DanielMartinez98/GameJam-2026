using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharcuterieBoardMinigame : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject plate;
    [SerializeField] private GameObject Hand;
    [SerializeField] private GameObject foodItems;// is a empty GameObject that will be used as a placeholder for the ui to show which food the suspect desires and how many of each food item is left to serve.
    // when an item is fully served the ui for that item disappears.
    [SerializeField] private GameObject foodItemUI;// is used to disable and enable the text that lets the player know that they can press e to exit the minigame and return to the dining room
    [SerializeField] private GameObject foodItemEntryPrefab;// optional look for one entry of the food items ui, needs an Image for the sprite and a TextMeshProUGUI for the counter. Left empty the entry is built from scratch.
    [SerializeField] private GameObject gameDirector;
    [SerializeField] private bool canExit = false;
    [SerializeField] private float handSpeed = 5f;
    [SerializeField] private float entrySpacing = 20f;
    [SerializeField] private Vector2 entrySize = new Vector2(90f, 110f);
    //The hand art is a whole forearm stretched into one big rect, and the fingers sit at the bottom
    //of it. The pickup box is placed there instead of on the Hand anchor, which is an empty 10px dot
    //somewhere up the wrist. Size is roughly a grip, height is how far up from the art's bottom edge
    //the fingers actually close.
    [SerializeField] private string visualHandChildName = "Visual Hand";
    [SerializeField] private Vector2 grabBoxSize = new Vector2(60f, 60f);
    [SerializeField] private float fingertipHeight = 50f;
    private PlayerControlsDiningRoom playerControls;
    private GameObject currentSuspect;
    private GameObject handPrefab;
    private GameObject carriedFood;
    private GameDirectorMemories.FoodItem carriedFoodItem;
    private Transform carriedFoodOriginalParent;
    private GameDirectorMemories.SuspectSpawnInfo currentSuspectInfo;
    private readonly List<FoodItemEntry> foodItemEntries = new List<FoodItemEntry>();
    private readonly HashSet<string> unknownFoodTags = new HashSet<string>();
    //where the fingers are, in the Hand's own local space. Zero until a hand prefab is spawned.
    private Vector2 grabOffset = Vector2.zero;

    // One entry of the food items ui: the sprite of a food the suspect wants, plus how many are still owed.
    private class FoodItemEntry
    {
        public GameDirectorMemories.FoodItem foodItem;
        public GameObject root;
        public TextMeshProUGUI counter;
        public int shownQuantity = -1;
    }

    public void SetCurrentSuspect(GameObject suspect)
    {
        currentSuspect = suspect;
        //get the suspect's hand prefab from the gameDirector's memories, the Hand field stays the spawn point in the scene
        try
        {
            handPrefab = gameDirector.GetComponent<GameDirectorMemories>().GetSuspectHand(currentSuspect);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error getting suspect hand: " + e.Message);
        }
        //get the suspect's spawn info from the gameDirector's memories and set it to the currentSuspectInfo variable
        try
        {
            currentSuspectInfo = gameDirector.GetComponent<GameDirectorMemories>().GetSuspectSpawnInfo(currentSuspect);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error getting suspect spawn info: " + e.Message);
        }
    }

    // This object starts inactive, so the first SetActive(true) runs OnEnable before
    // Start has ever run. Resolve the reference here rather than relying on Start.
    private void ResolvePlayerControls()
    {
        if (playerControls == null && player != null)
        {
            playerControls = player.GetComponent<PlayerControlsDiningRoom>();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResolvePlayerControls();
    }
    void OnEnable()
    {
        carriedFood = null;
        carriedFoodItem = null;
        carriedFoodOriginalParent = null;
        //a suspect with no hand prefab must not inherit the previous suspect's finger position
        ApplyGrabBox(Vector2.zero);
        ResolvePlayerControls();
        if (playerControls != null)
        {
            SetCurrentSuspect(playerControls.GetCurrentSuspect());
            //instantiate the hand prefab as a child of the hand object in the scene
            if (handPrefab != null && Hand != null)
            {
                SpawnHand();
            }
        }
        //delete all children of the plate
        foreach (Transform child in plate.transform)
        {
            Destroy(child.gameObject);
        }
        BuildFoodItemsUI();
    }

    // Update is called once per frame
    void Update()
    {
        //check if the screen contains any food items that the suspect desires in the game Director's memories
        // by checking all the values that are set to more than 0
        // and those that are more than 0, check if the food items id is found in any tags on any object in the scene
        //if so then the hand that was spawned should slowly move to the nearest food item with that id and then grab it and then move to the plate and drop it on the plate,
        //the moment the food is droped the tag of the food item should be set to "Served" and the quantity of that food item in the game director's memories should be decremented by 1
        // if not enough items were serveed then show the food items ui, and allow the player to press e to get more food.
        // in the food item display the food that the suspect desires should be displayed. with a counter of how many items are left to serve.
        //once the suspect eats all the food they desire the suspects served status should be set to true in the game director's memories
        // and the minigame should end and return to the dining room.
        if(currentSuspectInfo != null && Hand != null && plate != null)
        {
            UpdateHand();
        }
        //the counters follow the quantities in the game director's memories, an entry drops out once its food is fully served
        RefreshFoodItemsUI();
        if(currentSuspectInfo != null)
        {
            bool allFoodServed = true;
            foreach(GameDirectorMemories.FoodItem foodItem in currentSuspectInfo.foodItems)
            {
                if(foodItem.quantity > 0)
                {
                    allFoodServed = false;
                    break;
                }
            }
            if(allFoodServed)
            {
                //set the suspect's served status to true in the game director's memories
                gameDirector.GetComponent<GameDirectorMemories>().SetSuspectServed(currentSuspect, true);
                //end the minigame and return to the dining room
                playerControls.CloseCharcuterieBoard();
                return;
            }
        }
        //check if the scene has any food items that are still desired but not available in the scene,
        //if so allow the player to press e to leave and get more food
        bool needsMoreFood = false;
        if(currentSuspectInfo != null)
        {
            foreach(GameDirectorMemories.FoodItem foodItem in currentSuspectInfo.foodItems)
            {
                if(foodItem.quantity > 0 && FindFoodInScene(foodItem.foodItemId).Length == 0)
                {
                    needsMoreFood = true;
                    break;
                }
            }
        }
        canExit = needsMoreFood;
        if(foodItemUI != null)
        {
            foodItemUI.SetActive(needsMoreFood);
        }

        if(Input.GetKeyDown(KeyCode.E))
        {
            //stop the minigame and return to the dining room, set the suspect to null in the player controls script
            if(canExit)
            {
                playerControls.CloseCharcuterieBoard();
            }
        }
    }

    //The hand only steers here. What it is touching is decided by the trigger boxes, which report
    //through HandTouched, so nothing in this class measures distances to work out contact any more.
    private void UpdateHand()
    {
        if (carriedFood != null)
        {
            //carrying, so head for the plate and wait for the plate's trigger
            MoveHandTowards(plate.transform.position);
            return;
        }
        GameDirectorMemories.FoodItem wantedFoodItem;
        GameObject nearestFoodItem = FindNearestWantedFood(out wantedFoodItem);
        if (nearestFoodItem != null)
        {
            //head for the nearest wanted food and wait for its trigger
            MoveHandTowards(nearestFoodItem.transform.position);
        }
    }

    //HandFoodTrigger on the Hand calls this whenever the hand's trigger box overlaps another collider
    public void HandTouched(GameObject other)
    {
        if (other == null || currentSuspectInfo == null || !isActiveAndEnabled)
        {
            return;
        }
        if (carriedFood != null)
        {
            //only the plate ends a carry, everything else is brushed past
            if (plate != null && (other == plate || other.CompareTag("Plate")))
            {
                DropCarriedFoodOnPlate();
            }
            return;
        }
        GameDirectorMemories.FoodItem wantedFoodItem = FindWantedFoodItem(other.tag);
        if (wantedFoodItem != null)
        {
            GrabFood(other, wantedFoodItem);
        }
    }

    private GameDirectorMemories.FoodItem FindWantedFoodItem(string foodTag)
    {
        foreach (GameDirectorMemories.FoodItem foodItem in currentSuspectInfo.foodItems)
        {
            if (foodItem != null && foodItem.quantity > 0 && foodItem.foodItemId == foodTag)
            {
                return foodItem;
            }
        }
        return null;
    }

    private void GrabFood(GameObject food, GameDirectorMemories.FoodItem wantedFoodItem)
    {
        //the food becomes a child of the hand and rides along from here. SetParent keeps its world
        //position, so it stays exactly where it was touched instead of snapping to the hand.
        carriedFood = food;
        carriedFoodItem = wantedFoodItem;
        carriedFoodOriginalParent = food.transform.parent;
        food.transform.SetParent(Hand.transform, true);
    }

    private void DropCarriedFoodOnPlate()
    {
        //reached the plate, so the food stops being a child of the hand and becomes one of the plate,
        //which is also what lets the next suspect start from a clean plate
        carriedFood.transform.SetParent(plate.transform, true);
        carriedFood.tag = "Served";
        if (carriedFoodItem != null && carriedFoodItem.quantity > 0)
        {
            carriedFoodItem.quantity--;
        }
        carriedFood = null;
        carriedFoodItem = null;
        carriedFoodOriginalParent = null;
    }

    //bailing out mid-carry must not leave food parented to the hand, it would be destroyed with it
    private void ReleaseCarriedFood()
    {
        if (carriedFood != null && carriedFoodOriginalParent != null)
        {
            carriedFood.transform.SetParent(carriedFoodOriginalParent, true);
        }
        carriedFood = null;
        carriedFoodItem = null;
        carriedFoodOriginalParent = null;
    }

    //nearest across every food the suspect still wants, so the hand commits to a single target
    private GameObject FindNearestWantedFood(out GameDirectorMemories.FoodItem wantedFoodItem)
    {
        wantedFoodItem = null;
        GameObject nearestFoodItem = null;
        float nearestDistance = Mathf.Infinity;
        foreach (GameDirectorMemories.FoodItem foodItem in currentSuspectInfo.foodItems)
        {
            if (foodItem == null || foodItem.quantity <= 0 || string.IsNullOrEmpty(foodItem.foodItemId))
            {
                continue;
            }
            //check if the food item is in the scene
            foreach (GameObject foodItemInScene in FindFoodInScene(foodItem.foodItemId))
            {
                float distance = HandDistanceBetween(GrabPointWorld(), foodItemInScene.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestFoodItem = foodItemInScene;
                    wantedFoodItem = foodItem;
                }
            }
        }
        return nearestFoodItem;
    }

    //foodItemId doubles as a Unity tag and FindGameObjectsWithTag throws on an undefined one,
    //which would abort the rest of Update every frame. Report each bad id once and keep going.
    private GameObject[] FindFoodInScene(string foodItemId)
    {
        if (string.IsNullOrEmpty(foodItemId) || unknownFoodTags.Contains(foodItemId))
        {
            return System.Array.Empty<GameObject>();
        }
        try
        {
            return GameObject.FindGameObjectsWithTag(foodItemId);
        }
        catch (UnityException)
        {
            unknownFoodTags.Add(foodItemId);
            Debug.LogError("Food item id '" + foodItemId + "' is not a defined tag, nothing with that id can be served.");
            return System.Array.Empty<GameObject>();
        }
    }

    //Only the Hand in the scene decides where the hand is, nothing inside the prefab may shift it.
    //The prefab root is centred on Hand rather than dropped at Hand's world position, because its own
    //pivot is a corner while Hand's is its middle, and its children anchor to its rect centre.
    //Normalising the root discards whatever offset the prefab authored, so the pickup box is placed
    //from the art afterwards instead of trusting the Hand anchor to be anywhere near the fingers.
    private void SpawnHand()
    {
        GameObject handInstance = Instantiate(handPrefab, Hand.transform, false);
        RectTransform handRect = handInstance.transform as RectTransform;
        if (handRect == null)
        {
            handInstance.transform.localPosition = Vector3.zero;
            ApplyGrabBox(Vector2.zero);
            return;
        }
        handRect.anchorMin = new Vector2(0.5f, 0.5f);
        handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(0.5f, 0.5f);
        handRect.anchoredPosition = Vector2.zero;
        handRect.localRotation = Quaternion.identity;
        handRect.localScale = Vector3.one;
        ApplyGrabBox(FindGrabOffset(handInstance));
    }

    //The arm art is drawn from the wrist down, so the fingers are at the bottom of its rect and
    //fingertipHeight lifts the box off the very tip. Read from the art rather than authored per hand,
    //because every hand prefab offsets that art differently and the Hand anchor itself never moves.
    private Vector2 FindGrabOffset(GameObject handInstance)
    {
        RectTransform visual = FindVisualHand(handInstance);
        if (visual == null)
        {
            Debug.LogWarning("Hand prefab '" + handPrefab.name + "' has no '" + visualHandChildName +
                "' rect, the pickup box falls back to the Hand anchor and will not line up with the fingers.");
            return Vector2.zero;
        }
        Vector3 fingersWorld = visual.TransformPoint(new Vector3(
            visual.rect.center.x,
            visual.rect.yMin + fingertipHeight,
            0f));
        Vector3 fingersLocal = Hand.transform.InverseTransformPoint(fingersWorld);
        return new Vector2(fingersLocal.x, fingersLocal.y);
    }

    private RectTransform FindVisualHand(GameObject handInstance)
    {
        RectTransform[] rects = handInstance.GetComponentsInChildren<RectTransform>(true);
        //by name first, that is what every hand prefab calls its art
        foreach (RectTransform rect in rects)
        {
            if (rect.name == visualHandChildName)
            {
                return rect;
            }
        }
        //a renamed art child still gets found: it is the biggest drawn thing under the prefab
        RectTransform biggest = null;
        float biggestArea = 0f;
        foreach (RectTransform rect in rects)
        {
            if (rect.GetComponent<Image>() == null)
            {
                continue;
            }
            float area = rect.rect.width * rect.rect.height;
            if (area > biggestArea)
            {
                biggestArea = area;
                biggest = rect;
            }
        }
        return biggest;
    }

    //offset and size are in the Hand's local space, which is canvas pixels, same as the art
    private void ApplyGrabBox(Vector2 offset)
    {
        grabOffset = offset;
        if (Hand == null)
        {
            return;
        }
        BoxCollider2D box = Hand.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.offset = offset;
            box.size = grabBoxSize;
        }
    }

    //where the pickup box actually sits, which is what has to reach the food
    private Vector3 GrabPointWorld()
    {
        return Hand.transform.TransformPoint(new Vector3(grabOffset.x, grabOffset.y, 0f));
    }

    //Board and hand live on a canvas, so movement and contact are flat: x and y only, z is left alone.
    //The Hand anchor is steered, but it is the grab point that has to land on the target, so the aim
    //is pulled back by the gap between them. Without this the fingers overshoot to the far side.
    private void MoveHandTowards(Vector3 target)
    {
        Vector3 current = Hand.transform.position;
        Vector3 aim = target - (GrabPointWorld() - current);
        Vector2 stepped = Vector2.MoveTowards(current, aim, handSpeed * Time.deltaTime);
        Hand.transform.position = new Vector3(stepped.x, stepped.y, current.z);
    }

    private static float HandDistanceBetween(Vector3 from, Vector3 to)
    {
        return Vector2.Distance(from, to);
    }

    //builds one entry under the foodItems placeholder for every food the suspect still wants
    private void BuildFoodItemsUI()
    {
        ClearFoodItemsUI();
        if (foodItems == null || currentSuspectInfo == null || currentSuspectInfo.foodItems == null)
        {
            return;
        }
        EnsureFoodItemsLayout();
        foreach (GameDirectorMemories.FoodItem foodItem in currentSuspectInfo.foodItems)
        {
            if (foodItem == null || foodItem.quantity <= 0)
            {
                continue;
            }
            foodItemEntries.Add(CreateFoodItemEntry(foodItem));
        }
        RefreshFoodItemsUI();
    }

    //the placeholder is an empty GameObject in the editor, so give it a layout the first time we fill it
    private void EnsureFoodItemsLayout()
    {
        if (foodItems.GetComponent<LayoutGroup>() != null)
        {
            return;
        }
        HorizontalLayoutGroup layout = foodItems.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = entrySpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private FoodItemEntry CreateFoodItemEntry(GameDirectorMemories.FoodItem foodItem)
    {
        FoodItemEntry entry = new FoodItemEntry { foodItem = foodItem };
        if (foodItemEntryPrefab != null)
        {
            entry.root = Instantiate(foodItemEntryPrefab, foodItems.transform);
            Image prefabIcon = entry.root.GetComponentInChildren<Image>(true);
            if (prefabIcon != null)
            {
                prefabIcon.sprite = foodItem.foodItemSprite;
                prefabIcon.preserveAspect = true;
            }
            entry.counter = entry.root.GetComponentInChildren<TextMeshProUGUI>(true);
            return entry;
        }

        entry.root = new GameObject(foodItem.foodItemId + " Wanted", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        entry.root.layer = foodItems.layer;
        RectTransform rect = (RectTransform)entry.root.transform;
        rect.SetParent(foodItems.transform, false);
        rect.sizeDelta = entrySize;

        LayoutElement layoutElement = entry.root.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = entrySize.x;
        layoutElement.preferredHeight = entrySize.y;

        Image icon = entry.root.GetComponent<Image>();
        icon.sprite = foodItem.foodItemSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        //without a sprite an Image draws a plain white box, the counter shows the id instead
        icon.enabled = foodItem.foodItemSprite != null;

        GameObject counterObject = new GameObject("Counter", typeof(RectTransform), typeof(TextMeshProUGUI));
        counterObject.layer = foodItems.layer;
        RectTransform counterRect = (RectTransform)counterObject.transform;
        counterRect.SetParent(rect, false);
        counterRect.anchorMin = new Vector2(0f, 0f);
        counterRect.anchorMax = new Vector2(1f, 0f);
        counterRect.pivot = new Vector2(0.5f, 1f);
        counterRect.anchoredPosition = Vector2.zero;
        counterRect.sizeDelta = new Vector2(0f, 30f);

        entry.counter = counterObject.GetComponent<TextMeshProUGUI>();
        entry.counter.alignment = TextAlignmentOptions.Center;
        entry.counter.fontSize = 28f;
        entry.counter.color = Color.white;
        entry.counter.raycastTarget = false;
        return entry;
    }

    private void RefreshFoodItemsUI()
    {
        for (int i = foodItemEntries.Count - 1; i >= 0; i--)
        {
            FoodItemEntry entry = foodItemEntries[i];
            if (entry.root == null || entry.foodItem == null)
            {
                foodItemEntries.RemoveAt(i);
                continue;
            }
            //when an item is fully served the ui for that item disappears
            if (entry.foodItem.quantity <= 0)
            {
                Destroy(entry.root);
                foodItemEntries.RemoveAt(i);
                continue;
            }
            if (entry.foodItem.quantity == entry.shownQuantity)
            {
                continue;
            }
            entry.shownQuantity = entry.foodItem.quantity;
            if (entry.counter != null)
            {
                entry.counter.text = entry.foodItem.foodItemSprite != null
                    ? "x" + entry.shownQuantity
                    : entry.foodItem.foodItemId + " x" + entry.shownQuantity;
            }
        }
    }

    private void ClearFoodItemsUI()
    {
        foodItemEntries.Clear();
        if (foodItems == null)
        {
            return;
        }
        foreach (Transform child in foodItems.transform)
        {
            Destroy(child.gameObject);
        }
    }

    void OnDisable()
    {
        ClearFoodItemsUI();
        //put any half carried food back on the board before the hand goes, it is parented to it
        ReleaseCarriedFood();
        //destroy the spawned hand
        if(Hand != null)
        {
            foreach(Transform child in Hand.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
