Shader "Stronghold/Board" {
 Properties { _MainTex("Atlas",2D)="white"{} _EmissionTex("Emission",2D)="black"{} _Color("Tint",Color)=(1,1,1,1) }
 SubShader { Tags {"RenderType"="Opaque"} Cull Back
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float4 color:COLOR;};
 sampler2D _MainTex,_EmissionTex; float4 _Color;
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.normal=UnityObjectToWorldNormal(v.normal);o.color=v.color*_Color;return o;}
 fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv);clip(c.a-.01);float light=.45+.65*max(0,dot(normalize(i.normal),normalize(float3(-5.2,10,-3.4))));return fixed4(c.rgb*i.color.rgb*light+tex2D(_EmissionTex,i.uv).rgb*.25,1);}
 ENDCG
 }
 }
}
