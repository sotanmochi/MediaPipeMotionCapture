using UnityEngine;

namespace MediaPipeMotionCapture.Smoothing
{
    /// <summary>
    /// One Euro Filter - An adaptive low-pass filter that provides a good balance between jitter reduction and latency.
    /// It applies stronger smoothing when the movement is slow and allows more responsiveness when the movement is fast.
    /// Reference: http://cristal.univ-lille.fr/~casiez/1euro/
    /// </summary>
    public class OneEuroFilter
    {
        private readonly float _minCutoff;
        private readonly float _beta;
        private readonly float _dCutoff;
        private readonly LowPassFilter _xFilter;
        private readonly LowPassFilter _dxFilter;
        private float _prevTimestamp;

        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.0f, float dCutoff = 1.0f)
        {
            _minCutoff = minCutoff;
            _beta = beta;
            _dCutoff = dCutoff;
            _xFilter = new LowPassFilter();
            _dxFilter = new LowPassFilter();
            _prevTimestamp = -1f;
        }

        public float Filter(float value, float timestamp)
        {
            if (_prevTimestamp < 0f)
            {
                _prevTimestamp = timestamp;
                _xFilter.SetInitial(value);
                _dxFilter.SetInitial(0f);
                return value;
            }

            float dt = timestamp - _prevTimestamp;
            if (dt <= 0f) dt = 1f / 60f; // fallback to ~60fps

            _prevTimestamp = timestamp;

            // Estimate the derivative (velocity)
            float dx = (value - _xFilter.Last) / dt;

            // Smooth the velocity
            float edx = ComputeAlpha(dt, _dCutoff);
            float smoothedDx = _dxFilter.Filter(dx, edx);

            // Adjust the cutoff frequency based on the velocity
            float cutoff = _minCutoff + _beta * Mathf.Abs(smoothedDx);

            // Smooth the value
            float alpha = ComputeAlpha(dt, cutoff);
            return _xFilter.Filter(value, alpha);
        }

        public void Reset()
        {
            _xFilter.Reset();
            _dxFilter.Reset();
            _prevTimestamp = -1f;
        }

        private static float ComputeAlpha(float dt, float cutoff)
        {
            float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
            return 1.0f / (1.0f + tau / dt);
        }

        /// <summary>
        /// A simple low-pass filter based on exponential moving average.
        /// </summary>
        private class LowPassFilter
        {
            public float Last { get; private set; }
            private bool _hasValue;

            public void SetInitial(float value)
            {
                Last = value;
                _hasValue = true;
            }

            public float Filter(float value, float alpha)
            {
                if (!_hasValue)
                {
                    Last = value;
                    _hasValue = true;
                    return value;
                }

                Last = alpha * value + (1.0f - alpha) * Last;
                return Last;
            }

            public void Reset()
            {
                _hasValue = false;
                Last = 0f;
            }
        }
    }
}
