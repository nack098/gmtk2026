using System;

namespace Takayama.Math {
    public static class Random
    {
        private static readonly System.Random _rand = new System.Random();
        
        /// <summary>
        /// Generates a random sample from a Gaussian (Normal) distribution.
        /// </summary>
        /// <param name="mean">The mean (center) of the distribution.</param>
        /// <param name="stdDev">The standard deviation (spread) of the distribution.</param>
        public static float Gaussian(float mean = 0f, float stdDev = 1f)
        {
            double u1 = 1.0 - _rand.NextDouble();
            double u2 = 1.0 - _rand.NextDouble();

            double randStdNormal = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2);
            
            return (float)(mean + stdDev * randStdNormal);
        }
        
        public static float SigmoidGaussian(float mean = 0f, float stdDev = 1f)
        {
            float rawGaussian = Gaussian(mean, stdDev);
            return (float)(1.0 / (1.0 + System.Math.Exp(-rawGaussian)));
        }
    }
}

