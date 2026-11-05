using System;
using System.Collections.Generic;

namespace AlgorithmDemos
{
    public enum DemoKind { Boids, FlowField, QuadTree }

    public sealed class DemoDefinition
    {
        public DemoKind Kind { get; }
        public string Title { get; }
        public string Description { get; }
        public string SceneAsset { get; }
        public string ResourceName { get; }

        internal DemoDefinition(DemoKind kind, string title, string description, string scene, string resource)
        {
            Kind = kind;
            Title = title;
            Description = description;
            SceneAsset = scene;
            ResourceName = resource;
        }
    }

    public static class DemoCatalog
    {
        private static readonly DemoDefinition[] Items =
        {
            new DemoDefinition(DemoKind.Boids, "BOIDS", "Separation / alignment / cohesion",
                "Assets/AlgorithmDemos/Scenes/Boids.unity", "algorithms/scenes/boids"),
            new DemoDefinition(DemoKind.FlowField, "FLOW FIELD", "Cost / integration / direction / crowd",
                "Assets/AlgorithmDemos/Scenes/FlowField.unity", "algorithms/scenes/flow-field"),
            new DemoDefinition(DemoKind.QuadTree, "QUADTREE", "Spatial queries / flocking comparison",
                "Assets/AlgorithmDemos/Scenes/QuadTree.unity", "algorithms/scenes/quadtree")
        };

        public static IReadOnlyList<DemoDefinition> All { get; } = Array.AsReadOnly(Items);

        public static DemoDefinition Get(DemoKind kind)
        {
            foreach (DemoDefinition item in Items)
                if (item.Kind == kind) return item;
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
