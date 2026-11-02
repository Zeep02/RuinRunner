Shader "RuinRunner/ItemOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                float a=0;
                for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
                    a=max(a,tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy).a);
                // One native pixel: independent of world/canvas zoom. Transparent
                // padding in the imported sprite keeps the contour inside its mesh.
                fixed3 ink=fixed3(.067,.043,.09);
                return fixed4(lerp(ink,c.rgb,c.a)*i.color.rgb,max(c.a,a)*i.color.a);
            }
            ENDCG
        }
    }
}
