using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

// Menu: Lythrum > Combat > Setup Attack And Walk Animation
// Builds Player.controller states from the two Aseprite files:
//   AttackAnim V.1.0.aseprite  -> "Attack" state (plays once, started by the Attack / AreaSlash triggers PlayerCombat already sets)
//   MC V.2.3 1.aseprite        -> "Walk" state (loops, driven by the isWalking bool PlayerMovement already sets)
// Both blend trees pick the direction from LastInputX / LastInputY. Safe to run again.
public static class LythrumAttackAnimSetup
{
    const string AttackAsepritePath = "Assets/Sprites/AttackAnim V.1.0.aseprite";
    const string WalkAsepritePath = "Assets/Sprites/MC V.2.3 1.aseprite";
    const string ControllerPath = "Assets/Animation/Player.controller";
    const string ClipFolder = "Assets/Animation";

    // Aseprite tag (clock position), clip name, blend position
    static readonly (string tag, string name, Vector2 pos)[] AttackDirections =
    {
        ("06-m1", "AttackDown",  new Vector2(0f, -1f)),
        ("03-m1", "AttackRight", new Vector2(1f, 0f)),
        ("09-m1", "AttackLeft",  new Vector2(-1f, 0f)),
        ("12-m1", "AttackUp",    new Vector2(0f, 1f)),
    };

    static readonly (string tag, string name, Vector2 pos)[] WalkDirections =
    {
        ("Walk-6.0",  "MCWalkDown",      new Vector2(0f, -1f)),
        ("walk-7.3",  "MCWalkDownLeft",  new Vector2(-1f, -1f)),
        ("walk-9.0",  "MCWalkLeft",      new Vector2(-1f, 0f)),
        ("walk-10.3", "MCWalkUpLeft",    new Vector2(-1f, 1f)),
        ("walk-12.0", "MCWalkUp",        new Vector2(0f, 1f)),
        ("walk-1.3",  "MCWalkUpRight",   new Vector2(1f, 1f)),
        ("walk-3.0",  "MCWalkRight",     new Vector2(1f, 0f)),
        ("walk-4.3",  "MCWalkDownRight", new Vector2(1f, -1f)),
    };

