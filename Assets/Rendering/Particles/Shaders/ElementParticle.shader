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
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.0
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Sway;
            float _SwaySpeed;
            // Global, deliberately not a material property: every effect shares one grid.
            float _ParticlePixelWorldSize;
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
            struct PixelVaryings
            {
                float4 position : SV_POSITION;
                nointerpolation float2 a : TEXCOORD0;
                nointerpolation float2 b : TEXCOORD1;
                nointerpolation float2 c : TEXCOORD2;
                nointerpolation float4 colorA : TEXCOORD3;
                nointerpolation float4 colorB : TEXCOORD4;
                nointerpolation float4 colorC : TEXCOORD5;
                nointerpolation float3 inverseW : TEXCOORD6;
            };

            float CellSize()
            {
                // Convert game-space length using this camera's projection and render target.
                // Keep fractional sizes so resizing or zooming preserves the world-space scale.
                return max(1, _ParticlePixelWorldSize * abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5);
            }

            float2 ScreenPixel(float4 position)
            {
                float2 ndc = position.xy / position.w;
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y = -ndc.y;
                #endif
                return (ndc * 0.5 + 0.5) * _ScreenParams.xy;
            }

            float Edge(float2 a, float2 b, float2 p)
            {
                float2 d = b - a;
                float2 q = p - a;
                return d.x * q.y - d.y * q.x;
            }

            // A half-open edge rule gives adjacent triangles exactly one owner,
            // including cells whose centers land on an internal mesh diagonal.
            bool Inside(float edge, float2 a, float2 b)
            {
                float2 d = b - a;
                return edge > 0 || (edge == 0 && (d.y > 0 || (d.y == 0 && d.x < 0)));
            }

            [maxvertexcount(4)]
            void geom(triangle Varyings input[3], inout TriangleStream<PixelVaryings> stream)
            {
                if (min(input[0].position.w, min(input[1].position.w, input[2].position.w)) <= 0) return;
                PixelVaryings output;
                output.a = ScreenPixel(input[0].position);
                output.b = ScreenPixel(input[1].position);
                output.c = ScreenPixel(input[2].position);
                float area = Edge(output.a, output.b, output.c);
                if (abs(area) < 0.00001) return;
                output.colorA = input[0].color;
                output.colorB = input[1].color;
                output.colorC = input[2].color;
                output.inverseW = rcp(float3(input[0].position.w, input[1].position.w, input[2].position.w));
                if (area < 0)
                {
                    float2 swappedPosition = output.b; output.b = output.c; output.c = swappedPosition;
                    float4 color = output.colorB; output.colorB = output.colorC; output.colorC = color;
                    output.inverseW.yz = output.inverseW.zy;
                }
                float size = CellSize();
                // Expand coverage to whole cells. Snapping vertices alone cannot
                // pixelate sloping silhouettes, and fragment-only snapping clips cells.
                float2 lo = floor(min(output.a, min(output.b, output.c)) / size) * size;
                float2 hi = ceil(max(output.a, max(output.b, output.c)) / size) * size;
                for (int i = 0; i < 4; i++)
                {
                    float2 pixel = float2((i & 1) ? hi.x : lo.x, (i & 2) ? hi.y : lo.y);
                    float2 ndc = pixel / _ScreenParams.xy * 2 - 1;
                    #if UNITY_UV_STARTS_AT_TOP
                        ndc.y = -ndc.y;
                    #endif
                    // Authored effects are planar XY meshes, retaining their original Z.
                    output.position = float4(ndc, input[0].position.z / input[0].position.w, 1);
                    stream.Append(output);
                }
                stream.RestartStrip();
            }

            fixed4 frag(PixelVaryings input) : SV_Target
            {
                float size = CellSize();
                float2 samplePixel = (floor(input.position.xy / size) + 0.5) * size;
                float3 edges = float3(Edge(input.b, input.c, samplePixel),
                    Edge(input.c, input.a, samplePixel), Edge(input.a, input.b, samplePixel));
                if (!Inside(edges.x, input.b, input.c) || !Inside(edges.y, input.c, input.a)
                    || !Inside(edges.z, input.a, input.b)) discard;
                float3 weights = edges * input.inverseW;
                weights /= weights.x + weights.y + weights.z;
                return input.colorA * weights.x + input.colorB * weights.y + input.colorC * weights.z;
            }
            ENDCG
        }
    }
}
