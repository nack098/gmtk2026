using System;
using UnityEngine;
using Takayama.Math;

namespace TrashCount.Gameplay.Distributions
{
    public interface IDropDistribution
    {
        /// <summary>
        /// Samples a normalized float in range [0.0, 1.0].
        /// </summary>
        float Sample();
    }

    [Serializable]
    public class UniformDistribution : IDropDistribution
    {
        public float Sample() => Takayama.Math.Random.Value();
    }

    [Serializable]
    public class GaussianClampedDistribution : IDropDistribution
    {
        [SerializeField] private float _mean = 0f;
        [SerializeField] private float _stdDev = 1f;

        public float Sample() => Mathf.Clamp01(Takayama.Math.Random.Gaussian(_mean, _stdDev));
    }

    [Serializable]
    public class SigmoidGaussianDistribution : IDropDistribution
    {
        [SerializeField] private float _mean = 0f;
        [SerializeField] private float _stdDev = 1f;

        public float Sample() => Takayama.Math.Random.SigmoidGaussian(_mean, _stdDev);
    }

    [Serializable]
    public class BetaDistribution : IDropDistribution
    {
        [Tooltip("Alpha > Beta skews towards rare drops. Alpha < Beta skews towards common trash.")]
        [SerializeField] private float _alpha = 2f;
        [SerializeField] private float _beta = 5f;

        public float Sample() => Takayama.Math.Random.Beta(_alpha, _beta);
    }

    [Serializable]
    public class ExponentialDistribution : IDropDistribution
    {
        [SerializeField] private float _rate = 1f;

        public float Sample()
        {
            float raw = Takayama.Math.Random.Exponential(_rate);
            return raw / (1f + raw); // Smooth algebraic normalization x / (1 + x)
        }
    }

    [Serializable]
    public class ChiSquareDistribution : IDropDistribution
    {
        [SerializeField] private int _degreesOfFreedom = 3;

        public float Sample()
        {
            float raw = Takayama.Math.Random.ChiSquare(_degreesOfFreedom);
            return raw / (1f + raw);
        }
    }

    [Serializable]
    public class WeibullDistribution : IDropDistribution
    {
        [SerializeField] private float _scale = 1f;
        [SerializeField] private float _shape = 1f;

        public float Sample()
        {
            float raw = Takayama.Math.Random.Weibull(_scale, _shape);
            return raw / (1f + raw);
        }
    }
}