using Pathfinding;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class containing freeform roaming, target approach steering,
    /// and biased random-walk home meandering.
    /// Strictly adheres to Single Responsibility (SRP).
    /// </summary>
    public partial class AntTrailFollower
    {
        public void WanderTo(Vector3 targetPosition, float speed = -1f)
        {
            _mode = FollowerMode.Wander;
            _trackedSegment = null;
            _trackedNodeIndex = -1;
            ResumeMovement(speed > 0f ? speed : MoveSpeed);
            if (_ai != null)
            {
                _ai.destination = targetPosition;
            }
        }

        /// <summary>
        /// Paths directly toward a target position (e.g. food cache) without clearing the active trail
        /// segment context, allowing seamless inbound retracing once harvesting concludes.
        /// </summary>
        public void ApproachTarget(Vector3 targetPosition, float speed = -1f)
        {
            _mode = FollowerMode.Wander;
            ResumeMovement(speed > 0f ? speed : MoveSpeed);
            if (_ai != null)
            {
                _ai.destination = targetPosition;
            }
        }

        private void NavigateMeanderHome(float deltaTime)
        {
            if (_homeNest == null) return;

            Vector3 nestPos = _homeNest.SpawnPosition;
            Vector3 myPos = transform.position;

            float dxNest = nestPos.x - myPos.x;
            float dzNest = nestPos.z - myPos.z;
            float distToNestSqr = dxNest * dxNest + dzNest * dzNest;

            if (distToNestSqr <= (_config.NestArrivalThreshold * _config.NestArrivalThreshold))
            {
                OnReturnedToNest?.Invoke();
                return;
            }

            _meanderTimer += deltaTime;
            float dxWay = _currentMeanderWaypoint.x - myPos.x;
            float dzWay = _currentMeanderWaypoint.z - myPos.z;
            bool reachedWaypoint = (dxWay * dxWay + dzWay * dzWay) <= (_config.ArrivalThreshold * _config.ArrivalThreshold);

            if (_meanderTimer >= 2.0f || reachedWaypoint || _currentMeanderWaypoint == Vector3.zero)
            {
                _meanderTimer = 0f;
                PickNextMeanderWaypoint(myPos, nestPos);
            }
        }

        private void PickNextMeanderWaypoint(Vector3 myPos, Vector3 nestPos)
        {
            Vector3 dirToNest = nestPos - myPos;
            dirToNest.y = 0f;

            float distToNest = dirToNest.magnitude;
            if (distToNest <= 3.0f)
            {
                _currentMeanderWaypoint = nestPos;
                _ai.destination = nestPos;
                return;
            }

            dirToNest.Normalize();

            // Biased random walk: rotate base direction toward nest by a random jitter angle
            float jitterAngle = UnityEngine.Random.Range(-_config.MeanderJitterAngle, _config.MeanderJitterAngle);
            Quaternion rotation = Quaternion.Euler(0f, jitterAngle, 0f);
            Vector3 meanderDir = rotation * dirToNest;

            float stepDistance = Mathf.Min(3.0f, distToNest);
            Vector3 rawWaypoint = myPos + meanderDir * stepDistance;

            if (AstarPath.active != null)
            {
                var nearest = AstarPath.active.GetNearest(rawWaypoint, NearestNodeConstraint.Walkable);
                if (nearest.node != null)
                {
                    rawWaypoint = nearest.position;
                }
            }

            _currentMeanderWaypoint = rawWaypoint;
            _ai.destination = _currentMeanderWaypoint;
        }
    }
}
