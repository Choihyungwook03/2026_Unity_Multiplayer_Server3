using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour
{
    public UnityEvent Onpulled;

    private bool isUsed = false;

    private void Update()
    {
        if (isUsed) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            isUsed = true;
            Debug.Log("레버를 당겼다.");
            Onpulled.Invoke();
        }
    }
}
