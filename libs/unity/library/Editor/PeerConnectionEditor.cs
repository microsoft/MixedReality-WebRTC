// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Custom editor for the <see cref="Microsoft.MixedReality.WebRTC.Unity.PeerConnection"/> component.
    /// </summary>
    [CustomEditor(typeof(PeerConnection))]
    [CanEditMultipleObjects]
    public class PeerConnectionEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Height of a single line of controls (e.g. single sender or receiver).
        /// </summary>
        const float kLineHeight = 22;

        /// <summary>
        /// Spacing between list items (transceivers), for readability.
        /// </summary>
        const float kItemSpacing = 3;

        const float kIconSpacing = 25;

        SerializedProperty autoCreateOffer_;
        SerializedProperty autoLogErrors_;

        SerializedProperty iceServers_;
        SerializedProperty iceUsername_;
        SerializedProperty iceCredential_;

        SerializedProperty onInitialized_;
        SerializedProperty onShutdown_;
        SerializedProperty onError_;

        ReorderableList transceiverList_;
        SerializedProperty mediaLines_;

        enum IconType
        {
            Audio,
            Video,
            SendRecv,
            RecvOnly,
            SendOnly,
            Inactive
        }
        Sprite[] sprites_ = new Sprite[6];

        void DrawSpriteIcon(IconType type, Rect rect)
        {
            var sprite = sprites_[(int)type];
            var texture = sprite.texture;
            Rect texCoords = sprite.textureRect;
            Vector2 texelSize = texture.texelSize;
            texCoords.x *= texelSize.x;
            texCoords.y *= texelSize.y;
            texCoords.width *= texelSize.x;
            texCoords.height *= texelSize.y;
            if (EditorGUIUtility.isProSkin)
            {
                texCoords.x += 0.5f;
            }
            GUI.DrawTextureWithTexCoords(rect, texture, texCoords);
        }

        private void Awake()
        {
            // Load sprites for transceiver list control
            var objects = AssetDatabase.LoadAllAssetsAtPath("Packages/com.microsoft.mixedreality.webrtc/Editor/Icons/editor_icons.png");
            foreach (var obj in objects)
            {
                if (obj is Sprite sprite)
                {
                    if (sprite.name == "icon_audio")
                    {
                        sprites_[(int)IconType.Audio] = sprite;
                    }
                    else if (sprite.name == "icon_video")
                    {
                        sprites_[(int)IconType.Video] = sprite;
                    }
                    else if (sprite.name == "icon_sendrecv")
                    {
                        sprites_[(int)IconType.SendRecv] = sprite;
                    }
                    else if (sprite.name == "icon_recvonly")
                    {
                        sprites_[(int)IconType.RecvOnly] = sprite;
                    }
                    else if (sprite.name == "icon_sendonly")
                    {
                        sprites_[(int)IconType.SendOnly] = sprite;
                    }
                    else if (sprite.name == "icon_inactive")
                    {
                        sprites_[(int)IconType.Inactive] = sprite;
                    }
                }
            }
        }

        void OnEnable()
        {
            autoCreateOffer_ = serializedObject.FindProperty("AutoCreateOfferOnRenegotiationNeeded");
            autoLogErrors_ = serializedObject.FindProperty("AutoLogErrorsToUnityConsole");

            iceServers_ = serializedObject.FindProperty("IceServers");
            iceUsername_ = serializedObject.FindProperty("IceUsername");
            iceCredential_ = serializedObject.FindProperty("IceCredential");

            onInitialized_ = serializedObject.FindProperty("OnInitialized");
            onShutdown_ = serializedObject.FindProperty("OnShutdown");
            onError_ = serializedObject.FindProperty("OnError");

            mediaLines_ = serializedObject.FindProperty("_mediaLines");
            transceiverList_ = new ReorderableList(serializedObject, mediaLines_, draggable: true,
                displayHeader: true, displayAddButton: false, displayRemoveButton: true);
            transceiverList_.elementHeightCallback =
                (int index) =>
                {
                    float height = kItemSpacing + 2 * kLineHeight;
                    var element = transceiverList_.serializedProperty.GetArrayElementAtIndex(index);
                    var src = element.FindPropertyRelative("_source");
                    if (src.isExpanded)
                    {
                        var trackName = element.FindPropertyRelative("SenderTrackName");
                        // FIXME - SdpTokenDrawer.OnGUI() is called with h=16px instead of the total height, breaking the layout
                        height += kLineHeight; // EditorGUI.GetPropertyHeight(trackName) + kItemSpacing;
                    }
                    return height;
                };
            transceiverList_.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Transceivers");
            transceiverList_.drawElementCallback =
                (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var element = transceiverList_.serializedProperty.GetArrayElementAtIndex(index);
                    float x0 = rect.x;
                    float x1 = x0 + 16;
                    float y0 = rect.y + 2;
                    float y1 = y0 + kLineHeight;

                    // MID value
                    EditorGUI.LabelField(new Rect(x0 - 14, y1, 20, 20), $"{index}");

                    // Audio or video icon for transceiver kind
                    MediaKind mediaKind = (MediaKind)element.FindPropertyRelative("_mediaKind").intValue;
                    System.Type senderType, receiverType;
                    if (mediaKind == MediaKind.Audio)
                    {
                        senderType = typeof(AudioTrackSource);
                        receiverType = typeof(AudioReceiver);
                        DrawSpriteIcon(IconType.Audio, new Rect(x0, rect.y, 20, 20));
                    }
                    else
                    {
                        senderType = typeof(VideoTrackSource);
                        receiverType = typeof(VideoReceiver);
                        DrawSpriteIcon(IconType.Video, new Rect(x0, rect.y, 20, 20));
                    }

                    rect.x += (kIconSpacing + 10);
                    rect.width -= (kIconSpacing + 10);

                    float fieldWidth = rect.width;
                    bool hasSender = false;
                    bool hasReceiver = false;
                    bool sourceIsExpanded = false;
                    {
                        var p = element.FindPropertyRelative("_source");
                        Object obj = p.objectReferenceValue;
                        sourceIsExpanded = EditorGUI.Foldout(new Rect(rect.x, y0, 0, EditorGUIUtility.singleLineHeight), p.isExpanded, new GUIContent());
                        p.isExpanded = sourceIsExpanded;
                        obj = EditorGUI.ObjectField(
                            new Rect(rect.x, y0, fieldWidth, EditorGUIUtility.singleLineHeight),
                            obj, senderType, true);
                        hasSender = (obj != null);
                        p.objectReferenceValue = obj;
                        y0 += kLineHeight;
                    }
                    if (sourceIsExpanded)
                    {
                        var p = element.FindPropertyRelative("_senderTrackName");
                        // FIXME - SdpTokenDrawer.OnGUI() is called with h=16px instead of the total height, breaking the layout
                        //EditorGUI.PropertyField(new Rect(rect.x + 10, y0, fieldWidth - 8, EditorGUIUtility.singleLineHeight), p);
                        //y0 += EditorGUI.GetPropertyHeight(p) + 6;
                        string val = p.stringValue;
                        val = EditorGUI.TextField(new Rect(rect.x + 10, y0, fieldWidth - 8, EditorGUIUtility.singleLineHeight), "Track name", val);
                        p.stringValue = val;
                        y0 += kLineHeight;
                    }
                    {
                        var p = element.FindPropertyRelative("_receiver");
                        Object obj = p.objectReferenceValue;
                        obj = EditorGUI.ObjectField(
                            new Rect(rect.x, y0, fieldWidth, EditorGUIUtility.singleLineHeight),
                            obj, receiverType, true);
                        hasReceiver = (obj != null);
                        p.objectReferenceValue = obj;
                    }

                    IconType iconType = IconType.Inactive;
                    if (hasSender)
                    {
                        if (hasReceiver)
                        {
                            iconType = IconType.SendRecv;
                        }
                        else
                        {
                            iconType = IconType.SendOnly;
                        }
                    }
                    else if (hasReceiver)
                    {
                        iconType = IconType.RecvOnly;
                    }
                    DrawSpriteIcon(iconType, new Rect(x0, y1, 16, 16));
                };
            transceiverList_.drawNoneElementCallback = (Rect rect) =>
            {
                GUIStyle style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                EditorGUI.LabelField(rect, "(empty)", style);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

#if UNITY_WSA
            if (!PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone))
            {
                EditorGUILayout.HelpBox("The UWP player is missing the Microphone capability. Currently on UWP the native WebRTC implementation always tries to"
                    + " open the microphone while initializing the audio subsystem at startup. Not granting access will fail initialization, and generally crash the app."
                    + " Add the Microphone capability in Project Settings > Player > UWP > Publishing Settings > Capabilities.", MessageType.Error);
                if (GUILayout.Button("Open Player Settings"))
                {
                    SettingsService.OpenProjectSettings("Project/Player");
                }
                if (GUILayout.Button("Add Microphone Capability"))
                {
                    PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, true);
                }
            }
#endif

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(autoLogErrors_, new GUIContent("Log errors to the Unity console",
                "Log the WebRTC errors to the Unity console."));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Signaling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(iceServers_, true);
            EditorGUILayout.PropertyField(iceUsername_);
            EditorGUILayout.PropertyField(iceCredential_);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Media", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoCreateOffer_);
            transceiverList_.DoLayoutList();
            using (var _ = new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Audio", EditorStyles.miniButton))
                {
                    ((PeerConnection)serializedObject.targetObject).AddMediaLine(MediaKind.Audio);
                }
                if (GUILayout.Button("+ Video", EditorStyles.miniButton))
                {
                    ((PeerConnection)serializedObject.targetObject).AddMediaLine(MediaKind.Video);
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(onInitialized_);
            EditorGUILayout.PropertyField(onShutdown_);
            EditorGUILayout.PropertyField(onError_);

            serializedObject.ApplyModifiedProperties();
        }
    }

}
