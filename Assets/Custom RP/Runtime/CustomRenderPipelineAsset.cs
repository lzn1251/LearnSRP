using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset {

	[SerializeField]
	bool useDynamicBatching = true, useGPUInstancing = true, 
		useSRPBatcher = true, useLightsPerObject = true;

	[SerializeField] 
	private CameraBufferSettings cameraBufferSettings = new CameraBufferSettings
	{
		allowHDR = true
	};
	
	[SerializeField]
	ShadowSettings shadows = default;

	[SerializeField] 
	private PostFXSettings postFXSettings = default;
	
	public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

	[SerializeField]
	private ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

	[SerializeField]
	private Shader cameraRendererShader = default;
	
	protected override RenderPipeline CreatePipeline () {
		return new CustomRenderPipeline(
			cameraBufferSettings, useDynamicBatching, useGPUInstancing, useSRPBatcher, 
			useLightsPerObject, shadows, postFXSettings, (int) colorLUTResolution,
			cameraRendererShader
		);
	}
}