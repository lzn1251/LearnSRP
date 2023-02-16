﻿#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

float4 _PostFXSource_TexelSize;

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

float4 GetSource (float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);   // without mip maps
}

float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(
        TEXTURECUBE_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
        _PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

float4 GetSource2 (float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);   // without mip maps
}

Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0);
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
        );
    if (_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

bool _BloomBicubicUpsampling;
float _BloomIntensity;

float4 BloomAddPassFragment(Varyings input) : SV_TARGET {
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lowRes * _BloomIntensity + highRes, 1.0);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET {
    float3 color = 0.0;
    // gaussian filtering 9x9
    float offsets[] = {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };
    for (int i = 0; i < 9; ++i)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

float4 _BloomThreshold;

float3 ApplyBloomThreshold (float3 color) {
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET {
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
    return float4(color, 1.0);
}

// use 9x9 box filter
float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET {
    float3 color = 0.0;
    float weightSum = 0.0;
    // because we perform a Gaussian blur after this, we can get away with skipping
    // the four samples directly adjacent to the center, reducing the amount of samples from nine to five.
    float2 offsets[] = {
        float2(0.0, 0.0),
        float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
    };
    for (int i = 0; i < 5; i++) {
        float3 c =
            GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        float w = 1.0 / (Luminance(c) + 1.0);
        color += c * w;
        weightSum += w;
    }
    color /= weightSum;
    return float4(color, 1.0);
}

float4 BloomScatterPassFragment (Varyings input) : SV_TARGET {
    float3 lowRes;
    if (_BloomBicubicUpsampling) {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

// compensate for the missing scattered light
float4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET {
    float3 lowRes;
    if (_BloomBicubicUpsampling) {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    lowRes += highRes - ApplyBloomThreshold(highRes);          // add the missing light to the low-resolution pass
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET {
    float3 color = 0.0;
    // gaussian filtering 9x9, there use bilinear filtering to sample in between the Gaussian sampling points at appropriate offsets
    //    in order to reduce the amount of samples
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0; i < 5; ++i)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];  
    }
    return float4(color, 1.0);
}

float4 CopyPassFragment (Varyings input) : SV_TARGET {
    return GetSource(input.screenUV);
}

float4 ToneMappingACESPassFragment (Varyings input) : SV_TARGET {
    float4 color = GetSource(input.screenUV);
    color.rgb = min(color.rgb, 60.0);
    color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
    return color;
}

float4 ToneMappingNeutralPassFragment (Varyings input) : SV_TARGET {
    float4 color = GetSource(input.screenUV);
    color.rgb = min(color.rgb, 60.0);
    color.rgb = NeutralTonemap(color.rgb);
    return color;
}

float4 ToneMappingReinhardPassFragment (Varyings input) : SV_TARGET {
    float4 color = GetSource(input.screenUV);
    color.rgb = min(color.rgb, 60.0);          // without this, maybe wrong for very large values due to precision limitations
    color.rgb /= color.rgb + 1.0;
    return color;
}

#endif