// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//using UnityEngine;
//using UnityEditor;

//namespace Microsoft.MixedReality.WebRTC.Unity.Editor
//{
//    [CustomEditor(typeof(PeerConnection))]
//    [CanEditMultipleObjects]
//    public class PeerConnectionEditor : UnityEditor.Editor
//    {
//        SerializedProperty peerConnection;

//        SerializedProperty localEnabled;
//        SerializedProperty localPlayer;
//        SerializedProperty autoStartLocalFeed;

//        SerializedProperty remoteEnabled;
//        SerializedProperty remotePlayer;
//        SerializedProperty autoStartRemoteFeed;

//        void OnEnable()
//        {
//            peerConnection = serializedObject.FindProperty("PeerConnection");

//            localEnabled = serializedObject.FindProperty("LocalFeedEnabled");
//            localPlayer = serializedObject.FindProperty("LocalPlayer");
//            autoStartLocalFeed = serializedObject.FindProperty("AutoStartLocalFeed");

//            remoteEnabled = serializedObject.FindProperty("RemoteFeedEnabled");
//            remotePlayer = serializedObject.FindProperty("RemotePlayer");
//        }

//        public override void OnInspectorGUI()
//        {
//            serializedObject.Update();

//            GUILayout.Space(10);
//            EditorGUILayout.PropertyField(peerConnection);
//            if (!localEnabled.boolValue && !remoteEnabled.boolValue)
//            {
//                EditorGUILayout.HelpBox("Video source without any feed enabled. Consider removing the component if not used. You can ignore this message if one or more feeds are enabled at runtime dynamically.", MessageType.Warning);
//            }
//            using (var v = new EditorGUILayout.VerticalScope())
//            {
//                localEnabled.boolValue = EditorGUILayout.ToggleLeft("Send local video feed", localEnabled.boolValue);
//                GUI.enabled = localEnabled.boolValue;
//                {
//                    ++EditorGUI.indentLevel;
//                    autoStartLocalFeed.boolValue = EditorGUILayout.ToggleLeft("Auto start sending when peer ready", autoStartLocalFeed.boolValue);
//                    EditorGUILayout.PropertyField(localPlayer, new GUIContent("Optional video player"), true);
//                    --EditorGUI.indentLevel;
//                }
//                GUI.enabled = true;
//            }
//            GUILayout.Space(10);
//            using (var v = new EditorGUILayout.VerticalScope())
//            {
//                remoteEnabled.boolValue = EditorGUILayout.ToggleLeft("Handle incoming remote video feed", remoteEnabled.boolValue);
//                GUI.enabled = remoteEnabled.boolValue;
//                {
//                    ++EditorGUI.indentLevel;
//                    EditorGUILayout.HelpBox("The remote feed is controlled by the remote peer. Reception cannot be disabled locally, only ignored (not handled).", MessageType.Info);
//                    EditorGUILayout.PropertyField(remotePlayer, new GUIContent("Video player"), true);
//                    if (remotePlayer.objectReferenceValue == null)
//                    {
//                        EditorGUILayout.HelpBox("The remote feed is enabled but no remote player is configured. This will incur some performance hit for nothing, unless a player is set up at runtime to display that feed.\nIf you want to ignore the incoming remote video feed, disable it completely with the check box above.", MessageType.Warning);
//                    }
//                    --EditorGUI.indentLevel;
//                }
//                GUI.enabled = true;
//            }

//            serializedObject.ApplyModifiedProperties();
//        }
//    }

//}
