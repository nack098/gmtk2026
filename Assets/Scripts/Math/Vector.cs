using System;
using System.Runtime.InteropServices;

namespace Takayama.Math {
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Vec3<T> where T : struct
    {
        public T x;
        public T y;
        public T z;

        public Vec3(T x, T y, T z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public T this[int index]
        {
            get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                _ => throw new IndexOutOfRangeException()
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        public override string ToString() => $"({x}, {y}, {z})";
    }

    public static class Vec3Extensions
    {

        public static int Dot(this Vec3<int> a, Vec3<int> b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static int SqrMagnitude(this Vec3<int> v) => v.x * v.x + v.y * v.y + v.z * v.z;
        public static Vec3<int> Cross(this Vec3<int> a, Vec3<int> b) =>
            new Vec3<int>(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        public static float Dot(this Vec3<float> a, Vec3<float> b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float SqrMagnitude(this Vec3<float> v) => v.x * v.x + v.y * v.y + v.z * v.z;
        public static float Magnitude(this Vec3<float> v) => (float)System.Math.Sqrt(v.SqrMagnitude());
        public static Vec3<float> Normalized(this Vec3<float> v)
        {
            float mag = v.Magnitude();
            return mag > 1e-5f ? new Vec3<float>(v.x / mag, v.y / mag, v.z / mag) : default;
        }

        public static double Dot(this Vec3<double> a, Vec3<double> b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static double SqrMagnitude(this Vec3<double> v) => v.x * v.x + v.y * v.y + v.z * v.z;
        public static double Magnitude(this Vec3<double> v) => System.Math.Sqrt(v.SqrMagnitude());
    }
}