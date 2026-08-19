using System.Collections;
using UnityEngine;
using FarmSim.Core;

namespace FarmSim.UnityBridge
{
    public class AgentView : MonoBehaviour
    {
        [Tooltip("Degrees added so your sprite's default artwork direction lines up with " +
                 "'facing right' (0°). Sprite drawn facing up -> -90. Facing down -> 90. Facing left -> 180.")]
        public float spriteForwardOffset = -90f;

        [Tooltip("How fast the sprite turns to face the new direction, relative to how long " +
                 "the move itself takes. 1 = finishes turning exactly as it arrives (smooth, car-like). " +
                 "Higher = snaps to face the new direction faster, then drives straight.")]
        [Range(0.3f, 3f)]
        public float turnResponsiveness = 1.2f;

        private AgentModel model;
        private Coroutine moveRoutine;
        private float currentAngle;

        public void Init(AgentModel model, Vector3 startWorldPos)
        {
            this.model = model;
            transform.position = startWorldPos;
            currentAngle = transform.eulerAngles.z;
        }

        public void MoveTo(Vector3 targetWorldPos, float duration)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(LerpTo(targetWorldPos, duration));
        }

        private IEnumerator LerpTo(Vector3 target, float duration)
        {
            Vector3 start = transform.position;
            Vector3 delta = target - start;

            if (duration <= 0f || delta.sqrMagnitude < 0.0001f)
            {
                transform.position = target;
                yield break;
            }

            // Shortest-path angle so it never spins the long way around on a reversal.
            float targetAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + spriteForwardOffset;
            float startAngle = currentAngle;
            float angleDelta = Mathf.DeltaAngle(startAngle, targetAngle);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / duration);

                // Ease in/out instead of linear — reads as accelerating/braking, not teleporting.
                float posT = Mathf.SmoothStep(0f, 1f, progress);
                transform.position = Vector3.Lerp(start, target, posT);

                // Turns a bit faster than it moves, so it's already facing the new
                // direction while still finishing the approach — like steering into a turn.
                float rotT = Mathf.Clamp01(progress * turnResponsiveness);
                currentAngle = startAngle + angleDelta * rotT;
                transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

                yield return null;
            }

            transform.position = target;
            currentAngle = targetAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
        }
    }
}