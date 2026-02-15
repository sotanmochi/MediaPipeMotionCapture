Shader "MediaPipeMotionCapture/ImageInpainting"
{
    Properties
    {
        _CameraTex ("Camera Texture", 2D) = "" {}
        _BackgroundTex ("Background Texture", 2D) = "" {}
        _Width ("Mask Width", Int) = 0
        _Height ("Mask Height", Int) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _CameraTex;
            float4 _CameraTex_ST;
            sampler2D _BackgroundTex;
            int _Width;
            int _Height;
            uniform StructuredBuffer<float> _MaskBuffer;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _CameraTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                int idx = int(i.uv.y * _Height) * _Width + int(i.uv.x * _Width);
                float mask = _MaskBuffer[idx];

                fixed4 camera = tex2D(_CameraTex, i.uv);
                fixed4 background = tex2D(_BackgroundTex, i.uv);

                // mask=1 (人物) → 背景で置換、mask=0 (背景) → カメラ映像をそのまま表示
                return lerp(camera, background, mask);
            }
            ENDCG
        }
    }
}
