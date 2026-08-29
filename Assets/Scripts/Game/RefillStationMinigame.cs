using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//The other half of the charcuterie screen. Serving takes food off the board and the suspect's hand does
//all the moving; here the player does it themselves, dragging food off a row of plates that never run
//out and putting it wherever they like. Both minigames sit on the one canvas because it is the one
//board: what is laid out here is exactly what the next suspect can be served from. Whichever of the two
//components is left enabled owns the screen, so only one of them ever runs.
//Rearranging the board is only possible from this screen. The serving screen never hands the player a
//food item at all, so there is nothing there to rearrange with.
public class RefillStationMinigame : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private RectTransform board;// the tray the food sits on, the same one the suspects are served from
    [SerializeField] private GameObject refillPanel;// an empty rect in the editor, the plates are built inside it
    [SerializeField] private GameObject[] hiddenWhileRefilling;// the serving screen's own pieces, which have no part in this
    //A board only holds so much food. Rearranging never changes the count so it is never refused, only
    //taking something new off a plate can be.
    [SerializeField] private int boardCapacity = 20;
    [SerializeField] private GameObject[] foodPrefabs;// one plate is built per prefab, each an endless supply of it
    [SerializeField] private Sprite plateSprite;
    [SerializeField] private Vector2 plateSize = new Vector2(150f, 150f);
    [SerializeField] private Vector2 plateSpacing = new Vector2(24f, 12f);
    [SerializeField, Range(0.2f, 1f)] private float plateFoodScale = 0.62f;
    [SerializeField] private float headerHeight = 74f;
    [SerializeField] private string hint = "Click and hold on a plate to take food, or on food already on the board to move it. Drop it anywhere off the board to get rid of it. Press E to leave.";
    //Refusing to hand over food is the one thing this screen does that the player cannot see the reason
    //for, so it says so along the bottom rather than just ignoring the click.
    [SerializeField] private string boardFullMessage = "The board is full. Take something off it before adding more.";
    [SerializeField] private float boardFullMessageSeconds = 2.5f;
    [SerializeField] private float boardFullMessageHeight = 90f;

    private PlayerControlsDiningRoom playerControls;
    private Canvas canvas;
    private TextMeshProUGUI capacityText;
    private TextMeshProUGUI boardFullText;
    //when the message stops showing; unscaled so a paused game would still clear it
    private float boardFullHideTime;
    private RectTransform plateGrid;
    //what each built plate hands out, so a right click on one knows what to make
    private readonly Dictionary<GameObject, GameObject> plateFood = new Dictionary<GameObject, GameObject>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private RectTransform draggedFood;
    private bool draggedFromPlate;
    //which button started the drag, so letting go of that same button is what drops the food
    private int draggingButton = -1;
    private Vector3 draggedFrom;
    private int shownCount = -1;
    private int openedFrame = -1;

    // This screen starts inactive, so the first SetActive(true) runs OnEnable before Start has ever run.
    private void ResolvePlayerControls()
    {
        if (playerControls == null && player != null)
        {
            playerControls = player.GetComponent<PlayerControlsDiningRoom>();
        }
    }

    void Start()
    {
        ResolvePlayerControls();
    }

    void OnEnable()
    {
        openedFrame = Time.frameCount;
        ResolvePlayerControls();
        SetServingPiecesActive(false);
        if (refillPanel != null)
        {
            refillPanel.SetActive(true);
        }
        BuildPlates();
    }

    void OnDisable()
    {
        CancelDrag();
        ClearPlates();
        if (refillPanel != null)
        {
            refillPanel.SetActive(false);
        }
        //handed back exactly as they were found, the serving screen expects to have them
        SetServingPiecesActive(true);
    }

    void Update()
    {
        //the E that opened this screen is still down on the frame it opens, so nothing is read yet
        if (Time.frameCount == openedFrame)
        {
            return;
        }
        //Either button picks food up. Right click is what this screen has always asked for, but dragging
        //with the left is what a player reaches for first, and nothing else on this screen wants a click.
        if (draggedFood == null)
        {
            if (Input.GetMouseButtonDown(0))
            {
                draggingButton = 0;
                BeginDrag();
            }
            else if (Input.GetMouseButtonDown(1))
            {
                draggingButton = 1;
                BeginDrag();
            }
        }
        if (draggedFood != null)
        {
            DragTo(Input.mousePosition);
            //asked as "is the button still held" rather than "was it released", so a click that goes
            //down and up inside one frame still lets go of the food
            if (draggingButton < 0 || !Input.GetMouseButton(draggingButton))
            {
                EndDrag();
            }
        }
        RefreshCapacity();
        ExpireBoardFullMessage();
        if (Input.GetKeyDown(KeyCode.E) && draggedFood == null && playerControls != null)
        {
            playerControls.CloseCharcuterieBoard();
        }
    }

    private void SetServingPiecesActive(bool active)
    {
        if (hiddenWhileRefilling == null)
        {
            return;
        }
        foreach (GameObject piece in hiddenWhileRefilling)
        {
            if (piece != null)
            {
                piece.SetActive(active);
            }
        }
    }

    //Right click picks something up: either a fresh piece of food off a plate, or a piece already on
    //the board to move it somewhere else.
    private void BeginDrag()
    {
        GameObject hit = RaycastUI(Input.mousePosition);
        if (hit == null)
        {
            return;
        }
        GameObject foodPrefab = FindPlateFood(hit);
        //TEMPORARY diagnostic - delete once the plate/board pattern is understood
        Debug.Log("[Refill] DOWN hit=" + hit.name
            + " parent=" + (hit.transform.parent != null ? hit.transform.parent.name : "none")
            + " | resolvedPlate=" + (foodPrefab != null ? foodPrefab.name : "no")
            + " | resolvedBoardFood=" + (FindBoardFood(hit) != null ? FindBoardFood(hit).name : "no")
            + " | boardChildren=" + board.childCount
            + " | count=" + CountBoardItems() + "/" + boardCapacity);
        if (foodPrefab != null)
        {
            //the plate never runs out, only the board fills up
            if (CountBoardItems() >= boardCapacity)
            {
                ShowBoardFullMessage();
                return;
            }
            GameObject food = Instantiate(foodPrefab, board, false);
            food.name = food.name.Replace("(Clone)", "").Trim();
            PickUp((RectTransform)food.transform, true);
            return;
        }
        RectTransform boardFood = FindBoardFood(hit);
        if (boardFood != null)
        {
            //rearranging, so the board gains nothing and capacity has no say in it
            PickUp(boardFood, false);
        }
    }

    private void PickUp(RectTransform food, bool fromPlate)
    {
        draggedFood = food;
        draggedFromPlate = fromPlate;
        draggedFrom = food.position;
        //Carried inside the board rather than lifted out onto the canvas. The board is where the food was
        //drawn and where it is going, and leaving it there means it keeps rendering exactly as it did
        //sitting still - lifting it out was what made it vanish for the trip. Last sibling so it rides
        //over the rest of the food instead of under it.
        food.SetParent(board, true);
        food.SetAsLastSibling();
        DragTo(Input.mousePosition);
    }

    private void DragTo(Vector2 screenPoint)
    {
        Vector3 world;
        //put on the board's own plane, so where the cursor is and where the food lands are the same
        //point however the canvas has been scaled to the screen
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(board, screenPoint, UICamera(), out world))
        {
            draggedFood.position = world;
        }
    }

    private void EndDrag()
    {
        //TEMPORARY diagnostic - delete alongside the one in BeginDrag
        Debug.Log("[Refill] UP   food=" + (draggedFood != null ? draggedFood.name : "null")
            + " fromPlate=" + draggedFromPlate
            + " | onBoard=" + FoodIsOnBoard(draggedFood)
            + " -> " + (FoodIsOnBoard(draggedFood) ? "KEPT" : "DELETED")
            + " | localPos=" + (draggedFood != null && board != null
                ? board.InverseTransformPoint(draggedFood.position).ToString() : "n/a")
            + " | boardRect=" + (board != null ? board.rect.ToString() : "n/a"));
        if (FoodIsOnBoard(draggedFood))
        {
            //left exactly where the player let go of it
            draggedFood.SetAsLastSibling();
        }
        else
        {
            //Off the board is a discard, the plates included. Food only exists on the board, so letting
            //go of a piece anywhere else is how it is taken back off - whether it came from a plate a
            //moment ago or had been sitting on the board all along.
            Destroy(draggedFood.gameObject);
        }
        draggedFood = null;
        draggingButton = -1;
    }

    //leaving mid drag must not strand a piece halfway: one taken off a plate never made it onto the
    //board, and one already on the board has a spot of its own to go back to
    private void CancelDrag()
    {
        if (draggedFood == null)
        {
            return;
        }
        if (draggedFromPlate)
        {
            Destroy(draggedFood.gameObject);
        }
        else
        {
            //walking out mid drag is not a discard, so it goes back where it was picked up from
            draggedFood.position = draggedFrom;
        }
        draggedFood = null;
        draggingButton = -1;
    }

    //Asked of the food rather than of the cursor, and answered in the board's own local space: the food
    //is a child of the board, so this is a straight transform comparison with no screen or camera
    //conversion in it. Going through screen coordinates is what made a piece dropped squarely on the
    //board read as off it.
    private bool FoodIsOnBoard(RectTransform food)
    {
        if (food == null || board == null)
        {
            return false;
        }
        Vector3 local = board.InverseTransformPoint(food.position);
        return board.rect.Contains(new Vector2(local.x, local.y));
    }

    //what the cursor is over, topmost first, so overlapping food picks the one the player can see
    private GameObject RaycastUI(Vector2 screenPoint)
    {
        if (EventSystem.current == null)
        {
            return null;
        }
        PointerEventData pointer = new PointerEventData(EventSystem.current);
        pointer.position = screenPoint;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, raycastResults);
        return raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
    }

    //the plate art and its label are not raycast targets, so a hit on a plate is the plate itself,
    //but the walk up the parents keeps that from mattering
    private GameObject FindPlateFood(GameObject hit)
    {
        Transform step = hit.transform;
        while (step != null)
        {
            GameObject foodPrefab;
            if (plateFood.TryGetValue(step.gameObject, out foodPrefab))
            {
                return foodPrefab;
            }
            step = step.parent;
        }
        return null;
    }

    //food on the board is whatever sits directly under it, so a hit anywhere inside a piece of food
    //walks up to the piece rather than grabbing whichever part of it was drawn on top
    private RectTransform FindBoardFood(GameObject hit)
    {
        Transform step = hit.transform;
        while (step != null)
        {
            if (step.parent == board)
            {
                return IsFood(step) ? step as RectTransform : null;
            }
            step = step.parent;
        }
        return null;
    }

    //the same rule the board's stock is counted by in GameDirectorMemories: tagged, and not already eaten
    private bool IsFood(Transform item)
    {
        return item != null && !item.CompareTag("Untagged") && !item.CompareTag("Served");
    }

    private int CountBoardItems()
    {
        int count = 0;
        if (board != null)
        {
            foreach (Transform child in board)
            {
                if (IsFood(child))
                {
                    count++;
                }
            }
        }
        //food in hand stays parented to the board for the whole drag, so it is already in the count
        //above - the total does not dip while a piece is in the air, and it is not counted twice either
        return count;
    }

    //builds one plate per food, plus the line that says how full the board is
    private void BuildPlates()
    {
        ClearPlates();
        if (refillPanel == null || board == null || foodPrefabs == null)
        {
            return;
        }
        BuildPanelLayout();
        BuildBoardFullMessage();
        foreach (GameObject foodPrefab in foodPrefabs)
        {
            if (foodPrefab != null)
            {
                plateFood[CreatePlate(foodPrefab)] = foodPrefab;
            }
        }
        shownCount = -1;
        RefreshCapacity();
    }

    //the panel is an empty rect in the editor, so everything inside it is put together here
    private void BuildPanelLayout()
    {
        RectTransform panel = (RectTransform)refillPanel.transform;

        GameObject header = new GameObject("Capacity", typeof(RectTransform), typeof(TextMeshProUGUI));
        header.layer = refillPanel.layer;
        RectTransform headerRect = (RectTransform)header.transform;
        headerRect.SetParent(panel, false);
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, headerHeight);
        capacityText = header.GetComponent<TextMeshProUGUI>();
        capacityText.alignment = TextAlignmentOptions.Top;
        capacityText.fontSize = 26f;
        capacityText.color = Color.white;
        capacityText.raycastTarget = false;

        GameObject grid = new GameObject("Plates", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.layer = refillPanel.layer;
        plateGrid = (RectTransform)grid.transform;
        plateGrid.SetParent(panel, false);
        plateGrid.anchorMin = Vector2.zero;
        plateGrid.anchorMax = Vector2.one;
        plateGrid.pivot = new Vector2(0.5f, 0.5f);
        plateGrid.offsetMin = Vector2.zero;
        //the header sits above the plates rather than over them
        plateGrid.offsetMax = new Vector2(0f, -headerHeight);
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = plateSize;
        layout.spacing = plateSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
    }

    private GameObject CreatePlate(GameObject foodPrefab)
    {
        //the plate is the thing the player grabs from, so it is the only part of this the cursor can hit
        GameObject plate = new GameObject(foodPrefab.name + " Plate", typeof(RectTransform), typeof(Image));
        plate.layer = refillPanel.layer;
        RectTransform plateRect = (RectTransform)plate.transform;
        plateRect.SetParent(plateGrid, false);
        Image plateImage = plate.GetComponent<Image>();
        plateImage.sprite = plateSprite;
        plateImage.preserveAspect = true;
        //without a plate sprite it stays a faint square, which still reads as somewhere to grab from
        plateImage.color = plateSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);

        //a plain Image rather than the food prefab itself: the prefab carries the food's tag, and a
        //tagged copy sitting on a plate would be food the suspect's hand goes looking for
        Sprite foodSprite = FindSprite(foodPrefab);
        GameObject icon = new GameObject("Food", typeof(RectTransform), typeof(Image));
        icon.layer = refillPanel.layer;
        RectTransform iconRect = (RectTransform)icon.transform;
        iconRect.SetParent(plateRect, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = plateSize * plateFoodScale;
        Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = foodSprite;
        iconImage.preserveAspect = true;
        iconImage.enabled = foodSprite != null;
        iconImage.raycastTarget = false;

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = refillPanel.layer;
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.SetParent(plateRect, false);
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 28f);
        TextMeshProUGUI labelText = label.GetComponent<TextMeshProUGUI>();
        labelText.text = foodPrefab.name;
        labelText.alignment = TextAlignmentOptions.Top;
        labelText.fontSize = 20f;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        return plate;
    }

    private Sprite FindSprite(GameObject foodPrefab)
    {
        Image image = foodPrefab.GetComponent<Image>();
        if (image == null)
        {
            image = foodPrefab.GetComponentInChildren<Image>(true);
        }
        return image != null ? image.sprite : null;
    }

    private void ClearPlates()
    {
        plateFood.Clear();
        capacityText = null;
        plateGrid = null;
        //this one hangs off the canvas rather than the panel, so clearing the panel would leave it behind
        if (boardFullText != null)
        {
            Destroy(boardFullText.gameObject);
            boardFullText = null;
        }
        if (refillPanel == null)
        {
            return;
        }
        foreach (Transform child in refillPanel.transform)
        {
            Destroy(child.gameObject);
        }
    }

    //only writes the line when the count actually changed, so this is cheap enough to poll every frame
    private void RefreshCapacity()
    {
        if (capacityText == null)
        {
            return;
        }
        int count = CountBoardItems();
        if (count == shownCount)
        {
            return;
        }
        shownCount = count;
        //a full board still takes rearranging, it just will not take anything new
        string countColour = count >= boardCapacity ? "#FF8A6E" : "#FFFFFF";
        capacityText.text = "<size=130%><color=" + countColour + ">" + count + "</color> / " + boardCapacity +
            " on the board</size>\n" + hint;
    }

    //The message hangs off the canvas root rather than the plate panel, because the panel is up at the
    //top of the screen and a refusal belongs along the bottom, near the board the player is aiming at.
    private void BuildBoardFullMessage()
    {
        GameObject message = new GameObject("Board Full Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        message.layer = gameObject.layer;
        RectTransform rect = (RectTransform)message.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        //held a little off the bottom edge rather than flush against it
        rect.anchoredPosition = new Vector2(0f, 24f);
        //these fields were added to a component that was already sitting in the scene, so Unity can hand
        //them back as zero rather than as the value written above. A zero height is an invisible message.
        rect.sizeDelta = new Vector2(0f, boardFullMessageHeight > 0f ? boardFullMessageHeight : 90f);
        //drawn after the board and the plates, so it is never hidden behind them
        rect.SetAsLastSibling();
        boardFullText = message.GetComponent<TextMeshProUGUI>();
        boardFullText.alignment = TextAlignmentOptions.Bottom;
        boardFullText.fontSize = 30f;
        //the same warning colour the counter turns when the board is full
        boardFullText.color = new Color32(0xFF, 0x8A, 0x6E, 0xFF);
        boardFullText.raycastTarget = false;
        message.SetActive(false);
    }

    //shown on the click that was refused, so the reason arrives with the click that caused it
    private void ShowBoardFullMessage()
    {
        if (boardFullText == null)
        {
            return;
        }
        //same reason as the height above: an empty string or a zero delay would swallow the message
        boardFullText.text = string.IsNullOrEmpty(boardFullMessage)
            ? "The board is full. Take something off it before adding more."
            : boardFullMessage;
        boardFullText.gameObject.SetActive(true);
        //food dragged onto the canvas is parented here too, so the message is lifted back on top
        boardFullText.rectTransform.SetAsLastSibling();
        boardFullHideTime = Time.unscaledTime + (boardFullMessageSeconds > 0f ? boardFullMessageSeconds : 2.5f);
    }

    private void ExpireBoardFullMessage()
    {
        if (boardFullText == null || !boardFullText.gameObject.activeSelf)
        {
            return;
        }
        if (Time.unscaledTime >= boardFullHideTime)
        {
            boardFullText.gameObject.SetActive(false);
        }
    }

    //an overlay canvas takes a null camera, anything else needs the one it renders through
    private Camera UICamera()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }
        return canvas.worldCamera;
    }
}
