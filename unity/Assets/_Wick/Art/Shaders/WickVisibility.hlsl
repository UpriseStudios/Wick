// Wick.Sim's visibility, as written to _WickVisMask (RG8, one texel per tile) by VisibilityMask:
// R = lit now, G = seen (lit now, or lit earlier this raid when Remembered is on). Shaders never
// decide visibility: every tile centre shows exactly its own texel. Floors blend to their
// neighbours only across a soft band at tile edges (_WickVisEdge, half its width in tiles); wall
// faces read their own tile exactly. Shared by Wick/StylisedLit and Wick/Flame.
//
// Three looks (docs/m10-2-lighting.md): Lit = full colour; Remembered and Unseen = a fraction of
// the colour, partly desaturated and tinted cool. Static warm pools (sconces, torches, the
// entrance) come from _WickStaticMap, and a share of them shows through the dark on walls and floor.
#ifndef WICK_VISIBILITY_INCLUDED
#define WICK_VISIBILITY_INCLUDED

TEXTURE2D(_WickVisMask);
TEXTURE2D(_WickStaticMap); SAMPLER(sampler_WickStaticMap);
float4 _WickVisGrid;        // x = width, y = height, z = 1 when a mask is active, w = 1 when a static map is set
half _WickVisEdge;          // half the soft edge's width, in tiles (0 = hard edge)
half4 _WickRemembered;      // x = brightness, y = desaturation
half4 _WickRememberedTint;
half4 _WickUnseen;          // x = brightness, y = desaturation
half4 _WickUnseenTint;
half _WickLitBoost;         // extra flat light on lit tiles so they always read brighter than unlit ones
half4 _WickStaticColor;     // warm pool colour times intensity
half _WickStaticDark;       // share of a pool that shows on tiles Wick hasn't lit

half2 WickTexel(int2 tile)
{
    if (tile.x < 0 || tile.y < 0 || tile.x >= (int)_WickVisGrid.x || tile.y >= (int)_WickVisGrid.y)
    {
        return half2(0, 0);
    }

    return LOAD_TEXTURE2D(_WickVisMask, tile).rg;
}

// (lit, seen) for this fragment, 0 to 1. Tile (x, y) is centred on world (x, 0, -y); see SimPose.
// Nudging inward along the normal keeps a wall face (which sits exactly on a tile boundary) on its
// own tile. Fully visible when no mask is active or the material ignores it.
half2 WickVisSample(float3 positionWS, float3 normalWS, half ignoreMask)
{
    if (_WickVisGrid.z < 0.5 || ignoreMask > 0.5)
    {
        return half2(1, 1);
    }

    float3 p = positionWS - normalWS * 0.01;
    float2 u = float2(p.x, -p.z);
    if (abs(normalWS.y) < 0.5 || _WickVisEdge <= 0)
    {
        return WickTexel(int2(floor(u + 0.5)));
    }

    // Floors: bilinear between the four nearest tile centres, sharpened so each centre keeps its own
    // value within (0.5 - edge) of it and the change happens across the band at the boundary.
    int2 i0 = int2(floor(u));
    float2 f = u - i0;
    float2 w = smoothstep(0.5 - _WickVisEdge, 0.5 + _WickVisEdge, f);
    half2 a = lerp(WickTexel(i0), WickTexel(i0 + int2(1, 0)), w.x);
    half2 b = lerp(WickTexel(i0 + int2(0, 1)), WickTexel(i0 + int2(1, 1)), w.x);
    return lerp(a, b, w.y);
}

// The static warm-pool strength here (0 with no map). Pools are torchlight on the floor: they fade
// up the walls and leave wall tops (1 m up) almost dark.
half WickStatic(float3 positionWS)
{
    if (_WickVisGrid.w < 0.5)
    {
        return 0;
    }

    float2 uv = (float2(positionWS.x, -positionWS.z) + 0.5) / _WickVisGrid.xy;
    half height = saturate(1.15 - positionWS.y);
    return SAMPLE_TEXTURE2D(_WickStaticMap, sampler_WickStaticMap, uv).r * height * height;
}

half3 WickGrade(half3 c, half4 level, half4 tint)
{
    c *= level.x;
    half luma = dot(c, half3(0.299, 0.587, 0.114));
    return lerp(c, luma.xxx, level.y) * tint.rgb;
}

// The final colour: `lit` as Wick's light shows it, graded down to Remembered or Unseen where the
// sim says so. Static pools add to the lit colour, and a share of them to the dark ones.
half3 WickShade(half3 albedo, half3 lit, float3 positionWS, float3 normalWS, half ignoreMask)
{
    half2 vis = WickVisSample(positionWS, normalWS, ignoreMask);
    half3 pool = albedo * _WickStaticColor.rgb * WickStatic(positionWS);
    half3 unseen = WickGrade(albedo + pool * _WickStaticDark, _WickUnseen, _WickUnseenTint);
    half3 remembered = WickGrade(albedo + pool * _WickStaticDark, _WickRemembered, _WickRememberedTint);
    return lerp(lerp(unseen, remembered, vis.y), lit + pool, vis.x);
}

// How bright an emissive (a flame) shows here: 1 lit, the Remembered or Unseen fraction otherwise.
half WickVisBrightness(float3 positionWS, float3 normalWS, half ignoreMask)
{
    half2 vis = WickVisSample(positionWS, normalWS, ignoreMask);
    return lerp(lerp(_WickUnseen.x, _WickRemembered.x, vis.y), 1, vis.x);
}

#endif
