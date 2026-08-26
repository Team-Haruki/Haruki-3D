using System;
using NUnit.Framework;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvMusicVideoModelTests
    {
        [Test]
        public void RegistryKeepsMainAndCutInNodeModelsSeparate()
        {
            var model = MvMusicVideoModel.Create(2);
            var mainTimeline = new MvTimelineModel(null);
            var childTimeline = new MvTimelineModel(null);
            var camera = new MvCameraModel(null, null, null, null);
            var light = new MvLightModel(null, null);
            var stage = new MvStageModel(null, Array.Empty<GameObject>());
            var character = new MvCharacterModel(
                Array.Empty<MvCharacterInstance>());
            var penlight = new MvPenlightModel(null);

            model.RegisterMainTimeline(mainTimeline);
            model.RegisterMainCamera(camera);
            model.RegisterMainLight(light);
            model.RegisterMainStage(stage);
            model.RegisterMainCharacter(character);
            model.RegisterMainPenlight(penlight);
            model.RegisterCutInTimeline(childTimeline, 1);

            Assert.That(model.Main.Timeline, Is.SameAs(mainTimeline));
            Assert.That(model.CutIns, Has.Length.EqualTo(2));
            Assert.That(model.CutIns[0].Timeline, Is.Null);
            Assert.That(model.CutIns[1].Timeline, Is.SameAs(childTimeline));
            Assert.That(model.Main.Camera, Is.SameAs(camera));
            Assert.That(model.Main.Light, Is.SameAs(light));
            Assert.That(model.Main.Stage, Is.SameAs(stage));
            Assert.That(model.Main.Character, Is.SameAs(character));
            Assert.That(model.Main.Penlight, Is.SameAs(penlight));
        }

        [Test]
        public void RegistryRejectsAnOutOfRangeCutInOrder()
        {
            var model = MvMusicVideoModel.Create(1);

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                model.RegisterCutInTimeline(new MvTimelineModel(null), 1));
        }
    }
}
