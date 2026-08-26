using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sekai.Core
{
    [Serializable]
    public struct AccessoryTransform
    {
        public Vector3 pos;
        public Vector3 rot;
        public Vector3 scale;

        public AccessoryTransform(Vector3 pos, Vector3 rot, Vector3 scale)
        {
            this.pos = pos;
            this.rot = rot;
            this.scale = scale;
        }
    }

    [CreateAssetMenu(menuName = "CharacterAccessoryTransformData")]
    public sealed class CharacterAccessoryTransformData : ScriptableObject
    {
        [SerializeField]
        private SerializedDictionary<string, AccessoryTransform>
            _faceIdAccessoryTransformDict =
                new SerializedDictionary<string, AccessoryTransform>();

        public AccessoryTransform GetAccessoryTransformData(string faceId)
        {
            return !string.IsNullOrEmpty(faceId) &&
                _faceIdAccessoryTransformDict.TryGetValue(faceId, out var value)
                    ? value
                    : default;
        }

        public void SetTransformData(AccessoryTransform transformData, string faceId)
        {
            if (string.IsNullOrWhiteSpace(faceId))
            {
                throw new ArgumentException("Face id is required.", nameof(faceId));
            }
            _faceIdAccessoryTransformDict[faceId] = transformData;
        }
    }

    public sealed class CharacterAccessoryTransformController : MonoBehaviour
    {
        private GameObject _targetAccessoryObj;

        [SerializeField]
        private CharacterAccessoryTransformData _characterAccessoryTransformData;

        [SerializeField, HideInInspector]
        private string _faceId;

        public void ApplyCharacterAccessoryTransformData(string faceId)
        {
            _faceId = faceId;
            var data = _characterAccessoryTransformData == null
                ? default
                : _characterAccessoryTransformData.GetAccessoryTransformData(faceId);
            SetTransform(data.pos, data.rot, data.scale);
        }

        private void SetTransform(Vector3 pos, Vector3 rot, Vector3 scale)
        {
            var target = (_targetAccessoryObj != null
                ? _targetAccessoryObj
                : gameObject).transform;
            target.localPosition = pos;
            target.localRotation = Quaternion.Euler(rot);
            target.localScale = new Vector3(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
        }
    }
}
