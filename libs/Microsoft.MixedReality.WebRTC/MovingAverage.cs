using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Microsoft.MixedReality.WebRTC
{
    public class MovingAverage
    {
        public int Capacity { get; }

        public float Average { get; private set; } = 0f;

        private Queue<float> _samples;

        public MovingAverage(int capacity)
        {
            Capacity = capacity;
            _samples = new Queue<float>(capacity);
        }

        public void Push(float value)
        {
            var count = _samples.Count + 1;
            if (count <= Capacity)
            {
                Average += (value - Average) / count;
                Debug.Assert(!float.IsNaN(Average));
                _samples.Enqueue(value);
            }
            else
            {
                var popValue = _samples.Dequeue();
                Average += (value - popValue) / (count - 1);
                _samples.Enqueue(value);
            }
        }
    }
}
