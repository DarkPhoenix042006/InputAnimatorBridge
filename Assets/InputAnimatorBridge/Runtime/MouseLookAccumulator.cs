// ─────────────────────────────────────────────────────────────────────────────
//  MouseLookAccumulator  —  DEPRECATED in Input Animator Bridge v2
//
//  This script is no longer needed for driving Animator parameters.
//  Use InputAnimatorLinker with FloatMode = Accumulate instead:
//
//    • Add a Float binding on InputAnimatorLinker
//    • Set Float Mode  = Accumulate
//    • Set Vector Axis = X  (for horizontal)  or  Y (for vertical)
//    • Set Invert Input = true on the Y binding for standard camera feel
//    • Adjust Multiplier for sensitivity  (replaces the 'sensitivity' field below)
//    • Set Min / Max to your pitch limits  (replaces minY / maxY below)
//    • Call linker.ResetAccumulator("LookX") from code to snap back if needed
//
//  This file is kept ONLY if you need the accumulated look angles to also
//  drive GameObject transforms directly, outside the Animator.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.InputSystem;

[System.Obsolete("Use InputAnimatorLinker with FloatMode.Accumulate instead. See comment header.")]
public class MouseLookAccumulator : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference lookAction;

    [Header("Animator")]
    public Animator animator;
    public string lookXParam = "LookX";
    public string lookYParam = "LookY";

    [Header("Settings")]
    public float sensitivity = 1.5f;
    public float minY = -80f;
    public float maxY =  80f;

    private float lookX;
    private float lookY;

    private void OnEnable()  => lookAction.action.Enable();
    private void OnDisable() => lookAction.action.Disable();

    private void Update()
    {
        Vector2 delta = lookAction.action.ReadValue<Vector2>();

        lookX += delta.x * sensitivity;
        lookY -= delta.y * sensitivity;
        lookY  = Mathf.Clamp(lookY, minY, maxY);

        animator.SetFloat(lookXParam, lookX);
        animator.SetFloat(lookYParam, lookY);
    }
}
