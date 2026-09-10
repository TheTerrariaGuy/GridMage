Shader "Grid Mage/Element Particle"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Sway ("Grass tip sway", Float) = 0
        _SwaySpeed ("Wind speed", Float) = 2.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Sway;
            float _SwaySpeed;
            struct Attributes { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; fixed4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = mul(unity_ObjectToWorld, input.vertex).xyz;
                // UV.y pins the root of each blade and bends only its tip.
                float wind = sin(_Time.y * _SwaySpeed + world.x * 3.1 + world.y * 1.7);
                wind += 0.3 * sin(_Time.y * _SwaySpeed * 1.63 + world.x * 5.0);
                input.vertex.x += wind * _Sway * input.uv.y * input.uv.y;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Tint;
                return output;
            }
            fixed4 frag(Varyings input) : SV_Target { return input.color; }
            ENDCG
        }
    }
}
