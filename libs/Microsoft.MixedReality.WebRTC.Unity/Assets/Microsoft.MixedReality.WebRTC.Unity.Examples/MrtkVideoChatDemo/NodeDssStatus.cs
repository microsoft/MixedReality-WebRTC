// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.WebRTC.Unity;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class NodeDssStatus : MonoBehaviour
{
    public NodeDssSignaler Signaler;

    private TextMeshPro _text;
    private Color _baseColor;

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
        Signaler.Connected += Signaler_OnConnect;
        Signaler.FailureOccurred += Signaler_OnFailure;
        _text.SetText($"Connecting to node-dss server {Signaler.HttpServerAddress}...");
        _baseColor = _text.color;
    }

    private void Signaler_OnConnect()
    {
        _text.color = _baseColor;
        _text.SetText($"Connected to node-dss server {Signaler.HttpServerAddress}.");
    }

    private void Signaler_OnFailure(System.Exception obj)
    {
        _text.color = Color.red;
        _text.SetText($"Error: {obj.Message}");
    }
}
