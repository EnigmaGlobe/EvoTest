using UnityEngine;
using UnityEngine.InputSystem;

public class ExperimentConditionToggle_E3 : MonoBehaviour
{
    [Header("References (drag in Inspector)")]
    public Material e3Material;      // the same material used by your Fullscreen Pass
    public RabbitSpawner rc;         // drag your RabbitSpawner in Inspector

    [Header("Baseline (Control)")]
    [Range(0f, 1f)] public float baselineSuppression = 0f;

    [Header("E3 Condition (Experimental)")]
    [Range(0f, 1f)] public float e3Suppression = 0.7f;

    [Header("Greentint sequence (per round end)")]
    public float[] greentintSteps = new float[] { 0f, 0.3f, -0.3f };

    [Header("Optional: how strong the tint is applied")]
    [Range(0f, 1f)] public float greenTintStrength = 0.4f;  // matches your shader default

    [Header("Keybind")]
    public Key toggleKey = Key.E;

    private bool e3Enabled = false;
    private int tintIndex = 0;

    // Shader property names must match Shader Graph Blackboard "Reference"
    private static readonly int SuppressionStrengthID = Shader.PropertyToID("_SuppressionStrength");
    private static readonly int GreentintID           = Shader.PropertyToID("_Greentint");
    private static readonly int GreenTintStrengthID   = Shader.PropertyToID("_GreenTintStrength");

    // void OnEnable()
    // {
    //     if (rc != null)
    //         rc.OnRoundEnded += HandleRoundEnded;
    // }

    // void OnDisable()
    // {
    //     if (rc != null)
    //         rc.OnRoundEnded -= HandleRoundEnded;
    // }

    // void Start()
    // {
    //     if (e3Material == null)
    //     {
    //         Debug.LogError("[E3 Toggle] Missing e3Material. Drag your fullscreen material here.");
    //         enabled = false;
    //         return;
    //     }

    //     // Start in baseline + greentint step 0
    //     tintIndex = 0;
    //     ApplyBaseline();
    //     ApplyTintStep(tintIndex);

    //     Debug.Log("[E3 Toggle] Started BASELINE. Press E to toggle. Greentint starts at 0.");
    // }

    // void Update()
    // {
    //     if (Keyboard.current == null) return;

    //     if (Keyboard.current[toggleKey].wasPressedThisFrame)
    //     {
    //         e3Enabled = !e3Enabled;
    //         if (e3Enabled) ApplyE3();
    //         else ApplyBaseline();

    //         Debug.Log($"[E3 Toggle] Condition={(e3Enabled ? "E3_ON" : "BASELINE")} " +
    //                   $"Suppression={e3Material.GetFloat(SuppressionStrengthID):0.00} " +
    //                   $"Greentint={e3Material.GetFloat(GreentintID):0.00}");
    //     }
    // }

    // private void HandleRoundEnded(int roundIndex)
    // {
    //     if (greentintSteps == null || greentintSteps.Length == 0) return;

    //     // Move to next tint step each round end
    //     tintIndex = (tintIndex + 1) % greentintSteps.Length;
    //     ApplyTintStep(tintIndex);

    //     Debug.Log($"[E3 Toggle] Round ended. Set Greentint -> {greentintSteps[tintIndex]:0.00}");
    // }

    // private void ApplyTintStep(int idx)
    // {
    //     float tint = greentintSteps[Mathf.Clamp(idx, 0, greentintSteps.Length - 1)];
    //     e3Material.SetFloat(GreentintID, tint);
    //     e3Material.SetFloat(GreenTintStrengthID, greenTintStrength);
    // }

    // private void ApplyBaseline()
    // {
    //     e3Material.SetFloat(SuppressionStrengthID, baselineSuppression);
    // }

    // private void ApplyE3()
    // {
    //     e3Material.SetFloat(SuppressionStrengthID, e3Suppression);
    // }
}