    [MenuItem("Lythrum/Combat/Setup Attack And Walk Animation")]
    static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Fail("Could not find " + ControllerPath);
            return;
        }

        if (!ApplyPivot(AttackAsepritePath, AttackPivot) || !ApplyPivot(WalkAsepritePath, WalkPivot)) return;

        var attackClips = BuildClips(AttackAsepritePath, AttackDirections, loop: false);
        var walkClips = BuildClips(WalkAsepritePath, WalkDirections, loop: true);
        if (attackClips == null || walkClips == null) return;

        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "AreaSlash", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "ComboStep", AnimatorControllerParameterType.Int);
        AddParameter(controller, "isWalking", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = sm.defaultState;

        // Rebuild our states from scratch so running this twice does not stack duplicates.
        foreach (ChildAnimatorState child in sm.states.ToArray())
            if (child.state.name == "Attack" || child.state.name == "Walk") sm.RemoveState(child.state);

        AnimatorState walk = sm.AddState("Walk", new Vector3(300f, 150f, 0f));
        walk.motion = MakeTree(controller, "Walk", BlendTreeType.SimpleDirectional2D, walkClips);
        walk.speed = 2f;   // the Aseprite frames are 300 ms each, which is a slow walk

        AnimatorState attack = sm.AddState("Attack", new Vector3(300f, 50f, 0f));
        attack.motion = MakeTree(controller, "Attack", BlendTreeType.SimpleDirectional2D, attackClips);
        attack.speed = 1.5f;   // 4 frames at 80-100 ms is ~0.36 s, a bit slow for a 0.23 s combo step

        if (idle != null)
        {
            AddTransition(idle, walk, "isWalking", AnimatorConditionMode.If);
            AddTransition(walk, idle, "isWalking", AnimatorConditionMode.IfNot);

            AnimatorStateTransition back = attack.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0f;
        }

        AddAnyStateTrigger(sm, attack, "Attack");
        AddAnyStateTrigger(sm, attack, "AreaSlash");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Attack/Walk Anim] Done. Player.controller now has Idle, Walk and Attack states.");
        Verify();
    }

    // Puts the sprite origin (= the Player's position) on the character's body, at the same spot the Idle sprites use:
    // horizontally on the middle of the hair, 14 px below the top of the hair. Measured from the Aseprite canvases.
    static readonly Vector2 AttackPivot = new Vector2(32f / 64f, 33f / 64f);
    static readonly Vector2 WalkPivot = new Vector2(560f / 640f, 193f / 640f);
    const float PixelsPerUnit = 32f;   // same as the player's other sprites

    // The importer only recalculates sprite pivots when its pivot settings differ from the ones used on the last import,
    // so switch to a different alignment first, then to the real one. That way the pivots are always rebuilt.
    static bool ApplyPivot(string path, Vector2 pivot)
    {
        var importer = AssetImporter.GetAtPath(path) as AsepriteImporter;
        if (importer == null)
        {
            Fail("Could not find the Aseprite importer for " + path);
            return false;
        }
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.pivotSpace = PivotSpaces.Canvas;
        importer.pivotAlignment = SpriteAlignment.BottomCenter;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        importer = (AsepriteImporter)AssetImporter.GetAtPath(path);
        importer.pivotSpace = PivotSpaces.Canvas;
        importer.pivotAlignment = SpriteAlignment.Custom;
        importer.customPivotPosition = pivot;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        AssetDatabase.WriteImportSettingsIfDirty(path);   // keep the recalculated pivots in the .meta
        return true;
    }

    // Test cases: every sprite the Walk / Attack clips show must sit on the player, not off to one side.
    // Before the fix the walk sprites were ~7 units away and the attack sprites ~1 unit too high.
    [MenuItem("Lythrum/Combat/Verify Attack And Walk Animation")]
    static void Verify()
    {
        int passed = 0, failed = 0;
        void Check(bool ok, string what)
        {
            if (ok) passed++;
            else { failed++; Debug.LogError("[Attack/Walk Anim] FAIL " + what); }
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        string[] stateNames = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        foreach (string s in new[] { "Idle", "Walk", "Attack" })
            Check(stateNames.Contains(s), "controller has a " + s + " state");
        foreach (string p in new[] { "Attack", "AreaSlash", "isWalking", "LastInputX", "LastInputY" })
            Check(controller.parameters.Any(x => x.name == p), "controller has parameter " + p);

        // Walk: the body fills the sprite, so its bounds must be centred on the player.
        // Attack: the sword makes the bounds lopsided, but never by half a unit; the old bug was a full unit.
        foreach (var (clips, limit) in new[] { (WalkDirections, 0.2f), (AttackDirections, 0.5f) })
        {
            foreach (var d in clips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + "/" + d.name + ".anim");
                Check(clip != null, d.name + ".anim exists");
                if (clip == null) continue;
                Check(clip.isLooping == (clips == WalkDirections), d.name + " loop setting");

                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Check(bindings.Length == 1 && bindings[0].path == "" && bindings[0].propertyName == "m_Sprite",
                      d.name + " animates the Player's own SpriteRenderer");
                foreach (var b in bindings)
                {
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, b))
                    {
                        var sprite = key.value as Sprite;
                        Check(sprite != null, d.name + " frame at " + key.time + "s has a sprite");
                        if (sprite == null) continue;
                        Vector3 c = sprite.bounds.center;
                        Check(Mathf.Abs(c.x) <= limit && Mathf.Abs(c.y) <= limit,
                              d.name + " " + sprite.name + " is off the player by (" + c.x.ToString("0.00") + ", " + c.y.ToString("0.00") + ") units");
                        Check(Mathf.Approximately(sprite.pixelsPerUnit, PixelsPerUnit), d.name + " " + sprite.name + " pixels per unit is " + sprite.pixelsPerUnit);
                        // A frame bigger than one 64 px cell means stray pixels outside the Aseprite canvas got imported.
                        Check(sprite.rect.width <= 64f && sprite.rect.height <= 64f,
                              d.name + " " + sprite.name + " is " + sprite.rect.width + "x" + sprite.rect.height + " px (stray pixels outside the canvas?)");
                    }
                }
            }
        }

        string summary = "[Attack/Walk Anim] Tests: " + passed + " passed, " + failed + " failed.";
        if (failed == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    static List<(AnimationClip clip, Vector2 pos)> BuildClips(string asepritePath, (string tag, string name, Vector2 pos)[] directions, bool loop)
    {
        AnimationClip[] source = AssetDatabase.LoadAllAssetsAtPath(asepritePath).OfType<AnimationClip>().ToArray();
        if (source.Length == 0)
        {
            Fail("No animation clips found in " + asepritePath + ". Select the file, tick 'Generate Animation Clips' in the Inspector, press Apply and try again.");
            return null;
        }

        var result = new List<(AnimationClip, Vector2)>();
        foreach (var d in directions)
        {
            AnimationClip src = source.FirstOrDefault(c => string.Equals(c.name, d.tag, StringComparison.OrdinalIgnoreCase));
            if (src == null)
            {
                Fail("No clip named '" + d.tag + "' in " + asepritePath + ". Found: " + string.Join(", ", source.Select(c => c.name)));
                return null;
            }
            result.Add((MakeClip(src, d.name, loop), d.pos));
        }
        return result;
    }

    static BlendTree MakeTree(AnimatorController controller, string name, BlendTreeType type, List<(AnimationClip clip, Vector2 pos)> clips)
    {
        var tree = new BlendTree
        {
            name = name,
            blendType = type,
            blendParameter = "LastInputX",
            blendParameterY = "LastInputY",
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        foreach (var c in clips) tree.AddChild(c.clip, c.pos);
        return tree;
    }

    static void AddTransition(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(mode, 0f, param);
        t.hasExitTime = false;
        t.duration = 0f;
    }

    static void AddAnyStateTrigger(AnimatorStateMachine sm, AnimatorState target, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(target);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = true;   // pressing attack again restarts the swing
    }

    static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(name, type);
    }

    // Copies the imported (read-only) clip into an editable asset with the loop setting we want.
    static AnimationClip MakeClip(AnimationClip src, string name, bool loop)
    {
        var copy = new AnimationClip();
        EditorUtility.CopySerialized(src, copy);
        copy.name = name;

        // The player's SpriteRenderer sits on the Animator object itself, so the sprite curve must have an empty path.
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(copy))
            AnimationUtility.SetEditorCurve(copy, b, null);
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(copy))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(copy, b);
            AnimationUtility.SetObjectReferenceCurve(copy, b, null);
            if (b.propertyName != "m_Sprite") continue;
            var fixedBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(copy, fixedBinding, keys);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(copy, settings);

        string path = ClipFolder + "/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(copy, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }

    static void Fail(string message)
    {
        Debug.LogError("[Attack/Walk Anim] " + message);
        EditorUtility.DisplayDialog("Attack And Walk Animation Setup", message, "OK");
    }
}
