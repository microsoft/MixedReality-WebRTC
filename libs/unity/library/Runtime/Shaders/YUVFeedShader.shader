// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Simple shader mapping a YUV video feed using a Standard lit model.
Shader "Video/YUVFeedShader (standard lit)"
{
    Properties
    {
        [Toggle(MIRROR)] _Mirror("Horizontal Mirror", Float) = 0
        [HideInEditor][NoScaleOffset] _YPlane("Y plane", 2D) = "black" {}
        [HideInEditor][NoScaleOffset] _UPlane("U plane", 2D) = "gray" {}
        [HideInEditor][NoScaleOffset] _VPlane("V plane", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM

        #pragma surface surf Lambert //alpha
        #pragma multi_compile_instancing
        #pragma multi_compile __ MIRROR

        struct Input
        {
            float2 uv_YPlane;
        };

        sampler2D _YPlane;
        sampler2D _UPlane;
        sampler2D _VPlane;

        half3 yuv2rgb(half3 yuv)
        {
            // The YUV to RBA conversion, please refer to: http://en.wikipedia.org/wiki/YUV
            // Y'UV420p (I420) to RGB888 conversion section.
            half y_value = yuv[0];
            half u_value = yuv[1];
            half v_value = yuv[2];
            half r = y_value + 1.370705 * (v_value - 0.5);
            half g = y_value - 0.698001 * (v_value - 0.5) - (0.337633 * (u_value - 0.5));
            half b = y_value + 1.732446 * (u_value - 0.5);
            return half3(r, g, b);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half3 yuv;
            
            // Flip texture coordinates vertically.
            // Texture2D.LoadRawTextureData() always expects a bottom-up image, but the MediaPlayer
            // upload code always get a top-down frame from WebRTC. The most efficient is to upload
            // as is (inverted) and revert here.
            IN.uv_YPlane.y = 1 - IN.uv_YPlane.y;

#ifdef MIRROR
            // Optional left-right mirroring (horizontal flipping)
            IN.uv_YPlane.x = 1 - IN.uv_YPlane.x;
#endif
            yuv.x = tex2D(_YPlane, IN.uv_YPlane).r;
            yuv.y = tex2D(_UPlane, IN.uv_YPlane).r;
            yuv.z = tex2D(_VPlane, IN.uv_YPlane).r;
            o.Albedo = yuv2rgb(yuv);
        }

        ENDCG
    }

    Fallback "Diffuse"
}
