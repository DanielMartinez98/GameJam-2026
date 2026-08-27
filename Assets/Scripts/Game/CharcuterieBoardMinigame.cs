using UnityEngine;

public class CharcuterieBoardMinigame : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject plate;
    [SerializeField] private GameObject[] Hands;
    [SerializeField] private GameObject Hand;
    [SerializeField] private GameObject[] foodItems;
    private PlayerControlsDiningRoom playerControls;
    private GameObject currentSuspect;

    // This method is needed to set the current suspect from the PlayerControlsDiningRoom script
    public void SetCurrentSuspect(GameObject suspect)
    {
        currentSuspect = suspect;
        //make a switch statement to set the plate and hands based on the suspect's name, set the plate to active and fi
        switch (currentSuspect.name)
        {
            case "Chief":
                Hands[0].SetActive(true);
                break;
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
        ResolvePlayerControls();
        if (playerControls != null)
        {
            SetCurrentSuspect(playerControls.GetCurrentSuspect());
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            //stop the minigame and return to the dining room, set the suspect to null in the player controls script
            playerControls.CloseCharcuterieBoard();
        }
    }
}
