using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
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
    public string formId = null;
    public string dialogueId = null;
    public TMP_Text dialogueIdText;

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

    public bool TryGetRandomGreenTint(out float tint)
    {
        tint = 0f;
        List<Dialogue> dialogues = LoginController.Instance.dialogues;

        if (dialogues == null || dialogues.Count == 0)
            return false;

        // Try a few random picks to avoid null/empty entries
        const int maxTries = 20;

        for (int t = 0; t < maxTries; t++)
        {
            var d = dialogues[UnityEngine.Random.Range(0, dialogues.Count)];
            if (d?.formItemDatas == null || d.formItemDatas.Length == 0)
                continue;

            var fi = d.formItemDatas[0];
            if (fi == null || string.IsNullOrWhiteSpace(fi.answer1))
                continue;

            // Remove invisible chars just in case, then parse as float
            var s = fi.answer1
                .Replace("\u200B", "")
                .Replace("\u200C", "")
                .Replace("\u200D", "")
                .Replace("\uFEFF", "")
                .Trim();
            
            // Use dialogue category as dialogueId
            string code = d.category;
            dialogueIdText.text = code;
            dialogueId = code;
            // Parse with invariant culture so "0.3" works regardless of locale
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out tint))
                return true;
        }

        return false;
    }

    private void HandleRoundEnded(int roundIndex)
    {
        if (e3Material == null) return;

        // Try to get tint from server dialogues
        if (TryGetRandomGreenTint(out var tint))
        {
            e3Material.SetFloat(GreentintID, tint);
            e3Material.SetFloat(GreenTintStrengthID, greenTintStrength);

            Debug.Log($"[ExperimentController] Round {roundIndex} ended. Greentint (from dialogues) -> {tint:0.00}");
            return;
        }

        // Fallback to your local steps if dialogues aren't ready / no valid value found
        if (greentintSteps == null || greentintSteps.Length == 0) return;

        tintIndex = (tintIndex + 1) % greentintSteps.Length;
        ApplyTintStep(tintIndex);

        dialogueIdText.text = LoginController.Instance.envId;

        if (rc != null)
            rc.SetLoggingIds(formId, dialogueId, LoginController.Instance.userId, LoginController.Instance.envId);

        Debug.Log($"[ExperimentController] Round {roundIndex} ended. Greentint (fallback steps) -> {greentintSteps[tintIndex]:0.00}");
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
    public string type;
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

    public static FormResponse NewEvent(string formId, string userId, string sessionId, string envId, string category, float elapsedSeconds, string dialogueId)
    {
        return new FormResponse
        {
            formId = formId,
            userId = userId,
            sessionId = sessionId,
            envId = envId,
            type = category,
            category = dialogueId,
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
