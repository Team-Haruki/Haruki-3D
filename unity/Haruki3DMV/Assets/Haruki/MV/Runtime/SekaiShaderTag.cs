namespace Sekai.Rendering
{
    public enum SekaiShaderTagType
    {
        Default = 0,
        OpaqueOutline = 1,
        OpaqueReflection = 2,
        TransparentBase = 3,
        TransparentOutline = 4,
        TransparentReflection = 5,
        MeshFlarePara = 6,
        Monitor = 7,
        Eyelash = 8,
    }

    public static class SekaiShaderTag
    {
        public static string GetTag(SekaiShaderTagType shaderTagType)
        {
            switch (shaderTagType)
            {
                case SekaiShaderTagType.Default:
                    return "SRPDefaultUnlit";
                case SekaiShaderTagType.OpaqueOutline:
                    return "SekaiOutline";
                case SekaiShaderTagType.OpaqueReflection:
                    return "SekaiReflection";
                case SekaiShaderTagType.TransparentBase:
                    return "SekaiTransparentBase";
                case SekaiShaderTagType.TransparentOutline:
                    return "SekaiTransparentOutline";
                case SekaiShaderTagType.TransparentReflection:
                    return "SekaiTransparentReflection";
                case SekaiShaderTagType.MeshFlarePara:
                    return "SekaiMeshFlarePara";
                case SekaiShaderTagType.Monitor:
                    return "SekaiMonitor";
                case SekaiShaderTagType.Eyelash:
                    return "SekaiEyelash";
                default:
                    return "SRPDefaultUnlit";
            }
        }
    }
}
