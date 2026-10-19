// Wick.Sim's lit-tile set, as written to _WickVisMask (R8, one texel per tile) by VisibilityMask.
// Shaders never decide visibility: they read the texel for the tile under the fragment exactly
// (LOAD, no filtering). Shared by Wick/StylisedLit and Wick/Flame so both agree on every tile.
#ifndef WICK_VISIBILITY_INCLUDED
#define WICK_VISIBILITY_INCLUDED

TEXTURE2D(_WickVisMask);
float4 _WickVisGrid; // x = width, y = height, z = 1 when a mask is active
half _WickVisDark;   // unlit tiles show this fraction of their base colour
half _WickLitBoost;  // extra flat light on lit tiles so they always read brighter than unlit ones

// Tile (x, y) is centred on world (x, 0, -y); see SimPose. Nudging inward along the normal keeps
// a wall face (which sits exactly on a tile boundary) on its own tile. 1 lit, 0 not.
half WickVisibility(float3 positionWS, float3 normalWS, half ignoreMask)
{
    if (_WickVisGrid.z < 0.5 || ignoreMask > 0.5)
    {
        return 1;
    }

    float3 p = positionWS - normalWS * 0.01;
    int2 tile = int2(floor(p.x + 0.5), floor(-p.z + 0.5));
    if (tile.x < 0 || tile.y < 0 || tile.x >= (int)_WickVisGrid.x || tile.y >= (int)_WickVisGrid.y)
    {
        return 0;
    }

    return LOAD_TEXTURE2D(_WickVisMask, tile).r;
}

#endif
