using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace SpringIK
{
    /// <summary>
    /// Constraint job for a 3-bone chain folded by <see cref="SpringIKMath"/>, aimed at a target,
    /// with one hint per bend choosing the bend plane. Writes rotation deltas and stores no rest
    /// directions, so it composes with the animation already in the stream instead of replacing it.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public struct SpringIKConstraintJob : IWeightedAnimationJob
    {
        /// <summary>Hip. Driven.</summary>
        public ReadWriteTransformHandle root;
        /// <summary>Knee — the first bend. Driven.</summary>
        public ReadWriteTransformHandle knee;
        /// <summary>Hock — the second bend, opposed to the first. Driven.</summary>
        public ReadWriteTransformHandle hock;
        /// <summary>Foot. Driven (rotation only).</summary>
        public ReadWriteTransformHandle tip;

        /// <summary>Where the foot should go.</summary>
        public ReadOnlyTransformHandle target;
        /// <summary>Optional. Chooses the side the knee bends toward.</summary>
        public ReadOnlyTransformHandle kneeHint;
        /// <summary>Optional. Chooses the side the hock bends toward.</summary>
        public ReadOnlyTransformHandle hockHint;

        /// <summary>Applied to the target when maintainTargetPosition/RotationOffset is set.</summary>
        public AffineTransform targetOffset;

        public FloatProperty springAngleBias;
        public FloatProperty targetPositionWeight;
        public FloatProperty targetRotationWeight;
        public FloatProperty kneeHintWeight;
        public FloatProperty hockHintWeight;

        /// <inheritdoc />
        public FloatProperty jobWeight { get; set; }

        /// <inheritdoc />
        public void ProcessRootMotion(AnimationStream stream) { }

        /// <inheritdoc />
        public void ProcessAnimation(AnimationStream stream)
        {
            float w = jobWeight.Get(stream);
            if (w <= 0f) { PassThroughChain(stream); return; }

            target.GetGlobalTR(stream, out Vector3 targetPos, out Quaternion targetRot);

            bool hasKnee = kneeHint.IsValid(stream);
            bool hasHock = hockHint.IsValid(stream);
            Vector3 kneeHintPos = hasKnee ? kneeHint.GetPosition(stream) : Vector3.zero;
            Vector3 hockHintPos = hasHock ? hockHint.GetPosition(stream) : Vector3.zero;

            var pose = SpringIKPose.Solve(
                root.GetPosition(stream), knee.GetPosition(stream),
                hock.GetPosition(stream), tip.GetPosition(stream),
                root.GetRotation(stream), knee.GetRotation(stream),
                hock.GetRotation(stream), tip.GetRotation(stream),
                targetPos + targetOffset.translation, targetRot * targetOffset.rotation,
                hasKnee, kneeHintPos, hasHock, hockHintPos,
                springAngleBias.Get(stream),
                targetPositionWeight.Get(stream) * w,
                targetRotationWeight.Get(stream) * w,
                kneeHintWeight.Get(stream) * w,
                hockHintWeight.Get(stream) * w,
                out _);

            if (!pose.Valid) { PassThroughChain(stream); return; }

            // Absolute world rotations: assign parent-first, since writing a parent re-poses its children.
            root.SetRotation(stream, pose.Root);
            knee.SetRotation(stream, pose.Knee);
            hock.SetRotation(stream, pose.Hock);
            tip.SetRotation(stream, pose.Tip);
        }

        void PassThroughChain(AnimationStream stream)
        {
            // Required: without it, zero weight means "stopped writing" rather than a true no-op.
            AnimationRuntimeUtils.PassThrough(stream, root);
            AnimationRuntimeUtils.PassThrough(stream, knee);
            AnimationRuntimeUtils.PassThrough(stream, hock);
            AnimationRuntimeUtils.PassThrough(stream, tip);
        }
    }

    /// <summary>Data mapping for the SpringIK constraint.</summary>
    public interface ISpringIKConstraintData
    {
        /// <summary>Hip.</summary>
        Transform root { get; }
        /// <summary>Knee — must be a child of <see cref="root"/>.</summary>
        Transform knee { get; }
        /// <summary>Hock — must be a child of <see cref="knee"/>.</summary>
        Transform hock { get; }
        /// <summary>Foot — must be a child of <see cref="hock"/>.</summary>
        Transform tip { get; }

        /// <summary>The IK target.</summary>
        Transform target { get; }
        /// <summary>Optional hint for the knee's bend side.</summary>
        Transform kneeHint { get; }
        /// <summary>Optional hint for the hock's bend side.</summary>
        Transform hockHint { get; }

        /// <summary>Preserve the tip's position offset from the target at bind time.</summary>
        bool maintainTargetPositionOffset { get; }
        /// <summary>Preserve the tip's rotation offset from the target at bind time.</summary>
        bool maintainTargetRotationOffset { get; }

        /// <summary>Serialized path of the bias field.</summary>
        string springAngleBiasFloatProperty { get; }
        /// <summary>Serialized path of the target position weight.</summary>
        string targetPositionWeightFloatProperty { get; }
        /// <summary>Serialized path of the target rotation weight.</summary>
        string targetRotationWeightFloatProperty { get; }
        /// <summary>Serialized path of the knee hint weight.</summary>
        string kneeHintWeightFloatProperty { get; }
        /// <summary>Serialized path of the hock hint weight.</summary>
        string hockHintWeightFloatProperty { get; }
    }

    /// <summary>Binds <see cref="SpringIKConstraintJob"/> to a data struct.</summary>
    /// <typeparam name="T">The constraint data type.</typeparam>
    public class SpringIKConstraintJobBinder<T> : AnimationJobBinder<SpringIKConstraintJob, T>
        where T : struct, IAnimationJobData, ISpringIKConstraintData
    {
        /// <inheritdoc />
        public override SpringIKConstraintJob Create(Animator animator, ref T data, Component component)
        {
            var job = new SpringIKConstraintJob();

            job.root = ReadWriteTransformHandle.Bind(animator, data.root);
            job.knee = ReadWriteTransformHandle.Bind(animator, data.knee);
            job.hock = ReadWriteTransformHandle.Bind(animator, data.hock);
            job.tip = ReadWriteTransformHandle.Bind(animator, data.tip);
            job.target = ReadOnlyTransformHandle.Bind(animator, data.target);

            // Leaving a handle unbound is the "no hint" signal: it reports IsValid(stream) == false.
            if (data.kneeHint != null) job.kneeHint = ReadOnlyTransformHandle.Bind(animator, data.kneeHint);
            if (data.hockHint != null) job.hockHint = ReadOnlyTransformHandle.Bind(animator, data.hockHint);

            job.targetOffset = AffineTransform.identity;
            if (data.maintainTargetPositionOffset)
                job.targetOffset.translation = data.tip.position - data.target.position;
            if (data.maintainTargetRotationOffset)
                job.targetOffset.rotation = Quaternion.Inverse(data.target.rotation) * data.tip.rotation;

            job.springAngleBias = FloatProperty.Bind(animator, component, data.springAngleBiasFloatProperty);
            job.targetPositionWeight = FloatProperty.Bind(animator, component, data.targetPositionWeightFloatProperty);
            job.targetRotationWeight = FloatProperty.Bind(animator, component, data.targetRotationWeightFloatProperty);
            job.kneeHintWeight = FloatProperty.Bind(animator, component, data.kneeHintWeightFloatProperty);
            job.hockHintWeight = FloatProperty.Bind(animator, component, data.hockHintWeightFloatProperty);

            return job;
        }

        /// <inheritdoc />
        public override void Destroy(SpringIKConstraintJob job) { }
    }
}
