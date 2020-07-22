// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using UnityEngine.UI;
using Microsoft.MixedReality.WebRTC.Unity;

public class NodeDssSignalerUI : MonoBehaviour
{
    public NodeDssSignaler NodeDssSignaler;

    /// <summary>
    /// The id of the <see cref="PlayerPrefs"/> key that we cache the last connected target id under
    /// </summary>
    private const string kLastRemotePeerId = "lastRemotePeerId";

    /// <summary>
    /// The text field in which we display the device name
    /// </summary>
    [Tooltip("The text field in which we display the device name")]
    public Text DeviceNameLabel;

    /// <summary>
    /// The text input field in which we accept the target device name
    /// </summary>
    [Tooltip("The text input field in which we accept the target device name")]
    public InputField RemotePeerId;

    private void Start()
    {
        string localPeerId = NodeDssSignaler.LocalPeerId;
        DeviceNameLabel.text = localPeerId;
        Debug.Log($"NodeDSS local peer ID : {localPeerId}");

        if (!string.IsNullOrEmpty(NodeDssSignaler.RemotePeerId))
        {
            RemotePeerId.text = NodeDssSignaler.RemotePeerId;
        }
        else if (PlayerPrefs.HasKey(kLastRemotePeerId))
        {
            RemotePeerId.text = PlayerPrefs.GetString(kLastRemotePeerId);
        }
        Debug.Log($"NodeDSS remote peer ID : {RemotePeerId.text}");
    }

    /// <summary>
    /// If <see cref="RemotePeerId"/> is set, start a connection from the local peer.
    /// </summary>
    public void StartConnection()
    {
        if (!string.IsNullOrEmpty(RemotePeerId.text))
        {
            PlayerPrefs.SetString(kLastRemotePeerId, RemotePeerId.text);
            NodeDssSignaler.RemotePeerId = RemotePeerId.text;
            NodeDssSignaler.PeerConnection.StartConnection();
        }
    }
}
