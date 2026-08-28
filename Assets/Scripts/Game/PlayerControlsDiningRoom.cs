using UnityEngine;

public class PlayerControlsDiningRoom : MonoBehaviour
{
    [SerializeField] private GameObject camera;
    private GameObject[] suspects;
    [SerializeField] private float[] cameraMaxLimits = new float[4] { -28f, 10f, -10f, 4f };
    [SerializeField] private float[] playerMaxLimits = new float[4] { -50f, 33f, -10f, 20f };
    [SerializeField] private float cameraFollowSpeed = 5f;
    [SerializeField] private float playerMoveSpeed = 5f;
    [SerializeField] private GameObject popup;
    [SerializeField] private GameObject focusLight;
    [SerializeField] private GameObject CharcuterieBoard;
    [SerializeField] private GameObject gameDirector;
    [SerializeField] private GameObject currentSuspect;
    //The board is served from and refilled at two different places in the room, so the same screen is
    //opened in one of two modes: the suspect's own charcuterie run, or the refill station's.
    [SerializeField] private GameObject refillStation;
    [SerializeField] private float refillRange = 3f;
    private bool isCameraFollowingPlayer = true;
    private bool atRefillStation;
    private CharcuterieBoardMinigame serveMinigame;
    private RefillStationMinigame refillMinigame;
    private Collider refillStationCollider;
    private int boardClosedFrame = -1;
    // Update is called once per frame
    private void Start()
    {
        try
        {
            suspects = GameObject.FindGameObjectsWithTag("Suspect");
        }
        catch
        {
            suspects = new GameObject[0];
        }
    }
    void Update()
    {
        //basic wasd movement but up to a limit of -29 and 29 and in the z axis it should also be locked and staggered to 17.97
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        transform.Translate(new Vector3(
            moveHorizontal,
            0,
            moveVertical
        ) * Time.deltaTime * 30f);

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, playerMaxLimits[0], playerMaxLimits[1]);
        position.z = Mathf.Clamp(position.z, playerMaxLimits[2], playerMaxLimits[3]);
        transform.position = position;
        //if the player is within 5 units of a suspect the camera should focus on the suspect instead of the player, until the player is no longer within 5 units of the suspect, then the camera should follow the player again
        //find the nearest suspect in range first, so the state does not depend on the order of the array
        //check if the suspect has already been served in the gameDirector's memories, if so, do not focus on that suspect
        currentSuspect = null;
        foreach (GameObject suspect in suspects)
        {
            if (Vector3.Distance(transform.position, suspect.transform.position) < 5f && !gameDirector.GetComponent<GameDirectorMemories>().IsSuspectServed(suspect))
            {
                currentSuspect = suspect;
                break;
            }
        }
        isCameraFollowingPlayer = currentSuspect == null;
        //the station is only offered when nobody is waiting to be served, so one prompt is on screen
        //at a time and the suspect in front of the player always comes first
        atRefillStation = currentSuspect == null && IsAtRefillStation();

        if (currentSuspect != null)
        {
            popup.SetActive(true);
            popup.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text = "Press E to Serve food to " + currentSuspect.name;
            focusLight.SetActive(true);
            //move the focus light to the suspect's position but 25 units above the suspect
            focusLight.transform.position = currentSuspect.transform.position + new Vector3(0, 25, 0);
            camera.transform.position = new Vector3(
                Mathf.Clamp(currentSuspect.transform.position.x, cameraMaxLimits[0], cameraMaxLimits[1]),
                camera.transform.position.y,
                -34.36f + Mathf.Clamp(currentSuspect.transform.position.z, cameraMaxLimits[2], cameraMaxLimits[3])
            );
        }
        else
        {
            popup.SetActive(atRefillStation);
            if (atRefillStation)
            {
                popup.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text = "Press E to refill the charcuterie board";
            }
            focusLight.SetActive(false);
            //the camera should follow the player but up to a limit of -29 and 29 and in the z axis it should also be locked and staggered to -34.36
            camera.transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, cameraMaxLimits[0], cameraMaxLimits[1]),
                camera.transform.position.y,
                -34.36f + Mathf.Clamp(transform.position.z, cameraMaxLimits[2], cameraMaxLimits[3])
            );
        }

        if(Input.GetKeyDown(KeyCode.E))
        {
            //the E that just closed the board must not walk straight back into it, and neither must an
            //E the player is pressing inside a screen that is already open
            if(CharcuterieBoard.activeSelf || Time.frameCount == boardClosedFrame) return;
            if(currentSuspect != null)
            {
                print("Serving food to " + currentSuspect.name);
                OpenCharcuterieBoard(false);
            }
            else if(atRefillStation)
            {
                OpenCharcuterieBoard(true);
            }
        }
    }

    //Measured to the station's own collider rather than to its middle: it is a wide slab, and a player
    //standing at one end of it is as much at it as one standing in the centre.
    private bool IsAtRefillStation()
    {
        if(refillStation == null || !refillStation.activeInHierarchy)
        {
            return false;
        }
        if(refillStationCollider == null)
        {
            refillStationCollider = refillStation.GetComponent<Collider>();
        }
        if(refillStationCollider == null)
        {
            return Vector3.Distance(transform.position, refillStation.transform.position) < refillRange;
        }
        return Vector3.Distance(transform.position, refillStationCollider.ClosestPoint(transform.position)) < refillRange;
    }

    //One canvas, two minigames. Whichever component is left enabled is the one whose OnEnable runs when
    //the board comes up, so exactly one of them ever has the screen.
    private void OpenCharcuterieBoard(bool refilling)
    {
        if(serveMinigame == null)
        {
            serveMinigame = CharcuterieBoard.GetComponent<CharcuterieBoardMinigame>();
        }
        if(refillMinigame == null)
        {
            refillMinigame = CharcuterieBoard.GetComponent<RefillStationMinigame>();
        }
        if(serveMinigame != null)
        {
            serveMinigame.enabled = !refilling;
        }
        if(refillMinigame != null)
        {
            refillMinigame.enabled = refilling;
        }
        CharcuterieBoard.SetActive(true);
    }

    public void CloseCharcuterieBoard()
    {
        CharcuterieBoard.SetActive(false);
        //remembered so the same key press cannot reopen it further down the frame
        boardClosedFrame = Time.frameCount;
    }
    public GameObject GetCurrentSuspect()
    {
        return currentSuspect;
    }
    public void findSuspects()
    {
        try
        {
            suspects = GameObject.FindGameObjectsWithTag("Suspect");
        }
        catch
        {
            suspects = new GameObject[0];
        }
    }
}
