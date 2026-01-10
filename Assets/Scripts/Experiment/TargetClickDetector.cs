using UnityEngine;

public class TargetClickDetector : MonoBehaviour
{
    [HideInInspector] public ExperimentController_E3 controller;

    private void OnMouseDown()
    {
        // OnMouseDown works for desktop testing with a Camera + Collider.
        // For VR later we’ll swap this to XR ray interactor.
        if (controller != null)
            controller.NotifyTargetHit();
    }
}
