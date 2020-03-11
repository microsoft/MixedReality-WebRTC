// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Simple shader mapping an ARGB32 video feed using a Standard lit model.
Shader "Video/ARGBFeedShader (standard lit)"
{
    Properties
    {
        [HideInEditor] _MainTex("Main Tex", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM

        #pragma surface surf Lambert //alpha

        struct Input {
            float2 uv_MainTex;
        };

        // Texture containing the ARGB32 video frames
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
