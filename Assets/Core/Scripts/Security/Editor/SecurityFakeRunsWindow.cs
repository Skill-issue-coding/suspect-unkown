using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds completed runs through one entrance to the open level's security record, so adaptive security can
// be tested (S5) without playing the same run again and again. A fake run is an entry at the entrance and
// one transition from the street into the room behind it, no route through the shop. It fills JobLog and
// saves through SecurityRecorder.RecordRun, so it decays exactly as a real run does
public class SecurityFakeRunsWindow : EditorWindow
{
    private int entrance;
    private int runs = 2;

    private string[] entranceNames = Array.Empty<string>();

    [MenuItem(MenuPaths.SecurityFakeRuns, priority = MenuPaths.SecurityPriority + 3)]
    private static void Open() => GetWindow<SecurityFakeRunsWindow>("Fake Runs");

    private void OnGUI()
    {
        // JobLog holds the live run in play mode, and faking would wipe it
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Stop play mode: the job log holds the live run", MessageType.Info);
            return;
        }

        JobLayout layout = FindAnyObjectByType<JobLayout>();
        SecurityRecorder recorder = FindAnyObjectByType<SecurityRecorder>();
        if (layout == null || recorder == null)
        {
            EditorGUILayout.HelpBox("Open a level with JobLayout and SecurityRecorder", MessageType.Error);
            return;
        }

        if (entranceNames.Length != layout.EntranceCount)
        {
            entranceNames = new string[layout.EntranceCount];
            for (int i = 0; i < layout.EntranceCount; i++)
                entranceNames[i] = layout.EntranceName(i);
            entrance = Mathf.Clamp(entrance, 0, Mathf.Max(0, layout.EntranceCount - 1));
        }

        if (layout.EntranceCount == 0)
        {
            EditorGUILayout.HelpBox("The level's JobLayout lists no entrances", MessageType.Error);
            return;
        }

        entrance = EditorGUILayout.Popup("Entrance", entrance, entranceNames);
        runs = EditorGUILayout.IntSlider("Runs", runs, 1, 10);
        if (GUILayout.Button("Add runs")) AddRuns(layout, recorder);
    }

    private void AddRuns(JobLayout layout, SecurityRecorder recorder)
    {
        int room = layout.EntranceRoom(entrance);
        if (room < 0)
        {
            Debug.LogWarning($"Security: {layout.EntranceName(entrance)} opens into no listed room; nothing faked", layout);
            return;
        }

        // A fresh log per run, so each run adds only its own entry and transition
        for (int i = 0; i < runs; i++)
        {
            JobLog.Begin(this, layout.RoomCount, layout.EntranceCount);
            JobLog.RecordEntry(entrance);
            JobLog.RecordRoomChange(0, room);
            SecurityRecorder.RecordRun(SceneManager.GetActiveScene().name, layout, recorder.Decay, layout);
            JobLog.End(this);
        }

        Debug.Log($"Security: faked {runs} runs through {layout.EntranceName(entrance)}");
    }
}
