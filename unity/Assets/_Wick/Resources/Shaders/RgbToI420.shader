// Converts a rendered clip frame to I420 (BT.601, video range) in one blit, into an R8 target
// W wide and 1.5 H tall: H rows of Y, then the U plane, then the V plane, packed row after row.
// Row 0 of the target is the first row of bytes the readback returns, and holds the top of the
// picture. Loaded from Resources by ClipRenderer, so it ships with the build.
Shader "Hidden/Wick/RgbToI420"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            // x = video width, y = video height, z = 1 when the source holds linear colour, w = 1 to flip vertically.
            float4 _Clip;

            float3 Rgb(float2 uv)
            {
                if (_Clip.w > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                float3 c = tex2D(_MainTex, uv).rgb;
                if (_Clip.z > 0.5)
                {
                    c = LinearToGammaSpace(c);
                }

                return saturate(c);
            }

            float Luma(float3 c) { return (16.0 + 65.481 * c.r + 128.553 * c.g + 24.966 * c.b) / 255.0; }
            float Cb(float3 c) { return (128.0 - 37.797 * c.r - 74.203 * c.g + 112.0 * c.b) / 255.0; }
            float Cr(float3 c) { return (128.0 + 112.0 * c.r - 93.786 * c.g - 18.214 * c.b) / 255.0; }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float w = _Clip.x;
                float h = _Clip.y;
                float x = floor(i.uv.x * w);
                float row = floor(i.uv.y * h * 1.5);

                if (row < h)
                {
                    return Luma(Rgb(float2((x + 0.5) / w, 1.0 - (row + 0.5) / h)));
                }

                float index = (row - h) * w + x;
                float quarter = w * h * 0.25;
                bool isV = index >= quarter;
                if (isV)
                {
                    index -= quarter;
                }

                float half = w * 0.5;
                float cy = floor(index / half);
                float cx = index - cy * half;
                float3 c = Rgb(float2((cx * 2.0 + 1.0) / w, 1.0 - (cy * 2.0 + 1.0) / h)); // centre of the 2x2 block
                return isV ? Cr(c) : Cb(c);
            }
            ENDCG
        }
    }
}
