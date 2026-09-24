using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations.Rigging;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace SpringIK.Editor
{
    /// <summary>
    /// Inspector for <see cref="SpringIKConstraint"/>, plus the authoring menu items.
    /// </summary>
    [CustomEditor(typeof(SpringIKConstraint))]
    [CanEditMultipleObjects]
    public class SpringIKConstraintEditor : UnityEditor.Editor
    {
        static class Content
        {
            public static readonly GUIContent Weight = EditorGUIUtility.TrTextContent(
                "Weight", "Constraint strength. 0 = off.");
            public static readonly GUIContent Root = EditorGUIUtility.TrTextContent(
                "Root", "The hip.");
            public static readonly GUIContent Knee = EditorGUIUtility.TrTextContent(
                "Knee", "First bend. Child of Root.");
            public static readonly GUIContent Hock = EditorGUIUtility.TrTextContent(
                "Hock", "Second bend. Child of Knee.");
            public static readonly GUIContent Tip = EditorGUIUtility.TrTextContent(
                "Tip", "The foot. Child of Hock.");
            public static readonly GUIContent SourceObjects = EditorGUIUtility.TrTextContent("Source Objects");
            public static readonly GUIContent Target = EditorGUIUtility.TrTextContent(
                "Target", "Where the foot goes.");
            public static readonly GUIContent KneeHint = EditorGUIUtility.TrTextContent(
                "Knee Hint", "Optional. The knee bends toward it.");
            public static readonly GUIContent HockHint = EditorGUIUtility.TrTextContent(
                "Hock Hint", "Optional. The hock bends toward it.");
            public static readonly GUIContent Settings = EditorGUIUtility.TrTextContent("Settings");
            public static readonly GUIContent Bias = EditorGUIUtility.TrTextContent(
                "Spring Angle Bias", "Where the bend goes: -1 knee, 0 even, +1 hock.");
            public static readonly GUIContent PosWeight = EditorGUIUtility.TrTextContent(
                "Target Position Weight", "How much the target's position drives the leg.");
            public static readonly GUIContent RotWeight = EditorGUIUtility.TrTextContent(
                "Target Rotation Weight", "How much the target's rotation drives the foot.");
            public static readonly GUIContent KneeHintWeight = EditorGUIUtility.TrTextContent(
                "Knee Hint Weight", "How much the knee hint is followed.");
            public static readonly GUIContent HockHintWeight = EditorGUIUtility.TrTextContent(
                "Hock Hint Weight", "How much the hock hint is followed.");
            public static readonly GUIContent MaintainPos = EditorGUIUtility.TrTextContent(
                "Maintain Target Position Offset", "Keep the foot's starting offset from the target.");
            public static readonly GUIContent MaintainRot = EditorGUIUtility.TrTextContent(
                "Maintain Target Rotation Offset", "Keep the foot's starting rotation offset from the target.");
        }

        SerializedProperty m_Weight, m_Root, m_Knee, m_Hock, m_Tip, m_Target, m_KneeHint, m_HockHint;
        SerializedProperty m_Bias, m_PosWeight, m_RotWeight, m_KneeHintWeight, m_HockHintWeight;
        SerializedProperty m_MaintainPos, m_MaintainRot;

        // The package's foldout and content helpers are internal to its own assembly, so the
        // handful of lines they provide are reimplemented here instead of reused.
        const string k_SourcesKey = "SpringIK.Editor.Sources";
        const string k_SettingsKey = "SpringIK.Editor.Settings";

        void OnEnable()
        {
            m_Weight = serializedObject.FindProperty("m_Weight");
            var d = serializedObject.FindProperty("m_Data");
            m_Root = d.FindPropertyRelative("m_Root");
            m_Knee = d.FindPropertyRelative("m_Knee");
            m_Hock = d.FindPropertyRelative("m_Hock");
            m_Tip = d.FindPropertyRelative("m_Tip");
            m_Target = d.FindPropertyRelative("m_Target");
            m_KneeHint = d.FindPropertyRelative("m_KneeHint");
            m_HockHint = d.FindPropertyRelative("m_HockHint");
            m_Bias = d.FindPropertyRelative("m_SpringAngleBias");
            m_PosWeight = d.FindPropertyRelative("m_TargetPositionWeight");
            m_RotWeight = d.FindPropertyRelative("m_TargetRotationWeight");
            m_KneeHintWeight = d.FindPropertyRelative("m_KneeHintWeight");
            m_HockHintWeight = d.FindPropertyRelative("m_HockHintWeight");
            m_MaintainPos = d.FindPropertyRelative("m_MaintainTargetPositionOffset");
            m_MaintainRot = d.FindPropertyRelative("m_MaintainTargetRotationOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Weight, Content.Weight);
            EditorGUILayout.PropertyField(m_Root, Content.Root);
            EditorGUILayout.PropertyField(m_Knee, Content.Knee);
            EditorGUILayout.PropertyField(m_Hock, Content.Hock);
            EditorGUILayout.PropertyField(m_Tip, Content.Tip);

            bool sources = EditorPrefs.GetBool(k_SourcesKey, true);
            bool newSources = EditorGUILayout.BeginFoldoutHeaderGroup(sources, Content.SourceObjects);
            if (newSources != sources) EditorPrefs.SetBool(k_SourcesKey, newSources);
            if (newSources)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_Target, Content.Target);
                EditorGUILayout.PropertyField(m_KneeHint, Content.KneeHint);
                EditorGUILayout.PropertyField(m_HockHint, Content.HockHint);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            bool settings = EditorPrefs.GetBool(k_SettingsKey, true);
            bool newSettings = EditorGUILayout.BeginFoldoutHeaderGroup(settings, Content.Settings);
            if (newSettings != settings) EditorPrefs.SetBool(k_SettingsKey, newSettings);
            if (newSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_Bias, Content.Bias);
                EditorGUILayout.PropertyField(m_PosWeight, Content.PosWeight);
                EditorGUILayout.PropertyField(m_RotWeight, Content.RotWeight);
                EditorGUILayout.PropertyField(m_KneeHintWeight, Content.KneeHintWeight);
                EditorGUILayout.PropertyField(m_HockHintWeight, Content.HockHintWeight);
                EditorGUILayout.PropertyField(m_MaintainPos, Content.MaintainPos);
                EditorGUILayout.PropertyField(m_MaintainRot, Content.MaintainRot);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            DrawReachReadout();

            serializedObject.ApplyModifiedProperties();
        }

        // The inner reach limit moves with the bias whenever the bones are unequal, and no other
        // field in the inspector shows it.
        void DrawReachReadout()
        {
            if (targets.Length != 1) return;
            var c = (SpringIKConstraint)target;
            var d = c.data;
            if (d.root == null || d.knee == null || d.hock == null || d.tip == null) return;

            float l1 = Vector3.Distance(d.root.position, d.knee.position);
            float l2 = Vector3.Distance(d.knee.position, d.hock.position);
            float l3 = Vector3.Distance(d.hock.position, d.tip.position);
            float t = l1 + l2 + l3;
            if (t < SpringIKMath.LengthEpsilon) return;

            float fold = SpringIKMath.FoldLimit(l1, l2, l3, d.springAngleBias);
            float chainInner = SpringIKMath.ChainInnerLimit(l1, l2, l3);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reach (live)", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Bones   {l1:F3} / {l2:F3} / {l3:F3}   total {t:F3}");
            EditorGUILayout.LabelField($"Window at this bias   {fold:F3} .. {t:F3}" +
                                       $"   ({100f * (t - fold) / t:F0}% of full extension)");
            if (fold > chainInner + 1e-4f)
            {
                EditorGUILayout.LabelField(
                    $"The chain itself could fold to {chainInner:F3}; this bias costs " +
                    $"{100f * (fold - chainInner) / t:F0}% of travel.", EditorStyles.miniLabel);
            }
            if (d.target != null)
            {
                float dist = Vector3.Distance(d.root.position, d.target.position);
                if (dist > t + 1e-3f)
                    EditorGUILayout.HelpBox($"Target is {dist:F3} away — beyond full extension ({t:F3}). " +
                                            "The chain straightens and stops; it does not stretch.", MessageType.Info);
                else if (dist < fold - 1e-3f)
                    EditorGUILayout.HelpBox($"Target is {dist:F3} away — inside the fold limit ({fold:F3}) " +
                                            "at this bias. The chain holds at maximum fold.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }

        [MenuItem("CONTEXT/SpringIKConstraint/Auto Setup from Tip Transform", false, 631)]
        public static void AutoSetup(MenuCommand command)
        {
            var constraint = command.context as SpringIKConstraint;
            var data = constraint.data;

            Transform tip = data.tip;
            var animator = constraint.GetComponentInParent<Animator>()?.transform;
            if (tip == null)
            {
                foreach (var sel in Selection.transforms)
                {
                    if (animator != null && sel.IsChildOf(animator) && sel != constraint.transform) { tip = sel; break; }
                }
                if (tip == null)
                {
                    Debug.LogWarning("SpringIK: select the FOOT bone (or assign Tip) before running auto setup.");
                    return;
                }
            }

            if (tip.parent == null || tip.parent.parent == null || tip.parent.parent.parent == null)
            {
                Debug.LogWarning($"SpringIK: '{tip.name}' does not have three ancestors, so it cannot be " +
                                 "the tip of a 3-bone chain.");
                return;
            }

            Undo.RecordObject(constraint, "SpringIK auto setup");
            data.tip = tip;
            data.hock = tip.parent;
            data.knee = tip.parent.parent;
            data.root = tip.parent.parent.parent;

            if (data.target == null)
                data.target = FindOrCreateChild(constraint, "_target", tip.position, tip.rotation);

            // Hints are placed where the current pose implies, so auto setup leaves the authored
            // pose unchanged instead of re-posing it.
            Vector3 a = data.root.position, b = data.knee.position, c = data.hock.position, e = data.tip.position;
            float reach = Vector3.Distance(a, b) + Vector3.Distance(b, c) + Vector3.Distance(c, e);
            Vector3 n = (e - a);
            Vector3 m = Vector3.Cross((b - a).normalized, (c - b).normalized);
            Vector3 kneeSide;
            if (n.sqrMagnitude > SpringIKMath.SqrEpsilon && m.sqrMagnitude > SpringIKMath.SqrEpsilon)
            {
                // cross(n, m), in that order, is the side the knee displaces toward; swapping the
                // operands puts both hints on the wrong side of the chain.
                kneeSide = Vector3.Cross(n.normalized, m.normalized).normalized;
            }
            else
            {
                kneeSide = Vector3.forward;   // straight or degenerate chain: any side will do
            }

            Vector3 mid = 0.5f * (a + e);
            if (data.kneeHint == null)
                data.kneeHint = FindOrCreateChild(constraint, "_kneeHint", mid + kneeSide * reach, Quaternion.identity);
            if (data.hockHint == null)
                data.hockHint = FindOrCreateChild(constraint, "_hockHint", mid - kneeSide * reach, Quaternion.identity);

            constraint.data = data;
            EditorUtility.SetDirty(constraint);
            Debug.Log($"SpringIK: set up {data.root.name} -> {data.knee.name} -> {data.hock.name} -> {data.tip.name}" +
                      $" (reach {reach:F3}).");
        }

        [MenuItem("CONTEXT/SpringIKConstraint/Transfer motion to constraint", false, 611)]
        public static void TransferMotionToConstraint(MenuCommand command) =>
            BakeUtils.TransferMotionToConstraint(command.context as SpringIKConstraint);

        [MenuItem("CONTEXT/SpringIKConstraint/Transfer motion to skeleton", false, 612)]
        public static void TransferMotionToSkeleton(MenuCommand command) =>
            BakeUtils.TransferMotionToSkeleton(command.context as SpringIKConstraint);

        [MenuItem("CONTEXT/SpringIKConstraint/Transfer motion to constraint", true)]
        [MenuItem("CONTEXT/SpringIKConstraint/Transfer motion to skeleton", true)]
        public static bool TransferMotionValidate(MenuCommand command) =>
            BakeUtils.TransferMotionValidate(command.context as SpringIKConstraint);

        static Transform FindOrCreateChild(Component parent, string suffix, Vector3 pos, Quaternion rot)
        {
            string name = parent.gameObject.name + suffix;
            var existing = parent.transform.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "SpringIK create " + suffix);
            Undo.SetTransformParent(go.transform, parent.transform, "SpringIK parent " + suffix);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = 0.1f * Vector3.one;
            return go.transform;
        }
    }

    /// <summary>
    /// Makes the constraint bakeable both ways: IK to FK curves and back.
    /// </summary>
    /// <remarks>The forward solve iterates, but the inverse is closed form: the target is the tip
    /// and each hint sits on its own bend plane, so baking round-trips.</remarks>
    [BakeParameters(typeof(SpringIKConstraint))]
    public class SpringIKConstraintBakeParameters : BakeParameters<SpringIKConstraint>
    {
        /// <inheritdoc />
        public override bool canBakeToSkeleton => true;
        /// <inheritdoc />
        public override bool canBakeToConstraint => true;

        /// <inheritdoc />
        public override IEnumerable<EditorCurveBinding> GetSourceCurveBindings(RigBuilder rigBuilder, SpringIKConstraint constraint)
        {
            var bindings = new List<EditorCurveBinding>();
            EditorCurveBindingUtils.CollectTRBindings(rigBuilder.transform, constraint.data.target, bindings);
            if (constraint.data.kneeHint != null)
                EditorCurveBindingUtils.CollectPositionBindings(rigBuilder.transform, constraint.data.kneeHint, bindings);
            if (constraint.data.hockHint != null)
                EditorCurveBindingUtils.CollectPositionBindings(rigBuilder.transform, constraint.data.hockHint, bindings);
            return bindings;
        }

        /// <inheritdoc />
        public override IEnumerable<EditorCurveBinding> GetConstrainedCurveBindings(RigBuilder rigBuilder, SpringIKConstraint constraint)
        {
            var bindings = new List<EditorCurveBinding>();
            var d = constraint.data;
            EditorCurveBindingUtils.CollectRotationBindings(rigBuilder.transform, d.root, bindings);
            EditorCurveBindingUtils.CollectRotationBindings(rigBuilder.transform, d.knee, bindings);
            EditorCurveBindingUtils.CollectRotationBindings(rigBuilder.transform, d.hock, bindings);
            EditorCurveBindingUtils.CollectRotationBindings(rigBuilder.transform, d.tip, bindings);
            return bindings;
        }
    }
}
