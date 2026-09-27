using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>One neighbour's planar offset from the NPC, sampled by the Unity adapter.</summary>
    public struct NeighborSample
    {
        /// <summary>Offset from the neighbour to the NPC on the gameplay X axis (unnormalized).</summary>
        public float OffsetX;

        /// <summary>Offset from the neighbour to the NPC on the gameplay Y axis (unnormalized).</summary>
        public float OffsetY;

        /// <summary>Planar distance to the neighbour; 0 when the two are exactly stacked.</summary>
        public float Distance;

        public NeighborSample(float offsetX, float offsetY, float distance)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            Distance = distance;
        }
    }

    /// <summary>
    /// Engine-free local-avoidance math: sums a distance-weighted repulsion from sampled neighbours and
    /// blends it into a desired heading. The Unity adapter does the physics sampling (which colliders
    /// are near) and builds <see cref="NeighborSample"/> values; the vector math (which neighbours
    /// deflect a mover and how strongly) lives here and is unit-tested without a scene. Exactly stacked
    /// neighbours escape along a per-NPC angle derived from <paramref name="escapeSeed"/>, so a pair
    /// splits instead of copying the same escape.
    ///
    /// Unity setup: none — static helper.
    ///
    /// Runtime API:
    ///   TrySeparation(samples, radius, escapeSeed, out sepX, out sepY)
    ///   Steer(dirX, dirY, sepX, sepY, strength, out steerX, out steerY)
    /// </summary>
    public static class LocalAvoidance
    {
        /// <summary>
        /// Sums the weighted repulsion of every sample within <paramref name="radius"/>. Returns false
        /// when there is nothing to avoid. The separation points away from the neighbours; zero-distance
        /// neighbours use <paramref name="escapeSeed"/> for a stable direction.
        /// </summary>
        public static bool TrySeparation(
            List<NeighborSample> neighbors,
            float radius,
            int escapeSeed,
            out float separationX,
            out float separationY)
        {
            separationX = 0f;
            separationY = 0f;
            if (neighbors == null || neighbors.Count == 0 || radius <= 0f)
                return false;

            double escapeAngle = 0.0;
            bool hasEscapeAngle = false;
            int counted = 0;

            for (int i = 0; i < neighbors.Count; i++)
            {
                NeighborSample neighbor = neighbors[i];
                float offsetX = neighbor.OffsetX;
                float offsetY = neighbor.OffsetY;
                float distance = neighbor.Distance;

                if (distance < 0.0001f)
                {
                    if (!hasEscapeAngle)
                    {
                        escapeAngle = (Math.Abs(escapeSeed) % 360) * Math.PI / 180.0;
                        hasEscapeAngle = true;
                    }

                    offsetX = (float)Math.Cos(escapeAngle);
                    offsetY = (float)Math.Sin(escapeAngle);
                    distance = 0f;
                }
                else
                {
                    offsetX /= distance;
                    offsetY /= distance;
                }

                float weight = 1f - Clamp01(distance / radius);
                separationX += offsetX * weight;
                separationY += offsetY * weight;
                counted++;
            }

            return counted > 0 && separationX * separationX + separationY * separationY > 0.00000001f;
        }

        /// <summary>
        /// Blends a separation vector into a desired heading and normalizes the result. Returns the
        /// direction unchanged when there is no separation or no strength.
        /// </summary>
        public static void Steer(
            float directionX,
            float directionY,
            float separationX,
            float separationY,
            float strength,
            out float steerX,
            out float steerY)
        {
            if (separationX * separationX + separationY * separationY <= 0.0001f || strength <= 0f)
            {
                steerX = directionX;
                steerY = directionY;
                return;
            }

            float x = directionX + separationX * strength;
            float y = directionY + separationY * strength;
            float magnitude = (float)Math.Sqrt(x * x + y * y);
            if (magnitude > 0.0001f)
            {
                steerX = x / magnitude;
                steerY = y / magnitude;
            }
            else
            {
                steerX = directionX;
                steerY = directionY;
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
