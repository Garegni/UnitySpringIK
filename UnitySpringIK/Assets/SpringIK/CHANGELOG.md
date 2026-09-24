# Changelog

All notable changes to this package are documented here.

## [1.0.0] - 2026-09-23

First release. Needs Unity 6000.0.23f1 or newer; Animation Rigging and Burst install with it.

### Added
- `SpringIKConstraint`: an Animation Rigging constraint for three-bone legs that bend twice, the knee
  forward and the hock back. One Spring Angle Bias (-1..1) sets how the bend is shared between them,
  and the split holds as the leg compresses.
- `SpringIKMath`: closed-form reach limits and a fixed-cost solve (8 bisection + 4 Newton steps),
  deterministic and allocation-free. It runs inside the constraint's Burst-compiled animation job.
- Knee and hock hints with independent weights.
- Inspector with a live reach readout, Auto Setup from Tip Transform, and motion transfer between
  the constraint and the skeleton.
- 13 EditMode tests for the solver.

[1.0.0]: https://github.com/Garegni/UnitySpringIK/releases/tag/v1.0.0
