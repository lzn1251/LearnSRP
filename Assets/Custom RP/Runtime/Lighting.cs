
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const string bufferName = "Lighting";

    private CullingResults _cullingResults;

    private const int maxDirLightCount = 4;

    private static int
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];
    
    private CommandBuffer _buffer = new CommandBuffer
    {
        name = bufferName
    };

    private Shadows _shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this._cullingResults = cullingResults;
        _buffer.BeginSample(bufferName);
        _shadows.Setup(context, cullingResults, shadowSettings);
        SetupLights();
        _shadows.Render();
        _buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;

        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; ++i)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                    break;
            }
        }
        _buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        _buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        _buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        _buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirLightShadowData[index] = _shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }

    public void Cleanup()
    {
        _shadows.Cleanup();
    }
}