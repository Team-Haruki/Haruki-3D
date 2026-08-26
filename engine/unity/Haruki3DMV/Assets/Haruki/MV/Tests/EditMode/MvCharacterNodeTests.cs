using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvCharacterNodeTests
    {
        [Test]
        public void AttachSkinnedPartUsesOfficialNeckHeadGraft()
        {
            var body = CreateBody(
                out var bodyRenderer,
                out var bodyNeck,
                out var bodyHead);
            var bodyNeckParent = bodyNeck.parent;
            var bodyHeadChild = new GameObject("BodyHeadChild").transform;
            bodyHeadChild.SetParent(bodyHead, false);

            var face = CreateFace(
                out var faceRenderer,
                out var faceNeck,
                out var faceHead);
            var target = new GameObject("Look_target").transform;
            target.SetParent(faceNeck, false);

            try
            {
                MvCharacterNode.AttachSkinnedPart(body, face);

                Assert.That(bodyHeadChild.parent, Is.SameAs(faceHead));
                Assert.That(target.parent, Is.SameAs(bodyNeckParent));
                Assert.That(bodyRenderer.bones, Is.EqualTo(new[] { faceNeck, faceHead }));
                Assert.That(faceRenderer.rootBone, Is.SameAs(bodyRenderer.rootBone));
                Assert.That(faceRenderer.transform.parent, Is.SameAs(bodyRenderer.transform.parent));
                Assert.That(bodyNeck == null, Is.True);
                Assert.That(bodyHead == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(bodyRenderer.sharedMesh);
                Object.DestroyImmediate(faceRenderer.sharedMesh);
                Object.DestroyImmediate(body);
                if (face != null)
                {
                    Object.DestroyImmediate(face);
                }
            }
        }

        [Test]
        public void HeadOptionalMountUsesOfficialPartAndFallbackTransform()
        {
            var body = CreateBody(out var renderer, out _, out var head);
            var mount = new GameObject("a03").transform;
            mount.SetParent(head, false);
            var optional = new GameObject("optional");
            optional.transform.localPosition = Vector3.one;
            optional.transform.localEulerAngles = new Vector3(10f, 20f, 30f);
            optional.transform.localScale = Vector3.one * 2f;
            try
            {
                MvCharacterNode.AttachHeadOptional(body, optional, "a03");

                Assert.That(optional.transform.parent, Is.SameAs(mount));
                Assert.That(optional.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(optional.transform.localEulerAngles, Is.EqualTo(Vector3.zero));
                Assert.That(optional.transform.localScale, Is.EqualTo(Vector3.one * 2f));
            }
            finally
            {
                Object.DestroyImmediate(renderer.sharedMesh);
                Object.DestroyImmediate(body);
                if (optional != null) Object.DestroyImmediate(optional);
            }
        }

        [Test]
        public void CharacterAliasesUseNormalAndInsertTrackDomains()
        {
            var character = new GameObject("Character");
            var bindings = new Dictionary<string, Object>();
            try
            {
                Assert.That(
                    MvCharacterNode.CharacterTrackName(3, 5, false),
                    Is.EqualTo("Character3"));
                Assert.That(
                    MvCharacterNode.CharacterTrackName(5, 5, true),
                    Is.EqualTo("Character0_insert"));

                MvCharacterNode.BindCharacterAliases(bindings, "Character0_insert", character);
                MvCharacterNode.BindReflectionOffAll(bindings, character);

                Assert.That(bindings["Character0_insert"], Is.SameAs(character));
                Assert.That(bindings["Character0_insert_MV"], Is.SameAs(character));
                Assert.That(bindings["ReflectionOff_All"], Is.SameAs(character));
                Assert.That(
                    MvCharacterNode.HasCharacterTrack(bindings, "Character0_insert"),
                    Is.True);
                Assert.That(
                    MvCharacterNode.HasCharacterTrack(bindings, "Character1"),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void CharacterLayerIsAppliedToTheCompleteGraftedHierarchy()
        {
            var character = new GameObject("Character");
            var child = new GameObject("Face");
            child.transform.SetParent(character.transform, false);
            try
            {
                MvCharacterNode.SetLayerRecursively(
                    character,
                    MvRecoveredCameraResources.MainCharacterLayer);

                Assert.That(character.layer, Is.EqualTo(21));
                Assert.That(child.layer, Is.EqualTo(21));
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void GenderMotionUsesFormationIndexAndMasterFigure()
        {
            var info = new Sekai.Core.MusicVideoCharacterInfo
            {
                motionInfo = new Sekai.Core.MusicVideoMotionInfo
                {
                    motionType = Sekai.Core.MotionType.Gender,
                },
            };

            Assert.That(
                MvCharacterNode.ResolveCharacterKey(
                    info,
                    new MvCharacterLoadSpec { isFigureMan = true },
                    3,
                    5),
                Is.EqualTo("Character3_Male"));
            Assert.That(
                MvCharacterNode.ResolveCharacterKey(
                    info,
                    new MvCharacterLoadSpec { isFigureMan = false },
                    3,
                    5),
                Is.EqualTo("Character3_Female"));
        }

        [Test]
        public void AuxiliaryBindingsUseRecoveredInsertSuffixDomains()
        {
            var character = new GameObject("Character");
            var eye = character.AddComponent<MvWaterEyeState>();
            var bindings = new Dictionary<string, Object>
            {
                ["Character0_MeshOff_insert"] = null,
                ["Character0_ReflectionOff_insert"] = null,
                ["Character0_HeelOffsetOff_insert"] = null,
                ["Character0_DrawCameraSelect_insert"] = null,
                ["Water Eye Track 0_insert"] = null,
                ["Eye Flipbook Track 0_insert"] = null,
                ["Spring Bone Slow Track 0#insert"] = null,
                ["Spring Bone Control Track 0#insert"] = null,
            };
            try
            {
                MvCharacterNode.BindCharacterAuxiliaryTracks(
                    bindings, character, eye, 0, true);

                Assert.That(bindings["Water Eye Track 0_insert"], Is.SameAs(eye));
                Assert.That(bindings["Spring Bone Slow Track 0#insert"], Is.SameAs(character));
                Assert.That(bindings["Character0_MeshOff_insert"], Is.SameAs(character));
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [TestCase("tex_bdy_05_C", "_MainTex")]
        [TestCase("tex_bdy_05_S", "_ShadowTex")]
        [TestCase("tex_bdy_05_H", "_ValueTex")]
        [TestCase("tex_bdy_05_N", null)]
        public void ColorVariationUsesTheOfficialCshTextureRoles(
            string textureName,
            string expectedProperty)
        {
            Assert.That(
                MvCharacterNode.ColorVariationProperty(textureName),
                Is.EqualTo(expectedProperty));
        }

        [Test]
        public void MusicItemBindingsUseFormationPartAndInsertDomains()
        {
            var root = new GameObject("MusicItem");
            var model = root.AddComponent<Sekai.Core.MusicItemModel>();
            var bindings = new Dictionary<string, Object>
            {
                ["MusicItem2_0"] = null,
                ["MusicItem2_1_Opacity_insert"] = null,
                ["MusicItem2_1_UvScroll_insert"] = null,
            };
            try
            {
                MvCharacterNode.BindMusicItemTracks(
                    bindings, root, model, 2, 1, true);

                Assert.That(bindings["MusicItem2_0"], Is.Null);
                Assert.That(bindings["MusicItem2_1_Opacity_insert"], Is.SameAs(model));
                Assert.That(bindings["MusicItem2_1_UvScroll_insert"], Is.SameAs(model));

                MvCharacterNode.BindMusicItemTracks(
                    bindings, root, model, 2, 0, false);
                Assert.That(bindings["MusicItem2_0"], Is.SameAs(root));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateBody(
            out SkinnedMeshRenderer renderer,
            out Transform neck,
            out Transform head)
        {
            var root = new GameObject("body");
            var position = new GameObject("Position").transform;
            position.SetParent(root.transform, false);
            var hip = new GameObject("Hip").transform;
            hip.SetParent(position, false);
            neck = new GameObject("Neck").transform;
            neck.SetParent(hip, false);
            head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            var mesh = new GameObject("Body");
            mesh.transform.SetParent(root.transform, false);
            renderer = mesh.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = new Mesh();
            renderer.rootBone = position;
            renderer.bones = new[] { neck, head };
            return root;
        }

        private static GameObject CreateFace(
            out SkinnedMeshRenderer renderer,
            out Transform neck,
            out Transform head)
        {
            var root = new GameObject("face");
            var position = new GameObject("Position").transform;
            position.SetParent(root.transform, false);
            var hip = new GameObject("Hip").transform;
            hip.SetParent(position, false);
            neck = new GameObject("Neck").transform;
            neck.SetParent(hip, false);
            head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            var mesh = new GameObject("Face");
            mesh.transform.SetParent(root.transform, false);
            renderer = mesh.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = new Mesh();
            renderer.rootBone = neck;
            renderer.bones = new[] { neck, head };
            return root;
        }
    }
}
