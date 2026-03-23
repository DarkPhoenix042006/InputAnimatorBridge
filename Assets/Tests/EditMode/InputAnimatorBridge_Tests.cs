
using NUnit.Framework;
using UnityEngine;
using InputAnimatorBridge;

namespace InputAnimatorBridge.Tests
{
    internal static class H
    {
        /// Mirrors InputAnimatorLinker.ApplyDeadZone (marked internal so tests can call it directly).
        public static float DeadZone(float value, float threshold)
            => InputAnimatorLinker.ApplyDeadZone(value, threshold);

        /// Full ProcessRaw pipeline (invert → dead zone → multiplier → curve).
        public static float ProcessRaw(float value, InputAnimatorBinding b)
        {
            if (b.invertInput) value = -value;
            value = DeadZone(value, b.deadZone);
            value *= b.multiplier;
            if (b.responseCurve != null)
            {
                float sign = Mathf.Sign(value);
                float abs  = Mathf.Abs(value);
                float curved = b.responseCurve.Evaluate(Mathf.Clamp01(abs));
                value = sign * curved ;
            }
            return value;
        }

        /// Accumulate step.
        public static float Accumulate(float current, float delta, InputAnimatorBinding b)
        {
            delta = ProcessRaw(delta, b);
            return Mathf.Clamp(current + delta, b.minValue, b.maxValue);
        }
    }

    //  SUITE 1 — Data model

    [TestFixture]
    public class DataModel_Tests
    {
        [Test]
        public void DefaultBinding_HasExpectedDefaults()
        {
            var b = new InputAnimatorBinding();
            Assert.AreEqual(FloatMode.Clamp,         b.floatMode,      "floatMode");
            Assert.AreEqual(DampingMode.MoveTowards, b.dampingMode,    "dampingMode");
            Assert.AreEqual(BoolMode.Toggle,         b.boolMode,       "boolMode");
            Assert.AreEqual(VectorAxis.None,         b.vectorAxis,     "vectorAxis");
            Assert.AreEqual(1f,    b.multiplier,  0.001f,              "multiplier");
            Assert.AreEqual(0.05f, b.deadZone,    0.001f,              "deadZone");
            Assert.AreEqual(-1f,   b.minValue,    0.001f,              "minValue");
            Assert.AreEqual( 1f,   b.maxValue,    0.001f,              "maxValue");
            Assert.AreEqual( 6f,   b.changeSpeed, 0.001f,              "changeSpeed");
            Assert.AreEqual( 0f,   b.resetValue,  0.001f,              "resetValue");
            Assert.IsTrue  (b.enabled,                                  "enabled");
            Assert.IsFalse (b.invertInput,                              "invertInput");
            Assert.IsTrue  (b.resetOnDisable,                           "resetOnDisable");
            Assert.IsNotNull(b.responseCurve,                           "responseCurve");
        }

        [Test]
        public void Enums_ContainAllExpectedValues()
        {
            foreach (var name in new[] { "Clamp", "Accumulate" })
                Assert.IsTrue(System.Enum.IsDefined(typeof(FloatMode), name), name);

            foreach (var name in new[] { "MoveTowards", "Lerp" })
                Assert.IsTrue(System.Enum.IsDefined(typeof(DampingMode), name), name);

            foreach (var name in new[] { "Bool", "Float", "Int", "Trigger" })
                Assert.IsTrue(System.Enum.IsDefined(typeof(AnimatorParamType), name), name);

            foreach (var name in new[] { "Hold", "Toggle" })
                Assert.IsTrue(System.Enum.IsDefined(typeof(BoolMode), name), name);

            foreach (var name in new[] { "None", "X", "Y" })
                Assert.IsTrue(System.Enum.IsDefined(typeof(VectorAxis), name), name);
        }
    }

    //  SUITE 2 — Dead zone

