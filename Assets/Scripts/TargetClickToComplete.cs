using UnityEngine;
using UnityEngine.InputSystem;

public class TargetClickToComplete : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Camera mainCam;
    public float maxDistance = 200f;

    [Header("Trial State")]
    public bool trialRunning = true;

    private void Awake()
    {
        if (mainCam == null) mainCam = Camera.main;
    }

    void Update()
    {
        if (!trialRunning) return;

        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            Debug.Log($"[Click] Hit collider: {hit.collider.name}");

            // ✅ works even if collider is on a child
            RabbitTarget rabbit = hit.collider.GetComponentInParent<RabbitTarget>();
            if (rabbit != null)
            {
                Debug.Log("[Trial] Rabbit hit!");
                rabbit.TryHit();   // you add this method to RabbitTarget (see below)
            }
        }
    }
}
