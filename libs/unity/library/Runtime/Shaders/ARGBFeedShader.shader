// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Simple shader mapping an ARGB32 video feed using a Standard lit model.
Shader "Video/ARGBFeedShader (standard lit)"
{
    Properties
    {
        [Toggle(MIRROR)] _Mirror("Horizontal Mirror", Float) = 0
        [HideInEditor][NoScaleOffset] _MainTex("Main Tex", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM

        #pragma surface surf Lambert //alpha
        #pragma multi_compile_instancing
        #pragma multi_compile __ MIRROR

        struct Input {
            float2 uv_MainTex;
        };

        // Texture containing the ARGB32 video frames
        sampler2D _MainTex;

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Flip texture coordinates vertically.
            // Texture2D.LoadRawTextureData() always expects a bottom-up image, but the MediaPlayer
            // upload code always get a top-down frame from WebRTC. The most efficient is to upload
            // as is (inverted) and revert here.
            IN.uv_MainTex.y = 1 - IN.uv_MainTex.y;

#ifdef MIRROR
            // Optional left-right mirroring (horizontal flipping)
            IN.uv_MainTex.x = 1 - IN.uv_MainTex.x;
#endif

            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
