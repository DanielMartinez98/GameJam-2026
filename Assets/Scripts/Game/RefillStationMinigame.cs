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
    [SerializeField] private string hint = "Hold right click on a plate to take food and drag it onto the board. Drop it back on the plates to take it off. Press E to leave.";

    private PlayerControlsDiningRoom playerControls;
    private Canvas canvas;
    private TextMeshProUGUI capacityText;
    private RectTransform plateGrid;
    //what each built plate hands out, so a right click on one knows what to make
    private readonly Dictionary<GameObject, GameObject> plateFood = new Dictionary<GameObject, GameObject>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private RectTransform draggedFood;
    private bool draggedFromPlate;
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
        if (Input.GetMouseButtonDown(1))
        {
            BeginDrag();
        }
        if (draggedFood != null)
        {
            DragTo(Input.mousePosition);
            //asked as "is the button still held" rather than "was it released", so a click that goes
            //down and up inside one frame still lets go of the food
            if (!Input.GetMouseButton(1))
            {
                EndDrag();
            }
        }
        RefreshCapacity();
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
        if (foodPrefab != null)
        {
            //the plate never runs out, only the board fills up
            if (CountBoardItems() >= boardCapacity)
            {
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
        //carried on the canvas itself so it is never lost behind the board or under the plates.
        //Keeping the world transform means it neither jumps nor resizes on the way out or back.
        food.SetParent(transform, true);
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
        Vector2 mouse = Input.mousePosition;
        if (OverPlates(mouse))
        {
            //put back where the food comes from, so the board simply does not have it
            Destroy(draggedFood.gameObject);
        }
        else if (RectTransformUtility.RectangleContainsScreenPoint(board, mouse, UICamera()))
        {
            //left exactly where the player let go of it, and back under the board so it is stock again
            draggedFood.SetParent(board, true);
            draggedFood.SetAsLastSibling();
        }
        else if (draggedFromPlate)
        {
            //never reached the board, so nothing came off the plate after all
            Destroy(draggedFood.gameObject);
        }
        else
        {
            //let go over nothing, so the board keeps it where it was picked up from
            draggedFood.SetParent(board, true);
            draggedFood.position = draggedFrom;
        }
        draggedFood = null;
    }

    //leaving mid drag must not strand a food item on the canvas, where it is neither on the board nor
    //back on its plate
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
            draggedFood.SetParent(board, true);
            draggedFood.position = draggedFrom;
        }
        draggedFood = null;
    }

    private bool OverPlates(Vector2 screenPoint)
    {
        RectTransform panel = refillPanel != null ? refillPanel.transform as RectTransform : null;
        return panel != null && RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint, UICamera());
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
        //whatever is in the player's hand either came off the board or is on its way onto it, so the
        //count does not dip while a drag is in the air
        if (draggedFood != null && IsFood(draggedFood))
        {
            count++;
        }
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
