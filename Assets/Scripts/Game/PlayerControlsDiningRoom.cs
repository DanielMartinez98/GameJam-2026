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
    private Animator animator;
    //The player has one visual: the animated rig. Standing still is a state inside its own controller
    //(idle <-> Walk on the IsWalking bool), not a second object swapped in for it.
    private GameObject model;
    private bool isMoving;
    private Vector3 modelBaseScale;
    private Vector3 modelBasePosition;
    private bool facingRight = true;

    [Header("Collision")]
    //The player is walked by hand rather than handed to the physics engine, so it carries neither a
    //Rigidbody nor a collider: the suspects and the refill station are non-kinematic bodies, and a
    //player with a body of its own would shove them across the room every time it brushed past. The
    //room is read instead - a capsule of these dimensions is swept along the step about to be taken,
    //and the step is cut short at whatever it runs into.
    [SerializeField] private float bodyRadius = 2f;
    [SerializeField] private float bodyHeight = 18f;
    [SerializeField] private LayerMask blockingLayers = ~0;
    //left between the capsule and whatever stopped it, so next frame's sweep starts clear of the
    //surface instead of exactly on it, where it would read as already overlapping
    [SerializeField] private float skinWidth = 0.05f;
    private readonly RaycastHit[] sweepHits = new RaycastHit[16];
    // Update is called once per frame
    private void Start()
    {
        //the player is sorted against the suspects from where they stand, the same as the suspects are
        if (GetComponent<CharacterDepthSort>() == null)
        {
            gameObject.AddComponent<CharacterDepthSort>();
        }
        CacheModels();
        try
        {
            suspects = GameObject.FindGameObjectsWithTag("Suspect");
        }
        catch
        {
            suspects = new GameObject[0];
        }
    }

    private void CacheModels()
    {
        foreach (Transform child in transform)
        {
            Animator childAnimator = child.GetComponent<Animator>();
            if (childAnimator != null)
            {
                animator = childAnimator;
                model = child.gameObject;
            }
            else if (child.GetComponent<SpriteRenderer>() != null)
            {
                //The stand-in sprite from before the rig was animated. It sits at a different local
                //offset to the rig, so showing it in the rig's place threw the character across the
                //room on every step taken and every step stopped. The rig idles on its own now.
                child.gameObject.SetActive(false);
            }
        }
        if (model != null)
        {
            model.SetActive(true);
            modelBaseScale = model.transform.localScale;
            modelBasePosition = model.transform.localPosition;
        }
        isMoving = false;
        SetWalking(false);
    }

    //Flip the model to face the way it is walking. The rig is 2D sprites, so facing left is a mirror on
    //X: turning it 180 on Y would put the backs of the sprites to the camera and swing the rig, which
    //is pitched 25 degrees towards it, through the floor on the way round.
    //The mirror has to take the rig's position with it. The rig pivot is not the middle of the art -
    //the character hangs to one side of it, which is why the rig sits at a local x of ~12 to stand
    //centred on the player at all - so negating the scale alone swings the art out to the far side of
    //that pivot. Negating the local x as well mirrors the rig about the player's own centre, which
    //leaves the character standing exactly where it was and merely facing the other way.
    private void FaceDirection(bool right)
    {
        if (right == facingRight || model == null) return;
        facingRight = right;
        float mirror = right ? 1f : -1f;
        Vector3 scale = model.transform.localScale;
        scale.x = modelBaseScale.x * mirror;
        model.transform.localScale = scale;
        Vector3 position = model.transform.localPosition;
        position.x = modelBasePosition.x * mirror;
        model.transform.localPosition = position;
    }

    private void SetWalking(bool walking)
    {
        if (animator != null) animator.SetBool("IsWalking", walking);
    }
    void Update()
    {
        //basic wasd movement but up to a limit of -29 and 29 and in the z axis it should also be locked and staggered to 17.97
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        //the player root never turns - only the model inside it mirrors - so a world space step and the
        //Translate this replaces come to the same thing, minus the walking through walls
        MoveWithCollisions(new Vector3(
            moveHorizontal,
            0,
            moveVertical
        ) * Time.deltaTime * 30f);

        //Read the raw keys for the animation, not the smoothed axes: those coast back through zero when
        //the player turns around, which reads as a frame or two of standing still in the middle of a
        //turn and makes the walk cycle stutter every time direction changes.
        float rawHorizontal = Input.GetAxisRaw("Horizontal");
        float rawVertical = Input.GetAxisRaw("Vertical");

        //drive the walk/idle state from whether the player is actually giving movement input
        bool isWalking = Mathf.Abs(rawHorizontal) > 0.01f || Mathf.Abs(rawVertical) > 0.01f;
        if (isWalking != isMoving)
        {
            isMoving = isWalking;
            SetWalking(isWalking);
        }

        //face the way we are moving horizontally, keeping the last facing while moving straight up/down
        if (rawHorizontal > 0.01f) FaceDirection(true);
        else if (rawHorizontal < -0.01f) FaceDirection(false);

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
            if (Vector3.Distance(transform.position, suspect.transform.position) < 7f && !gameDirector.GetComponent<GameDirectorMemories>().IsSuspectServed(suspect))
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

    //Walks the requested step, stopping at the first thing in the way and carrying whatever is left of
    //it along that surface, so walking into a wall at an angle slides along the wall rather than
    //sticking to it. Three slides cover a room of flat walls and the corners between them; past that,
    //stopping dead is the answer anyway.
    private void MoveWithCollisions(Vector3 motion)
    {
        //the room is walked on the flat, and nothing here should ever lift the player off it
        motion.y = 0f;
        if (motion.sqrMagnitude < 0.0000001f)
        {
            return;
        }
        Vector3 position = transform.position;
        for (int slide = 0; slide < 3 && motion.sqrMagnitude > 0.0000001f; slide++)
        {
            float distance = motion.magnitude;
            Vector3 direction = motion / distance;
            Vector3 bottom;
            Vector3 top;
            GetCapsule(position, out bottom, out top);
            RaycastHit hit;
            if (!SweepCapsule(bottom, top, direction, distance + skinWidth, out hit))
            {
                position += motion;
                break;
            }
            //A hit at zero distance means the capsule is already inside something, and a sweep that
            //starts inside a collider has no surface to stop against - blocking on it would pin the
            //player there for good. Let the step through instead, so whatever they are stuck in can be
            //walked back out of, and collision picks up again the moment they are clear.
            if (hit.distance <= 0f)
            {
                position += motion;
                break;
            }
            float travel = Mathf.Max(hit.distance - skinWidth, 0f);
            position += direction * travel;
            //flatten the surface being slid along: a wall that is modelled with any tilt to it should
            //still read as a plain upright barrier, not as a ramp that walks the player up or down it
            Vector3 normal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (normal.sqrMagnitude < 0.0000001f)
            {
                break;
            }
            motion = Vector3.ProjectOnPlane(motion - direction * travel, normal.normalized);
            motion.y = 0f;
        }
        transform.position = position;
    }

    //Sweeps for the nearest thing in the way that is not the player themselves. The player object
    //carries a box collider of its own, and the sweeping capsule stands in the same place: a plain
    //CapsuleCast reports that box as an overlap on the very first step and nothing ever gets anywhere.
    //Everything under the player's transform is skipped, and the nearest of what is left is the answer.
    private bool SweepCapsule(Vector3 bottom, Vector3 top, Vector3 direction, float distance, out RaycastHit nearest)
    {
        nearest = default(RaycastHit);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, bodyRadius, direction, sweepHits, distance, blockingLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider other = sweepHits[i].collider;
            if (other == null || other.transform.IsChildOf(transform))
            {
                continue;
            }
            if (!found || sweepHits[i].distance < nearest.distance)
            {
                nearest = sweepHits[i];
                found = true;
            }
        }
        return found;
    }

    //The shape the room is read with: a capsule standing on the transform's own position, which is
    //where the character's feet are. It is not the player's own collider and does not have to match it;
    //nothing is ever moved by physics here, so this is only ever asking the room a question.
    private void GetCapsule(Vector3 position, out Vector3 bottom, out Vector3 top)
    {
        bottom = position + Vector3.up * bodyRadius;
        top = position + Vector3.up * Mathf.Max(bodyHeight - bodyRadius, bodyRadius);
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
            return FloorDistance(refillStation.transform.position) < refillRange;
        }
        return FloorDistance(refillStationCollider.ClosestPoint(transform.position)) < refillRange;
    }

    //Distance across the floor, with the height between the two thrown away. The station is a counter
    //standing at about chest height while the player's transform is down at their feet, so a straight
    //line between the two is mostly the climb: the underside of the station's collider alone sits about
    //3.4 above the player, which is further than refillRange, and the prompt could never appear however
    //close they were standing. Being at the station is about where the player is standing on the floor,
    //not about how tall the thing they are standing at happens to be.
    private float FloorDistance(Vector3 point)
    {
        return new Vector2(point.x - transform.position.x, point.z - transform.position.z).magnitude;
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
