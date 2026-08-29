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
    //The hand art is a whole forearm stretched into one big rect with the fingers at the bottom of it.
    //Everything is measured from the Hand object; the art is slid so its fingers land on it, rather than
    //the measuring being chased around after the art. fingertipHeight says how far up the art the fingers
    //close. grabMargin scales how close the hand must get, as a fraction of whatever it is reaching for,
    //so 1 is that item's own footprint and a nut demands a closer approach than a dinner plate.
    [SerializeField] private string visualHandChildName = "Visual Hand";
    [SerializeField] private float fingertipHeight = 50f;
    [SerializeField, Range(0.1f, 1.5f)] private float grabMargin = 0.6f;
    //The suspect does the talking on this screen: their line sits next to a portrait, and the portrait
    //is their own dining room sprite with everything but the head cropped away, so no separate face
    //art has to exist for it.
    [Header("Conversation")]
    [SerializeField] private GameObject ConversationUI;
    [SerializeField] private TextMeshProUGUI conversationText;
    [SerializeField] private Image suspectPortrait;// the frame the face is cropped into, the face itself is built under it
    [SerializeField] private string suspectFaceName = "Suspect Face";
    //A character sprite is a whole standing body, so the portrait is that sprite blown up until only
    //the head is left inside the frame. Both numbers are fractions of the sprite rather than pixels,
    //so one setting fits every character whatever size its art is: faceHeight is how much of the
    //sprite's height the frame shows, facePivot is the point on it that ends up in the middle.
    [SerializeField, Range(0.05f, 1f)] private float faceHeight = 0.18f;
    [SerializeField] private Vector2 facePivot = new Vector2(0.5f, 0.88f);
    [SerializeField] private GameObject Conversation;
    //The Hand sits outside the board panel and is drawn after it, so the moment it picks something up
    //that food rides over everything inside the panel - the suspect's line, their order, the prompt -
    //on its way to the plate. Reordering inside the panel cannot fix that, because the hand is not in
    //the panel to be reordered against. The panels below are lifted onto a sorting layer of their own
    //instead, above the board and therefore above anything the hand is carrying across it.
    [Header("UI layering")]
    [SerializeField] private int uiSortingOffset = 10;
    //The order being complete and the screen going away are two things, not one. Closing on the frame
    //the last item lands snatches the finished plate away before the player has seen it, so the board
    //is held for a beat with everything already done and only then handed back to the dining room.
    [Header("Serving")]
    [SerializeField] private float servedExitDelay = 1f;
    //How much of the plate gets used when an item is set down. Every item is given its own spot when
    //it is picked up, so a finished plate reads as laid out rather than as one pile in the middle.
    //Below 1 the outer ring of the plate is left clear, which keeps food off the rim.
    [SerializeField, Range(0.1f, 1f)] private float plateSpread = 0.8f;
    //counting down while the finished board is being held; negative means nothing is pending
    private float servedExitCountdown = -1f;
    private PlayerControlsDiningRoom playerControls;
    private GameObject currentSuspect;
    private GameObject handPrefab;
    private GameObject carriedFood;
    private GameDirectorMemories.FoodItem carriedFoodItem;
    private Transform carriedFoodOriginalParent;
    //the spot on the plate the item in hand is being carried to, chosen when it was picked up
    private Vector3 carriedFoodTarget;
    private GameDirectorMemories.SuspectSpawnInfo currentSuspectInfo;
    private readonly List<FoodItemEntry> foodItemEntries = new List<FoodItemEntry>();
    private readonly HashSet<string> unknownFoodTags = new HashSet<string>();

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
        servedExitCountdown = -1f;
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
        ShowConversation();
        LiftInformationUI();
    }

    //Sibling order is no help here: the panels are inside the board and the hand is outside it, so no
    //amount of reordering within the board lifts them past it. A nested canvas is what does - turning
    //on overrideSorting pulls an object out of the board's one draw list and gives it a layer of its
    //own above it, and it does that without moving or relaying out a thing.
    private void LiftInformationUI()
    {
        //the minigame lives on the board's own canvas, which is the thing these panels have to beat
        Canvas boardCanvas = GetComponent<Canvas>();
        if (boardCanvas == null)
        {
            boardCanvas = GetComponentInParent<Canvas>();
        }
        if (boardCanvas == null)
        {
            return;
        }
        LiftAboveBoard(foodItemUI, boardCanvas);
        LiftAboveBoard(ConversationUI, boardCanvas);
        LiftAboveBoard(foodItems, boardCanvas);
    }

    private void LiftAboveBoard(GameObject uiObject, Canvas boardCanvas)
    {
        if (uiObject == null)
        {
            return;
        }
        Canvas canvas = uiObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = uiObject.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        //The board is on a sorting layer of its own. Layer is compared before order, so a nested canvas
        //left on the default layer would sort behind the very thing it is meant to sit on top of - it
        //has to join the board's layer first and win on order there.
        canvas.sortingLayerID = boardCanvas.sortingLayerID;
        canvas.sortingOrder = boardCanvas.sortingOrder + uiSortingOffset;
        //a nested canvas is also its own raycast root, so without a raycaster of its own nothing inside
        //it answers the mouse any more, however it behaved before it was lifted
        if (uiObject.GetComponent<GraphicRaycaster>() == null)
        {
            uiObject.AddComponent<GraphicRaycaster>();
        }
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
        //The order is already complete and the screen is on its way out, so the rest of this is done
        //with: the hand has nothing left to fetch, and no prompt should appear over the finished board
        //in the beat before it goes.
        if(servedExitCountdown >= 0f)
        {
            servedExitCountdown -= Time.deltaTime;
            if(servedExitCountdown <= 0f)
            {
                servedExitCountdown = -1f;
                playerControls.CloseCharcuterieBoard();
            }
            return;
        }
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
                if(foodItem.remaining > 0)
                {
                    allFoodServed = false;
                    break;
                }
            }
            if(allFoodServed)
            {
                //set the suspect's served status to true in the game director's memories. This is the
                //moment they are served, not the moment the screen closes: the count and their focus
                //light out in the room should follow the plate, not the beat that comes after it.
                gameDirector.GetComponent<GameDirectorMemories>().SetSuspectServed(currentSuspect, true);
                //nothing is owed any more, so the "go and get more food" prompt has no business being
                //up while the finished board is held
                if(foodItemUI != null)
                {
                    foodItemUI.SetActive(false);
                }
                //hold the finished board, then return to the dining room
                if(servedExitDelay > 0f)
                {
                    servedExitCountdown = servedExitDelay;
                }
                else
                {
                    playerControls.CloseCharcuterieBoard();
                }
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
                if(foodItem.remaining > 0 && FindFoodInScene(foodItem.foodItemId).Length == 0)
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
    //The Hand object IS the grab point. Nothing here derives, projects, or compensates for where the
    //fingers are: a pickup is decided by the Hand's own position and the target's own rect, both of
    //which always exist. Lining the drawn fingers up with that point is done once, when the art is
    //spawned, and is purely cosmetic - if it is off the hand looks wrong but still picks up correctly.
    private void UpdateHand()
    {
        if (carriedFood != null)
        {
            //carrying, so head for this item's own spot on the plate and let go once the hand is over
            //it. Steering to the spot rather than to the plate is what puts the food there: aiming at
            //the plate as a whole meant arriving wherever its edge was first crossed, which from a hand
            //that always comes in from the same side is the same place every time.
            MoveHandTowards(carriedFoodTarget);
            if (ReachedPoint(carriedFoodTarget, carriedFood))
            {
                DropCarriedFoodOnPlate();
            }
            return;
        }
        GameDirectorMemories.FoodItem wantedFoodItem;
        GameObject nearestFoodItem = FindNearestWantedFood(out wantedFoodItem);
        if (nearestFoodItem == null)
        {
            return;
        }
        //head for the nearest wanted food and take it on arrival
        MoveHandTowards(nearestFoodItem.transform.position);
        if (Reached(nearestFoodItem))
        {
            GrabFood(nearestFoodItem, wantedFoodItem);
        }
    }

    //Arrived once the hand is inside the target's own footprint. The tolerance comes from the target
    //rather than from a constant, so a pea-sized nut needs a closer approach than a dinner plate and
    //nothing has to be retuned when the resolution changes. Only what the hand is steering towards can
    //be reached, so a run to one item never comes back holding another.
    private bool Reached(GameObject target)
    {
        if (target == null)
        {
            return false;
        }
        float tolerance = ToleranceFor(target);
        if (tolerance <= 0f)
        {
            //a zero footprint means the canvas has not been laid out yet this frame, and every distance
            //would collapse to zero with it. Nothing is close to anything until it has real size.
            return false;
        }
        //Everything on this board sits on the canvas plane, so this distance is the same one the camera
        //draws. Nothing is projected, adapted or compensated for anywhere in the pickup path.
        return Vector3.Distance(Hand.transform.position, target.transform.position) <= tolerance;
    }

    //Arrived at a bare point rather than at an object. A spot on the plate has no rect of its own, so
    //the tolerance comes from the item being carried instead - the same rule the pickups run on, which
    //is why a nut is set down more precisely than a salami. Measured flat, because the hand only ever
    //moves in x and y and any gap in z would be one it could never close.
    private bool ReachedPoint(Vector3 target, GameObject carried)
    {
        if (carried == null)
        {
            return false;
        }
        float tolerance = ToleranceFor(carried);
        if (tolerance <= 0f)
        {
            return false;
        }
        return Vector2.Distance(Hand.transform.position, target) <= tolerance;
    }

    //A spot on the plate for one item to be set down on, drawn fresh for every item picked up. The
    //plate is round, so the point comes from a disc rather than a rect - which stays on the plate for a
    //square one too, should the art ever change. The square root on the radius spreads the draws evenly
    //across the disc; without it they crowd towards the middle, which is the very thing being fixed
    //here. The item's own footprint is held back off the edge so nothing is left hanging off the plate.
    private Vector3 RandomPointOnPlate(GameObject food)
    {
        RectTransform plateRect = plate.transform as RectTransform;
        if (plateRect == null)
        {
            return plate.transform.position;
        }
        Vector3[] corners = new Vector3[4];
        plateRect.GetWorldCorners(corners);
        //the corners' own midpoint, so the plate's pivot has no say in where its middle is
        Vector3 centre = (corners[0] + corners[2]) * 0.5f;
        float radius = HalfExtent(plateRect) * plateSpread - HalfExtent(food.transform as RectTransform);
        if (radius <= 0f)
        {
            //a plate no bigger than what is going on it: the middle is the only spot there is
            return centre;
        }
        float angle = Random.value * Mathf.PI * 2f;
        float distance = radius * Mathf.Sqrt(Random.value);
        //stepped out along the plate's own axes, so the spread follows the plate however it is turned
        return centre
            + plateRect.right * (Mathf.Cos(angle) * distance)
            + plateRect.up * (Mathf.Sin(angle) * distance);
    }

    //half the target's shorter side, measured off its drawn corners so it already carries whatever
    //scaling the canvas is under, then trimmed by grabMargin
    private float ToleranceFor(GameObject target)
    {
        return HalfExtent(target.transform as RectTransform) * grabMargin;
    }

    //half the shorter side as drawn, which is zero for anything the canvas has not laid out yet
    private static float HalfExtent(RectTransform rect)
    {
        if (rect == null)
        {
            return 0f;
        }
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float width = Vector3.Distance(corners[0], corners[3]);
        float height = Vector3.Distance(corners[0], corners[1]);
        return Mathf.Min(width, height) * 0.5f;
    }

    private GameDirectorMemories.FoodItem FindWantedFoodItem(string foodTag)
    {
        foreach (GameDirectorMemories.FoodItem foodItem in currentSuspectInfo.foodItems)
        {
            if (foodItem != null && foodItem.remaining > 0 && foodItem.foodItemId == foodTag)
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
        //where this one is going, decided now so the hand can carry it straight there
        carriedFoodTarget = RandomPointOnPlate(food);
        food.transform.SetParent(Hand.transform, true);
        food.transform.position = new Vector3(food.transform.position.x, food.transform.position.y, 1);
        //Drawn behind the hand art but in front of the board. This canvas is Screen Space - Overlay, so
        //what stacks UI is sibling order, not z: parenting alone appends the food after the art and it
        //covers the fingers. Sending it to the front of the Hand puts the art back over it, and because
        //the Hand is itself the last child of the canvas the food still clears every unserved item.
        food.transform.SetAsFirstSibling();
    }

    private void DropCarriedFoodOnPlate()
    {
        //reached the plate, so the food stops being a child of the hand and becomes one of the plate,
        //which is also what lets the next suspect start from a clean plate
        carriedFood.transform.SetParent(plate.transform, true);
        //set down exactly on the spot it was carried to. The hand stops as soon as it is within the
        //item's own footprint of it, so without this the last fraction of the approach - and which side
        //it came in from - would still show in where the food ends up sitting.
        carriedFood.transform.position = carriedFoodTarget;
        carriedFood.tag = "Served";
        if (carriedFoodItem != null && carriedFoodItem.remaining > 0)
        {
            carriedFoodItem.remaining--;
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
            if (foodItem == null || foodItem.remaining <= 0 || string.IsNullOrEmpty(foodItem.foodItemId))
            {
                continue;
            }
            //check if the food item is in the scene
            foreach (GameObject foodItemInScene in FindFoodInScene(foodItem.foodItemId))
            {
                //nearest by centre; the hand steers to that same centre, so target choice and arrival agree
                float distance = Vector2.Distance(Hand.transform.position, foodItemInScene.transform.position);
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
            return;
        }
        handRect.anchorMin = new Vector2(0.5f, 0.5f);
        handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(0.5f, 0.5f);
        handRect.anchoredPosition = Vector2.zero;
        handRect.localRotation = Quaternion.identity;
        handRect.localScale = Vector3.one;
    }

    //Slides the whole arm so its drawn fingers sit on the Hand, which is the point everything else
    //measures from. Each prefab parks its art at a different offset (-287,-71 on Baron, -339.7,-66.4 on
    //Henchman), and normalising the root above throws those offsets away, so the shift is worked out
    //from the art itself. Cosmetic only: getting it wrong moves the picture, never the pickup.
    private void AlignFingersToHand(RectTransform handRect)
    {
        RectTransform visual = FindVisualHand(handRect.gameObject);
        if (visual == null)
        {
            Debug.LogWarning("Hand prefab '" + handPrefab.name + "' has no '" + visualHandChildName +
                "' rect, so its fingers cannot be lined up with the hand's grab point.");
            return;
        }
        //Walked up the parents rather than routed through world space. This runs from OnEnable, before
        //the Canvas has driven its own transform for the frame, so a world round trip through it is not
        //trustworthy here. Only the steps between the art and the Hand matter anyway.
        Vector3 fingers = new Vector3(visual.rect.center.x, visual.rect.yMin + fingertipHeight, 0f);
        Transform step = visual;
        while (step != null && step != Hand.transform)
        {
            fingers = step.localRotation * Vector3.Scale(fingers, step.localScale) + step.localPosition;
            step = step.parent;
        }
        if (step == null)
        {
            return;
        }
        handRect.anchoredPosition -= new Vector2(fingers.x, fingers.y);
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

    //Board and hand live on a canvas, so movement is flat: x and y only, z is left alone. The Hand is
    //steered straight at the target because the Hand is the grab point, nothing to compensate for.
    private void MoveHandTowards(Vector3 target)
    {
        Vector3 current = Hand.transform.position;
        Vector2 stepped = Vector2.MoveTowards(current, target, handSpeed * Time.deltaTime);
        Hand.transform.position = new Vector3(stepped.x, stepped.y, current.z);
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
            if (foodItem == null || foodItem.remaining <= 0)
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
            if (entry.foodItem.remaining <= 0)
            {
                Destroy(entry.root);
                foodItemEntries.RemoveAt(i);
                continue;
            }
            if (entry.foodItem.remaining == entry.shownQuantity)
            {
                continue;
            }
            entry.shownQuantity = entry.foodItem.remaining;
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

    //Only conversation1 is spoken for now, conversation2 is authored but nothing reads it yet.
    private void ShowConversation()
    {
        if (ConversationUI == null)
        {
            return;
        }
        //nothing to talk to, so the panel stays out of the way
        if (currentSuspectInfo == null)
        {
            ConversationUI.SetActive(false);
            return;
        }
        ConversationUI.SetActive(true);
        if (conversationText != null)
        {
            conversationText.text = currentSuspectInfo.conversation1;
        }
        BuildSuspectPortrait();
    }

    private void HideConversation()
    {
        ClearSuspectPortrait();
        if (ConversationUI != null)
        {
            ConversationUI.SetActive(false);
        }
    }

    //Crops the suspect's own sprite down to their face inside the portrait frame. The frame clips
    //whatever hangs outside it, so the crop is just a matter of making the sprite far bigger than the
    //frame and sliding the head into the middle of it. The mask is added here rather than authored on
    //the frame so a frame dropped in from the editor crops without anyone having to remember it.
    private void BuildSuspectPortrait()
    {
        ClearSuspectPortrait();
        if (suspectPortrait == null)
        {
            return;
        }
        Sprite face = FindSuspectSprite();
        if (face == null)
        {
            return;
        }
        Rect frame = suspectPortrait.rectTransform.rect;
        if (frame.width <= 0f || frame.height <= 0f)
        {
            //a zero sized frame has no crop to speak of, and every size below would collapse with it
            return;
        }
        if (suspectPortrait.GetComponent<RectMask2D>() == null)
        {
            suspectPortrait.gameObject.AddComponent<RectMask2D>();
        }

        GameObject faceObject = new GameObject(suspectFaceName, typeof(RectTransform), typeof(Image));
        faceObject.layer = suspectPortrait.gameObject.layer;
        RectTransform faceRect = (RectTransform)faceObject.transform;
        faceRect.SetParent(suspectPortrait.transform, false);
        faceRect.anchorMin = new Vector2(0.5f, 0.5f);
        faceRect.anchorMax = new Vector2(0.5f, 0.5f);
        faceRect.pivot = new Vector2(0.5f, 0.5f);

        Image faceImage = faceObject.GetComponent<Image>();
        faceImage.sprite = face;
        faceImage.raycastTarget = false;

        //blown up until the slice of the sprite named by faceHeight is as tall as the frame, keeping
        //the sprite's own proportions, then shifted so facePivot lands in the centre of the frame
        float height = frame.height / Mathf.Max(faceHeight, 0.01f);
        float width = height * (face.rect.width / face.rect.height);
        faceRect.sizeDelta = new Vector2(width, height);
        faceRect.anchoredPosition = new Vector2((0.5f - facePivot.x) * width, (0.5f - facePivot.y) * height);
    }

    private void ClearSuspectPortrait()
    {
        if (suspectPortrait == null)
        {
            return;
        }
        foreach (Transform child in suspectPortrait.transform)
        {
            if (child.name == suspectFaceName)
            {
                Destroy(child.gameObject);
            }
        }
    }

    //the dining room suspect carries its art on a child, so the whole instance is searched
    private Sprite FindSuspectSprite()
    {
        if (currentSuspect == null)
        {
            return null;
        }
        SpriteRenderer renderer = currentSuspect.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    void OnDisable()
    {
        ClearFoodItemsUI();
        HideConversation();
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
