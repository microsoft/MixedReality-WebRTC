/*
The MIT License (MIT)

Copyright (c) 2016, Unity Technologies

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

This file is taken from:
https://github.com/Unity-Technologies/NativeRenderingPlugin
*/

#pragma once
#include "IUnityInterface.h"


// Should only be used on the rendering thread unless noted otherwise.
UNITY_DECLARE_INTERFACE(IUnityGraphicsD3D11)
{
    ID3D11Device* (UNITY_INTERFACE_API * GetDevice)();

    ID3D11Resource* (UNITY_INTERFACE_API * TextureFromRenderBuffer)(UnityRenderBuffer buffer);
    ID3D11Resource* (UNITY_INTERFACE_API * TextureFromNativeTexture)(UnityTextureID texture);

    ID3D11RenderTargetView* (UNITY_INTERFACE_API * RTVFromRenderBuffer)(UnityRenderBuffer surface);
    ID3D11ShaderResourceView* (UNITY_INTERFACE_API * SRVFromNativeTexture)(UnityTextureID texture);
};

UNITY_REGISTER_INTERFACE_GUID(0xAAB37EF87A87D748ULL, 0xBF76967F07EFB177ULL, IUnityGraphicsD3D11)
