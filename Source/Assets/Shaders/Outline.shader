Shader "Custom/Outline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        
        _UseOutline("Outline", Float) = 1 // _Outline이 0이면 비활성화, 이외에는 활성화
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineSize("Outline Size", Range(0,10)) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float _UseOutline;
                float4 _OutlineColor;
                float _OutlineSize;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 원본 스프라이트 샘플링
                half4 spriteColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // outline 비활성화, 알파값이 존재할 경우 원본 스프라이트 반환
                if(_UseOutline == 0 || spriteColor.a > 0.01)
                {
                    return spriteColor * input.color;
                }

                float2 texelSize = _OutlineSize * 0.01;
                float maxAlpha = 0;
                
                // outline을 찾기 위한 주변 픽셀 샘플링
                for(int x = -1; x <= 1; x++)
                {
                    for(int y = -1; y <= 1; y++)
                    {
                        if(x == 0 && y == 0) continue;
                        float2 sampleUV = input.uv + float2(x * texelSize.x, y * texelSize.y);
                        float sampleAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).a;
                        maxAlpha = max(maxAlpha, sampleAlpha);
                    }
                }
                
                // Return outline color if there's alpha in surrounding pixels
                half4 finalColor = half4(_OutlineColor.rgb, maxAlpha * _OutlineColor.a);
                return finalColor * input.color;
            }
            ENDHLSL
        }
    }
}
