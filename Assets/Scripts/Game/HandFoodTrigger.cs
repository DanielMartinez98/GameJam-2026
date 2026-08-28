using UnityEngine;

//Sits on the Hand in the scene. The minigame lives on the canvas root, so the hand's own trigger
//box reports what it is overlapping through here.
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class HandFoodTrigger : MonoBehaviour
{
    [SerializeField] private CharcuterieBoardMinigame minigame;

    private void Reset()
    {
        minigame = GetComponentInParent<CharcuterieBoardMinigame>(true);
    }

    private void Awake()
    {
        if (minigame == null)
        {
            minigame = GetComponentInParent<CharcuterieBoardMinigame>(true);
        }
        if (minigame == null)
        {
            Debug.LogError("HandFoodTrigger found no CharcuterieBoardMinigame above it, the hand cannot pick anything up.");
        }
        EnforceContactSetup();
    }

    //The food carries plain solid boxes and no body of its own, so to 2D physics it is static.
    //A kinematic body ignores static colliders unless it is told to report full contacts, and without
    //that the hand slides straight through the food and no callback is ever raised. Forced here rather
    //than left to the inspector so the pickup cannot quietly stop working.
    private void EnforceContactSetup()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.useFullKinematicContacts = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }
        Collider2D box = GetComponent<Collider2D>();
        if (box != null)
        {
            box.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Report(other);
    }

    //Enter alone is not enough: the hand can already be sitting inside the plate's box when it picks
    //food up, and an overlap that started before the board opened never fires Enter at all.
    private void OnTriggerStay2D(Collider2D other)
    {
        Report(other);
    }

    private void Report(Collider2D other)
    {
        if (minigame != null && other != null)
        {
            minigame.HandTouched(other.gameObject);
        }
    }
}
