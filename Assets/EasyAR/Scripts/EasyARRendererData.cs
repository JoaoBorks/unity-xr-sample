using UnityEngine;

[CreateAssetMenu(fileName = "EasyARRendererData", menuName = "EasyAR/Data")]
public class EasyARRendererData : ScriptableObject
{
    [HideInInspector]
    public Material material;
}