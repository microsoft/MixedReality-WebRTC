// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

Shader "Video/YUVFeedShader (unlit)"
{
    Properties
    {
		[Toggle(MIRROR)] _Mirror("Horizontal Mirror", Float) = 0
		[HideInEditor][NoScaleOffset] _YPlane("Y plane", 2D) = "white" {}
		[HideInEditor][NoScaleOffset] _UPlane("U plane", 2D) = "white" {}
		[HideInEditor][NoScaleOffset] _VPlane("V plane", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile __ MIRROR

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
#if !UNITY_UV_STARTS_AT_TOP
				o.uv.y = 1 - v.uv.y;
#endif
#ifdef MIRROR
				o.uv.x = 1 - v.uv.x;
#endif
                return o;
            }

            sampler2D _YPlane;
            sampler2D _UPlane;
            sampler2D _VPlane;

			float3 yuv2rgb(float3 yuv)
			{
                // The YUV to RBA conversion, please refer to: http://en.wikipedia.org/wiki/YUV
				// Y'UV420p (I420) to RGB888 conversion section.
				float y_value = yuv[0];
				float u_value = yuv[1];
				float v_value = yuv[2];
				float r = y_value + 1.370705 * (v_value - 0.5);
				float g = y_value - 0.698001 * (v_value - 0.5) - (0.337633 * (u_value - 0.5));
				float b = y_value + 1.732446 * (u_value - 0.5);
				return float3(r, g, b);
			}

            fixed4 frag (v2f i) : SV_Target
            {
				float3 yuv;
				yuv.x = tex2D(_YPlane, i.uv).r;
				yuv.y = tex2D(_UPlane, i.uv).r;
				yuv.z = tex2D(_VPlane, i.uv).r;
                return fixed4(yuv2rgb(yuv), 1.0);
            }
            ENDCG
        }
    }
}