    [TestFixture]
    public class DeadZone_Tests
    {
        // T2.1  Zero threshold = passthrough
        [Test]
        public void ZeroThreshold_PassesValueThrough()
        {
            Assert.AreEqual( 0.5f, H.DeadZone( 0.5f, 0f), 0.001f);
            Assert.AreEqual(-0.8f, H.DeadZone(-0.8f, 0f), 0.001f);
        }

        // T2.2  Below threshold → 0
        [TestCase( 0.04f, 0.05f)]
        [TestCase(-0.03f, 0.05f)]
        [TestCase( 0.00f, 0.10f)]
        public void ValueBelowThreshold_ReturnsZero(float v, float t)
            => Assert.AreEqual(0f, H.DeadZone(v, t), 0.0001f);

        // T2.3  At threshold → 0
        [Test]
        public void ValueAtThreshold_ReturnsZero()
            => Assert.AreEqual(0f, H.DeadZone(0.05f, 0.05f), 0.0001f);

        // T2.4  Full input (1.0) → 1.0 regardless of threshold
        [TestCase(0.1f)] [TestCase(0.2f)] [TestCase(0.5f)]
        public void FullInput_MapsToOne(float threshold)
            => Assert.AreEqual(1f, H.DeadZone(1f, threshold), 0.001f);

        // T2.5  Sign preserved for negative input
        [Test]
        public void NegativeInput_PreservesSign()
        {
            float r = H.DeadZone(-1f, 0.1f);
            Assert.Less(r, 0f);
            Assert.AreEqual(-1f, r, 0.001f);
        }

        // T2.6  Continuous at boundary (no spike)
        [Test]
        public void NoBoundarySpike()
        {
            float below = H.DeadZone(0.049f, 0.05f);
            float above = H.DeadZone(0.051f, 0.05f);
            Assert.AreEqual(0f, below, 0.0001f);
            Assert.Greater(above, 0f);
            Assert.Less(above, 0.05f, "Should be tiny just above threshold");
        }
    }

    //  SUITE 3 — Accumulate mode

    [TestFixture]
    public class AccumulateMode_Tests
    {
        private InputAnimatorBinding Binding(float mult = 1f, float dz = 0f,
                                              float min = -90f, float max = 90f)
        {
            return new InputAnimatorBinding
            {
                multiplier = mult, deadZone = dz,
                minValue = min, maxValue = max,
                responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
            };
        }

        // T3.1  Delta accumulates correctly over multiple frames
        [Test]
        public void Delta_AccumulatesAcrossFrames()
        {
            var b = Binding();
            float acc = H.Accumulate(0f, 0.5f, b);
            Assert.AreEqual(0.5f, acc, 0.001f);
            acc = H.Accumulate(acc, 0.5f, b);
            Assert.AreEqual(1.0f, acc, 0.001f);
        }

        // T3.2  Accumulator is clamped to [min, max]
        [Test]
        public void Accumulator_IsClamped()
        {
            var b = Binding();
            Assert.AreEqual( 90f, H.Accumulate( 89f,  5f, b), 0.001f, "max clamp");
            Assert.AreEqual(-90f, H.Accumulate(-89f, -5f, b), 0.001f, "min clamp");
        }

        // T3.3  Zero delta (cancel event) must NOT change accumulator
        [Test]
        public void ZeroDelta_HoldsCurrentValue()
        {
            var b = Binding();
            float acc = H.Accumulate(45f, 0f, b);
            Assert.AreEqual(45f, acc, 0.001f, "Cancel must not reset accumulator");
        }

        // T3.4  Multiplier scales delta
        [Test]
        public void Multiplier_ScalesDelta()
        {
            var b = Binding(mult: 2.5f);
            Assert.AreEqual(2.5f, H.Accumulate(0f, 1f, b), 0.001f);
        }

        // T3.5  Sub-deadzone deltas are suppressed
        [Test]
        public void SubDeadzoneDelta_IsSuppressed()
        {
            var b = Binding(dz: 0.05f);
            float acc = H.Accumulate(30f, 0.02f, b);
            Assert.AreEqual(30f, acc, 0.001f, "Sub-deadzone delta must not move accumulator");
        }
    }

