using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class DebugUI : MonoBehaviour
{
    public static DebugUI Instance;
    public TMP_Text debugText;

    Queue<string> logs = new();
    const int MAX = 25;

    void Awake()
    {
        Instance = this;
    }

    public void Log(string msg)
    {
        logs.Enqueue(msg);

        if (logs.Count > MAX)
            logs.Dequeue();

        debugText.text = string.Join("\n", logs);
    }
}