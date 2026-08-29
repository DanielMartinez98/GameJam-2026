using UnityEngine;
using UnityEngine.Rendering;

//Who is standing in front of whom in this room is decided by where their feet are, and nothing else.
//Left to itself Unity decides it by how far each sprite's centre is from the camera, which gets this
//room wrong twice over. The camera looks down from well above, so a tall sprite measures as nearer
//than a short one standing right beside it. And art that is offset from the character it belongs to -
//the player's rig sits over five units towards the camera from the player's own feet - measures as
//nearer than the character actually is. Both make a character draw over someone it is standing behind.
//Ordering the sprites from their standing position instead takes sprite height, pivots, art offsets
//and the camera's angle out of it altogether: two characters are compared on the one thing that
//decides the answer, which is where on the floor each of them is.
[DisallowMultipleComponent]
public class CharacterDepthSort : MonoBehaviour
{
    //z runs away from the camera, so the nearer a character stands the higher up it has to be drawn.
    //Scaled before rounding because sorting order is a whole number and characters stand well within
    //a unit of one another.
    private const float OrdersPerUnit = 100f;
    //sorting order is stored as a short, and a character thrown somewhere absurd must not wrap around
    //into the front of the room
    private const int MinOrder = -32768;
    private const int MaxOrder = 32767;

    private SortingGroup sortingGroup;
    private SpriteRenderer[] spriteRenderers;
    private int appliedOrder = int.MinValue;

    private void Awake()
    {
        Collect();
    }

    private void Collect()
    {
        sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup != null)
        {
            //the whole character is one unit to sort, however many pieces the art is in
            return;
        }
        //no group, so every piece is ordered by hand. They all stand in the same place, so they all
        //get the same order and go on sorting among themselves exactly as they did before.
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    //LateUpdate, so this reads the position the character actually ended the frame at rather than
    //whatever it held before it was moved
    private void LateUpdate()
    {
        int order = Mathf.Clamp(Mathf.RoundToInt(-transform.position.z * OrdersPerUnit), MinOrder, MaxOrder);
        if (order == appliedOrder)
        {
            return;
        }
        appliedOrder = order;
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
            return;
        }
        if (spriteRenderers == null)
        {
            return;
        }
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = order;
            }
        }
    }
}