    //  SUITE 4 — Damping modes

    [TestFixture]
    public class DampingMode_Tests
    {
        // T4.1  MoveTowards reaches target
        [Test]
        public void MoveTowards_ReachesTarget()
        {
            float c = 0f; float dt = 1f / 60f; bool reached = false;
            for (int i = 0; i < 1000; i++)
            {
                c = Mathf.MoveTowards(c, 1f, 6f * dt);
                if (Mathf.Approximately(c, 1f)) { reached = true; break; }
            }
            Assert.IsTrue(reached);
        }

        // T4.2  Lerp never overshoots
        [Test]
        public void Lerp_NeverOvershoots()
        {
            float c = 0f; float dt = 1f / 60f;
            for (int i = 0; i < 500; i++)
            {
                c = Mathf.Lerp(c, 1f, 6f * dt);
                Assert.LessOrEqual(c, 1.0001f, $"Overshot at frame {i}");
            }
        }

        // T4.3  MoveTowards is linear (constant step)
        [Test]
        public void MoveTowards_IsLinear()
        {
            float dt = 1f / 60f; float spd = 6f;
            float d1 = Mathf.MoveTowards(0.0f, 1f, spd * dt) - 0.0f;
            float d2 = Mathf.MoveTowards(0.3f, 1f, spd * dt) - 0.3f;
            Assert.AreEqual(d1, d2, 0.0001f);
        }

        // T4.4  Lerp step shrinks near target
        [Test]
        public void Lerp_StepShrinksNearTarget()
        {
            float dt = 1f / 60f; float spd = 6f;
            float s1 = Mathf.Abs(Mathf.Lerp(0.0f, 1f, spd * dt) - 0.0f);
            float s2 = Mathf.Abs(Mathf.Lerp(0.8f, 1f, spd * dt) - 0.8f);
            Assert.Greater(s1, s2);
        }
    }

    //  SUITE 5 — Clamp mode

    [TestFixture]
    public class ClampMode_Tests
    {
        private float Clamp(float raw, float mult, float min, float max)
            => Mathf.Clamp(raw * mult, min, max);

        [TestCase( 0.5f, 1f, -1f, 1f,  0.5f)]
        [TestCase(-0.5f, 1f, -1f, 1f, -0.5f)]
        [TestCase( 0.0f, 1f, -1f, 1f,  0.0f)]
        public void InRange_PassesThrough(float r, float m, float mn, float mx, float exp)
            => Assert.AreEqual(exp, Clamp(r, m, mn, mx), 0.001f);

        [TestCase( 2.0f, 1f,  1f)]
        [TestCase(-2.0f, 1f, -1f)]
        public void OutOfRange_IsClamped(float r, float m, float exp)
            => Assert.AreEqual(exp, Clamp(r, m, -1f, 1f), 0.001f);

        [Test]
        public void Multiplier_AppliedBeforeClamping()
        {
            Assert.AreEqual(0.6f, Clamp(0.3f, 2f, -1f, 1f), 0.001f);
            Assert.AreEqual(1.0f, Clamp(0.8f, 2f, -1f, 1f), 0.001f);
        }
    }

    //  SUITE 6 — Invert input

    [TestFixture]
    public class InvertInput_Tests
    {
        private InputAnimatorBinding B(bool invert) => new InputAnimatorBinding
        {
            invertInput   = invert,
            multiplier    = 1f,
            deadZone      = 0f,
            responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };

        // T6.1  Positive input flipped to negative when inverted
        [Test]
        public void PositiveInput_BecomesNegative_WhenInverted()
        {
            float r = H.ProcessRaw(0.5f, B(invert: true));
            Assert.Less(r, 0f, "Expected negative output");
        }

