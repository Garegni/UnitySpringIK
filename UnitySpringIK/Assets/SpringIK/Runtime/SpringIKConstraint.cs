using UnityEngine;
using UnityEngine.Animations;          // NotKeyable: implicit inside the Rigging package, must be imported here
using UnityEngine.Animations.Rigging;

namespace SpringIK
{
    /// <summary>Serialized data for <see cref="SpringIKConstraint"/>.</summary>
    [System.Serializable]
    public struct SpringIKConstraintData : IAnimationJobData, ISpringIKConstraintData
    {
        [SerializeField] Transform m_Root;
        [SerializeField] Transform m_Knee;
        [SerializeField] Transform m_Hock;
        [SerializeField] Transform m_Tip;

        [SyncSceneToStream, SerializeField] Transform m_Target;
        [SyncSceneToStream, SerializeField] Transform m_KneeHint;
        [SyncSceneToStream, SerializeField] Transform m_HockHint;

        // Signed, not 0..1, so the serialized default of 0 reads as neutral and the range stays symmetric.
        [SyncSceneToStream, SerializeField, Range(-1f, 1f)] float m_SpringAngleBias;

        [SyncSceneToStream, SerializeField, Range(0f, 1f)] float m_TargetPositionWeight;
        [SyncSceneToStream, SerializeField, Range(0f, 1f)] float m_TargetRotationWeight;
        [SyncSceneToStream, SerializeField, Range(0f, 1f)] float m_KneeHintWeight;
        [SyncSceneToStream, SerializeField, Range(0f, 1f)] float m_HockHintWeight;

        [NotKeyable, SerializeField] bool m_MaintainTargetPositionOffset;
        [NotKeyable, SerializeField] bool m_MaintainTargetRotationOffset;

        /// <inheritdoc />
        public Transform root { get => m_Root; set => m_Root = value; }
        /// <inheritdoc />
        public Transform knee { get => m_Knee; set => m_Knee = value; }
        /// <inheritdoc />
        public Transform hock { get => m_Hock; set => m_Hock = value; }
        /// <inheritdoc />
        public Transform tip { get => m_Tip; set => m_Tip = value; }
        /// <inheritdoc />
        public Transform target { get => m_Target; set => m_Target = value; }
        /// <inheritdoc />
        public Transform kneeHint { get => m_KneeHint; set => m_KneeHint = value; }
        /// <inheritdoc />
        public Transform hockHint { get => m_HockHint; set => m_HockHint = value; }

        /// <summary>Where the bend concentrates. -1 = at the knee, 0 = even, +1 = at the hock.</summary>
        public float springAngleBias { get => m_SpringAngleBias; set => m_SpringAngleBias = Mathf.Clamp(value, -1f, 1f); }
        /// <summary>How much the target's position drives the solve.</summary>
        public float targetPositionWeight { get => m_TargetPositionWeight; set => m_TargetPositionWeight = Mathf.Clamp01(value); }
        /// <summary>How much the target's rotation drives the foot.</summary>
        public float targetRotationWeight { get => m_TargetRotationWeight; set => m_TargetRotationWeight = Mathf.Clamp01(value); }
        /// <summary>How much the knee hint steers the bend plane.</summary>
        public float kneeHintWeight { get => m_KneeHintWeight; set => m_KneeHintWeight = Mathf.Clamp01(value); }
        /// <summary>How much the hock hint steers the bend plane.</summary>
        public float hockHintWeight { get => m_HockHintWeight; set => m_HockHintWeight = Mathf.Clamp01(value); }

        /// <inheritdoc />
        public bool maintainTargetPositionOffset { get => m_MaintainTargetPositionOffset; set => m_MaintainTargetPositionOffset = value; }
        /// <inheritdoc />
        public bool maintainTargetRotationOffset { get => m_MaintainTargetRotationOffset; set => m_MaintainTargetRotationOffset = value; }

        string ISpringIKConstraintData.springAngleBiasFloatProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_SpringAngleBias));
        string ISpringIKConstraintData.targetPositionWeightFloatProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_TargetPositionWeight));
        string ISpringIKConstraintData.targetRotationWeightFloatProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_TargetRotationWeight));
        string ISpringIKConstraintData.kneeHintWeightFloatProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_KneeHintWeight));
        string ISpringIKConstraintData.hockHintWeightFloatProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_HockHintWeight));

        // Returning false removes the constraint from the rig layer with no error; assert the whole chain.
        bool IAnimationJobData.IsValid() =>
            m_Root != null && m_Knee != null && m_Hock != null && m_Tip != null && m_Target != null &&
            m_Tip.IsChildOf(m_Hock) && m_Hock.IsChildOf(m_Knee) && m_Knee.IsChildOf(m_Root);

        void IAnimationJobData.SetDefaultValues()
        {
            m_Root = null; m_Knee = null; m_Hock = null; m_Tip = null;
            m_Target = null; m_KneeHint = null; m_HockHint = null;
            m_SpringAngleBias = 0f;
            m_TargetPositionWeight = 1f;
            m_TargetRotationWeight = 1f;
            m_KneeHintWeight = 1f;
            m_HockHintWeight = 1f;
            m_MaintainTargetPositionOffset = false;
            m_MaintainTargetRotationOffset = false;
        }
    }

    /// <summary>
    /// Three-bone IK constraint: the chain folds with a controllable bend distribution and a
    /// separate hint per bend, holding the knee:hock angle ratio constant across compression.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Animation Rigging/Spring IK Constraint")]
    public class SpringIKConstraint : RigConstraint<
        SpringIKConstraintJob,
        SpringIKConstraintData,
        SpringIKConstraintJobBinder<SpringIKConstraintData>
        >
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            // [Range] only styles the inspector slider; script-set values still need clamping.
            m_Data.springAngleBias = Mathf.Clamp(m_Data.springAngleBias, -1f, 1f);
            m_Data.targetPositionWeight = Mathf.Clamp01(m_Data.targetPositionWeight);
            m_Data.targetRotationWeight = Mathf.Clamp01(m_Data.targetRotationWeight);
            m_Data.kneeHintWeight = Mathf.Clamp01(m_Data.kneeHintWeight);
            m_Data.hockHintWeight = Mathf.Clamp01(m_Data.hockHintWeight);
        }
    }
}
