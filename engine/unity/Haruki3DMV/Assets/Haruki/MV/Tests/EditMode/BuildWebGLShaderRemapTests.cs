using System.IO;
using System.Reflection;
using Haruki.MV.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Haruki.MV.Tests
{
    public sealed class BuildWebGLShaderRemapTests
    {
        private const string TestRoot = "Assets/Haruki/MV/Generated/ShaderRemapTest";
        private const string RecoveredGuid = "0123456789abcdef0123456789abcdef";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void ReplacesRecoveredDummyShaderGuidAndRemovesThePlaceholder()
        {
            var shaderPath = TestRoot + "/recovered.shader";
            var materialPath = TestRoot + "/material.mat";
            Directory.CreateDirectory(TestRoot);
            File.WriteAllText(
                shaderPath,
                "Shader \"Hidden/Sekai/V2/UberPost\" { //DummyShaderTextExporter\nSubShader {} }");
            File.WriteAllText(
                shaderPath + ".meta",
                $"fileFormatVersion: 2\nguid: {RecoveredGuid}\nShaderImporter:\n  externalObjects: {{}}\n");
            File.WriteAllText(
                materialPath,
                $"m_Shader: {{fileID: 4800000, guid: {RecoveredGuid}, type: 3}}\n");

            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredShaderGuids",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { TestRoot, TestRoot });

            var replacementGuid = AssetDatabase.AssetPathToGUID(
                "Assets/Haruki/MV/Shaders/Hidden_Sekai_V2_UberPost.shader");
            Assert.That(File.ReadAllText(materialPath), Does.Contain(replacementGuid));
            Assert.That(File.ReadAllText(materialPath), Does.Not.Contain(RecoveredGuid));
            Assert.That(File.Exists(shaderPath), Is.False);
            Assert.That(File.Exists(shaderPath + ".meta"), Is.False);
        }

        [TestCase("material.path_0x686C589_loqslIL", "material._MainTex_ST.x")]
        [TestCase("material.path_0x3021EAFC_tTthhWH", "material._ColorTex_ST.w")]
        [TestCase("material.path_0x2AE5C260_vNNuVPJ", "material._SubTex_ST.z")]
        [TestCase("material.path_0x7DAF8B71_hLQmWoN", "material._Color.a")]
        public void RestoresRecoveredMaterialAnimationVectorComponents(
            string recovered,
            string expected)
        {
            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredMaterialAnimationProperties",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var remapped = (string)method.Invoke(null, new object[] { $"attribute: {recovered}" });

            Assert.That(remapped, Is.EqualTo($"attribute: {expected}"));
        }

        [Test]
        public void PreservesUnknownRecoveredMaterialAnimationProperties()
        {
            const string recovered = "attribute: material.path_0x12345678_unknown";
            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredMaterialAnimationProperties",
                BindingFlags.NonPublic | BindingFlags.Static);

            var remapped = (string)method.Invoke(null, new object[] { recovered });

            Assert.That(remapped, Is.EqualTo(recovered));
        }

        [TestCase("script_0x3738F1E6_rJPntKJ", "fogColor.r")]
        [TestCase("script_0x150CCE81_sPUTwqL", "fogEnd")]
        [TestCase("script_0xE9B2B892_ulhTsTL", "intensity")]
        [TestCase("script_0xBC8CF78A_nVwTVoL", "ambientColor.r")]
        public void RestoresRecoveredScriptAnimationPropertiesWithoutNoiseSuffix(
            string recovered,
            string expected)
        {
            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredScriptAnimationProperties",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var remapped = (string)method.Invoke(null, new object[] { $"attribute: {recovered}" });

            Assert.That(remapped, Is.EqualTo($"attribute: {expected}"));
        }

        [Test]
        public void PreservesUnknownRecoveredScriptAnimationProperties()
        {
            const string recovered = "attribute: script_0x12345678_unknown";
            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredScriptAnimationProperties",
                BindingFlags.NonPublic | BindingFlags.Static);

            var remapped = (string)method.Invoke(null, new object[] { recovered });

            Assert.That(remapped, Is.EqualTo(recovered));
        }

        [TestCase("path: path_0xC647CF32_JWkttsH", "path: mainCam")]
        [TestCase("path: path_0x4F5CF102_hTJRkiJ", "path: mainCam/Camera")]
        [TestCase("path: path_0xC3BD6806_pKPwvmJ", "path: mainCam/CamParam")]
        [TestCase("path: path_0xA150FA61_tVjUTqN", "path: subCam")]
        [TestCase("path: path_0x738D4D1_UNrHoiJ", "path: subCam/target")]
        [TestCase("path: path_0x7DE7101E_HniqUJN", "path: subCam/Camera")]
        [TestCase("path: path_0xF2C7D50E_LjIqsjJ", "path: subCam/CamParam")]
        public void RestoresRecoveredCameraAnimationPaths(
            string recovered,
            string expected)
        {
            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredAnimationPaths",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var remapped = (string)method.Invoke(null, new object[] { recovered });

            Assert.That(remapped, Is.EqualTo(expected));
        }

        [Test]
        public void PreservesUnknownRecoveredAnimationPaths()
        {
            const string recovered = "path: path_0x12345678_unknown";
            var method = typeof(BuildWebGL).GetMethod(
                "RemapRecoveredAnimationPaths",
                BindingFlags.NonPublic | BindingFlags.Static);

            var remapped = (string)method.Invoke(null, new object[] { recovered });

            Assert.That(remapped, Is.EqualTo(recovered));
        }
    }
}
