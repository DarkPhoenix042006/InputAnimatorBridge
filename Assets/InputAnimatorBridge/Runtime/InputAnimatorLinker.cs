using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace InputAnimatorBridge
{

    [DisallowMultipleComponent]
    public class InputAnimatorLinker : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────

        [Header("References")]
        public InputActionAsset inputActions;
        public Animator         animator;

        [Header("Global Settings")]
        [Tooltip("Multiplies ALL float binding values simultaneously. " +
                 "Use as a runtime sensitivity slider without touching individual bindings.")]
        [Range(0f, 5f)]
        public float globalMultiplier = 1f;

        [Header("Bindings")]
        public List<InputAnimatorBinding> bindings = new();

        // ─── Private state ────────────────────────────────────────────────────────

        /// <summary>Smoothed value currently pushed to the Animator each frame.</summary>
        private Dictionary<InputAnimatorBinding, float> currentValues    = new();

        /// <summary>Desired end-value we are smoothing toward (Clamp mode).</summary>
        private Dictionary<InputAnimatorBinding, float> targetValues     = new();

        /// <summary>Running total for Accumulate mode.</summary>
        private Dictionary<InputAnimatorBinding, float> accumulatedValues = new();

        /// <summary>Maps each InputAction → all bindings that subscribe to it.</summary>
        private Dictionary<InputAction, List<InputAnimatorBinding>> actionMap = new();

        /// <summary>Reverse lookup: bindingName → binding (for public API).</summary>
        private Dictionary<string, InputAnimatorBinding> bindingByName = new();

        // ─── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            foreach (var b in bindings)
            {
                // Register float state
                if (b.paramType == AnimatorParamType.Float)
                {
                    currentValues[b]     = b.resetValue;
                    targetValues[b]      = b.resetValue;
                    accumulatedValues[b] = b.resetValue;
                }

                // Name → binding lookup (first non-empty name wins for duplicates)
                if (!string.IsNullOrEmpty(b.bindingName) && !bindingByName.ContainsKey(b.bindingName))
                    bindingByName[b.bindingName] = b;

                // Resolve the Input Action
                var action = inputActions.FindAction(b.inputActionName, true);
                if (action == null)
                {
                    Debug.LogWarning($"[InputAnimatorLinker] Input action not found: '{b.inputActionName}'");
                    continue;
                }

                if (!actionMap.TryGetValue(action, out var list))
                {
                    list = new List<InputAnimatorBinding>();
                    actionMap[action] = list;
                }
                list.Add(b);
            }
        }

        private void OnEnable()
        {
            foreach (var pair in actionMap)
            {
                pair.Key.performed += OnAction;
                pair.Key.canceled  += OnAction;
                pair.Key.Enable();
            }
        }

        private void OnDisable()
        {
            foreach (var pair in actionMap)
            {
                pair.Key.performed -= OnAction;
                pair.Key.canceled  -= OnAction;
                pair.Key.Disable();
            }

            // Per-binding reset-on-disable
            if (animator == null) return;
            foreach (var b in bindings)
            {
                if (!b.resetOnDisable) continue;

                switch (b.paramType)
                {
                    case AnimatorParamType.Float:
                        animator.SetFloat(b.animatorParameter, b.resetValue);
                        if (currentValues.ContainsKey(b))    currentValues[b]     = b.resetValue;
                        if (targetValues.ContainsKey(b))     targetValues[b]      = b.resetValue;
                        if (accumulatedValues.ContainsKey(b))accumulatedValues[b] = b.resetValue;
                        break;
                    case AnimatorParamType.Bool:
                        animator.SetBool(b.animatorParameter, false);
                        break;
                    case AnimatorParamType.Int:
                        animator.SetInteger(b.animatorParameter, Mathf.RoundToInt(b.resetValue));
                        break;
                }
            }
        }

        private void Update()
        {
            // ── Poll continuous Float / Value-type actions every frame ────────────
            // Prevents the 1-frame gap between 'canceled' and 'performed' that
            // previously caused movement jitter.
            foreach (var pair in actionMap)
            {
                InputAction action = pair.Key;
                if (action.type != InputActionType.Value) continue;

                foreach (var b in pair.Value)
                {
                    if (!b.enabled)                              continue;
                    if (b.paramType != AnimatorParamType.Float)  continue;
                    if (b.floatMode != FloatMode.Clamp)          continue;

                    float raw = ReadFloatFromAction(action, b);
                    raw = ProcessRaw(raw, b);
                    targetValues[b] = Mathf.Clamp(raw, b.minValue, b.maxValue);
                }
            }

            // ── Smooth & push all Float bindings to the Animator ─────────────────
            var keys = new List<InputAnimatorBinding>(currentValues.Keys);
            foreach (var b in keys)
            {
                if (!b.enabled) continue;

                float target = b.floatMode == FloatMode.Accumulate
                    ? accumulatedValues[b]
                    : targetValues[b];

                // Apply global multiplier
                float scaledTarget = target * globalMultiplier;

                float current = b.dampingMode == DampingMode.Lerp
                    ? Mathf.Lerp(currentValues[b], scaledTarget, b.changeSpeed * Time.deltaTime)
                    : Mathf.MoveTowards(currentValues[b], scaledTarget, b.changeSpeed * Time.deltaTime);

                currentValues[b] = current;
                animator.SetFloat(b.animatorParameter, current);
            }
        }

        // ─── Input callbacks ──────────────────────────────────────────────────────

        private void OnAction(InputAction.CallbackContext ctx)
        {
            if (!actionMap.TryGetValue(ctx.action, out var list)) return;

            foreach (var b in list)
            {
                if (!b.enabled) continue;

                switch (b.paramType)
                {
                    case AnimatorParamType.Bool:
                        HandleBool(b, ctx.performed);
                        break;

                    case AnimatorParamType.Float:
                        HandleFloat(b, ctx);
                        break;

                    case AnimatorParamType.Int:
                        if (ctx.performed)
                        {
                            float raw = ReadFloatFromContext(ctx, b);
                            raw = ProcessRaw(raw, b);
                            animator.SetInteger(b.animatorParameter, Mathf.RoundToInt(raw));
                        }
                        break;

                    case AnimatorParamType.Trigger:
                        if (ctx.performed)
                            animator.SetTrigger(b.animatorParameter);
                        break;
                }
            }
        }

        // ─── Float handling ───────────────────────────────────────────────────────

        private void HandleFloat(InputAnimatorBinding b, InputAction.CallbackContext ctx)
        {
            if (b.floatMode == FloatMode.Accumulate)
            {
                // Accumulate: add delta on performed; ignore cancel so value holds
                if (!ctx.performed) return;

                float delta = ReadFloatFromContext(ctx, b);
                delta = ProcessRaw(delta, b);
                float acc = accumulatedValues[b] + delta;
                accumulatedValues[b] = Mathf.Clamp(acc, b.minValue, b.maxValue);
            }
            else // Clamp mode
            {
                if (ctx.performed)
                {
                    float raw = ReadFloatFromContext(ctx, b);
                    raw = ProcessRaw(raw, b);
                    targetValues[b] = Mathf.Clamp(raw, b.minValue, b.maxValue);
                }
                else // canceled
                {
                    targetValues[b] = b.resetValue;
                }
            }
        }

        // ─── Raw value pipeline ───────────────────────────────────────────────────

        private static float ProcessRaw(float value, InputAnimatorBinding b)
        {
            // 1. Invert
            if (b.invertInput) value = -value;

            // 2. Dead zone (continuous rescaling — no boundary spike)
            value = ApplyDeadZone(value, b.deadZone);

            // 3. Multiplier
            value *= b.multiplier;

            // 4. Response curve  (evaluated on |value|, sign reapplied)
            if (b.responseCurve != null)
            {
                float sign     = Mathf.Sign(value);
                float absValue = Mathf.Abs(value);
                // Normalise to 0-1 for curve evaluation, then scale back
                // We use Mathf.Clamp01 so the curve always gets a valid input
                float curved   = b.responseCurve.Evaluate(Mathf.Clamp01(absValue));
                value = sign * curved ; // curve adjusts shape, magnitude restores scale
            }

            return value;
        }

        // ─── Input reading helpers ────────────────────────────────────────────────

        private float ReadFloatFromAction(InputAction action, InputAnimatorBinding b)
        {
            if (action.activeControl?.valueType == typeof(Vector2))
            {
                Vector2 v = action.ReadValue<Vector2>();
                return b.vectorAxis == VectorAxis.X ? v.x
                     : b.vectorAxis == VectorAxis.Y ? v.y
                     : v.magnitude;
            }
            return action.ReadValue<float>();
        }

        private float ReadFloatFromContext(InputAction.CallbackContext ctx, InputAnimatorBinding b)
        {
            if (ctx.valueType == typeof(Vector2))
            {
                Vector2 v = ctx.ReadValue<Vector2>();
                return b.vectorAxis == VectorAxis.X ? v.x
                     : b.vectorAxis == VectorAxis.Y ? v.y
                     : v.magnitude;
            }
            return ctx.ReadValue<float>();
        }

        // ─── Bool handling ────────────────────────────────────────────────────────

        private void HandleBool(InputAnimatorBinding b, bool performed)
        {
            if (b.boolMode == BoolMode.Hold)
            {
                animator.SetBool(b.animatorParameter, performed);
            }
            else // Toggle
            {
                if (!performed) return;
                animator.SetBool(b.animatorParameter, !animator.GetBool(b.animatorParameter));
            }
        }

        // ─── Dead zone math ───────────────────────────────────────────────────────

        internal static float ApplyDeadZone(float value, float threshold)
        {
            if (threshold <= 0f) return value;
            float abs = Mathf.Abs(value);
            if (abs < threshold) return 0f;
            return Mathf.Sign(value) * ((abs - threshold) / (1f - threshold));
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>Enable or disable a named binding at runtime.</summary>
        public void SetBindingEnabled(string bindingName, bool isEnabled)
        {
            if (bindingByName.TryGetValue(bindingName, out var b))
                b.enabled = isEnabled;
            else
                Debug.LogWarning($"[InputAnimatorLinker] SetBindingEnabled: binding '{bindingName}' not found.");
        }

        /// <summary>Returns whether a named binding is currently enabled.</summary>
        public bool IsBindingEnabled(string bindingName)
        {
            return bindingByName.TryGetValue(bindingName, out var b) && b.enabled;
        }

        /// <summary>
        /// Returns the current smoothed float value being pushed to the Animator
        /// for a named binding. Returns 0 if the binding is not found or is not Float type.
        /// </summary>
        public float GetCurrentValue(string bindingName)
        {
            if (bindingByName.TryGetValue(bindingName, out var b) && currentValues.TryGetValue(b, out float v))
                return v;
            return 0f;
        }

        /// <summary>
        /// Resets an Accumulate-mode binding's internal accumulator to <c>resetValue</c>.
        /// Useful when teleporting the player or transitioning game states.
        /// </summary>
        public void ResetAccumulator(string bindingName)
        {
            if (!bindingByName.TryGetValue(bindingName, out var b)) return;
            if (!accumulatedValues.ContainsKey(b)) return;

            accumulatedValues[b] = b.resetValue;
            currentValues[b]     = b.resetValue;
            animator.SetFloat(b.animatorParameter, b.resetValue);
        }

        /// <summary>
        /// Sets an Accumulate-mode binding's accumulator to an exact value.
        /// Useful for saving and restoring camera orientation.
        /// </summary>
        public void SetAccumulatorValue(string bindingName, float value)
        {
            if (!bindingByName.TryGetValue(bindingName, out var b)) return;
            if (!accumulatedValues.ContainsKey(b)) return;

            float clamped = Mathf.Clamp(value, b.minValue, b.maxValue);
            accumulatedValues[b] = clamped;
        }
    }
}
