using System;
using System.Collections.Generic;
using Sekai.Core;
using UnityEngine;

namespace Haruki.MV
{
    public sealed class MvPenlightNode : IDisposable
    {
        private readonly MvBundleSetLoader _bundles;
        private readonly IDictionary<string, UnityEngine.Object> _bindings;
        private readonly Transform _root;

        public MvPenlightNode(
            MvBundleSetLoader bundles,
            IDictionary<string, UnityEngine.Object> bindings,
            Transform root)
        {
            _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        }

        public GameObject Penlight { get; private set; }

        public void Load(MusicVideoPenlightInfo info)
        {
            if (info == null || info.id <= 0)
            {
                return;
            }

            Penlight = _bundles.InstantiatePrefab(
                new MvPrefabLoadRequest
                {
                    bundleName = MvOfficialRuntimeData.PenlightBundleName(info.id),
                    assetName = "penlight",
                },
                _root,
                null);
            MvOfficialObjectBinding.InitializePenlight(Penlight);
            MvOfficialObjectBinding.BindPenlightTransforms(Penlight, _bindings);
        }

        public void Dispose()
        {
            if (Penlight != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Penlight);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Penlight);
                }
            }
            Penlight = null;
        }
    }
}
