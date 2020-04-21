// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
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

        SerializedProperty autoInitOnStart_;
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
            var objects = AssetDatabase.LoadAllAssetsAtPath("Assets/Microsoft.MixedReality.WebRTC.Unity.Editor/editor_icons.png");
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
            autoInitOnStart_ = serializedObject.FindProperty("AutoInitializeOnStart");
            autoCreateOffer_ = serializedObject.FindProperty("AutoCreateOfferOnRenegotiationNeeded");
            autoLogErrors_ = serializedObject.FindProperty("AutoLogErrorsToUnityConsole");

            iceServers_ = serializedObject.FindProperty("IceServers");
            iceUsername_ = serializedObject.FindProperty("IceUsername");
            iceCredential_ = serializedObject.FindProperty("IceCredential");

            onInitialized_ = serializedObject.FindProperty("OnInitialized");
            onShutdown_ = serializedObject.FindProperty("OnShutdown");
            onError_ = serializedObject.FindProperty("OnError");

            mediaLines_ = serializedObject.FindProperty("_mediaLines");
            transceiverList_ = new ReorderableList(serializedObject, mediaLines_, true, true, true, true);
            transceiverList_.elementHeight = 2 * kLineHeight + kItemSpacing;
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
                    EditorGUI.LabelField(new Rect(x0, y0, 20, 20), $"{index}");

                    // Audio or video icon for transceiver kind
                    MediaKind kind = (MediaKind)element.FindPropertyRelative("_kind").intValue;
                    System.Type senderType, receiverType;
                    if (kind == MediaKind.Audio)
                    {
                        senderType = typeof(AudioSender);
                        receiverType = typeof(AudioReceiver);
                        DrawSpriteIcon(IconType.Audio, new Rect(x1, rect.y, 20, 20));
                    }
                    else
                    {
                        senderType = typeof(VideoSender);
                        receiverType = typeof(VideoReceiver);
                        DrawSpriteIcon(IconType.Video, new Rect(x1, rect.y, 20, 20));
                    }

                    rect.x += kIconSpacing;
                    rect.width -= kIconSpacing;

                    rect.x += 18;
                    rect.width -= 18;

                    float fieldWidth = rect.width;
                    bool hasSender = false;
                    bool hasReceiver = false;
                    {

                        var p = element.FindPropertyRelative("_sender");
                        Object obj = p.objectReferenceValue;
                        obj = EditorGUI.ObjectField(
                            new Rect(rect.x, y0, fieldWidth, EditorGUIUtility.singleLineHeight),
                            obj, senderType, true);
                        hasSender = (obj != null);
                        p.objectReferenceValue = obj;
                    }
                    {
                        var p = element.FindPropertyRelative("_receiver");
                        Object obj = p.objectReferenceValue;
                        obj = EditorGUI.ObjectField(
                            new Rect(rect.x, y1, fieldWidth, EditorGUIUtility.singleLineHeight),
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
                    DrawSpriteIcon(iconType, new Rect(x0 + 8, y1, 16, 16));
                };
            transceiverList_.drawNoneElementCallback = (Rect rect) =>
            {
                GUIStyle style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                EditorGUI.LabelField(rect, "(empty)", style);
            };
            transceiverList_.displayAdd = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(autoInitOnStart_, new GUIContent("Initialize during component start",
                "Automatically initialize the peer connection when the component is started."));
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
