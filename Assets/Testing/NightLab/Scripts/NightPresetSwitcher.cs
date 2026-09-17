using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Play-mode preset comparison: number keys 1-9 pick a preset, [ and ] cycle.
/// Lets you judge each look through the real gameplay camera.
/// </summary>
public class NightPresetSwitcher : MonoBehaviour
{
    [SerializeField] private NightPreset[] m_Presets;
    [SerializeField] private Light m_Sun;
    [SerializeField] private Volume m_Volume;
    [SerializeField] private bool m_ShowLabel = true;

    private int m_Index = -1;

    public void Setup(NightPreset[] presets, Light sun, Volume volume)
    {
        m_Presets = presets;
        m_Sun = sun;
        m_Volume = volume;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || m_Presets == null || m_Presets.Length == 0)
            return;

        for (int i = 0; i < Mathf.Min(9, m_Presets.Length); i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                Select(i);
        }

        if (keyboard.rightBracketKey.wasPressedThisFrame)
            Select((m_Index + 1) % m_Presets.Length);
        if (keyboard.leftBracketKey.wasPressedThisFrame)
            Select((m_Index - 1 + m_Presets.Length) % m_Presets.Length);
    }

    private void Select(int index)
    {
        if (m_Presets[index] == null)
            return;

        m_Index = index;
        m_Presets[index].Apply(m_Sun, m_Volume);
    }

    private void OnGUI()
    {
        if (!m_ShowLabel || m_Presets == null || m_Presets.Length == 0)
            return;

        string current = m_Index >= 0 && m_Presets[m_Index] != null ? m_Presets[m_Index].name : "scene default";
        GUI.Label(new Rect(12, 12, 600, 24), $"Night preset: {current}   (1-{Mathf.Min(9, m_Presets.Length)} or [ ])");
    }
}
