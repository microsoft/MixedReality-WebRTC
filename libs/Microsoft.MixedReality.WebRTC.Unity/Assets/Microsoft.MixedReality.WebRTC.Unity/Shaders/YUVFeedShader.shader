Shader "Video/YUVFeedShader (standard lit)" {
	Properties
	{
		[HideInEditor] _YPlane("Y plane", 2D) = "white" {}
		[HideInEditor] _UPlane("U plane", 2D) = "white" {}
		[HideInEditor] _VPlane("V plane", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		CGPROGRAM

		#pragma surface surf Lambert //alpha

		struct Input
	    {
			float2 uv_YPlane;
			float2 uv_UPlane;
			float2 uv_VPlane;
		};

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

		void surf(Input IN, inout SurfaceOutput o)
		{
			float3 yuv;
			yuv.x = tex2D(_YPlane, IN.uv_YPlane).r;
			yuv.y = tex2D(_UPlane, IN.uv_UPlane).r;
			yuv.z = tex2D(_VPlane, IN.uv_VPlane).r;
			o.Albedo = yuv2rgb(yuv);
		}

		ENDCG
	}
	Fallback "Diffuse"
}
