// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/Paint"
{

    Properties
    {
        [HDR]_Color ("Color", Color) = (1,1,1,1)
        _SplatSize ("SplatSize", Float) = 0
        _Lit ("Lit", Float) = 1
        _s0 ("Splat0", Float) = (1,1,1)
        _s1 ("Splat1", Float) = (1,1,1)
        _s2 ("Splat2", Float) = (1,1,1)
        _s3 ("Splat3", Float) = (1,1,1)
        _s4 ("Splat4", Float) = (1,1,1)
        _s5 ("Splat5", Float) = (1,1,1)
        _s6 ("Splat6", Float) = (1,1,1)
        _s7 ("Splat7", Float) = (1,1,1)
        _s8 ("Splat8", Float) = (1,1,1)
        _s9 ("Splat9", Float) = (1,1,1)
        _s10 ("Splat10", Float) = (1,1,1)
        _s11 ("Splat11", Float) = (1,1,1)
        _s12 ("Splat12", Float) = (1,1,1)
        _s13 ("Splat13", Float) = (1,1,1)
        _s14 ("Splat14", Float) = (1,1,1)
        _s15 ("Splat15", Float) = (1,1,1)
        _s16 ("Splat16", Float) = (1,1,1)
        
        // Advanced options.
		//[Header(System Render Flags)]
        [Enum(RenderingMode)] _Mode("Rendering Mode", Float) = 0                                     // "Opaque"
        [Enum(CustomRenderingMode)] _CustomMode("Mode", Float) = 0                                   // "Opaque"
        [Enum(DepthWrite)] _AtoCMode("Alpha to Mask", Float) = 0                                     // "Off"
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1                 // "One"
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0            // "Zero"
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Operation", Float) = 0                 // "Add"
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4                // "LessEqual"
        [Enum(DepthWrite)] _ZWrite("Depth Write", Float) = 1                                         // "On"
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorWriteMask("Color Write Mask", Float) = 15 // "All"
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 2                     // "Back"
        _RenderQueueOverride("Render Queue Override", Range(-1.0, 5000)) = -1
        
        [IntRange] _Stencil ("Stencil ID [0;255]", Range(0,255)) = 0
	    _ReadMask ("ReadMask [0;255]", Int) = 255
	    _WriteMask ("WriteMask [0;255]", Int) = 255
	    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Int) = 0
	    [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Int) = 0
	    [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Int) = 0
	    [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Int) = 0
        _ColorMask ("Color Mask", Float) = 15
	    [HideInInspector]__Baked ("Is this material referencing a baked shader?", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Blend[_SrcBlend][_DstBlend]
        BlendOp[_BlendOp]
        ZTest[_ZTest]
        ZWrite[_ZWrite]
        Cull[_CullMode]
        LOD 100
        
        Stencil
        {
            Ref [_Stencil]
            ReadMask [_ReadMask]
            WriteMask [_WriteMask]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }
        ColorMask [_ColorMask]
        CGPROGRAM
            #include "Assets/MMMaellon/SCRIPTS/SimplexNoise3D.hlsl"
            #pragma surface surf Lambert finalcolor:mycolor vertex:myvert
            #pragma multi_compile_fog
            uniform float _Lit;
            uniform fixed4 _Color;					// RGBA : Color + Opacity
            uniform float _SplatSize;
            uniform fixed3 _s0;
            uniform fixed3 _s1;
            uniform fixed3 _s2;
            uniform fixed3 _s3;
            uniform fixed3 _s4;
            uniform fixed3 _s5;
            uniform fixed3 _s6;
            uniform fixed3 _s7;
            uniform fixed3 _s8;
            uniform fixed3 _s9;
            uniform fixed3 _s10;
            uniform fixed3 _s11;
            uniform fixed3 _s12;
            uniform fixed3 _s13;
            uniform fixed3 _s14;
            uniform fixed3 _s15;
            uniform fixed3 _s16;
            
            struct Input {
                float2 uv : TEXCOORD0;
                float4 objectPos;
                float3 worldPos;
                float4 color;
            };
            
            // float Falloff_Xsq_C2( float xsq ) { xsq = 1.0 - xsq; return xsq*xsq*xsq; }	// ( 1.0 - x*x )^3.   NOTE: 2nd derivative is 0.0 at x=1.0, but non-zero at x=0.0
            // float4 Falloff_Xsq_C2( float4 xsq ) { xsq = 1.0 - xsq; return xsq*xsq*xsq; }
            // float4 FAST32_hash_3D_Cell( float3 gridcell )	//	generates 4 different random numbers for the single given cell point
            // {
            //     //    gridcell is assumed to be an integer coordinate

            //     //	TODO: 	these constants need tweaked to find the best possible noise.
            //     //			probably requires some kind of brute force computational searching or something....
            //     const float2 OFFSET = float2( 50.0, 161.0 );
            //     const float DOMAIN = 69.0;
            //     const float4 SOMELARGEFLOATS = float4( 635.298681, 682.357502, 668.926525, 588.255119 );
            //     const float4 ZINC = float4( 48.500388, 65.294118, 63.934599, 63.279683 );

            //     //	truncate the domain
            //     gridcell.xyz = gridcell - floor(gridcell * ( 1.0 / DOMAIN )) * DOMAIN;
            //     gridcell.xy += OFFSET.xy;
            //     gridcell.xy *= gridcell.xy;
            //     return frac( ( gridcell.x * gridcell.y ) * ( 1.0 / ( SOMELARGEFLOATS + gridcell.zzzz * ZINC ) ) );
            // }
            
            // //
            // //	PolkaDot Noise 3D
            // //	http://briansharpe.files.wordpress.com/2011/12/polkadotsample.jpg
            // //	http://briansharpe.files.wordpress.com/2012/01/polkaboxsample.jpg
            // //	TODO, these images have random intensity and random radius.  This noise now has intensity as proportion to radius.  Images need updated.  TODO
            // //
            // //	Generates a noise of smooth falloff polka dots.
            // //	Allow for control on radius.  Intensity is proportional to radius
            // //	Return value range of 0.0->1.0
            // //
            // float PolkaDot3D( 	float3 P,
            //                     float radius_low,		//	radius range is 0.0->1.0
            //                     float radius_high	)
            // {
            //     //	establish our grid cell and unit position
            //     float3 Pi = floor(P);
            //     float3 Pf = P - Pi;

            //     //	calculate the hash.
            //     float4 hash = FAST32_hash_3D_Cell( Pi );

            //     //	user variables
            //     float RADIUS = max( 0.0, radius_low + hash.w * ( radius_high - radius_low ) );
            //     float VALUE = RADIUS / max( radius_high, radius_low );	//	new keep value in proportion to radius.  Behaves better when used for bumpmapping, distortion and displacement

            //     //	calc the noise and return
            //     RADIUS = 2.0/RADIUS;
            //     Pf *= RADIUS;
            //     Pf -= ( RADIUS - 1.0 );
            //     Pf += hash.xyz * ( RADIUS - 2.0 );
            //     //Pf *= Pf;		//	this gives us a cool box looking effect
            //     return Falloff_Xsq_C2( min( dot( Pf, Pf ), 1.0 ) ) * VALUE;
            // }
            
            void myvert (inout appdata_full v, out Input data){
                UNITY_INITIALIZE_OUTPUT(Input,data);
                data.objectPos = v.vertex;
            }
            
            void mycolor (Input IN, SurfaceOutput o, inout fixed4 color) {
                if (_Lit <= 0){
                    color = _Color;
                }
            }
            
            void surf (Input i, inout SurfaceOutput o) {
                if (_SplatSize > 1000){
                    o.Albedo = _Color.rgb;
                    o.Alpha = _Color.a;
                    return;
                }
                float dist = length(mul(unity_ObjectToWorld, i.objectPos - _s0));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s1)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s2)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s3)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s4)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s5)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s6)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s7)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s8)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s9)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s10)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s11)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s12)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s13)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s14)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s15)));
                dist = min(dist, length(mul(unity_ObjectToWorld, i.objectPos - _s16)));
                float visible = _SplatSize - (dist);
                
                clip (visible * 15 - 1 * (SimplexNoise(i.worldPos, 10)));
                o.Albedo = _Color.rgb;
                o.Alpha = _Color.a;
            }
        ENDCG
        // Pass
        // {
        //     CGPROGRAM
        //     #pragma vertex vert
        //     #pragma fragment frag
        //     // make fog work
        //     #pragma multi_compile_fog

        //     #include "UnityCG.cginc"

        //     struct appdata
        //     {
        //         float4 vertex : POSITION;
        //         float2 uv : TEXCOORD0;
        //         fixed4 color : COLOR0;
        //     };

        //     struct v2f
        //     {
        //         float2 uv : TEXCOORD0;
        //         UNITY_FOG_COORDS(1)
        //         float4 vertex : SV_POSITION;
        //         fixed4 color : COLOR0;
        //     };

        //     sampler2D _MainTex;
        //     float4 _MainTex_ST;
        //     uniform fixed4 _Color;					// RGBA : Color + Opacity

        //     v2f vert (appdata v)
        //     {
        //         v2f o;
        //         o.vertex = UnityObjectToClipPos(v.vertex);
        //         o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        //         o.color = v.color;
        //         UNITY_TRANSFER_FOG(o,o.vertex);
        //         return o;
        //     }
        //     fixed4 frag (v2f i) : SV_Target
        //     {
        //         // sample the texture
        //         fixed4 col = tex2D(_MainTex, i.uv);
        //         // apply fog
        //         // UNITY_APPLY_FOG(i.fogCoord, col);
        //         return col * _Color * i.color;
        //     }
        //     ENDCG
        // }
    }
}
