// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
