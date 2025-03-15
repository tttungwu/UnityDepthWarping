using System;
using TMPro;
using UnityEngine;

namespace Stat
{
    public class FPSDisplay : MonoBehaviour
    {
        public TextMeshProUGUI uiText;
        public int startFrameIdx = 10;
        public float accumTimeLimit = 60.0f;
        public float deltaTimeUpdateFrequency = 0.1f;
        private int _frameIdx;
        private float _minDeltaTime = float.MaxValue;
        private float _maxDeltaTime = float.MinValue;
        private float _accumTime;
        private float _displayDeltaTime;
        private float _displayAccumTime;
        private int _displayFrameCount;

        private void Update()
        {
            if (_accumTime >= accumTimeLimit) return;
            _frameIdx++;
            if (_frameIdx < startFrameIdx) return;
        
            _displayAccumTime += Time.deltaTime;
            _displayFrameCount++;
            if (_displayAccumTime >= deltaTimeUpdateFrequency)
            {
                _displayDeltaTime = _displayAccumTime / _displayFrameCount;
                _displayAccumTime = 0;
                _displayFrameCount = 0;
            }
            
            _minDeltaTime = Math.Min(_minDeltaTime, Time.deltaTime);
            _maxDeltaTime = Math.Max(_maxDeltaTime, Time.deltaTime);
            _accumTime += Time.deltaTime;
        }

        private void OnGUI()
        {
            string text;
            if (_frameIdx < startFrameIdx)
            {
                text = $"FrameCount: {_frameIdx}";
            }
            else
            {
                text = $"FPS: {1f / _displayDeltaTime:F3}" +
                       $"\nDeltaTime: {_displayDeltaTime * 1000f:F3}" +
                       $"\nMinFPS: {1f / _maxDeltaTime:F3}" +
                       $"\nMaxFPS: {1f / _minDeltaTime:F3}" +
                       $"\nAvgFPS: {(_frameIdx - startFrameIdx + 1) / _accumTime:F3}" +
                       $"\nFrameCount: {_frameIdx - startFrameIdx + 1}" +
                       $"\nAccumTime: {_accumTime:F3}";
            }
            uiText.text = text;
        }
    }
}