import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const unityRoot = path.join(repoRoot, "unity", "Haruki3DMV");

test("repository contains a Unity 2022.3 WebGL MV project", () => {
  assert.match(
    fs.readFileSync(path.join(unityRoot, "ProjectSettings", "ProjectVersion.txt"), "utf8"),
    /^m_EditorVersion: 2022\.3\.62f2$/m
  );

  const packages = JSON.parse(
    fs.readFileSync(path.join(unityRoot, "Packages", "manifest.json"), "utf8")
  );
  assert.equal(packages.dependencies["com.unity.timeline"], "1.7.6");
  assert.match(packages.dependencies["com.unity.render-pipelines.universal"], /^14\./);

  for (const relativePath of [
    "Assets/Haruki/MV/Runtime/Haruki.MV.Runtime.asmdef",
    "Assets/Haruki/MV/Runtime/IMvPlaybackParticipant.cs",
    "Assets/Haruki/MV/Runtime/MvPlaybackCoordinator.cs",
    "Assets/Haruki/MV/Runtime/MvTimelineBinding.cs",
    "Assets/Haruki/MV/Runtime/MvTimelineNode.cs",
    "Assets/Haruki/MV/Runtime/MvTimelinePlaybackParticipant.cs",
    "Assets/Haruki/MV/Runtime/MvTimelineLifecycle.cs",
    "Assets/Haruki/MV/Runtime/MvMotionSequence.cs",
    "Assets/Haruki/MV/Runtime/MvCharacterNode.cs",
    "Assets/Haruki/MV/Runtime/MvStageNode.cs",
    "Assets/Haruki/MV/Runtime/MvCameraAdjustment.cs",
    "Assets/Haruki/MV/Runtime/MvRecoveredRendererContract.cs",
    "Assets/Haruki/MV/Runtime/SekaiCharacterOutlineFeature.cs",
    "Assets/Haruki/MV/Runtime/MvPlayerAssembler.cs",
    "Assets/Haruki/MV/Runtime/MvOfficialRuntimeData.cs",
    "Assets/Haruki/MV/Runtime/MvPlayerRenderSettings.cs",
    "Assets/Haruki/MV/Runtime/MvSceneBundleLoader.cs",
    "Assets/Haruki/MV/Runtime/MvBundleSetLoader.cs",
    "Assets/Haruki/MV/Runtime/HarukiMvBridge.cs",
    "Assets/Haruki/MV/Plugins/WebGL/HarukiMvBridge.jslib",
    "Assets/Haruki/MV/Editor/BuildWebGL.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvPlaybackCoordinatorTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvTimelineNodeTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvMotionSequenceTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvCharacterNodeTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvCameraAdjustmentTests.cs",
    "Assets/Haruki/MV/Tests/EditMode/MvOfficialRuntimeDataTests.cs",
  ]) {
    assert.equal(fs.existsSync(path.join(unityRoot, relativePath)), true, relativePath);
  }

  const build = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Editor/BuildWebGL.cs"),
    "utf8"
  );
  assert.match(build, /entry\.kind\?\.StartsWith\("timeline_"/);
  assert.match(build, /RemapRecoveredScriptGuids/);
  assert.match(build, /var recoveredScriptsRoot = Path\.Combine\(recoveredAssets, "Scripts"\)/);
  assert.match(build, /"LookAtAxis"/);
  const characterNodeHairSetup = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Runtime/MvCharacterNode.cs"),
    "utf8"
  );
  assert.match(characterNodeHairSetup, /hair\.Setup\(head, info\.useHairShadow\)/);
  assert.match(build, /ValidateRecoveredRuntimePrerequisites/);
  assert.match(build, /PreserveProjectShaders/);
  assert.match(build, /m_AlwaysIncludedShaders/);
  assert.match(build, /Assets\/Haruki\/MV\/Shaders/);
  assert.match(build, /DummyShaderTextExporter/);
  assert.match(build, /HARUKI_MV_ALLOW_DUMMY_SHADERS/);
  assert.match(build, /AllowDummyShadersForDevelopment/);
  assert.match(build, /The development player keeps the inactive pass disabled/);
  assert.match(build, /StringComparison\.Ordinal/);
  assert.match(build, /GraphicsSettings\.defaultRenderPipeline/);
  assert.match(build, /MvRecoveredCameraResources\.Create/);
  assert.match(build, /MvRecoveredRendererContract\.Validate/);
  assert.match(build, /m_RenderScale/);
  assert.match(build, /m_MSAA/);
  assert.match(build, /settings\.outlineWidthMin/);
  assert.match(build, /settings\.fovCurve/);
  assert.match(build, /CreateOutlineFovCurve/);
  assert.match(build, /CurveMatches/);
  assert.match(build, /"SekaiCharacterOutlineFeature",/);
  assert.match(build, /_planarReflectionInfo\.width/);
  assert.match(build, /_drawStencilShader/);
  assert.match(build, /ApplyDistortionShader/);
  assert.match(build, /stageInfo\?\.enablePlanarReflection == true/);
  assert.match(build, /stageInfo\?\.enableEffectDistortion == true/);
  assert.match(build, /if \(!requiresPlanarReflection\)/);
  assert.match(build, /if \(requiresEffectDistortion &&/);
  assert.match(build, /live_pv\/model\/mesh_flare_para\/common/);
  assert.match(build, /html = html\.Replace\(exposure, string\.Empty\)/);
  assert.match(build, /config\.devicePixelRatio = 1/);
  assert.match(build, /config\.matchWebGLToCanvasSize = false/);
  assert.match(build, /width=960 height=540/);
  assert.match(build, /canvas\.style\.height = \\"540px\\"/);

  const browserSmoke = fs.readFileSync(
    path.join(repoRoot, "scripts", "smoke-unity-mv.mjs"),
    "utf8"
  );
  assert.match(browserSmoke, /HARUKI_MV_ALLOW_RENDER_ERRORS/);
  assert.match(browserSmoke, /GL_INVALID_OPERATION/);
  assert.match(browserSmoke, /doesn't have a float or range property/);
  assert.match(browserSmoke, /rendererErrors/);
  assert.match(browserSmoke, /development bypass accepted known non-publishable errors/);

  const outlineFeature = fs.readFileSync(
    path.join(
      unityRoot,
      "Assets/Haruki/MV/Runtime/SekaiCharacterOutlineFeature.cs"
    ),
    "utf8"
  );
  assert.match(outlineFeature, /class SekaiCharacterOutlineFeature : ScriptableRendererFeature/);
  assert.match(outlineFeature, /Shader\.SetGlobalVector\(OutlineWidthId/);
  assert.match(outlineFeature, /Shader\.SetGlobalVector\(OutlineFactorId/);
  assert.match(build, /addressableNames = new\[\] \{ "timeline" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "decoration" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "penlight" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "body" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "face" \}/);
  assert.match(build, /addressableNames = new\[\] \{ "head_optional" \}/);
  assert.match(build, /deps = entry\.dependencies/);
  assert.doesNotMatch(build, /new GameObject\("Main Camera"\)/);
  assert.doesNotMatch(build, /new GameObject\("Preview Light"\)/);
  assert.doesNotMatch(build, /fieldOfView\s*=/);

  const mvData = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Runtime/MusicVideoData.cs"),
    "utf8"
  );
  assert.match(mvData, /class MusicVideoData : ScriptableObject/);
  assert.match(mvData, /enum MotionType/);
  assert.match(mvData, /public int id;\s*public bool useNonDefaultShader;/);

  const characterNode = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Runtime/MvCharacterNode.cs"),
    "utf8"
  );
  assert.match(characterNode, /renderer => string\.Equals\(renderer\.name, "Face"/);
  assert.match(characterNode, /MoveImmediateChildren\(bodyHead, faceHead\)/);
  assert.match(characterNode, /EndsWith\("_target", StringComparison\.Ordinal\)/);
  assert.match(characterNode, /A head_optional bundle requires its official MasterCostume3DModel\.part mount/);
  assert.match(characterNode, /Character\{formationIndex\}_\{\(spec\.isFigureMan \? "Male" : "Female"\)\}/);

  const cameraNode = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Runtime/MvCameraNode.cs"),
    "utf8"
  );
  assert.match(cameraNode, /CameraDecorationBundleName\(mvData\.id\)/);
  assert.match(cameraNode, /BindCameraDecorationTargets/);

  const bundleLoader = fs.readFileSync(
    path.join(unityRoot, "Assets/Haruki/MV/Runtime/MvBundleSetLoader.cs"),
    "utf8"
  );
  assert.doesNotMatch(bundleLoader, /frameCamera|FrameWithMainCamera/);
  assert.doesNotMatch(bundleLoader, /Camera\.main|fieldOfView/);
});

test("Unity activation material cannot be tracked by accident", () => {
  const ignore = fs.readFileSync(path.join(repoRoot, ".gitignore"), "utf8");
  assert.match(ignore, /^\*\.alf$/m);
  assert.match(ignore, /^\*\.ulf$/m);
  assert.match(ignore, /^UnityEntitlementLicense\.xml$/m);

  const skippedDirectories = new Set([
    ".git", "Build", "Library", "Logs", "Temp", "TestResults",
    "UserSettings", "dist", "dist-consumer", "node_modules",
  ]);
  const activationFiles = [];
  const scan = (directory) => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      if (entry.isDirectory() && !skippedDirectories.has(entry.name)) {
        scan(path.join(directory, entry.name));
      } else if (
        entry.isFile()
        && /^(?:UnityEntitlementLicense\.xml|.+\.(?:alf|ulf))$/i.test(entry.name)
      ) {
        activationFiles.push(path.relative(repoRoot, path.join(directory, entry.name)));
      }
    }
  };
  scan(repoRoot);
  assert.deepEqual(activationFiles, []);
});

test("Unity build wrapper refuses to fake a build without an editor", () => {
  const script = fs.readFileSync(path.join(repoRoot, "scripts", "build-unity-mv.sh"), "utf8");
  assert.match(script, /UNITY_EDITOR/);
  assert.match(script, /-executeMethod Haruki\.MV\.Editor\.BuildWebGL\.PerformBuild/);
  assert.match(script, /license_pipe="LicenseClient-\$\{license_user\}-2022\.3\.62"/);
  assert.match(script, /editor_license_pipe="Unity-\$\{license_pipe\}"/);
  assert.match(script, /flock 9/);
  assert.match(script, /pgrep -f/);
  assert.match(script, /\^Shader error\|error CS\[0-9\]\+:\|not safe to publish/);
  assert.doesNotMatch(script, /Haruki-Unity-LicenseClient-\$\{license_nonce\/\/-\/\}/);
  assert.doesNotMatch(script, /touch .*\.wasm|echo .*\.wasm/);
});
