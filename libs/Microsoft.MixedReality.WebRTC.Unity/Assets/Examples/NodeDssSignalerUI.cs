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
    public TextMesh DeviceNameLabel2;

    /// <summary>
    /// The text input field in which we accept the target device name
    /// </summary>
    [Tooltip("The text input field in which we accept the target device name")]
    public InputField RemotePeerId;

    /// <summary>
    /// The button that generates an offer to a given target
    /// </summary>
    [Tooltip("The button that generates an offer to a given target")]
    public Button CreateOfferButton;

    private void Start()
    {
        // Show device label (local peer ID)
        string localPeerId = NodeDssSignaler.LocalPeerId;
        DeviceNameLabel.text = localPeerId;
        DeviceNameLabel2.text = localPeerId;
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

        // bind our handler for creating the offer
        CreateOfferButton.onClick.AddListener(() =>
        {
            // create offer if we were given a valid RemotePeerId
            if (!string.IsNullOrEmpty(RemotePeerId.text))
            {
                PlayerPrefs.SetString(kLastRemotePeerId, RemotePeerId.text);
                NodeDssSignaler.PeerConnection.Peer.CreateOffer();
            }
        });
    }
}
