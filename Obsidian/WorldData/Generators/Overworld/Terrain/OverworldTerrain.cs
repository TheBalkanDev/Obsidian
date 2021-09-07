using Obsidian.API.Noise;
using Obsidian.WorldData.Generators.Overworld.Carvers;
using SharpNoise.Modules;
using Blend = Obsidian.API.Noise.Blend;
using System.Collections.Generic;
using static Obsidian.API.Noise.VoronoiBiomes;
using Obsidian.WorldData.Generators.Overworld.BiomeNoise;

namespace Obsidian.WorldData.Generators.Overworld.Terrain
{
    public class OverworldTerrain
    {
        public Module Result { get; set; }

        public readonly OverworldTerrainSettings settings;

        private readonly BaseTerrain ocean, deepocean, badlands, plains, hills, mountains, rivers, forestFeature;

        private readonly BaseCarver cave;

        private Module FinalBiomes;

        private readonly Dictionary<int, Module> BiomeTerrainFeatures;



        public OverworldTerrain(bool isUnitTest = false)
        {
            settings = OverworldGenerator.GeneratorSettings;
            ocean = new OceanTerrain();
            deepocean = new DeepOceanTerrain();
            plains = new PlainsTerrain();
            hills = new HillsTerrain();
            badlands = new BadlandsTerrain();
            mountains = new MountainsTerrain();
            rivers = new RiverTerrain();
            cave = new CavesCarver();
            forestFeature = new ForestFeatureTerrain();

            Dictionary<int, Module> biomesMap = new Dictionary<int, Module>()
            {
                { 0, ocean.Result },
                { 1, plains.Result },
                { 2, plains.Result },
                { 3, mountains.Result },
                { 4, plains.Result },
                { 5, plains.Result },
                { 6, plains.Result },
                { 7, rivers.Result },
                { 10, ocean.Result },
                { 11, rivers.Result },
                { 12, hills.Result },
                { 13, mountains.Result },
                { 14, plains.Result },
                { 15, new Constant { ConstantValue = 0.0 } },
                { 16, new Constant { ConstantValue = 0.0 } },
                { 17, hills.Result },
                { 18, hills.Result },
                { 19, hills.Result },
                { 20, hills.Result },
                { 21, plains.Result },
                { 22, hills.Result },
                { 23, new Constant { ConstantValue = 0.0 } },
                { 24, deepocean.Result },
                { 25, new Constant { ConstantValue = 0.0 } },
                { 26, new Constant { ConstantValue = 0.0 } },
                { 27, plains.Result },
                { 28, hills.Result },
                { 29, plains.Result },
                { 30, plains.Result },
                { 31, hills.Result },
                { 32, plains.Result },
                { 33, hills.Result },
                { 34, mountains.Result },
                { 35, plains.Result },
                { 36, hills.Result },
                { 37, plains.Result },
                { 38, hills.Result },
                { 39, hills.Result },
                { 44, ocean.Result },
                { 45, ocean.Result },
                { 46, ocean.Result },
                { 47, deepocean.Result },
                { 48, deepocean.Result },
                { 49, deepocean.Result },
                { 50, deepocean.Result },
                { 129, plains.Result },
                { 130, plains.Result },
                { 131, mountains.Result },
                { 132, plains.Result },
                { 133, mountains.Result },
                { 134, hills.Result },
                { 140, hills.Result },
                { 149, hills.Result },
                { 151, plains.Result },
                { 155, plains.Result },
                { 156, hills.Result },
                { 157, hills.Result },
                { 158, mountains.Result },
                { 160, plains.Result },
                { 161, hills.Result },
                { 162, mountains.Result },
                { 163, plains.Result },
                { 164, hills.Result },
                { 165, hills.Result },
                { 166, mountains.Result },
                { 167, mountains.Result },
                { 168, plains.Result },
                { 169, hills.Result },
            };

            BiomeTerrainFeatures = new Dictionary<int, Module>()
            {
                { 0, new Constant { ConstantValue = 0.0 } },
                { 1, new Constant { ConstantValue = 0.0 } },
                { 2, new Constant { ConstantValue = 0.0 } },
                { 3, new Constant { ConstantValue = 0.0 } },
                { 4, new Constant { ConstantValue = 0.0 } },
                { 5, new Constant { ConstantValue = 0.0 } },
                { 6, new Constant { ConstantValue = 0.0 } },
                { 7, new Constant { ConstantValue = 0.0 } },
                { 10, new Constant { ConstantValue = 0.0 } },
                { 11, new Constant { ConstantValue = 0.0 } },
                { 12, new Constant { ConstantValue = 0.0 } },
                { 13, new Constant { ConstantValue = 0.0 } },
                { 14, new Constant { ConstantValue = 0.0 } },
                { 15, new Constant { ConstantValue = 0.0 } },
                { 16, new Constant { ConstantValue = 0.0 } },
                { 17, new Constant { ConstantValue = 0.0 } },
                { 18, new Constant { ConstantValue = 0.0 } },
                { 19, new Constant { ConstantValue = 0.0 } },
                { 20, new Constant { ConstantValue = 0.0 } },
                { 21, new Constant { ConstantValue = 0.0 } },
                { 22, new Constant { ConstantValue = 0.0 } },
                { 23, new Constant { ConstantValue = 0.0 } },
                { 24, new Constant { ConstantValue = 0.0 } },
                { 25, new Constant { ConstantValue = 0.0 } },
                { 26, new Constant { ConstantValue = 0.0 } },
                { 27, forestFeature.Result },
                { 28, new Constant { ConstantValue = 0.0 } },
                { 29, new Constant { ConstantValue = 0.0 } },
                { 30, new Constant { ConstantValue = 0.0 } },
                { 31, new Constant { ConstantValue = 0.0 } },
                { 32, new Constant { ConstantValue = 0.0 } },
                { 33, new Constant { ConstantValue = 0.0 } },
                { 34, new Constant { ConstantValue = 0.0 } },
                { 35, new Constant { ConstantValue = 0.0 } },
                { 36, new Constant { ConstantValue = 0.0 } },
                { 37, new Constant { ConstantValue = 0.0 } },
                { 38, new Constant { ConstantValue = 0.0 } },
                { 39, new Constant { ConstantValue = 0.0 } },
                { 44, new Constant { ConstantValue = 0.0 } },
                { 45, new Constant { ConstantValue = 0.0 } },
                { 46, new Constant { ConstantValue = 0.0 } },
                { 47, new Constant { ConstantValue = 0.0 } },
                { 48, new Constant { ConstantValue = 0.0 } },
                { 49, new Constant { ConstantValue = 0.0 } },
                { 50, new Constant { ConstantValue = 0.0 } },
                { 129, new Constant { ConstantValue = 0.0 } },
                { 130, new Constant { ConstantValue = 0.0 } },
                { 131, new Constant { ConstantValue = 0.0 } },
                { 132, new Constant { ConstantValue = 0.0 } },
                { 133, new Constant { ConstantValue = 0.0 } },
                { 134, new Constant { ConstantValue = 0.0 } },
                { 140, new Constant { ConstantValue = 0.0 } },
                { 149, new Constant { ConstantValue = 0.0 } },
                { 151, new Constant { ConstantValue = 0.0 } },
                { 155, new Constant { ConstantValue = 0.0 } },
                { 156, new Constant { ConstantValue = 0.0 } },
                { 157, new Constant { ConstantValue = 0.0 } },
                { 158, new Constant { ConstantValue = 0.0 } },
                { 160, new Constant { ConstantValue = 0.0 } },
                { 161, new Constant { ConstantValue = 0.0 } },
                { 162, new Constant { ConstantValue = 0.0 } },
                { 163, new Constant { ConstantValue = 0.0 } },
                { 164, new Constant { ConstantValue = 0.0 } },
                { 165, new Constant { ConstantValue = 0.0 } },
                { 166, new Constant { ConstantValue = 0.0 } },
                { 167, new Constant { ConstantValue = 0.0 } },
                { 168, new Constant { ConstantValue = 0.0 } },
                { 169, new Constant { ConstantValue = 0.0 } },
            };

            FinalBiomes = VoronoiBiomeNoise.Instance.result;

            var biomeTransitionSel2 = new Cache
            {
                Source0 = new TransitionMap
                {
                    Distance = 5,
                    Source0 = FinalBiomes
                }
            };

            Module scaled = new Blend
            {
                Distance = 2,
                Source0 = new TerrainSelect
                {
                    BiomeSelector = FinalBiomes,
                    Control = biomeTransitionSel2,
                    TerrainModules = biomesMap,
                }
            };

            if (isUnitTest)
            {
                scaled = new ScaleBias
                {
                    Source0 = FinalBiomes,
                    Scale = 1 / 85.0,
                    //Bias = -1
                };
            }

            // Scale bias scales the verical output (usually -1.0 to +1.0) to
            // Minecraft values. If MinElev is 40 (leaving room for caves under oceans)
            // and MaxElev is 168, a value of -1 becomes 40, and a value of 1 becomes 168.
            var biased = new ScaleBias
            {
                Scale = (settings.MaxElev - settings.MinElev) / 2.0,
                Bias = settings.MinElev + ((settings.MaxElev - settings.MinElev) / 2.0),
                Source0 = scaled
            };

            Result = isUnitTest ? scaled : biased;

        }

        internal BaseBiome GetBiome(double x, double z, double y = 0)
        {
            return (BaseBiome)FinalBiomes.GetValue(x, y, z);
        }

        public double GetValue(double x, double z)
        {
            return Result.GetValue(x, 0, z);
        }

        public Module GetFeatureModule(int biome)
        {
            return BiomeTerrainFeatures[biome];
        }

        public bool IsCave(double x, double y, double z)
        {
            var val = cave.Result.GetValue(x, y, z);
            return val > -0.5;
        }
    }
}
