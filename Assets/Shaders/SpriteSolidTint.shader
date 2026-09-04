// Sprite'in RGB'sini yok sayar, YALNIZCA alfa kanalini maske olarak kullanip _Color ile boyar.
//
// Neden gerekli: dial_needle.png sadece %3,5 opak, ince bir kontur cizimi (navy 39-53-76).
// Kademe 6/7/8 kadran zeminleri koyu lacivert oldugu icin ibre orada gorunmez kalirdi.
// Carpimsal Image.color tint de ise yaramaz — koyu konturu daha da koyulastirir.
// Bu shader konturu istenen renkte, tam parlaklikta cizer.
//
// _AlphaBoost: antialias kenarlarin alfasini yukselterek ince cizgiyi optik olarak kalinlastirir.
Shader "SpeedDownload/SpriteSolidTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AlphaBoost ("Alpha Boost", Range(1,4)) = 1

        // UI Mask / RectMask2D icinde dogru calismasi icin gereken standart alanlar
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
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
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _AlphaBoost;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                fixed a = tex2D(_MainTex, IN.texcoord).a;
                a = saturate(a * _AlphaBoost);

                fixed4 c;
                c.rgb = _Color.rgb * IN.color.rgb;
                c.a   = a * _Color.a * IN.color.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
