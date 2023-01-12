using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

partial class CameraRenderer
{
    partial void DrawGizmos();
    
    partial void DrawUnsupportedShaders();

    partial void PrepareForSceneWindow();

    partial void PrepareBuffer();
    
#if UNITY_EDITOR
    
    private static Material errorMaterial;

    private string SampleName { get; set; }

    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            _context.DrawGizmos(_camera, GizmoSubset.PreImageEffects);
            _context.DrawGizmos(_camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        
        var drawSettings = new DrawingSettings(
            legacyShaderTagIds[0], new SortingSettings(_camera)
        )
        {
            overrideMaterial = errorMaterial
        };
        
        for (int i = 1; i < legacyShaderTagIds.Length; ++i)
        {
            drawSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        _context.DrawRenderers(
            _cullingResults, ref drawSettings, ref filteringSettings);
    }

    partial void PrepareForSceneWindow()
    {
        if (_camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
        }
    }

    partial void PrepareBuffer()
    {
        _buffer.name = SampleName = _camera.name;
    }
#else
    const string SampleName = bufferName;

#endif
}