using UnityEditor;
using UnityEngine;

namespace InputAnimatorBridge.Editor
{
    [CustomEditor(typeof(InputAnimatorLinker))]
    public class InputAnimatorLinkerEditor : UnityEditor.Editor
    {
        private Texture2D logo;

        private SerializedProperty inputActions;
        private SerializedProperty animator;
        private SerializedProperty globalMultiplier;
        private SerializedProperty bindings;

        // ── Colours ───────────────────────────────────────────────────────────────
        private static Color SectionBg      => EditorGUIUtility.isProSkin
            ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.80f, 0.80f, 0.80f);
        private static Color CardBg         => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.90f, 0.90f, 0.90f);
        private static Color AccentBlue     => new Color(0.31f, 0.64f, 1.00f);
        private static Color AccentOrange   => new Color(1.00f, 0.62f, 0.20f);
        private static Color AccentGreen    => new Color(0.35f, 0.85f, 0.45f);
        private static Color AccentGrey     => new Color(0.50f, 0.50f, 0.50f);
        private static Color DisabledTint   => new Color(1f, 1f, 1f, 0.38f);

        // Accent colour per param type so cards are instantly recognisable
        private static Color ParamColor(AnimatorParamType t) => t switch
        {
            AnimatorParamType.Float   => AccentBlue,
            AnimatorParamType.Bool    => AccentGreen,
            AnimatorParamType.Int     => AccentOrange,
            AnimatorParamType.Trigger => AccentGrey,
            _                        => AccentBlue
        };

        private void OnEnable()
        {
            logo             = Resources.Load<Texture2D>("DarkPhoenix04(logo)");
            inputActions     = serializedObject.FindProperty("inputActions");
            animator         = serializedObject.FindProperty("animator");
            globalMultiplier = serializedObject.FindProperty("globalMultiplier");
            bindings         = serializedObject.FindProperty("bindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawLogo();
            DrawToolHeader();

            EditorGUILayout.Space(8);
            DrawReferencesSection();

            EditorGUILayout.Space(6);
            DrawGlobalSettingsSection();

            EditorGUILayout.Space(10);
            DrawBindingsSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ─── Top-level sections ───────────────────────────────────────────────────

        private void DrawReferencesSection()
        {
            DrawSectionHeader("References");
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(inputActions);
            EditorGUILayout.PropertyField(animator);
            EditorGUI.indentLevel--;
        }

        private void DrawGlobalSettingsSection()
        {
            DrawSectionHeader("Global Settings");
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(globalMultiplier,
                new GUIContent("Global Multiplier",
                    "Multiplies ALL float binding outputs simultaneously.\n" +
                    "Use as a runtime sensitivity slider without touching individual bindings."));

            // Live readout of binding count
            int enabled  = 0;
            int disabled = 0;
            foreach (var b in ((InputAnimatorLinker)target).bindings)
            {
                if (b.enabled) enabled++; else disabled++;
            }

            EditorGUILayout.HelpBox(
                $"{enabled} binding{(enabled == 1 ? "" : "s")} active" +
                (disabled > 0 ? $"  ·  {disabled} disabled" : ""),
                MessageType.None);

            EditorGUI.indentLevel--;
        }

        private void DrawBindingsSection()
        {
            DrawSectionHeader($"Bindings  ({bindings.arraySize})");

            for (int i = 0; i < bindings.arraySize; i++)
            {
                bool removed = DrawBinding(bindings.GetArrayElementAtIndex(i), i);
                EditorGUILayout.Space(3);
                if (removed)
                {
                    bindings.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            EditorGUILayout.Space(4);

            bool refsReady = inputActions.objectReferenceValue != null
                          && animator.objectReferenceValue != null;

            using (new EditorGUI.DisabledScope(!refsReady))
            {
                if (GUILayout.Button("＋  Add Binding", GUILayout.Height(30)))
                    bindings.InsertArrayElementAtIndex(bindings.arraySize);
            }

            if (!refsReady)
                EditorGUILayout.HelpBox(
                    "Assign Input Actions and Animator above before adding bindings.",
                    MessageType.Warning);
        }

        // ─── Single binding card ──────────────────────────────────────────────────

        /// <returns>true if the binding was deleted.</returns>
        private bool DrawBinding(SerializedProperty el, int index)
        {
            var bindingNameProp = el.FindPropertyRelative("bindingName");
            var enabledProp     = el.FindPropertyRelative("enabled");
            var actionNameProp  = el.FindPropertyRelative("inputActionName");
            var paramTypeProp   = el.FindPropertyRelative("paramType");
            var animParamProp   = el.FindPropertyRelative("animatorParameter");

            bool   isEnabled   = enabledProp.boolValue;
            var    paramType   = (AnimatorParamType)paramTypeProp.enumValueIndex;
            string bindingName = NotEmpty(bindingNameProp.stringValue, $"Binding {index + 1}");
            string actionName  = NotEmpty(actionNameProp.stringValue,  "?");
            string animParam   = NotEmpty(animParamProp.stringValue,   "?");
            string typeTag     = paramType.ToString().ToUpper();
            string title       = $"[{typeTag}]  {bindingName}   {actionName}  →  {animParam}";

            // ── Card outline ──────────────────────────────────────────────────────
            var cardRect = EditorGUILayout.BeginVertical(GUI.skin.box);

            // Background tint for disabled cards
            if (!isEnabled)
                EditorGUI.DrawRect(cardRect, new Color(0f, 0f, 0f, 0.15f));

            // Left accent bar (colour = param type)
            DrawColorBar(cardRect, isEnabled ? ParamColor(paramType) : AccentGrey);

            // ── Header row ────────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            // Enabled toggle
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(16));
            if (EditorGUI.EndChangeCheck()) enabledProp.boolValue = newEnabled;

            // Foldout (greyed when disabled)
            GUI.color = isEnabled ? Color.white : DisabledTint;
            el.isExpanded = EditorGUILayout.Foldout(el.isExpanded, title, true, EditorStyles.foldoutHeader);
            GUI.color = Color.white;

            // Delete button
            GUI.color = new Color(1f, 0.40f, 0.40f);
            bool removed = GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18));
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            // ── Body ──────────────────────────────────────────────────────────────
            if (el.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(4);

                // ── Identity ──────────────────────────────────────────────────────
                DrawSubHeader("Identity");
                EditorGUILayout.PropertyField(bindingNameProp,
                    new GUIContent("Binding Name",
                        "Used as display label AND as the key for the public API " +
                        "(SetBindingEnabled, GetCurrentValue, ResetAccumulator, etc.)"));

                EditorGUILayout.Space(4);

                // ── Input → Animator ──────────────────────────────────────────────
                DrawSubHeader("Input  →  Animator");
                EditorGUILayout.PropertyField(actionNameProp, new GUIContent("Input Action"));
                EditorGUILayout.PropertyField(paramTypeProp,  new GUIContent("Parameter Type"));
                EditorGUILayout.PropertyField(animParamProp,  new GUIContent("Animator Parameter"));

                EditorGUILayout.Space(4);

                // ── Type-specific sections ────────────────────────────────────────
                switch (paramType)
                {
                    case AnimatorParamType.Bool:
                        DrawBoolSection(el);
                        break;
                    case AnimatorParamType.Float:
                        DrawFloatSection(el);
                        break;
                    case AnimatorParamType.Int:
                        DrawIntSection(el);
                        break;
                    case AnimatorParamType.Trigger:
                        DrawTriggerSection(el);
                        break;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndVertical();
            return removed;
        }

        // ─── Type-specific sections ───────────────────────────────────────────────

        private void DrawBoolSection(SerializedProperty el)
        {
            DrawSubHeader("Bool Settings");
            EditorGUILayout.PropertyField(el.FindPropertyRelative("boolMode"),
                new GUIContent("Bool Mode",
                    "Hold = true while pressed, false on release.\n" +
                    "Toggle = flips state on each press."));

            EditorGUILayout.Space(4);
            DrawResetSection(el, isBool: true);
        }

        private void DrawFloatSection(SerializedProperty el)
        {
            // ── Input processing ──────────────────────────────────────────────────
            DrawSubHeader("Input Processing");

            var floatModeProp = el.FindPropertyRelative("floatMode");
            EditorGUILayout.PropertyField(floatModeProp,
                new GUIContent("Float Mode",
                    "Clamp = resets to 0 on key release — use for WASD / gamepad sticks.\n" +
                    "Accumulate = adds delta, never auto-resets — use for mouse look."));

            EditorGUILayout.PropertyField(el.FindPropertyRelative("invertInput"),
                new GUIContent("Invert Input",
                    "Flips the sign of the raw input before any other processing.\n" +
                    "Useful for inverted Y-axis look or reversing stick direction."));

            EditorGUILayout.PropertyField(el.FindPropertyRelative("vectorAxis"),
                new GUIContent("Vector Axis",
                    "X or Y component to extract from a Vector2 action.\n" +
                    "None = vector magnitude."));

            EditorGUILayout.PropertyField(el.FindPropertyRelative("multiplier"),
                new GUIContent("Multiplier",
                    "Scales raw input BEFORE the response curve and clamping."));

            EditorGUILayout.PropertyField(el.FindPropertyRelative("deadZone"),
                new GUIContent("Dead Zone",
                    "Input magnitudes below this value are treated as exactly 0.\n" +
                    "Eliminates controller stick drift. Range 0–1."));

            EditorGUILayout.Space(4);

            // ── Response curve ────────────────────────────────────────────────────
            DrawSubHeader("Response Curve");
            EditorGUILayout.PropertyField(el.FindPropertyRelative("responseCurve"),
                new GUIContent("Response Curve",
                    "Non-linear input remapping applied after dead zone + multiplier.\n" +
                    "X = normalised input (0–1), Y = output shape (0–1).\n" +
                    "Straight diagonal = linear (default).\n" +
                    "S-curve = soft centre, sharp edges.\n" +
                    "Quadratic = fine low-speed control."));

            EditorGUILayout.Space(4);

            // ── Range ─────────────────────────────────────────────────────────────
            DrawSubHeader("Range");
            EditorGUILayout.PropertyField(el.FindPropertyRelative("minValue"), new GUIContent("Min"));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("maxValue"), new GUIContent("Max"));

            EditorGUILayout.Space(4);

            // ── Smoothing ─────────────────────────────────────────────────────────
            DrawSubHeader("Smoothing");
            EditorGUILayout.PropertyField(el.FindPropertyRelative("dampingMode"),
                new GUIContent("Damping Mode",
                    "MoveTowards = linear speed (units/sec).\n" +
                    "Lerp = exponential ease-out — organic / springy feel."));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("changeSpeed"),
                new GUIContent("Change Speed",
                    "MoveTowards: units/sec.\n" +
                    "Lerp: blend factor — higher = snappier. Try 1–20."));

            EditorGUILayout.Space(4);
            DrawResetSection(el, isBool: false);

            // ── Context hint ──────────────────────────────────────────────────────
            var floatMode = (FloatMode)floatModeProp.enumValueIndex;
            EditorGUILayout.HelpBox(
                floatMode == FloatMode.Accumulate
                    ? "Accumulate mode  ·  Mouse look / free camera.  " +
                      "Use ResetAccumulator(bindingName) from code to snap back."
                    : "Clamp mode  ·  Movement axes / gamepad sticks.  " +
                      "Value smoothly returns to Reset Value when input is released.",
                MessageType.None);
        }

        private void DrawIntSection(SerializedProperty el)
        {
            DrawSubHeader("Int Settings");
            EditorGUILayout.PropertyField(el.FindPropertyRelative("invertInput"),
                new GUIContent("Invert Input"));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("multiplier"),
                new GUIContent("Multiplier", "Scales raw input before rounding to int."));
            EditorGUILayout.PropertyField(el.FindPropertyRelative("deadZone"),
                new GUIContent("Dead Zone"));
            EditorGUILayout.Space(4);
            DrawResetSection(el, isBool: false);
        }

        private void DrawTriggerSection(SerializedProperty el)
        {
            EditorGUILayout.HelpBox(
                "Trigger fires SetTrigger on 'performed'. No extra settings required.",
                MessageType.None);
        }

        private void DrawResetSection(SerializedProperty el, bool isBool)
        {
            DrawSubHeader("Reset Behaviour");
            if (!isBool)
            {
                EditorGUILayout.PropertyField(el.FindPropertyRelative("resetValue"),
                    new GUIContent("Reset Value",
                        "Value written to the Animator parameter when the binding resets " +
                        "(on cancel for Clamp, or on component disable if resetOnDisable is true)."));
            }
            EditorGUILayout.PropertyField(el.FindPropertyRelative("resetOnDisable"),
                new GUIContent("Reset On Disable",
                    "Write the reset value when this component disables.\n" +
                    "Uncheck to HOLD the last value (e.g. keep camera rotation between scenes)."));
        }

        // ─── UI helpers ───────────────────────────────────────────────────────────

        private void DrawLogo()
        {
            if (logo == null) return;
            GUILayout.Space(8);
            Rect r = GUILayoutUtility.GetRect(1, 110, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(r, logo, ScaleMode.ScaleToFit);
            GUILayout.Space(6);
        }

        private void DrawToolHeader()
        {
            GUILayout.Label("Input Animator Bridge", EditorStyles.whiteLargeLabel);
            EditorGUILayout.HelpBox(
                "Bind Input System actions to Animator parameters — no code required.\n" +
                "v2  ·  Per-binding enable/disable  ·  Invert  ·  Response Curve  ·  " +
                "Reset-on-Disable  ·  Global Multiplier  ·  Public API",
                MessageType.Info);
        }

        private void DrawSectionHeader(string label)
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, SectionBg);
            rect.xMin += 6;
            EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
        }

        private static void DrawSubHeader(string label) =>
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        private static void DrawColorBar(Rect cardRect, Color color) =>
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3f, cardRect.height), color);

        private static string NotEmpty(string v, string fallback) =>
            string.IsNullOrEmpty(v) ? fallback : v;
    }
}
