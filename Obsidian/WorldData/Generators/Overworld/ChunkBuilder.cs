using Obsidian.API;
using Obsidian.Utilities.Registry;
using Obsidian.WorldData.Generators.Overworld.Terrain;
using SharpNoise.Modules;
using System;
using System.Diagnostics;

namespace Obsidian.WorldData.Generators.Overworld
{
    public static class ChunkBuilder
    {
        public static void FillChunk(Chunk chunk, double[,] terrainHeightmap, double[,] bedrockHeightmap)
        {
            var bedrock = Registry.GetBlock(Material.Bedrock);
            var stone = Registry.GetBlock(Material.Stone);

            for (int bx = 0; bx < 16; bx++)
            {
                for (int bz = 0; bz < 16; bz++)
                {
                    for (int by = 0; by < 256; by++)
                    {
                        if (by <= bedrockHeightmap[bx, bz]) 
                        {
                            chunk.SetBlock(bx, by, bz, bedrock);
                        }
                        else if (by <= terrainHeightmap[bx, bz]) 
                        {
                            chunk.SetBlock(bx, by, bz, stone);
                        }
                    }
                }
            }
        }

        public static void AddTerrainFeatures(Chunk chunk, double[,] terrainHeightmap, OverworldTerrain t)
        {
            var stone = Registry.GetBlock(Material.Stone);
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int ty = (int)terrainHeightmap[x, z];
                    for (int by = ty; by < ty + 11; by++)
                    {
                        int bx = x + chunk.X * 16;
                        int bz = z + chunk.Z * 16;
                        int biome = (int)chunk.BiomeContainer.GetBiome(x >> 2, by >> 2, z >> 2);
                        var noiseVal = t.GetFeatureModule(biome).GetValue(bx, by, bz);
                        if (noiseVal > 0.9f)
                        {
                            chunk.SetBlock(bx, by, bz, stone);
                        }
                    }
                }
            }
        }

        public static void CarveCaves(OverworldTerrain noiseGen, Chunk chunk, double[,] rhm, double[,] bhm, bool debug = false)
        {
            var b = Registry.GetBlock(Material.CaveAir);
            for (int bx = 0; bx < 16; bx++)
            {
                for (int bz = 0; bz < 16; bz++)
                {
                    int tY = Math.Min((int)rhm[bx, bz], 64);
                    int brY = (int)bhm[bx, bz];
                    for (int by = brY; by < tY; by++)
                    {
                        bool caveAir = noiseGen.IsCave(bx + (chunk.X * 16), by, bz + (chunk.Z * 16));
                        if (caveAir)
                        {
                            if (debug) { b = Registry.GetBlock(Material.LightGrayStainedGlass); }
                            chunk.SetBlock(bx, by, bz, b);
                        }
                    }
                }
            }
        }
    }
}
