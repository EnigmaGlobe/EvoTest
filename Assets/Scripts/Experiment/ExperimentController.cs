using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Changes fullscreen material params (E3 vs baseline) + changes _Greentint each round end
public class ExperimentController : MonoBehaviour
{
    [Header("References (drag in Inspector)")]
    public Material e3Material;
    public RabbitSpawner rc;

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

    [Header("Logging IDs")]
    public string formId = "RabbitHunt_V1";
    public string userId = "P001";
    public string envId = "Terrain01";

    [Tooltip("Auto-generated on Start if empty")]
    public string sessionId = "";

    [Header("Captured FormResponses (view in Inspector)")]
    [SerializeField] private List<FormResponse> responses = new List<FormResponse>();
    public List<FormResponse> Responses => responses; // read-only accessor

    private bool e3Enabled = false;
    private int tintIndex = 0;

    private static readonly int SuppressionStrengthID = Shader.PropertyToID("_SuppressionStrength");
    private static readonly int GreentintID = Shader.PropertyToID("_Greentint");
    private static readonly int GreenTintStrengthID = Shader.PropertyToID("_GreenTintStrength");


    public void Submit()
    {
        // Placeholder for submission logic if needed
    }

    private void OnEnable()
    {
        if (rc != null)
        {
            rc.OnRoundEnded += HandleRoundEnded;
            rc.OnFormResponse += HandleFormResponse;
        }
    }

    private void OnDisable()
    {
        if (rc != null)
        {
            rc.OnRoundEnded -= HandleRoundEnded;
            rc.OnFormResponse -= HandleFormResponse;
        }
    }

    private void Start()
    {
        if (e3Material == null)
        {
            Debug.LogError("[ExperimentController] Missing e3Material. Drag your fullscreen material here.");
            enabled = false;
            return;
        }

        if (string.IsNullOrEmpty(sessionId))
            sessionId = Guid.NewGuid().ToString("N");

        // Send IDs down to spawner so it can fill FormResponses
        if (rc != null)
            rc.SetLoggingIds(formId, userId, envId, sessionId);

        // Start at tint step 0
        tintIndex = 0;
        ApplyTintStep(tintIndex);

        // Start in baseline
        ApplyBaseline();
        Debug.Log("[ExperimentController] Started BASELINE. Press E to toggle.");
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            e3Enabled = !e3Enabled;

            if (e3Enabled) ApplyE3();
            else ApplyBaseline();

            Debug.Log($"[ExperimentController] Condition={(e3Enabled ? "E3_ON" : "BASELINE")} " +
                      $"Suppression={e3Material.GetFloat(SuppressionStrengthID):0.00} " +
                      $"Greentint={e3Material.GetFloat(GreentintID):0.00}");
        }
    }

    private void HandleRoundEnded(int roundIndex)
    {
        if (e3Material == null) return;
        if (greentintSteps == null || greentintSteps.Length == 0) return;

        tintIndex = (tintIndex + 1) % greentintSteps.Length;
        ApplyTintStep(tintIndex);

        Debug.Log($"[ExperimentController] Round {roundIndex} ended. Greentint -> {greentintSteps[tintIndex]:0.00}");
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

    // -------------------------
    // Logging capture
    // -------------------------

    private void HandleFormResponse(FormResponse fr)
    {
        if (fr == null) return;

        responses.Add(fr);

        // Optional: keep list from growing forever during long tests
        // if (responses.Count > 5000) responses.RemoveAt(0);
    }

    [ContextMenu("Clear Responses")]
    public void ClearResponses()
    {
        responses.Clear();
    }


}

// -------------------------------------------------
// Data classes (Serializable so Inspector can show)
// -------------------------------------------------

[Serializable]
public class FormResponse
{
    public string formId;
    public string userId;

    public string tokens;
    public string tokenType;
    public bool isAnsCorrect;

    public string questId;
    public string formItemId;
    public string envId;

    public string answer;
    public string answer2;
    public string answer3;
    public string answer4;

    public string answerRes;
    public string answerRes2;
    public string answerRes3;
    public string answerRes4;

    public string elapsedTime;

    public string dialogueId;
    public string interactableId;

    public string sessionId;
    public string sessionID;

    public string category;
    public string battleId;

    public string GPTReply;
    public string npc;
    public string status;

    public bool exclude;

    public string summary;
    public string prompt;

    public List<string> segments = new List<string>();

    public string justification;
    public string reply;
    public string justify;

    public List<string> objectDatas = new List<string>();

    public static FormResponse NewEvent(string formId, string userId, string sessionId, string envId, string category, float elapsedSeconds)
    {
        return new FormResponse
        {
            formId = formId,
            userId = userId,
            sessionId = sessionId,
            envId = envId,
            category = category,
            elapsedTime = elapsedSeconds.ToString("F3")
        };
    }


}

[Serializable]
public class SimpleRabbitData
{
    public string rabbitId;
    public int roundIndex;
    public string eventType;     // "spawn" / "hit"
    public float timeFromSpawn;  // only for hit

    // world pos
    public float x, y, z;

    // viewport pos
    public float vpx, vpy, vpz;

    public float distance;
}

[Serializable]
public class RoundSummaryData
{
    public int roundIndex;
    public int rabbitsPlanned;
    public int rabbitsSpawned;
    public int rabbitsHit;
    public int spawnFails;
}
