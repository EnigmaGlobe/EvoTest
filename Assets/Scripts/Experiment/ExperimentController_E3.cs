using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExperimentController_E3 : MonoBehaviour
{
    [Header("References")]
    public Material e3Material;                  // MAT_E3_GradedSuppression
    public TrialLoggerCSV logger;                // Logger component in scene
    public Camera mainCamera;                    // Usually Main Camera
    public TargetClickDetector target;           // Target_Sphere detector

    [Header("E3 Params")]
    public float e3Suppression = 0.7f;
    public float e3ContrastLoss = 0.5f;

    [Header("Keys")]
    public Key startTrialKey = Key.Space;
    public Key toggleConditionKey = Key.E;

    [Header("State (read-only)")]
    public ExperimentCondition condition = ExperimentCondition.Baseline;
    public int trialIndex = 0;

    private bool trialRunning = false;
    private float trialStartTime = 0f;
    private int clickCount = 0;

    private static readonly int SuppressionStrengthID = Shader.PropertyToID("_SuppressionStrength");
    private static readonly int ContrastLossID = Shader.PropertyToID("_ContrastLoss");

    private void Start()
    {
        if (e3Material == null) Debug.LogError("[E3] e3Material missing.");
        if (logger == null) Debug.LogError("[E3] logger missing.");
        if (mainCamera == null) mainCamera = Camera.main;
        if (target != null) target.controller = this;

        ApplyBaseline();
        Debug.Log("[E3] Ready. SPACE=start trial. E=toggle Baseline/E3.");
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[toggleConditionKey].wasPressedThisFrame)
        {
            ToggleCondition();
        }

        if (Keyboard.current[startTrialKey].wasPressedThisFrame)
        {
            StartTrial();
        }

        // Count clicks anywhere (for error rate / click count)
        if (trialRunning && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            clickCount++;
        }
    }

    public void NotifyTargetHit()
    {
        if (!trialRunning) return;

        float ttc = Time.time - trialStartTime;
        EndTrial(TrialResult.Hit, ttc);
    }

    private void StartTrial()
    {
        if (trialRunning)
        {
            Debug.Log("[E3] Trial already running.");
            return;
        }

        trialRunning = true;
        clickCount = 0;
        trialStartTime = Time.time;

        trialIndex++;
        Debug.Log($"[E3] Trial {trialIndex} START. Condition={condition}");
    }

    private void EndTrial(TrialResult result, float timeToClickSec)
    {
        trialRunning = false;

        float s = e3Material != null ? e3Material.GetFloat(SuppressionStrengthID) : -1f;
        float c = e3Material != null ? e3Material.GetFloat(ContrastLossID) : -1f;

        logger.AppendRow(
            DateTime.UtcNow,
            condition.ToString(),
            s,
            c,
            trialIndex,
            clickCount,
            timeToClickSec,
            result.ToString()
        );

        Debug.Log($"[E3] Trial {trialIndex} END. Result={result}, TTC={timeToClickSec:0.###}s, clicks={clickCount}");
    }

    private void ToggleCondition()
    {
        if (condition == ExperimentCondition.Baseline)
        {
            condition = ExperimentCondition.E3;
            ApplyE3();
        }
        else
        {
            condition = ExperimentCondition.Baseline;
            ApplyBaseline();
        }

        Debug.Log($"[E3] Condition now: {condition}");
    }

    private void ApplyBaseline()
    {
        if (e3Material == null) return;
        e3Material.SetFloat(SuppressionStrengthID, 0f);
        e3Material.SetFloat(ContrastLossID, 0f);
    }

    private void ApplyE3()
    {
        if (e3Material == null) return;
        e3Material.SetFloat(SuppressionStrengthID, e3Suppression);
        e3Material.SetFloat(ContrastLossID, e3ContrastLoss);
    }
}
