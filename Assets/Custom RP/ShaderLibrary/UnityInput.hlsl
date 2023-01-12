#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

float3 _WorldSpaceCameraPos;

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float4x4 unity_PrevObjectToWorld;
float4x4 unity_PrevWorldToObject;
#endif
