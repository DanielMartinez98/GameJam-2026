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
    private GameObject currentSuspect;
    private bool isCameraFollowingPlayer = true;
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
        foreach (GameObject suspect in suspects)
        {
            if (Vector3.Distance(transform.position, suspect.transform.position) < 5f)
            {
                currentSuspect = suspect;
                popup.SetActive(true);
                popup.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text = "Press E to talk to " + suspect.name;
                focusLight.SetActive(true);
                //move the focus light to the suspect's position but 25 units above the suspect
                focusLight.transform.position = suspect.transform.position + new Vector3(0, 25, 0);
                camera.transform.position = new Vector3(
                    Mathf.Clamp(suspect.transform.position.x, cameraMaxLimits[0], cameraMaxLimits[1]),
                    camera.transform.position.y,
                    -34.36f + Mathf.Clamp(suspect.transform.position.z, cameraMaxLimits[2], cameraMaxLimits[3])
                    
                );
                isCameraFollowingPlayer = false;
                return;
            }
            currentSuspect = null;
            popup.SetActive(false);
            focusLight.SetActive(false);
            isCameraFollowingPlayer = true;
        }   
        //the camera should follow the player but up to a limit of -29 and 29 and in the z axis it should also be locked and staggered to -34.36
        if(isCameraFollowingPlayer)
        {
            camera.transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, cameraMaxLimits[0], cameraMaxLimits[1]),
                camera.transform.position.y,
                -34.36f + Mathf.Clamp(transform.position.z, cameraMaxLimits[2], cameraMaxLimits[3])
            );
        }
    }
}
