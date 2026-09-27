using UnityEngine;

public static class AnimatorExtensions
{
    // Lets combat scripts fire animation triggers only once the parameter exists in the controller,
    // so they work (without warnings) before the attack animations are made.
    public static bool HasParameter(this Animator animator, string name)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == name) return true;
        }
        return false;
    }
}
