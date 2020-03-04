// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    /// <summary>
    /// Test for the open/close async pattern using a single open and close task per instance,
    /// and allowing concurrent OpenAsync and CloseAsync calls from any thread with guaranteed
    /// open/close matching sequence.
    /// </summary>
    /// 
    /// This pattern is used for long-running open and close operations and guarantees that:
    /// 1. multiple consecutive calls, possibly from different threads, to <see cref="OpenAsync"/>
    ///    return the same open task.
    /// 2. this open task is cancellable by the user via a cancellation token.
    /// 3. multiple consecutive calls, possibly from different threads, to <see cref="CloseAsync"/>
    ///    return the same close task.
    /// 4. this close task is not cancellable by the user.
    /// 5. a call to <see cref="CloseAsync"/> following a call to <see cref="OpenAsync"/> will
    ///    cancel the open task and wait for it before scheduling a new close task.
    /// 6. a call to <see cref="OpenAsync"/> following a call to <see cref="CloseAsync"/> will
    ///    wait for the pending close task before scheduling a new open task.
    /// 7. calls, possibly from different threads, to <see cref="CloseAsync"/> have no effect
    ///    and return a <see cref="Task.CompletedTask"/> if <see cref="OpenAsync"/> was not
    ///    called before.
    [TestFixture]
    internal class OpenCloseAsync
    {
        /// <summary>
        /// Lock object protecting the open/close-related variables.
        /// </summary>
        private readonly object lockObj = new object();

        /// <summary>
        /// Cancellation token source for cancelling a pending open operation
        /// when calling <see cref="CloseAsync"/>.
        /// </summary>
        private CancellationTokenSource initCTS;

        /// <summary>
        /// Single open task created on first call to <see cref="OpenAsync"/> and
        /// returned by sebsequent calls, until a later <see cref="CloseAsync"/> completed.
        /// </summary>
        private Task openTask;

        /// <summary>
        /// Single close task created on first call to <see cref="CloseAsync"/> and
        /// returned by sebsequent calls, until a later <see cref="OpenAsync"/> completed.
        /// </summary>
        private Task closeTask = Task.CompletedTask;

        [Test]
        public void OpenCloseSimpleTest()
        {
            var openTask = OpenAsync();
            Assert.That(openTask, Is.Not.Null);
            var closeTask = CloseAsync();
            Assert.That(closeTask, Is.Not.Null);
            closeTask.Wait();
            Assert.That(closeTask.IsCompletedSuccessfully);
            Assert.That(CloseAsync(), Is.EqualTo(Task.CompletedTask));
        }

        [Test]
        public void MultiOpenTest()
        {
            var openTask = OpenAsync();
            Assert.That(openTask, Is.Not.Null);
            var tasks = new Task[10];
            for (int i = 0; i < 10; ++i)
            {
                tasks[i] = OpenAsync();
                Assert.That(tasks[i], Is.EqualTo(openTask));
            }
            Task.WaitAll(tasks);
        }

        [Test]
        public void MultiCloseTest()
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; ++i)
            {
                tasks[i] = CloseAsync();
                Assert.That(tasks[i], Is.EqualTo(Task.CompletedTask));
            }
            Task.WaitAll(tasks);
        }

        [Test]
        public void MultiCloseWithOpenTest()
        {
            var closeTask = CloseAsync();
            Assert.That(closeTask, Is.Not.Null);
            var tasks = new Task[10];
            // After running below, CloseAsync() will have completed to will return Task.Completed
            //Assert.That(() => CloseAsync(), Is.EqualTo(Task.CompletedTask).After(5500));
            // This runs in < 5 seconds so CloseAsync() will return always the same pending task
            for (int i = 0; i < 10; ++i)
            {
                tasks[i] = CloseAsync();
                Assert.That(tasks[i], Is.EqualTo(closeTask));
            }
            Task.WaitAll(tasks);
        }

        [Test]
        public void OpenCloseSerialTest()
        {
            var tasks = new Task[20];
            for (int i = 0; i < 10; ++i)
            {
                tasks[2 * i] = OpenAsync();
                tasks[2 * i + 1] = CloseAsync();
            }
            Task.WaitAll(tasks);
        }

        [Test]
        public void OpenCloseParallelTest()
        {
            var tasks = new Task[20];
            for (int i = 0; i < 10; ++i)
            {
                tasks[2 * i] = Task.Run(() => OpenAsync());
                tasks[2 * i + 1] = Task.Run(() => CloseAsync());
            }
            Task.WaitAll(tasks);
        }

        public Task OpenAsync(CancellationToken cancellationToken = default) // 2. open task is cancellable by user
        {
            lock (lockObj)
            {
                if (openTask != null)
                {
                    // 1. multiple consecutive calls to OpenAsync() return the same open task
                    return openTask;
                }

                initCTS = new CancellationTokenSource();

                // Open task can be cancelled by the user (2.) or by CloseAsync() (5.)
                var hybridCTS = CancellationTokenSource.CreateLinkedTokenSource(initCTS.Token, cancellationToken);

                return openTask = Task.Run(() => OpenAsyncImpl(closeTask, hybridCTS.Token), hybridCTS.Token);
            }
        }

        private async Task OpenAsyncImpl(Task previousTask, CancellationToken token)
        {
            // 6. Wait for previous close task to complete (possibly cancelled) before starting a new open
            await previousTask;

            // For testing purpose only - actual open work would go here
            await Task.Delay(5000, token);
        }

        public Task CloseAsync() // 4. close task cannot be cancelled by the user
        {
            lock (lockObj)
            {
                if (openTask == null)
                {
                    // 7. Not initialized with OpenAsync()
                    return Task.CompletedTask;
                }

                if (closeTask != null)
                {
                    // 3. Multiple consecutive calls to CloseAsync() return the same close task
                    return closeTask;
                }

                // 5. CloseAsync() will cancel the open task from the previous OpenAsync() call
                initCTS.Cancel();
                initCTS.Dispose();
                initCTS = null;

                return closeTask = Task.Run(() => CloseAsyncImpl(openTask));
            }
        }

        private async Task CloseAsyncImpl(Task previousTask)
        {
            // 5. Wait for previous open task to complete (possibly cancelled) before continuing
            await previousTask.IgnoreCancellation(); // 4. close task cannot be cancelled by the user

            // For testing purpose only - actual close work would go here
            await Task.Delay(5000);

            // Allow new open/close sequence to start
            lock (lockObj)
            {
                openTask = null;
            }
        }
    }
}
