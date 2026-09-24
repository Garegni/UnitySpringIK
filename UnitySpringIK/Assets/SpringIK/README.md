# Spring IK

Three-bone IK for digitigrade legs, as an Animation Rigging constraint.

**Documentation, a rigging walkthrough and a demo:** https://github.com/Garegni/UnitySpringIK#readme

- Needs Unity 6000.0.23f1 or newer. Animation Rigging and Burst install with it.
- `Runtime/` is the constraint and its solver (`PixelCyto.SpringIK`), `Editor/` the Inspector
  (`PixelCyto.SpringIK.Editor`) and `Tests/Editor/` the EditMode tests.
- To run the tests in a project that installed this package, add
  `"testables": ["com.pixelcyto.springik"]` to that project's `Packages/manifest.json`.

MIT licensed, see [LICENSE.md](LICENSE.md). Made by José Ignacio Alonso Kuri · PixelCyto.
