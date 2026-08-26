Shader "Sekai/Live/Character/Toon"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _ShadowTex ("Shadow", 2D) = "white" {}
        _ValueTex ("Value", 2D) = "gray" {}
        _FaceShadowTex ("FaceShadow", 2D) = "white" {}
        _EyelashMaskTex ("Eyelash Mask", 2D) = "white" {}
        [Toggle(_LAMBERT)] _UseLambert ("Use Lambert", Float) = 1
        _OutlineWidth ("Outline Width", Float) = 0.001
        _OutlineOffset ("Outline Offset", Float) = 0
        _ShadowTexWeight ("Shadow Texture Weight", Range(0,1)) = 1
        _ShadowWidth ("Shadow Width", Range(0,1)) = 0
        _FadeMode ("Fade Mode", Float) = 0
        _RimThreshold ("Rim Threshold", Range(0.01,1)) = 0.2
        _SpecularPower ("Specular Power", Range(0,5)) = 0
        _DefaultSkinColor ("Default Skin", Color) = (0.9921875,0.9609375,0.921875,1)
        _Shadow1SkinColor ("Shadow 1 Skin", Color) = (0.890625,0.76953125,0.796875,1)
        _Shadow2SkinColor ("Shadow 2 Skin", Color) = (0.796875,0.59375,0.63671875,1)
        _PartsAmbientColor ("Parts Ambient", Color) = (1,1,1,1)
        [HideInInspector] _HeadDotDirectionalLightValues ("Head Dot Light", Vector) = (1,1,1,1)
        [HideInInspector] _HeadPosition ("Head Position", Vector) = (0,0,0,0)
        [HideInInspector] _FaceFront ("Face Front", Vector) = (0,0,1,0)
        [HideInInspector] _UseFaceSDF ("Use Face SDF", Float) = 0
        [HideInInspector] _UseValueTex ("Use Value", Float) = 1
        [HideInInspector] _UseFaceShadowLimiter ("Use Face Limiter", Float) = 0
        [HideInInspector] _RangeLimit ("Range Limit", Float) = 0
        [HideInInspector] _FaceSdfMirror ("Face SDF Mirror", Float) = 1
        [HideInInspector] _FaceSdfBias ("Face SDF Bias", Float) = 0
        [HideInInspector] _UseSkinColor ("Use Skin", Float) = 0
        [HideInInspector] _SkinMaskMode ("Skin Mask Mode", Float) = 1
        [HideInInspector] _FaceSphereShadowEdge ("Face Sphere Edge", Float) = 0
        [HideInInspector] _FaceSphereShadowSmoothness ("Face Sphere Smoothness", Float) = 0
        [HideInInspector] _FaceSphereShadowWeight ("Face Sphere Weight", Float) = 0
        [HideInInspector] _FinalSat ("Final Saturation", Float) = 0.95
        [HideInInspector] _Brightness ("Brightness", Float) = 1
        [HideInInspector] _HighlightRolloff ("Highlight Rolloff", Float) = 0.8
        [HideInInspector] _HueSinAngle ("Hue Sin", Float) = 0
        [HideInInspector] _HueCosAngle ("Hue Cos", Float) = 1
        [HideInInspector] _Saturation ("Saturation", Float) = 0.5
        [HideInInspector] _Value ("Value", Float) = 0.5
        [HideInInspector] _Contrast ("Contrast", Float) = 0.5
        [HideInInspector] _CharacterId ("Character Id", Float) = -1
        [HideInInspector] _FormationId ("Formation Id", Float) = 0
        [HideInInspector] _Transparency ("Transparency", Float) = 0
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _UseAlphaClip ("Use Alpha Clip", Float) = 0
        [HideInInspector] _UseEyelash ("Use Eyelash Pass", Float) = 0
        [HideInInspector] _IsLeftEyeClose ("Left Eye Close", Float) = 0
        [HideInInspector] _IsRightEyeClose ("Right Eye Close", Float) = 0
        [HideInInspector] _EyelashTransparent ("Eyelash Transparent", Float) = 1
        [HideInInspector] _EyelashFaceCameraEdge1 ("Eyelash Edge 1", Float) = 0.5
        [HideInInspector] _EyelashFaceCameraEdge2 ("Eyelash Edge 2", Float) = 0
        [HideInInspector] _Stencil ("Stencil", Float) = 1
        [HideInInspector] _ReadMask ("Read Mask", Float) = 255
        [HideInInspector] _WriteMask ("Write Mask", Float) = 255
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilOp ("Stencil Op", Float) = 2
        [HideInInspector] _StencilFail ("Stencil Fail", Float) = 0
        [HideInInspector] _StencilZFail ("Stencil ZFail", Float) = 0
        [HideInInspector] _EyelashStencil ("Eyelash Stencil", Float) = 1
        [HideInInspector] _EyelashReadMask ("Eyelash Read Mask", Float) = 255
        [HideInInspector] _EyelashWriteMask ("Eyelash Write Mask", Float) = 255
        [HideInInspector] _EyelashStencilComp ("Eyelash Stencil Comp", Float) = 3
        [HideInInspector] _EyelashStencilOp ("Eyelash Stencil Op", Float) = 2
        [HideInInspector] _EyelashStencilFail ("Eyelash Stencil Fail", Float) = 0
        [HideInInspector] _EyelashStencilZFail ("Eyelash Stencil ZFail", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        UsePass "Sekai/Live/Character/Toon-v3/Base"
        UsePass "Sekai/Live/Character/Toon-v3/Outline"
        UsePass "Sekai/Live/Character/Toon-v3/Eyelash"
    }
}
