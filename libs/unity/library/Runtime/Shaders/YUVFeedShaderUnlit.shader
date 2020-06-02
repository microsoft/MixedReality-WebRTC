// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Simple shader mapping a YUV video feed without any lighting model.
Shader "Video/YUVFeedShader (unlit)"
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
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ MIRROR

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                // Flip texture coordinates vertically.
                // Texture2D.LoadRawTextureData() always expects a bottom-up image, but the MediaPlayer
                // upload code always get a top-down frame from WebRTC. The most efficient is to upload
                // as is (inverted) and revert here.
                o.uv.y = 1 - v.uv.y;

#ifdef MIRROR
                // Optional left-right mirroring (horizontal flipping)
                o.uv.x = 1 - v.uv.x;
#endif
                return o;
            }

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

            fixed3 frag (v2f i) : SV_Target
            {
                half3 yuv;
                yuv.x = tex2D(_YPlane, i.uv).r;
                yuv.y = tex2D(_UPlane, i.uv).r;
                yuv.z = tex2D(_VPlane, i.uv).r;
                return yuv2rgb(yuv);
            }
            ENDCG
        }
    }
}
