// Casts a soft drop / floor shadow behind a 2D sprite or a uGUI element.
//
// Pass 1 draws the sprite silhouette, displaced + skewed + squashed in local
// space and tinted with the shadow colour. Pass 2 draws the sprite itself on top.
// Because the shadow is real geometry (not an offset texture lookup) it is never
// clipped by the sprite quad, so it can fall well outside the sprite bounds.
//
// Works with SpriteRenderer (turn "Sprite Renderer Mode" on) and with
// Image / RawImage on a Canvas (leave it off). Mask, RectMask2D and CanvasGroup
// alpha are all respected.
Shader "GameJam/2D Drop Shadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Shadow)]
        [Space(4)]
        _ShadowColor ("Shadow Color", Color) = (0,0,0,1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.5
        _ShadowOffset ("Shadow Offset (XY, local units)", Vector) = (0.15,-0.15,0,0)
        _ShadowScale ("Shadow Scale (XY)", Vector) = (1,1,0,0)
        _ShadowSkew ("Shadow Skew X", Range(-3,3)) = 0
        _ShadowAnchorY ("Shadow Anchor Y (local units)", Float) = 0
        _ShadowSoftness ("Shadow Softness (texels)", Range(0,16)) = 2
        _UVRect ("Sprite UV Rect (xy = min, zw = max)", Vector) = (0,0,1,1)

        [Header(Cast Shadow)]
        [Space(4)]
        _CastShadowCutoff ("Cast Shadow Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Lighting)]
        [Space(4)]
        [KeywordEnum(None, Simple, Lights2D, Scene3D)] _Lighting ("Lighting Mode", Float) = 0
        _AmbientColor ("Ambient (Simple mode)", Color) = (0.55,0.55,0.6,1)
        _MaskTex ("Light Mask (Lights2D mode)", 2D) = "white" {}
        _AmbientBoost ("Ambient Fill (Scene3D mode)", Color) = (0,0,0,1)
        _NormalInfluence ("Directional Shading (Scene3D mode)", Range(0,1)) = 0.6
        _Roundness ("Sprite Roundness (Scene3D mode)", Range(0,2)) = 0.6

        [Header(Setup)]
        [Space(4)]
        [Toggle(_SPRITE_MODE)] _SpriteMode ("Sprite Renderer Mode (tint plus flip)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4

        // Standard uGUI plumbing - lets Mask / RectMask2D drive this material.
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest [_ZTest]
        ColorMask [_ColorMask]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_MaskTex);
        SAMPLER(sampler_MaskTex);

        // Per-renderer / per-canvas data, deliberately outside the material cbuffer.
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;
        float4 _ClipRect;

        // Simple lighting: filled in by SimpleLight2D.cs through global arrays, so
        // one shared material still sees every light in the scene.
        #define MAX_SIMPLE_LIGHTS 8
        float4 _SimpleLightPositions[MAX_SIMPLE_LIGHTS];  // xyz = world position, w = range
        float4 _SimpleLightColors[MAX_SIMPLE_LIGHTS];     // rgb = colour * intensity, a = falloff
        int _SimpleLightCount;

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _ShadowColor;
            half4 _AmbientColor;
            half4 _AmbientBoost;
            half _NormalInfluence;
            half _Roundness;
            float4 _ShadowOffset;
            float4 _ShadowScale;
            float4 _UVRect;
            float _ShadowStrength;
            float _ShadowSkew;
            float _ShadowAnchorY;
            float _ShadowSoftness;
            float _CastShadowCutoff;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
            half4  color      : COLOR;
        };

        struct Varyings
        {
            float4 positionCS    : SV_POSITION;
            float2 uv            : TEXCOORD0;
            float2 positionLocal : TEXCOORD1;
            float3 positionWS    : TEXCOORD2;
            half2  lightingUV    : TEXCOORD3;
            half3  normalWS      : TEXCOORD4;
            half4  color         : COLOR;
        };

        // uGUI rect clipping (RectMask2D). _ClipRect is in canvas space, which is
        // the object space of the canvas mesh - the same space as positionLocal.
        float Get2DClipping(float2 position, float4 clipRect)
        {
            float2 inside = step(clipRect.xy, position) * step(position, clipRect.zw);
            return inside.x * inside.y;
        }

        float3 ApplySpriteFlip(float3 positionOS)
        {
        #ifdef _SPRITE_MODE
            positionOS.xy *= unity_SpriteProps.xy;
        #endif
            return positionOS;
        }

        half4 ApplyTint(half4 vertexColor)
        {
            half4 c = vertexColor * _Color;
        #ifdef _SPRITE_MODE
            c *= unity_SpriteColor;
        #endif
            return c;
        }

        half SampleAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, clamp(uv, _UVRect.xy, _UVRect.zw)).a;
        }

        // 9 tap ring blur on the silhouette alpha - cheap, and enough for the
        // small radii a drop shadow needs.
        half SampleShadowAlpha(float2 uv)
        {
            half a = SampleAlpha(uv);

            UNITY_BRANCH
            if (_ShadowSoftness > 0.001)
            {
                float2 r = _MainTex_TexelSize.xy * _ShadowSoftness;
                float2 d = r * 0.70710678;

                half sum = a;
                sum += (SampleAlpha(uv + float2(r.x, 0)) + SampleAlpha(uv - float2(r.x, 0)) +
                        SampleAlpha(uv + float2(0, r.y)) + SampleAlpha(uv - float2(0, r.y))) * 0.75;
                sum += (SampleAlpha(uv + d) + SampleAlpha(uv - d) +
                        SampleAlpha(uv + float2(d.x, -d.y)) + SampleAlpha(uv + float2(-d.x, d.y))) * 0.5;
                a = sum / 6.0;
            }

            return a;
        }

        // Ambient plus a handful of point lights, evaluated per pixel in world
        // space. Independent of the render pipeline's own lighting, so it works
        // with a perspective camera, with 3D geometry in the scene, and on UI.
        half3 ComputeSimpleLighting(float3 positionWS)
        {
            half3 sum = _AmbientColor.rgb;
            int count = min(_SimpleLightCount, MAX_SIMPLE_LIGHTS);

            UNITY_LOOP
            for (int i = 0; i < count; i++)
            {
                float range = max(_SimpleLightPositions[i].w, 0.0001);
                float dist = distance(_SimpleLightPositions[i].xyz, positionWS);
                half atten = saturate(1.0 - dist / range);
                atten = pow(atten, max(_SimpleLightColors[i].a, 0.0001));
                sum += _SimpleLightColors[i].rgb * atten;
            }

            return sum;
        }

        half4 ClipUI(half4 color, float2 positionLocal)
        {
        #ifdef UNITY_UI_CLIP_RECT
            color.a *= Get2DClipping(positionLocal, _ClipRect);
        #endif
        #ifdef UNITY_UI_ALPHACLIP
            clip(color.a - 0.001);
        #endif
            return color;
        }

        Varyings ShadowVertex(Attributes IN)
        {
            Varyings OUT = (Varyings)0;

            float3 positionOS = ApplySpriteFlip(IN.positionOS);

            // Squash / skew / offset around the anchor line, all in local units.
            float2 p = positionOS.xy;
            p.y -= _ShadowAnchorY;
            p   *= _ShadowScale.xy;
            p.x += p.y * _ShadowSkew;
            p.y += _ShadowAnchorY;
            p   += _ShadowOffset.xy;

            OUT.positionLocal = p;
            OUT.positionCS = TransformObjectToHClip(float3(p, positionOS.z));
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

            half4 shadow = _ShadowColor;
            shadow.a *= _ShadowStrength * _Color.a * IN.color.a;
        #ifdef _SPRITE_MODE
            shadow.a *= unity_SpriteColor.a;
        #endif
            OUT.color = shadow;

            return OUT;
        }

        half4 ShadowFragment(Varyings IN) : SV_Target
        {
            half4 c = half4(IN.color.rgb, SampleShadowAlpha(IN.uv) * IN.color.a);
            return ClipUI(c, IN.positionLocal);
        }

        Varyings MainVertex(Attributes IN)
        {
            Varyings OUT = (Varyings)0;

            float3 positionOS = ApplySpriteFlip(IN.positionOS);

            OUT.positionLocal = positionOS.xy;
            OUT.positionCS = TransformObjectToHClip(positionOS);
            OUT.positionWS = TransformObjectToWorld(positionOS);
            OUT.lightingUV = half2(ComputeScreenPos(OUT.positionCS / OUT.positionCS.w).xy);

            // A sprite quad's normal faces straight out, which shades dead flat under
            // a directional light. Bending it across the sprite's width fakes a
            // cylinder, so the character catches the light down one side.
            float u = (IN.uv.x - _UVRect.x) / max(_UVRect.z - _UVRect.x, 1e-5);
            float3 normalOS = float3((u - 0.5) * 2.0 * _Roundness, 0, -1);
        #ifdef _SPRITE_MODE
            normalOS.x *= unity_SpriteProps.x;
        #endif
            OUT.normalWS = half3(TransformObjectToWorldNormal(normalize(normalOS)));
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
            OUT.color = ApplyTint(IN.color);

            return OUT;
        }

        half4 MainFragment(Varyings IN) : SV_Target
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

        #if defined(_LIGHTING_SIMPLE)
            c.rgb *= ComputeSimpleLighting(IN.positionWS);
        #elif defined(_LIGHTING_LIGHTS2D)
            SurfaceData2D surfaceData;
            InputData2D inputData;
            half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv);

            InitializeSurfaceData(c.rgb, c.a, mask, surfaceData);
            InitializeInputData(IN.uv, IN.lightingUV, inputData);
            c = CombinedShapeLightShared(surfaceData, inputData);
        #endif

            return ClipUI(c, IN.positionLocal);
        }
        ENDHLSL

        // Drawn first. Tagged SRPDefaultUnlit because that is the pass the URP 2D
        // renderer submits ahead of Universal2D, which keeps the shadow behind the
        // sprite without needing a second GameObject.
        Pass
        {
            Name "DropShadow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma shader_feature_local _SPRITE_MODE
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            ENDHLSL
        }

        Pass
        {
            Name "Sprite"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex MainVertex
            #pragma fragment MainFragment
            #pragma shader_feature_local _SPRITE_MODE
            #pragma shader_feature_local _LIGHTING_NONE _LIGHTING_SIMPLE _LIGHTING_LIGHTS2D
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // Declares the USE_SHAPE_LIGHT_TYPE_x keywords the 2D renderer sets.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            ENDHLSL
        }

        // Identical to the pass above, for the Universal (3D) Renderer, which draws
        // UniversalForward and never Universal2D. The 2D renderer is the mirror
        // image, so exactly one of the two is ever submitted - no double draw.
        Pass
        {
            Name "SpriteForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex MainVertex
            #pragma fragment ForwardFragment
            #pragma shader_feature_local _SPRITE_MODE
            #pragma shader_feature_local _LIGHTING_NONE _LIGHTING_SIMPLE _LIGHTING_LIGHTS2D _LIGHTING_SCENE3D
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // Real URP lighting, for the Scene3D mode.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Ambient probe, main light with its shadow, then any additional lights.
            // The sprite has no real surface, so Directional Shading blends between
            // flat (the sprite just takes the light's colour and shadowing) and a
            // full N dot L off the faked cylindrical normal.
            half3 ComputeSceneLighting(Varyings IN)
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 lighting = SampleSHPixel(half3(0, 0, 0), normalWS) + _AmbientBoost.rgb;

            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
            #else
                Light mainLight = GetMainLight();
            #endif

                half shading = lerp(1.0h, saturate(dot(normalWS, mainLight.direction)), _NormalInfluence);
                lighting += mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * shading);

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                    half addShading = lerp(1.0h, saturate(dot(normalWS, light.direction)), _NormalInfluence);
                    lighting += light.color * (light.distanceAttenuation * light.shadowAttenuation * addShading);
                LIGHT_LOOP_END
            #endif

                return lighting;
            }

            half4 ForwardFragment(Varyings IN) : SV_Target
            {
            #if defined(_LIGHTING_SCENE3D)
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                c.rgb *= ComputeSceneLighting(IN);
                return ClipUI(c, IN.positionLocal);
            #else
                return MainFragment(IN);
            #endif
            }
            ENDHLSL
        }

        // Real cast shadows, for the Universal (3D) Renderer. The sprite's alpha is
        // clipped so the shadow takes the shape of the character rather than of the
        // quad. Requires the SpriteRenderer's Cast Shadows to be On, and does
        // nothing under the 2D renderer, which has no shadow map.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex ShadowCasterVertex
            #pragma fragment ShadowCasterFragment
            #pragma shader_feature_local _SPRITE_MODE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by ShadowUtils.SetupShadowCasterConstantBuffer.
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            ShadowVaryings ShadowCasterVertex(ShadowAttributes IN)
            {
                ShadowVaryings OUT = (ShadowVaryings)0;

                float3 positionOS = ApplySpriteFlip(IN.positionOS);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                OUT.positionCS = ApplyShadowClamping(
                    TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return OUT;
            }

            half4 ShadowCasterFragment(ShadowVaryings IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * _Color.a;
                clip(alpha - _CastShadowCutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
