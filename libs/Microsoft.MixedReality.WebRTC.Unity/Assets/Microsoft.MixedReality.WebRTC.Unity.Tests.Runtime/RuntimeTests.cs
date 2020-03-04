// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime
{
    public class PeerConnectionTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Shutdown()
        {
        }

        [UnityTest]
        public IEnumerator SimplePeerConnection()
        {
            // Create the component
            var go = new GameObject("test_go");
            go.SetActive(false); // prevent auto-activation of components
            var pc = go.AddComponent<PeerConnection>();

            // Disable auto-init
            pc.AutoInitializeOnStart = false;

            // Activate
            go.SetActive(true);

            // Initialize
            var initializedEvent = new ManualResetEventSlim(initialState: false);
            pc.OnInitialized.AddListener(() => initializedEvent.Set());
            Assert.IsNull(pc.Peer);
            pc.InitializeAsync().Wait();

            // Wait a frame so that the Unity event OnInitialized can propagate
            yield return null;

            // Check the event was raised
            Assert.IsTrue(initializedEvent.Wait(millisecondsTimeout: 50000));
            Assert.IsNotNull(pc.Peer);

            // Destroy
            Object.Destroy(pc);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator RuntimeTestsWithEnumeratorPasses()
        {
            yield return null;
        }
    }
}