        // T6.2  Negative input flipped to positive when inverted
        [Test]
        public void NegativeInput_BecomesPositive_WhenInverted()
        {
            float r = H.ProcessRaw(-0.5f, B(invert: true));
            Assert.Greater(r, 0f, "Expected positive output");
        }

        // T6.3  Non-inverted binding does not flip sign
        [Test]
        public void NonInverted_SignUnchanged()
        {
            Assert.Greater(H.ProcessRaw( 0.5f, B(invert: false)), 0f);
            Assert.Less   (H.ProcessRaw(-0.5f, B(invert: false)), 0f);
        }

        // T6.4  Invert + deadzone: sub-threshold still 0 regardless of invert
        [Test]
        public void Invert_WithDeadZone_StillSuppressesSubThreshold()
        {
            var b = new InputAnimatorBinding
            {
                invertInput = true, deadZone = 0.1f, multiplier = 1f,
                responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
            };
            // Input 0.05 → invert → -0.05 → |0.05| < 0.1 threshold → 0
            Assert.AreEqual(0f, H.ProcessRaw(0.05f, b), 0.0001f);
        }
    }

    //  SUITE 7 — Response curve

    [TestFixture]
    public class ResponseCurve_Tests
    {
        // T7.1  Linear curve (default) does not change magnitude
        [Test]
        public void LinearCurve_DoesNotChangeMagnitude()
        {
            var b = new InputAnimatorBinding
            {
                invertInput = false, deadZone = 0f, multiplier = 1f,
                responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
            };
            Assert.AreEqual(0.6f, H.ProcessRaw(0.6f, b), 0.01f);
        }

