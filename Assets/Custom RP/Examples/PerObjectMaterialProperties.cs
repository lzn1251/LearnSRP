using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        cutoffId = Shader.PropertyToID("_Cutoff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField] private Color baseColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);

    [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

    private static MaterialPropertyBlock _block;

    private void OnValidate()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }
        _block.SetColor(baseColorId, baseColor);
        _block.SetFloat(cutoffId, alphaCutoff);
        _block.SetFloat(metallicId, metallic);
        _block.SetFloat(smoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    private void Awake()
    {
        OnValidate();
    }
}