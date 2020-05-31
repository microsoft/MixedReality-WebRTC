// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Simple shader mapping an ARGB32 video feed without any lighting model.
Shader "Video/ARGBFeedShader (unlit)"
{
    Properties
    {
        [Toggle(MIRROR)] _Mirror("Horizontal Mirror", Float) = 0
        [HideInEditor][NoScaleOffset] _MainTex("Main Tex", 2D) = "black" {}
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

            sampler2D _MainTex;

            fixed3 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv).rgb;
            }
            ENDCG
        }
    }
}
