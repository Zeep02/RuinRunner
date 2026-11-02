Shader "RuinRunner/PixelTrail"
{
    Properties { _MainTex ("Trail Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
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
            fixed4 frag(Output input) : SV_Target { return tex2D(_MainTex,input.uv)*input.color; }
            ENDCG
        }
    }
}
