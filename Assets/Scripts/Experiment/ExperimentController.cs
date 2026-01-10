using UnityEngine;
using UnityEngine.InputSystem;

// Changes fullscreen material params (E3 vs baseline) + changes _Greentint each round end:
// 0  -> 0.3 -> -0.3 -> repeat
public class ExperimentConditionToggle : MonoBehaviour
{
    [Header("References (drag in Inspector)")]
    public Material e3Material;     // MUST be the same material used in your URP Fullscreen Pass feature
    public RabbitSpawner rc;        // drag RabbitSpawner here

    [Header("Baseline (Control)")]
    [Range(0f, 1f)] public float baselineSuppression = 0f;

    [Header("E3 Condition (Experimental)")]
    [Range(0f, 1f)] public float e3Suppression = 0.7f;

    [Header("Greentint per round end")]
    public float[] greentintSteps = new float[] { 0f, 0.3f, -0.3f };

    [Header("Green tint strength (shader _GreenTintStrength)")]
    [Range(0f, 1f)] public float greenTintStrength = 0.4f;

    [Header("Keybind")]
    public Key toggleKey = Key.E;

    private bool e3Enabled = false;
    private int tintIndex = 0;

    // Shader Graph Blackboard "Reference" names:
    private static readonly int SuppressionStrengthID = Shader.PropertyToID("_SuppressionStrength");
    private static readonly int GreentintID           = Shader.PropertyToID("_Greentint");
    private static readonly int GreenTintStrengthID   = Shader.PropertyToID("_GreenTintStrength");

    private void OnEnable()
    {
        if (rc != null) rc.OnRoundEnded += HandleRoundEnded;
    }

    private void OnDisable()
    {
        if (rc != null) rc.OnRoundEnded -= HandleRoundEnded;
    }

    private void Start()
    {
        if (e3Material == null)
        {
            Debug.LogError("[E3 Toggle] Missing e3Material. Drag your fullscreen material here.");
            enabled = false;
            return;
        }

        // Start at tint step 0 (0.0)
        tintIndex = 0;
        ApplyTintStep(tintIndex);

        // Start in baseline
        ApplyBaseline();
        Debug.Log("[E3 Toggle] Started BASELINE. Press E to toggle. Greentint starts at 0.");
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            e3Enabled = !e3Enabled;

            if (e3Enabled) ApplyE3();
            else ApplyBaseline();

            Debug.Log($"[E3 Toggle] Condition={(e3Enabled ? "E3_ON" : "BASELINE")} " +
                      $"Suppression={e3Material.GetFloat(SuppressionStrengthID):0.00} " +
                      $"Greentint={e3Material.GetFloat(GreentintID):0.00}");
        }
    }

    private void HandleRoundEnded(int roundIndex)
    {
        if (e3Material == null) return;
        if (greentintSteps == null || greentintSteps.Length == 0) return;

        tintIndex = (tintIndex + 1) % greentintSteps.Length; // cycles 0 -> 1 -> 2 -> 0 ...
        ApplyTintStep(tintIndex);

        Debug.Log($"[E3 Toggle] Round {roundIndex} ended. Greentint -> {greentintSteps[tintIndex]:0.00}");
    }

    private void ApplyTintStep(int idx)
    {
        float tint = greentintSteps[Mathf.Clamp(idx, 0, greentintSteps.Length - 1)];
        e3Material.SetFloat(GreentintID, tint);
        e3Material.SetFloat(GreenTintStrengthID, greenTintStrength);
    }

    private void ApplyBaseline()
    {
        e3Material.SetFloat(SuppressionStrengthID, baselineSuppression);
    }

    private void ApplyE3()
    {
        e3Material.SetFloat(SuppressionStrengthID, e3Suppression);
    }
}
