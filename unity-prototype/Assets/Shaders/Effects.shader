Shader "Stronghold/Effects" {
 Properties { _MainTex("Atlas",2D)="white"{} }
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 sampler2D _MainTex;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(v2f i):SV_Target{return tex2D(_MainTex,i.uv)*i.color;}
 ENDCG
 }
 }
}
