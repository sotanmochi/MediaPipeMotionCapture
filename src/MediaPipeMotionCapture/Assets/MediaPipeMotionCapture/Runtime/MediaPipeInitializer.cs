using Cysharp.Threading.Tasks;
using Mediapipe;
using Mediapipe.Unity;

namespace MediaPipeMotionCapture
{
    public static class MediaPipeInitializer
    {
        private static bool _isInitialized;
        private static bool _isGlogInitialized;
        private static IResourceManager _resourceManager;

        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// MediaPipe サブシステムを初期化する。アプリ起動時に一度だけ呼ぶ。
        /// </summary>
        public static async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                DebugLogger.Log("[MediaPipeInitializer] Already initialized.");
                return;
            }

            Protobuf.SetLogHandler(Protobuf.DefaultLogHandler);

            Glog.Logtostderr = true;
            Glog.Minloglevel = 0;
            Glog.Initialize("MediaPipeMotionCapture");
            _isGlogInitialized = true;

            _resourceManager = new StreamingAssetsResourceManager();

#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
            DebugLogger.Log("[MediaPipeInitializer] Current platform does not support GPU inference mode, so falling back to CPU mode.");
#else
            DebugLogger.Log("[MediaPipeInitializer] Initializing GPU resources...");
            await GpuManager.Initialize().ToUniTask();
#endif

            _isInitialized = true;
            DebugLogger.Log($"[MediaPipeInitializer] Initialization complete. GpuManager.IsInitialized={GpuManager.IsInitialized}");
        }

        /// <summary>
        /// モデルアセットを使用可能にする。タスク作成前に呼ぶ必要がある。
        /// </summary>
        public static async UniTask PrepareModelAsync(string modelFileName, bool overwrite = false)
        {
            if (_resourceManager == null)
            {
                DebugLogger.LogError("[MediaPipeInitializer] Not initialized. Call InitializeAsync first.");
                return;
            }
            await _resourceManager.PrepareAssetAsync(modelFileName, overwrite).ToUniTask();
        }

        /// <summary>
        /// MediaPipe サブシステムをシャットダウンする。アプリ終了時に呼ぶ。
        /// </summary>
        public static void Shutdown()
        {
            GpuManager.Shutdown();

            if (_isGlogInitialized)
            {
                Glog.Shutdown();
                _isGlogInitialized = false;
            }

            Protobuf.ResetLogHandler();
            _resourceManager = null;
            _isInitialized = false;
        }
    }
}
