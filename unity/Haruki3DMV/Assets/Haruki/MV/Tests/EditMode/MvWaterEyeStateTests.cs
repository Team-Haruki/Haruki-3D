using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using UnityEngine;

namespace Haruki.MV.Tests
{
    public sealed class MvWaterEyeStateTests
    {
        [Test]
        public void EmptyPresetDisablesTheEyeMaterialOverride()
        {
            var host = new GameObject("Eye");
            var state = host.AddComponent<MvWaterEyeState>();
            var preset = ScriptableObject.CreateInstance<WaterEyePreset>();
            var table = ScriptableObject.CreateInstance<WaterEyePresetTable>();
            try
            {
                typeof(WaterEyePresetTable)
                    .GetField("items", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(table, new[]
                    {
                        new WaterEyePresetTable.TableItem
                        {
                            PresetId = "0001",
                            DisplayName = "Default",
                            Preset = preset,
                        },
                    });
                WaterEyePresetSettings.Setup(table);
                state.Enable("0001", "Default");
                Assert.That(state.IsEnabled, Is.True);
                Assert.That(state.PresetId, Is.EqualTo("0001"));

                state.Enable(string.Empty, "None");
                Assert.That(state.IsEnabled, Is.False);
                Assert.That(state.PresetId, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(preset);
            }
        }
    }
}
