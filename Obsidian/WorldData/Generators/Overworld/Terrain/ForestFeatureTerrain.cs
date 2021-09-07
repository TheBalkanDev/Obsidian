using Obsidian.API.Noise;
using SharpNoise;
using SharpNoise.Modules;
using Blend = Obsidian.API.Noise.Blend;

namespace Obsidian.WorldData.Generators.Overworld.Terrain
{
    public class ForestFeatureTerrain : BaseTerrain
    {
        internal Module Source0 { get; set; } = new Constant { ConstantValue = 0 };

        private readonly Cache featureNoise;

        // Generates the plains terrain.
        // Outputs will be between 0 and 1
        public ForestFeatureTerrain() : base()
        {
            featureNoise = new Cache
            {
                Source0 = new Turbulence
                {
                    Frequency = 0.7,
                    Power = 1.1,
                    Roughness = 1,
                    Source0 = new Cell
                    {
                        Seed = settings.Seed + 70,
                        Frequency = 0.0325,
                        Type = Cell.CellType.Voronoi,
                        EnableDistance = false
                    }
                }
            };


            this.Result = new Select
            {
                Source1 = this.Source0,
                Source0 = featureNoise,
                Control = featureNoise,
                EdgeFalloff = 0.1,
                LowerBound = 0.9,
                UpperBound = 2
            };
        }
    }
}


// Todo: caves? 
/*
 * new Turbulence
                    {
                        Frequency = 3.4578,
                        Power = 0,
                        Roughness = 0,
                        Seed = settings.Seed,
                        Source0 = new Cell
                        {
                            Seed = settings.Seed + 70,
                            Frequency = 0.1125,
                            Type = Cell.CellType.Manhattan
                        }
                    }*/