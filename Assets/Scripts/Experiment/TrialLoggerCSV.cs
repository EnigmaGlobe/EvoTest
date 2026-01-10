using System;
using System.IO;
using System.Text;
using UnityEngine;

public class TrialLoggerCSV : MonoBehaviour
{
    [Header("Output")]
    public string fileName = "E3_trials.csv";

    private string filePath;

    private void Awake()
    {
        // Writes to a persistent folder (works in builds too)
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(filePath))
        {
            var header = "timestamp_iso,condition,suppressionStrength,contrastLoss,trialIndex,clickCount,timeToClick_sec,result\n";
            File.WriteAllText(filePath, header, Encoding.UTF8);
        }

        Debug.Log($"[Logger] CSV path: {filePath}");
    }

    public void AppendRow(
        DateTime timestamp,
        string condition,
        float suppressionStrength,
        float contrastLoss,
        int trialIndex,
        int clickCount,
        float timeToClickSec,
        string result
    )
    {
        var line =
            $"{timestamp:O}," +
            $"{condition}," +
            $"{suppressionStrength:0.###}," +
            $"{contrastLoss:0.###}," +
            $"{trialIndex}," +
            $"{clickCount}," +
            $"{timeToClickSec:0.###}," +
            $"{result}\n";

        File.AppendAllText(filePath, line, Encoding.UTF8);
    }
}
