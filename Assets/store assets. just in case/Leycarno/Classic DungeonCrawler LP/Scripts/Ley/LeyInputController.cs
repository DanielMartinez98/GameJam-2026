using UnityEngine;

namespace Scripts.Ley
{
    public class LeyInputController : LeyABehaviour
    {

        [SerializeField] protected LeyController controller;
        private void Update()
        {
            if (!controller)
            {
                Debug.LogWarning("no controller on inputController...");
            }
            
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) controller.Move(1);
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) controller.Move(-1);
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) controller.Move(1, true);
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) controller.Move(-1, true);
            if (Input.GetKey(KeyCode.PageDown) || Input.GetKey(KeyCode.E)) controller.Rotate(true);
            if (Input.GetKey(KeyCode.Delete) || Input.GetKey(KeyCode.Q)) controller.Rotate(false);
        }
    }
}