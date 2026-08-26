using System.Reflection;
using NUnit.Framework;
using Sekai.Rendering;
using UnityEngine.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class SekaiCopyPassTests
    {
        [Test]
        public void ConstructorUsesTheRecoveredProfilerTag()
        {
            var pass = new SekaiCopyPass("Copy Sekai Buffer");

            Assert.That(GetProfilingSampler(pass).name, Is.EqualTo("Copy Sekai Buffer"));
        }

        [Test]
        public void SetupStoresTheExactSourceAndDestinationHandles()
        {
            var source = RTHandles.Alloc(8, 8, name: "SekaiCopySource");
            var destination = RTHandles.Alloc(8, 8, name: "SekaiCopyDestination");
            try
            {
                var pass = new SekaiCopyPass("Copy Sekai Buffer");
                pass.Setup(source, destination);

                Assert.That(GetHandle(pass, "m_Source"), Is.SameAs(source));
                Assert.That(GetHandle(pass, "m_Dest"), Is.SameAs(destination));
            }
            finally
            {
                source.Release();
                destination.Release();
            }
        }

        private static ProfilingSampler GetProfilingSampler(SekaiCopyPass pass)
        {
            var property = typeof(UnityEngine.Rendering.Universal.ScriptableRenderPass)
                .GetProperty(
                    "profilingSampler",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (ProfilingSampler)property.GetValue(pass);
        }

        private static RTHandle GetHandle(SekaiCopyPass pass, string fieldName)
        {
            var field = typeof(SekaiCopyPass).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (RTHandle)field.GetValue(pass);
        }
    }
}
