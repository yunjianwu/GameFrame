using UnityEngine;
using UnityEngine.AI;

namespace Starter.Navigation
{
    public enum MoveRequestResult
    {
        Accepted,
        AgentUnavailable,
        InvalidDestination,
        Unreachable,
        AssignmentFailed
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshUnitMover : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _destinationSampleRadius = 0.75f;
        private NavMeshAgent _agent;

        public NavMeshAgent Agent => _agent != null ? _agent : _agent = GetComponent<NavMeshAgent>();

        public MoveRequestResult MoveTo(Vector3 destination)
        {
            NavMeshAgent agent = Agent;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
                return MoveRequestResult.AgentUnavailable;
            if (!IsFinite(destination.x) || !IsFinite(destination.y) || !IsFinite(destination.z))
                return MoveRequestResult.InvalidDestination;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, Mathf.Max(0.01f, _destinationSampleRadius), filter))
                return MoveRequestResult.InvalidDestination;

            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return MoveRequestResult.Unreachable;
            if (!agent.SetPath(path))
                return MoveRequestResult.AssignmentFailed;

            agent.isStopped = false;
            return MoveRequestResult.Accepted;
        }

        public bool Stop()
        {
            NavMeshAgent agent = Agent;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
                return false;

            // Unity 清除路径时会重置停止状态；同时清零速度，避免停止后继续滑行。
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
