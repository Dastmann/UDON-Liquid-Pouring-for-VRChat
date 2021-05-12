// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "ASESampleShaders/GlassSimple"
{
	Properties
	{
		_Color("Color", Color) = (0,0,0,0)
		_DistortionNormal("DistortionNormal", 2D) = "bump" {}
		_Smoothness("Smoothness", Range( 0 , 1)) = 0
		_Distortion("Distortion", Range( 0 , 1)) = 0.292
		_EmissionMultiplier("EmissionMultiplier", Range( 0 , 1)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Pass
		{
			ColorMask 0
			ZWrite On
		}

		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		GrabPass{ "_ScreenGrabGlass" }
		CGPROGRAM
		#pragma target 3.0
		#pragma multi_compile_instancing
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Standard alpha:fade keepalpha nometa noforwardadd 
		struct Input
		{
			float4 screenPos;
			float2 uv_texcoord;
		};

		uniform float4 _Color;
		ASE_DECLARE_SCREENSPACE_TEXTURE( _ScreenGrabGlass )
		uniform sampler2D _DistortionNormal;
		uniform float4 _DistortionNormal_ST;
		uniform float _Distortion;
		uniform float _EmissionMultiplier;
		uniform float _Smoothness;


		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		void surf( Input i , inout SurfaceOutputStandard o )
		{
			o.Albedo = _Color.rgb;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float2 uv_DistortionNormal = i.uv_texcoord * _DistortionNormal_ST.xy + _DistortionNormal_ST.zw;
			float4 screenColor10 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_ScreenGrabGlass,( (ase_grabScreenPosNorm).xy + (( UnpackNormal( tex2D( _DistortionNormal, uv_DistortionNormal ) ) * _Distortion )).xy ));
			o.Emission = ( screenColor10 * _EmissionMultiplier ).rgb;
			o.Smoothness = _Smoothness;
			o.Alpha = _Color.a;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18707
2028;325;1740;898;2027.912;522.9225;1.6;True;True
Node;AmplifyShaderEditor.SamplerNode;4;-1568,288;Inherit;True;Property;_DistortionNormal;DistortionNormal;1;0;Create;True;0;0;False;0;False;-1;None;1f75665b848e29d478b34cdee6339622;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;3;-1540.397,508.501;Float;False;Property;_Distortion;Distortion;3;0;Create;True;0;0;False;0;False;0.292;0.058;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;-1136,416;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GrabScreenPosition;5;-1136,176;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;8;-944,416;Inherit;False;True;True;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode;7;-880,192;Inherit;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;9;-656,272;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScreenColorNode;10;-464,224;Float;False;Global;_ScreenGrabGlass;Screen Grab Glass;-1;0;Create;True;0;0;False;0;False;Object;-1;True;False;1;0;FLOAT2;0,0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;13;-608,528;Inherit;False;Property;_EmissionMultiplier;EmissionMultiplier;4;0;Create;True;0;0;False;0;False;0;0.321;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;1;-357.1782,-39.81099;Float;False;Property;_Color;Color;0;0;Create;True;0;0;False;0;False;0,0,0,0;0.5518868,0.7012579,1,1;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;2;-218.2174,496.2177;Float;False;Property;_Smoothness;Smoothness;2;0;Create;True;0;0;False;0;False;0;0.689;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;11;-241.6,241.6;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;75.2,0;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;ASESampleShaders/GlassSimple;False;False;False;False;False;False;False;False;False;False;True;True;False;False;True;False;True;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;True;0;Transparent;0.5;True;False;0;False;Transparent;;Transparent;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;6;0;4;0
WireConnection;6;1;3;0
WireConnection;8;0;6;0
WireConnection;7;0;5;0
WireConnection;9;0;7;0
WireConnection;9;1;8;0
WireConnection;10;0;9;0
WireConnection;11;0;10;0
WireConnection;11;1;13;0
WireConnection;0;0;1;0
WireConnection;0;2;11;0
WireConnection;0;4;2;0
WireConnection;0;9;1;4
ASEEND*/
//CHKSM=A7FF395A0E21213C15C7A02EA2DC68D98F192E0A