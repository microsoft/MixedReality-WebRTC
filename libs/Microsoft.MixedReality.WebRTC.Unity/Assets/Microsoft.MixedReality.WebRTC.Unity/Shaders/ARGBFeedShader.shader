// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

Shader "Video/ARGBFeedShader"
{
    Properties
    {
        [HideInEditor] _MainTex("Main Tex", 2D) = "white" {}
    }
    SubShader
    {

        Tags { "RenderType" = "Opaque" }

        CGPROGRAM

        #pragma surface surf Lambert //alpha

        struct Input {
            float2 uv_MainTex;
        };

        sampler2D _MainTex;

        void surf(Input IN, inout SurfaceOutput o)
        {
#if UNITY_UV_STARTS_AT_TOP
            IN.uv_MainTex.y = 1 - IN.uv_MainTex.y;
#endif
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
