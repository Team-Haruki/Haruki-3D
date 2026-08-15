using NUnit.Framework;
using Sekai.Rendering;

namespace Haruki.MV.Tests
{
    public sealed class SekaiCharacterReflectionOffSettingsTests
    {
        private sealed class ReflectionOff : ISekaiCharacterReflectionOff
        {
            public bool IsReflectionHiding { get; set; }

            public int FormationId { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            SekaiCharacterReflectionOffSettings.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SekaiCharacterReflectionOffSettings.Clear();
        }

        [Test]
        public void ReflectionExistsOnlyWhenEveryFormationMemberIsHidden()
        {
            var first = new ReflectionOff { IsReflectionHiding = true, FormationId = 0 };
            var second = new ReflectionOff { IsReflectionHiding = true, FormationId = 1 };
            SekaiCharacterReflectionOffSettings.SetMemberNum(2);

            SekaiCharacterReflectionOffSettings.RegisterCharacterReflectionOff(first);
            SekaiCharacterReflectionOffSettings.RegisterCharacterReflectionOff(first);
            Assert.That(
                SekaiCharacterReflectionOffSettings.ExistCharacterReflection(),
                Is.False);

            SekaiCharacterReflectionOffSettings.RegisterCharacterReflectionOff(second);
            Assert.That(
                SekaiCharacterReflectionOffSettings.ExistCharacterReflection(),
                Is.True);

            SekaiCharacterReflectionOffSettings.UnregisterCharacterReflectionOff(first);
            Assert.That(
                SekaiCharacterReflectionOffSettings.ExistCharacterReflection(),
                Is.False);
        }

        [Test]
        public void VisibleMembersAreNotRegisteredAndGlobalHideIsIndependent()
        {
            SekaiCharacterReflectionOffSettings.SetMemberNum(1);
            SekaiCharacterReflectionOffSettings.RegisterCharacterReflectionOff(
                new ReflectionOff { IsReflectionHiding = false, FormationId = 0 });
            Assert.That(
                SekaiCharacterReflectionOffSettings.ExistCharacterReflection(),
                Is.False);

            SekaiCharacterReflectionOffSettings.SetIsHidingAll(true);
            Assert.That(SekaiCharacterReflectionOffSettings.IsHidingAll, Is.True);
            Assert.That(
                SekaiCharacterReflectionOffSettings.ExistCharacterReflection(),
                Is.False);
        }

        [Test]
        public void ZeroMembersMatchesTheRecoveredCountEquality()
        {
            Assert.That(
                SekaiCharacterReflectionOffSettings.ExistCharacterReflection(),
                Is.True);
        }
    }
}
