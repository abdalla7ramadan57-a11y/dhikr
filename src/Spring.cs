using System;

namespace Dhikr
{
    /// <summary>Damped harmonic spring (mass = 1). Low damping ratio = jelly wobble.</summary>
    public class Spring
    {
        public double Value, Velocity, Target;
        double _k = 220, _c = 26;
        public double Epsilon = 0.02;

        public Spring(double value) { Value = Target = value; }

        /// <param name="stiffness">Higher = faster.</param>
        /// <param name="dampingRatio">1 = no overshoot, ~0.3 = bouncy.</param>
        public Spring Tune(double stiffness, double dampingRatio)
        {
            _k = stiffness;
            _c = 2 * dampingRatio * Math.Sqrt(stiffness);
            return this;
        }

        public void Snap(double v) { Value = Target = v; Velocity = 0; }

        public bool Settled
        {
            get { return Math.Abs(Velocity) < Epsilon * 2 && Math.Abs(Value - Target) < Epsilon; }
        }

        public void Step(double dt)
        {
            // Semi-implicit Euler in small sub-steps for stability.
            while (dt > 0)
            {
                double h = Math.Min(dt, 0.004);
                double a = -_k * (Value - Target) - _c * Velocity;
                Velocity += a * h;
                Value += Velocity * h;
                dt -= h;
            }
            if (Settled) { Value = Target; Velocity = 0; }
        }
    }
}
