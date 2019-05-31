using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Microsoft.MixedReality.WebRTC;

public class NodeDssSignalerUI : MonoBehaviour
{
    //[Tooltip("Automatically log all errors to the Unity console")]
    //public bool AutoLogErrors = false;

    ///// <summary>
    ///// The https://github.com/bengreenier/node-dss HTTP service address to connect to
    ///// </summary>
    //[Header("Server")]
    //[Tooltip("The node-dss server to connect to")]
    //public string HttpServerAddress;

    ///// <summary>
    ///// The interval (in ms) that the server is polled at
    ///// </summary>
    //[Tooltip("The interval (in ms) that the server is polled at")]
    //public float PollTimeMs = 5f;


    //public string LocalId { get; set; }
    //public string RemoteId { get; }


    /////// <summary>
    /////// The id of the <see cref="PlayerPrefs"/> key that we cache the last connected target id under
    /////// </summary>
    ////private const string kLastTargetId = "lastTargetId";

    /////// <summary>
    /////// The text field in which we display the device name
    /////// </summary>
    ////[Tooltip("The text field in which we display the device name")]
    ////public Text DeviceNameLabel;
    ////public TextMesh DeviceNameLabel2;

    /////// <summary>
    /////// The text input field in which we accept the target device name
    /////// </summary>
    ////[Tooltip("The text input field in which we accept the target device name")]
    ////public InputField TargetIdField;

    /////// <summary>
    /////// The button that generates an offer to a given target
    /////// </summary>
    ////[Tooltip("The button that generates an offer to a given target")]
    ////public Button CreateOfferButton;

    ///// <summary>
    ///// Internal timing helper
    ///// </summary>
    //private float timeSincePoll = 0f;
        
    ///// <summary>
    ///// Internal last poll response status flag
    ///// </summary>
    //private bool lastGetComplete = true;

    ///// <summary>
    ///// Unity Engine Start() hook
    ///// </summary>
    ///// <remarks>
    ///// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
    ///// </remarks>
    //private void Start()
    //{
    //    if (string.IsNullOrEmpty(HttpServerAddress))
    //    {
    //        throw new ArgumentNullException("HttpServerAddress");
    //    }
    //    if (!HttpServerAddress.EndsWith("/"))
    //    {
    //        HttpServerAddress += "/";
    //    }

    //    if (string.IsNullOrEmpty(LocalId))
    //    {
    //        LocalId = SystemInfo.deviceUniqueIdentifier;
    //    }

    //    // show device label
    //    string deviceId = SystemInfo.deviceUniqueIdentifier;
    //    //DeviceNameLabel.text = deviceId;
    //    //DeviceNameLabel2.text = deviceId;
    //    Debug.Log($"NodeDSS device ID : {deviceId}");

    //    //// if playerprefs has a last target id, autofill the field
    //    //if (PlayerPrefs.HasKey(kLastTargetId))
    //    //{
    //    //    TargetIdField.text = PlayerPrefs.GetString(kLastTargetId);
    //    //}
    //    //else
    //    //{
    //    //    //< TODO - HACK for HL2 without input
    //    //    TargetIdField.text = "987ab3d796dadefb757dfa9a45d6a3e34289e784";
    //    //}

    //    //// bind our handler for creating the offer
    //    //CreateOfferButton.onClick.AddListener(() =>
    //    //{
    //    //    // create offer if we were given a real targetId
    //    //    if (TargetIdField.text.Length > 0)
    //    //    {
    //    //        // cache the targetId in PlayerPrefs so we can autofill it in the future
    //    //        PlayerPrefs.SetString(kLastTargetId, TargetIdField.text);

    //    //        _nativePeer.CreateOffer();
    //    //    }
    //    //});
    //}

    ///// <summary>
    ///// Callback fired when an ICE candidate message has been generated and is ready to
    ///// be sent to the remote peer by the signaling object.
    ///// </summary>
    ///// <param name="candidate"></param>
    ///// <param name="sdpMlineIndex"></param>
    ///// <param name="sdpMid"></param>
    //protected override void OnIceCandiateReadyToSend(string candidate, int sdpMlineIndex, string sdpMid)
    //{
    //    StartCoroutine(PostToServer(new SignalerMessage()
    //    {
    //        MessageType = SignalerMessage.WireMessageType.Ice,
    //        Data = $"{candidate}|{sdpMlineIndex}|{sdpMid}",
    //        IceDataSeparator = "|"
    //    }));
    //}

    ///// <summary>
    ///// Callback fired when a local SDP offer has been generated and is ready to
    ///// be sent to the remote peer by the signaling object.
    ///// </summary>
    ///// <param name="type"></param>
    ///// <param name="sdp"></param>
    //protected override void OnSdpOfferReadyToSend(string offer)
    //{
    //    StartCoroutine(PostToServer(new SignalerMessage()
    //    {
    //        MessageType = SignalerMessage.WireMessageType.Offer,
    //        Data = offer
    //    }));
    //}

