using System;
using UnityEngine;

namespace Sekai.Core
{
    public sealed class CharacterAdjuster : MonoBehaviour
    {
        public sealed class CharacterAdjustData
        {
            public CharacterAdjustData(int characterId, float characterHeight)
            {
                CharacterId = characterId;
                CharacterHeight = characterHeight;
            }

            public int CharacterId { get; }
            public float CharacterHeight { get; }
        }

        [SerializeField]
        private int _formationId;

        public int FormationId { get => _formationId; set => _formationId = value; }

        public void Adjust(CharacterAdjustData[] characterAdjustDatas)
        {
            if (characterAdjustDatas == null)
            {
                throw new ArgumentNullException(nameof(characterAdjustDatas));
            }
            if (_formationId < 0 || _formationId >= characterAdjustDatas.Length ||
                characterAdjustDatas[_formationId] == null)
            {
                throw new InvalidOperationException(
                    $"Stage CharacterAdjuster formation {_formationId} has no character data.");
            }
            transform.localScale = Vector3.one *
                (characterAdjustDatas[_formationId].CharacterHeight * 0.01f);
        }

        public static void AdjustGameObjects(
            GameObject gameObjectRoot,
            CharacterAdjustData[] characterAdjustDatas,
            bool includeInactive = true)
        {
            if (gameObjectRoot == null)
            {
                throw new ArgumentNullException(nameof(gameObjectRoot));
            }
            foreach (var adjuster in gameObjectRoot.GetComponentsInChildren<CharacterAdjuster>(
                includeInactive))
            {
                adjuster.Adjust(characterAdjustDatas);
            }
        }
    }
}
