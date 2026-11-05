using System.Collections.Generic;
using UnityEngine;

namespace Demos.BoidsFlocking
{
    public struct FlockWeights
    {
        public float Sep;
        public float Ali;
        public float Coh;

        public FlockWeights(float sep, float ali, float coh)
        {
            Sep = sep;
            Ali = ali;
            Coh = coh;
        }
    }

    /// <summary>
    /// 单只鸟的纯数据与受力计算，不依赖 MonoBehaviour，可在普通 .NET 里测试。
    /// 只用到 Vector2 / Mathf 这类纯托管 API，随机数用 System.Random 以便固定种子复现。
    /// </summary>
    public sealed class Boid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        /// <summary>本帧受到的合力，Integrate 结束后清零</summary>
        public Vector2 Acceleration;

        public float MaxSpeed = 240f;
        public float MaxForce = 9f;

        public Boid(System.Random rng)
        {
            Velocity = new Vector2((float)(rng.NextDouble() - 0.5) * 240f, (float)(rng.NextDouble() - 0.5) * 240f);
        }

        public static Vector2 Limit(Vector2 v, float max)
        {
            return v.sqrMagnitude > max * max ? v.normalized * max : v;
        }

        /// <summary>分离：离得越近排斥越强，安全距离取感知半径的一半</summary>
        public Vector2 Separation(List<Boid> boids, float perception)
        {
            Vector2 steering = Vector2.zero;
            int total = 0;
            float safe = perception * 0.5f;
            for (int i = 0; i < boids.Count; i++)
            {
                Boid other = boids[i];
                if (other == this) continue;
                float dist = Vector2.Distance(Position, other.Position);
                if (dist > 0f && dist < safe)
                {
                    steering += (Position - other.Position) * (1f / (dist * dist));
                    total++;
                }
            }
            if (total > 0)
            {
                steering *= 1f / total;
                steering = steering.normalized * MaxSpeed - Velocity;
                steering = Limit(steering, MaxForce);
            }
            return steering;
        }

        /// <summary>对齐：向视野内同伴的平均速度靠拢</summary>
        public Vector2 Alignment(List<Boid> boids, float perception)
        {
            Vector2 steering = Vector2.zero;
            int total = 0;
            for (int i = 0; i < boids.Count; i++)
            {
                Boid other = boids[i];
                if (other == this) continue;
                float dist = Vector2.Distance(Position, other.Position);
                if (dist > 0f && dist < perception)
                {
                    steering += other.Velocity;
                    total++;
                }
            }
            if (total > 0)
            {
                steering *= 1f / total;
                steering = steering.normalized * MaxSpeed - Velocity;
                steering = Limit(steering, MaxForce);
            }
            return steering;
        }

        /// <summary>凝聚：飞向视野内同伴的质心</summary>
        public Vector2 Cohesion(List<Boid> boids, float perception)
        {
            Vector2 center = Vector2.zero;
            int total = 0;
            for (int i = 0; i < boids.Count; i++)
            {
                Boid other = boids[i];
                if (other == this) continue;
                float dist = Vector2.Distance(Position, other.Position);
                if (dist > 0f && dist < perception)
                {
                    center += other.Position;
                    total++;
                }
            }
            return total > 0 ? Seek(center * (1f / total)) : Vector2.zero;
        }

        public Vector2 Seek(Vector2 target)
        {
            Vector2 desired = (target - Position).normalized * MaxSpeed;
            return Limit(desired - Velocity, MaxForce);
        }

        /// <summary>逃离：进入 radius 内才触发，2 倍速度、3 倍转向力</summary>
        public Vector2 Flee(Vector2 target, float radius)
        {
            if (Vector2.Distance(Position, target) >= radius) return Vector2.zero;
            Vector2 desired = (Position - target).normalized * (MaxSpeed * 2f);
            return Limit(desired - Velocity, MaxForce * 3f);
        }

        public void Flock(List<Boid> boids, float perception, FlockWeights w)
        {
            Acceleration += Separation(boids, perception) * w.Sep;
            Acceleration += Alignment(boids, perception) * w.Ali;
            Acceleration += Cohesion(boids, perception) * w.Coh;
        }

        /// <summary>加速度按 60 FPS 归一（与原文 dt * 60 一致），速度单位为像素/秒</summary>
        public void Integrate(float dt)
        {
            Velocity += Acceleration * (dt * 60f);
            Velocity = Limit(Velocity, MaxSpeed);
            Position += Velocity * dt;
            Acceleration = Vector2.zero;
        }

        /// <summary>吃豆人式穿屏，halfW / halfH 已包含缓冲区</summary>
        public bool Wrap(float halfW, float halfH)
        {
            bool wrapped = false;
            if (Position.x > halfW) { Position.x = -halfW; wrapped = true; }
            else if (Position.x < -halfW) { Position.x = halfW; wrapped = true; }
            if (Position.y > halfH) { Position.y = -halfH; wrapped = true; }
            else if (Position.y < -halfH) { Position.y = halfH; wrapped = true; }
            return wrapped;
        }
    }
}
