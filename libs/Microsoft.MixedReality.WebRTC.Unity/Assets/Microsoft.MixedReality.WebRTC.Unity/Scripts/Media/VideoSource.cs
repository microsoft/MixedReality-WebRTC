using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class VideoSource : MonoBehaviour
    {
        public VideoFrameQueue<I420VideoFrameStorage> FrameQueue;
    }
}
