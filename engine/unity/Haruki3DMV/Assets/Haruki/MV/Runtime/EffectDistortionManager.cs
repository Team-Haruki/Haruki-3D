namespace Sekai.Rendering
{
    /// <summary>
    /// Official global stage gate consumed by the after-transparent distortion
    /// feature. Multiple stage loads OR into this state.
    /// </summary>
    public sealed class EffectDistortionManager
    {
        private static EffectDistortionManager _instance;

        public bool EnableUseEffectDistortion { get; set; }

        public static EffectDistortionManager Instance =>
            _instance ?? (_instance = new EffectDistortionManager());

        public static void Dispose()
        {
            _instance = null;
        }
    }
}
