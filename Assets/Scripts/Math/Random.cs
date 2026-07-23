using System;

namespace Takayama.Math
{
    public static class Random
    {
        private static readonly System.Random _rand = new System.Random();

        #region Basic & Uniform

        /// <summary>
        /// Standard uniform sample in range [0.0, 1.0].
        /// </summary>
        public static float Value() => (float)_rand.NextDouble();

        /// <summary>
        /// Uniform sample in range [min, max].
        /// </summary>
        public static float Range(float min, float max) => min + (max - min) * Value();

        #endregion

        #region Gaussian / Normal Distributions
        /// <summary>
        /// Generates a random sample from a Gaussian (Normal) distribution.
        /// </summary>
        public static float Gaussian(float mean = 0f, float stdDev = 1f)
        {
            double u1 = 1.0 - _rand.NextDouble();
            double u2 = 1.0 - _rand.NextDouble();

            double randStdNormal = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2);

            return (float)(mean + stdDev * randStdNormal);
        }

        /// <summary>
        /// Maps a Gaussian sample into [0, 1] via the Sigmoid function.
        /// </summary>
        public static float SigmoidGaussian(float mean = 0f, float stdDev = 1f)
        {
            float rawGaussian = Gaussian(mean, stdDev);
            return (float)(1.0 / (1.0 + System.Math.Exp(-rawGaussian)));
        }

        #endregion

        #region Continuous Distributions (Loot, Spawns & Intervals)
        /// <summary>
        /// Exponential distribution (e.g., time intervals between random events, trash spawns).
        /// Rate (lambda) controls frequency.
        /// </summary>
        public static float Exponential(float rate = 1f)
        {
            if (rate <= 0f) rate = 0.0001f;
            return (float)(-System.Math.Log(1.0 - _rand.NextDouble()) / rate);
        }

        /// <summary>
        /// Chi-Square distribution with k degrees of freedom (sum of k squared standard normals).
        /// Great for skewed loot rarity where lower values are very common and high values are super rare!
        /// </summary>
        public static float ChiSquare(int degreesOfFreedom = 3)
        {
            if (degreesOfFreedom < 1) degreesOfFreedom = 1;
            double sum = 0.0;
            for (int i = 0; i < degreesOfFreedom; i++)
            {
                float g = Gaussian(0f, 1f);
                sum += g * g;
            }
            return (float)sum;
        }

        /// <summary>
        /// Gamma distribution sampled using Marsaglia and Tsang method (for k >= 1).
        /// </summary>
        public static float Gamma(float shape, float scale = 1f)
        {
            if (shape < 1f)
            {
                // Stuart's Theorem for shape < 1
                return Gamma(shape + 1f, scale) * (float)System.Math.Pow(_rand.NextDouble(), 1.0 / shape);
            }

            double d = shape - 1.0 / 3.0;
            double c = 1.0 / System.Math.Sqrt(9.0 * d);

            while (true)
            {
                double z = Gaussian(0f, 1f);
                double v = 1.0 + c * z;

                if (v <= 0.0) continue;

                v = v * v * v;
                double u = _rand.NextDouble();

                if (u < 1.0 - 0.0331 * z * z * z * z)
                    return (float)(d * v * scale);

                if (System.Math.Log(u) < 0.5 * z * z + d * (1.0 - v + System.Math.Log(v)))
                    return (float)(d * v * scale);
            }
        }

        /// <summary>
        /// Beta distribution in range [0, 1].
        /// Perfect for loot rarity! 
        /// Alpha > Beta skews towards 1 (rare high loot).
        /// Alpha < Beta skews towards 0 (common junk).
        /// </summary>
        public static float Beta(float alpha, float beta)
        {
            float x = Gamma(alpha, 1f);
            float y = Gamma(beta, 1f);
            if (x + y == 0f) return 0f;
            return x / (x + y);
        }

        /// <summary>
        /// Weibull distribution (used for reliability/failure rates, ideal for item degradation or hazard spawns).
        /// </summary>
        public static float Weibull(float scale = 1f, float shape = 1f)
        {
            if (shape <= 0f) shape = 0.0001f;
            double u = 1.0 - _rand.NextDouble();
            return (float)(scale * System.Math.Pow(-System.Math.Log(u), 1.0 / shape));
        }

        #endregion

        #region Discrete Distributions (Counts & Occurrences)
        /// <summary>
        /// Poisson distribution (number of random independent events occurring in a fixed interval).
        /// Great for deciding "how many pieces of trash drop from a heap at once".
        /// </summary>
        public static int Poisson(float lambda = 3f)
        {
            if (lambda <= 0f) return 0;

            double L = System.Math.Exp(-lambda);
            double k = 0;
            double p = 1.0;

            do
            {
                k++;
                p *= _rand.NextDouble();
            } while (p > L);

            return (int)(k - 1);
        }

        /// <summary>
        /// Binomial distribution (number of successes in n independent Yes/No trials with probability p).
        /// </summary>
        public static int Binomial(int trials, float probability)
        {
            int successes = 0;
            for (int i = 0; i < trials; i++)
            {
                if (_rand.NextDouble() < probability)
                {
                    successes++;
                }
            }
            return successes;
        }
        #endregion
    }
}