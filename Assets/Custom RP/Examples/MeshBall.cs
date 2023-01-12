using UnityEngine;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    private static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField] private Mesh _mesh = default;

    [SerializeField] private Material _material = default;

    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] baseColors = new Vector4[1023];

    private float[]
        metallic = new float[1023],
        smoothness = new float[1023];

    private MaterialPropertyBlock _block;

    private void Awake()
    {
        for (int i = 0; i < matrices.Length; ++i)
        {
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f, Quaternion.identity, Vector3.one);
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
            metallic[i] = Random.value < 0.25f ? 1f : 0f;
            smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
            _block.SetVectorArray(baseColorId, baseColors);
            _block.SetFloatArray(metallicId, metallic);
            _block.SetFloatArray(smoothnessId, smoothness);
        }
        Graphics.DrawMeshInstanced(_mesh, 0, _material, matrices, 1023, _block);
    }
}