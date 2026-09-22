using UnityEngine;

public class OccluderFadeSource : MonoBehaviour
{
    private static readonly int PlayerWorldPos = Shader.PropertyToID("_PlayerWorldPos");

    [Tooltip("Aim at chest height")]
    [SerializeField] private Transform _target;

    private void LateUpdate()
    {
        Shader.SetGlobalVector(PlayerWorldPos, _target.position);
    }
}
