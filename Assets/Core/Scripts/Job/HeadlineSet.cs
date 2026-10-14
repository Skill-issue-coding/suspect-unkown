using UnityEngine;

[CreateAssetMenu(menuName = "Suspect Unknown/Headline Set")]
public class HeadlineSet : ScriptableObject
{
    [SerializeField, Tooltip("One is picked at random each time")]
    private string[] headlines;

    public string PickRandom() => headlines.Length == 0 ? "" : headlines[Random.Range(0, headlines.Length)];

    private void OnValidate()
    {
        if (headlines.Length == 0)
            Debug.LogWarning($"Headlines are emtpy for {name}", this);
    }
}