    ///// <summary>
    ///// Callback fired when a local SDP answer has been generated and is ready to
    ///// be sent to the remote peer by the signaling object.
    ///// </summary>
    ///// <param name="type"></param>
    ///// <param name="sdp"></param>
    //protected override void OnSdpAnswerReadyToSend(string answer)
    //{
    //    StartCoroutine(PostToServer(new SignalerMessage()
    //    {
    //        MessageType = SignalerMessage.WireMessageType.Answer,
    //        Data = answer,
    //    }));
    //}

    ///// <summary>
    ///// Internal helper for sending http data to the dss server using POST
    ///// </summary>
    ///// <param name="msg">the message to send</param>
    //private IEnumerator PostToServer(SignalerMessage msg)
    //{
    //    var data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
    //    var www = new UnityWebRequest($"{HttpServerAddress}data/{RemoteId}", UnityWebRequest.kHttpVerbPOST);
    //    www.uploadHandler = new UploadHandlerRaw(data);

    //    yield return www.SendWebRequest();

    //    if (AutoLogErrors && (www.isNetworkError || www.isHttpError))
    //    {
    //        Debug.Log("Failure sending message: " + www.error);
    //    }
    //}

    ///// <summary>
    ///// Internal coroutine helper for receiving HTTP data from the DSS server using GET
    ///// and processing it as needed
    ///// </summary>
    ///// <returns>the message</returns>
    //private IEnumerator CO_GetAndProcessFromServer()
    //{
    //    var www = UnityWebRequest.Get($"{HttpServerAddress}data/{LocalId}");
    //    yield return www.SendWebRequest();

    //    if (!www.isNetworkError && !www.isHttpError)
    //    {
    //        var json = www.downloadHandler.text;

    //        var msg = JsonUtility.FromJson<SignalerMessage>(json);

    //        // if the message is good
    //        if (msg != null)
    //        {
    //            // depending on what type of message we get, we'll handle it differently
    //            // this is the "glue" that allows two peers to establish a connection.
    //            Debug.Log($"Received SDP message: type={msg.MessageType} data={msg.Data}");
    //            switch (msg.MessageType)
    //            {
    //                case SignalerMessage.WireMessageType.Offer:
    //                    _nativePeer.SetRemoteDescription("offer", msg.Data);
    //                    // if we get an offer, we immediately send an answer
    //                    _nativePeer.CreateAnswer();
    //                    break;
    //                case SignalerMessage.WireMessageType.Answer:
    //                    _nativePeer.SetRemoteDescription("answer", msg.Data);
    //                    break;
    //                case SignalerMessage.WireMessageType.Ice:
    //                    // this "parts" protocol is defined above, in PeerEventsInstance.OnIceCandiateReadyToSend listener
    //                    var parts = msg.Data.Split(new string[] { msg.IceDataSeparator }, StringSplitOptions.RemoveEmptyEntries);
    //                    _nativePeer.AddIceCandidate(parts[0], int.Parse(parts[1]), parts[2]);
    //                    break;
    //                //case SignalerMessage.WireMessageType.SetPeer:
    //                //    // this allows a remote peer to set our text target peer id
    //                //    // it is primarily useful when one device does not support keyboard input
    //                //    //
    //                //    // note: when running this sample on HoloLens (for example) we may use postman or a similar
    //                //    // tool to use this message type to set the target peer. This is NOT a production-quality solution.
    //                //    TargetIdField.text = msg.Data;
    //                //    break;
    //                default:
    //                    Debug.Log("Unknown message: " + msg.MessageType + ": " + msg.Data);
    //                    break;
    //            }
    //        }
    //        else if (AutoLogErrors)
    //        {
    //            Debug.LogError($"Failed to deserialize JSON message : {json}");
    //        }
    //    }
    //    else if (AutoLogErrors && www.isNetworkError)
    //    {
    //        Debug.LogError($"Network error trying to send data to {HttpServerAddress}: {www.error}");
    //    }
    //    else
    //    {
    //        // This is very spammy because the node-dss protocol uses 404 as regular "no data yet" message, which is an HTTP error
    //        //Debug.LogError($"HTTP error: {www.error}");
    //    }

    //    lastGetComplete = true;
    //}

    ///// <summary>
    ///// Unity Engine Update() hook
    ///// </summary>
    ///// <remarks>
    ///// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html
    ///// </remarks>
    //private void Update()
    //{
    //    // if we have not reached our PollTimeMs value...
    //    if (timeSincePoll <= PollTimeMs)
    //    {
    //        // we keep incrementing our local counter until we do.
    //        timeSincePoll += Time.deltaTime;
    //        return;
    //    }

    //    // if we have a pending request still going, don't queue another yet.
    //    if (!lastGetComplete)
    //    {
    //        return;
    //    }

    //    // when we have reached our PollTimeMs value...
    //    timeSincePoll = 0f;

    //    // begin the poll and process.
    //    lastGetComplete = false;
    //    StartCoroutine(CO_GetAndProcessFromServer());
    //}
}
