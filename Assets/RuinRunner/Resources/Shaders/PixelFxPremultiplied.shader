Shader "RuinRunner/PixelFxPremultiplied"
{
    Properties { [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Input { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            Output vert(Input input)
            {
                Output output;
                output.position=UnityObjectToClipPos(input.vertex);
                output.color=input.color;
                output.uv=input.uv;
                return output;
            }
            // Colour with coverage over the scene, plus light added to it. Fading scales both.
            fixed4 frag(Output input) : SV_Target
            {
                fixed4 pixel=tex2D(_MainTex,input.uv);
                pixel.rgb*=input.color.rgb*input.color.a;
                pixel.a*=input.color.a;
                return pixel;
            }
            ENDCG
        }
    }
}
