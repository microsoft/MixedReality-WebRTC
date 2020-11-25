//
// Copyright (C) Microsoft. All rights reserved.
//
Shader "Microsoft.MixedReality.WebRTC/YUVFeedShader (unlit)"
{
	Properties
	{
		[Toggle(MIRROR)] _Mirror("Horizontal Mirror", Float) = 0
		[Toggle(VMIRROR)] _VMirror("Vertical Mirror", Float) = 0
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
			#pragma multi_compile __ MIRROR VMIRROR

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
#ifdef VMIRROR
				o.uv.y = 1 - v.uv.y;
#endif
				return o;
			}

			sampler2D _YPlane;
			sampler2D _UPlane;
			sampler2D _VPlane;

			half3 yuv2rgb(float3 yuv)
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

			fixed4 frag (v2f i) : SV_Target
			{
				half3 yuv;
				yuv.x = tex2D(_YPlane, i.uv).r;
				yuv.y = tex2D(_UPlane, i.uv).r;
				yuv.z = tex2D(_VPlane, i.uv).r;
				return fixed4(yuv2rgb(yuv), 1.0);
			}
			ENDCG
		}
	}
}
