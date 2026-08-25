using UnityEngine;

public class PlayerControlsDiningRoom : MonoBehaviour
{
    [SerializeField] private GameObject camera;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
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
        position.x = Mathf.Clamp(position.x, -50f, 33f);
        position.z = Mathf.Clamp(position.z, -10f, 20f);
        transform.position = position;

        //the camera should follow the player but up to a limit of -29 and 29 and in the z axis it should also be locked and staggered to -34.36

        camera.transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, -28f, 10f),
            camera.transform.position.y,
            -34.36f + Mathf.Clamp(transform.position.z, -10f, 4f)
        );
    }
}
