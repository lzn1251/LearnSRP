using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    private const string bufferName = "Post FX";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    enum Pass
    {
        BloomAdd,
        BloomHorizontal,
        BloomPrefilter,
        BloomPrefilterFireflies,     // to fade out the fireflies and decrease flickering
        BloomScatter,
        BloomScatterFinal,
        BloomVertical,
        Copy,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard
    }

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    public bool IsActive => settings != null;

    private const int maxBloomPyramidLevels = 16;

    private int bloomPyramidId;

    private bool useHDR;

    private int
        bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomResultId = Shader.PropertyToID("_BloomResult"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; ++i)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(
        ScriptableRenderContext context, Camera camera, PostFXSettings settings,
        bool useHDR)
    {
        this.useHDR = useHDR;
        this.context = context;
        this.camera = camera;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;  // check whether we have a game or scene camera
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        if (DoBloom(sourceId))
        {
            DoToneMapping(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DoToneMapping(sourceId);
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool DoBloom(int sourceId)
    {
        
        PostFXSettings.BloomSettings bloomSettings = settings.Bloom;
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;

        if (
            bloomSettings.maxIterations == 0 || bloomSettings.intensity <= 0f ||
            height < bloomSettings.downScaleLimit * 2 || width < bloomSettings.downScaleLimit * 2)
        {
            return false;
        }

        buffer.BeginSample("Bloom");
        
        // multiply the color by a weight to make only some things glow
        //    weight s = min( max(0, b - t + tk), 2tk)^2 / (4tk + 0.00001)
        //     b -- maximum of the color's RGB channels  t -- threshold  k -- knee
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloomSettings.threshold); // configure threshold as a gamma value, because that's visually more intuitive
        threshold.y = threshold.x * bloomSettings.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        
        RenderTextureFormat format = useHDR ?
                RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(
            bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceId, bloomPrefilterId, bloomSettings.fadeFireflies ? 
            Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        // create bloom pyramid
        int i;
        for (i = 0; i < bloomSettings.maxIterations; ++i)
        {
            if (height < bloomSettings.downScaleLimit || width < bloomSettings.downScaleLimit)
            {
                break;
            }

            int midId = toId - 1;
            buffer.GetTemporaryRT(
                midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(
                toId, width, height, 0, FilterMode.Bilinear, format);
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }

        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        
        buffer.SetGlobalFloat(
            bloomBucibicUpsamplingId, bloomSettings.bicubicUpsampling ? 1f : 0f
        );

        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloomSettings.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
            finalIntensity = bloomSettings.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId, bloomSettings.scatter);
            finalIntensity = Mathf.Min(bloomSettings.intensity, 0.95f);
        }
        
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;

            for (i -= 1; i > 0; --i)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }

        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        buffer.GetTemporaryRT(
            bloomResultId, camera.pixelWidth, camera.pixelHeight, 0,
            FilterMode.Bilinear, format);
        Draw(fromId, bloomResultId, finalPass);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        return true;
    }

    void DoToneMapping(int sourceId)
    {
        PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = mode < 0 ? Pass.Copy : Pass.ToneMappingACES + (int)mode;
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(
            Matrix4x4.identity, settings.Material, (int)pass,
            MeshTopology.Triangles, 3);               // single triangle
    }
}