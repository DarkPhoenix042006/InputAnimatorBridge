using UnityEngine;

namespace InputAnimatorBridge
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  Enums
    // ─────────────────────────────────────────────────────────────────────────────

    public enum AnimatorParamType { Bool, Float, Int, Trigger }

    public enum BoolMode
    {
        /// <summary>True while the button is held; false on release.</summary>
        Hold,
        /// <summary>Flips the bool value on every press.</summary>
        Toggle
    }

    public enum VectorAxis
    {
        /// <summary>Use the magnitude of the Vector2.</summary>
        None,
        X,
        Y
    }

    /// <summary>
    /// How a Float binding tracks its target value.
    ///   Clamp      – value = clamped raw input. Resets to <c>resetValue</c> on cancel.
    ///                Best for WASD / gamepad sticks.
    ///   Accumulate – each delta is ADDED to a running total clamped to [min, max].
    ///                Value never auto-resets on cancel. Best for mouse look.
    /// </summary>
    public enum FloatMode { Clamp, Accumulate }

    /// <summary>
    /// Interpolation curve used when smoothing a Float binding toward its target.
    ///   MoveTowards – linear, constant units/second. Predictable.
    ///   Lerp         – exponential ease-out. More organic / springy feel.
    /// </summary>
    public enum DampingMode { MoveTowards, Lerp }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Binding data
    // ─────────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class InputAnimatorBinding
    {
        // ── Identity ──────────────────────────────────────────────────────────────
        [Tooltip("Editor-only label used for display and for the public API lookup methods.")]
        public string bindingName;

        [Tooltip("Uncheck to disable this binding at runtime without deleting it.")]
        public bool enabled = true;

        // ── Input ────────────────────────────────────────────────────────────────
        [Tooltip("Exact name of the Input Action inside your Input Actions asset.")]
        public string inputActionName;

        // ── Animator ─────────────────────────────────────────────────────────────
        public AnimatorParamType paramType;

        [Tooltip("Exact name of the Animator parameter to drive.")]
        public string animatorParameter;

        // ── Bool ─────────────────────────────────────────────────────────────────
        [Tooltip("Hold = true while pressed, false on release.\nToggle = flips on each press.")]
        public BoolMode boolMode = BoolMode.Toggle;

        // ── Float / Vector ────────────────────────────────────────────────────────
        [Tooltip("X / Y axis to extract from a Vector2 input. None = magnitude.")]
        public VectorAxis vectorAxis = VectorAxis.None;

        [Tooltip("Clamp = resets to resetValue on cancel (WASD/sticks).\n" +
                 "Accumulate = adds delta to a running total, never auto-resets (mouse look).")]
        public FloatMode floatMode = FloatMode.Clamp;

        [Tooltip("Flip the sign of the raw input before any other processing. " +
                 "Use for inverted Y-axis look, or reversing a stick direction.")]
        public bool invertInput = false;

        [Tooltip("Scales the raw input value before the response curve and clamping.")]
        public float multiplier = 1f;

        [Tooltip("Input magnitudes below this threshold are treated as exactly 0. " +
                 "Eliminates controller stick drift.")]
        [Range(0f, 1f)]
        public float deadZone = 0.05f;

        [Tooltip("Non-linear response curve applied AFTER dead zone and multiplier, BEFORE clamping.\n" +
                 "X axis = normalised input (0–1), Y axis = output (0–1).\n" +
                 "A straight diagonal is the default linear response.\n" +
                 "Use a gentle S-curve for organic feel, or quadratic for fine low-speed control.")]
        public AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // ── Range ─────────────────────────────────────────────────────────────────
        public float minValue = -1f;
        public float maxValue =  1f;

        // ── Smoothing ─────────────────────────────────────────────────────────────
        [Tooltip("MoveTowards = linear (units/sec).\nLerp = exponential ease-out (blend factor, 1–20 recommended).")]
        public DampingMode dampingMode = DampingMode.MoveTowards;

        [Tooltip("MoveTowards: units/sec to move toward target.\nLerp: blend factor – higher = snappier.")]
        public float changeSpeed = 6f;

        // ── Reset behaviour ───────────────────────────────────────────────────────
        [Tooltip("The value written to the Animator parameter when this binding resets " +
                 "(on cancel for Clamp mode, or on component disable if resetOnDisable is true).")]
        public float resetValue = 0f;

        [Tooltip("When the InputAnimatorLinker component is disabled, write resetValue to the " +
                 "Animator parameter. Uncheck to hold the last value (e.g. keep camera rotation).")]
        public bool resetOnDisable = true;
    }
}