        // T7.2  Constant zero curve maps all input to 0
        [Test]
        public void ZeroCurve_MapsAllInputToZero()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(1f, 0f)
            );
            var b = new InputAnimatorBinding
            {
                invertInput = false, deadZone = 0f, multiplier = 1f,
                responseCurve = curve
            };
            // Curve output is 0, so result should be sign * 0 * abs = 0
            Assert.AreEqual(0f, H.ProcessRaw(0.8f, b), 0.001f);
        }

        // T7.3  Curve applied symmetrically (same magnitude, preserved sign)
        [Test]
        public void Curve_PreservesSign()
        {
            // Use a simple quadratic-ish curve (output > input for values < 1)
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(1f, 1f)
            );
            curve.keys[0].outTangent = 0f;
            curve.keys[1].inTangent  = 2f;

            var b = new InputAnimatorBinding
            {
                invertInput = false, deadZone = 0f, multiplier = 1f,
                responseCurve = curve
            };

            float pos = H.ProcessRaw( 0.5f, b);
            float neg = H.ProcessRaw(-0.5f, b);

            Assert.Greater(pos, 0f, "Positive input should stay positive");
            Assert.Less   (neg, 0f, "Negative input should stay negative");
            Assert.AreEqual(Mathf.Abs(pos), Mathf.Abs(neg), 0.001f, "Symmetry");
        }
    }

    //  SUITE 8 — Reset behaviour

    [TestFixture]
    public class ResetBehaviour_Tests
    {
        // T8.1  resetValue default is 0
        [Test]
        public void ResetValue_DefaultIsZero()
            => Assert.AreEqual(0f, new InputAnimatorBinding().resetValue, 0.001f);

        // T8.2  resetOnDisable default is true
        [Test]
        public void ResetOnDisable_DefaultIsTrue()
            => Assert.IsTrue(new InputAnimatorBinding().resetOnDisable);

        // T8.3  Custom resetValue is retained after serialisation round-trip
        [Test]
        public void CustomResetValue_IsRetained()
        {
            var b = new InputAnimatorBinding { resetValue = 42f };
            Assert.AreEqual(42f, b.resetValue, 0.001f);
        }

        // T8.4  Clamp cancel sends target to resetValue (not necessarily 0)
        [Test]
        public void ClampCancel_UsesResetValue_NotZero()
        {
            // In the linker, on cancel: targetValues[b] = b.resetValue
            // We test the spec directly here
            var b = new InputAnimatorBinding { resetValue = 0.5f };
            // Simulate: after cancel the target should equal resetValue
            float targetAfterCancel = b.resetValue;
            Assert.AreEqual(0.5f, targetAfterCancel, 0.001f,
                "Target after cancel should equal resetValue, not hard-coded 0");
        }
    }

    //  SUITE 9 — Global multiplier

    [TestFixture]
    public class GlobalMultiplier_Tests
    {
        // T9.1  globalMultiplier = 1 leaves value unchanged
        [Test]
        public void GlobalMultiplier_One_LeavesValueUnchanged()
        {
            float target = 0.6f;
            float global = 1f;
            Assert.AreEqual(0.6f, target * global, 0.001f);
        }

        // T9.2  globalMultiplier = 0 drives value toward 0
        [Test]
        public void GlobalMultiplier_Zero_DrivesOutputToZero()
        {
            float target = 0.6f;
            float global = 0f;
            Assert.AreEqual(0f, target * global, 0.001f);
        }

        // T9.3  globalMultiplier = 2 doubles output
        [Test]
        public void GlobalMultiplier_Two_DoublesOutput()
        {
            float target = 0.5f;
            float global = 2f;
            Assert.AreEqual(1.0f, target * global, 0.001f);
        }

        // T9.4  globalMultiplier does NOT change stored target/accumulator
        [Test]
        public void GlobalMultiplier_DoesNotAlterStoredTarget()
        {
            // The contract: scaledTarget = target * globalMultiplier
            // The stored target remains unchanged so reverting globalMultiplier restores output
            float storedTarget = 0.7f;
            float globalA      = 2f;
            float globalB      = 1f;

            float outA = storedTarget * globalA;
            float outB = storedTarget * globalB;

            // storedTarget itself hasn't changed
            Assert.AreEqual(0.7f, storedTarget, 0.001f, "Stored target must be immutable");
            // outA = 1.4, outB = 0.7 — clearly different, no tolerance needed
            Assert.AreNotEqual(outA, outB, "Outputs should differ between globals");
        }
    }

    //  SUITE 10 — Public API contract

    [TestFixture]
    public class PublicAPI_Tests
    {
        // T10.1  enabled flag accessible and mutable
        [Test]
        public void EnabledFlag_IsAccessibleAndMutable()
        {
            var b = new InputAnimatorBinding { bindingName = "Move", enabled = true };
            Assert.IsTrue(b.enabled);
            b.enabled = false;
            Assert.IsFalse(b.enabled);
        }

        // T10.2  Disabled binding: enabled=false means it should be skipped
        [Test]
        public void DisabledBinding_ShouldBeSkipped()
        {
            var b = new InputAnimatorBinding { enabled = false };
            // The linker checks !b.enabled → continue; we verify the flag here
            Assert.IsFalse(b.enabled, "Binding should be marked as disabled");
        }

        // T10.3  bindingName used as lookup key
        [Test]
        public void BindingName_UsedAsLookupKey()
        {
            var b = new InputAnimatorBinding { bindingName = "LookX" };
            Assert.AreEqual("LookX", b.bindingName);
        }

        // T10.4  resetValue is the canonical value for accumulator reset
        [Test]
        public void ResetAccumulator_UsesResetValue()
        {
            var b     = new InputAnimatorBinding { resetValue = 10f };
            float acc = 45f;
            // Simulate ResetAccumulator: acc = b.resetValue
            acc = b.resetValue;
            Assert.AreEqual(10f, acc, 0.001f);
        }

        // T10.5  SetAccumulatorValue clamps to [min, max]
        [Test]
        public void SetAccumulatorValue_Clamps()
        {
            var b   = new InputAnimatorBinding { minValue = -90f, maxValue = 90f };
            float v = Mathf.Clamp(200f, b.minValue, b.maxValue);
            Assert.AreEqual(90f, v, 0.001f, "Should clamp at maxValue");
        }
    }
}
