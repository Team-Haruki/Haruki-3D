using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Haruki.MV
{
    public static class MvOfficialObjectBinding
    {
        private static readonly Regex StageTextName = new Regex(
            "TypeWriter[0-9]{1,}|TextOverwrite[0-9]{1,}",
            RegexOptions.CultureInvariant);

        public static void InitializePenlight(GameObject penlight)
        {
            if (penlight == null)
            {
                throw new ArgumentNullException(nameof(penlight));
            }
            Component parameter = null;
            foreach (var component in penlight.GetComponents<Component>())
            {
                if (component != null && HasTypeName(component.GetType(), "PenlightParameter"))
                {
                    parameter = component;
                    break;
                }
            }
            if (parameter == null)
            {
                return;
            }

            var initialize = parameter.GetType().GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (initialize == null)
            {
                throw new MissingMethodException(parameter.GetType().FullName, "Initialize");
            }
            initialize.Invoke(parameter, null);
        }

        public static void BindPenlightTransforms(
            GameObject penlight,
            IDictionary<string, UnityEngine.Object> bindings)
        {
            EnsureArguments(penlight, bindings);
            foreach (var target in penlight.GetComponentsInChildren<Transform>())
            {
                bindings[target.name] = target.gameObject;
            }
        }

        public static void BindControlGroups(
            GameObject stage,
            IDictionary<string, UnityEngine.Object> bindings)
        {
            EnsureArguments(stage, bindings);
            foreach (var group in FindComponents(stage, "ControlGroupBase", false))
            {
                bindings[group.name] = group;
            }
        }

        public static void BindStageDecorationTargets(
            GameObject decoration,
            IDictionary<string, UnityEngine.Object> bindings)
        {
            EnsureArguments(decoration, bindings);
            foreach (var text in FindComponents(decoration, "TextMeshPro", false))
            {
                if (StageTextName.IsMatch(text.name))
                {
                    bindings[text.gameObject.name] = text;
                }
            }

            var indexedTargets = FindComponents(
                decoration,
                "StageObjDrawCameraSelectController",
                true);
            for (var index = 0; index < indexedTargets.Count; index++)
            {
                bindings[$"StageObjDrawCameraSelectTrack{index}"] = indexedTargets[index];
            }
        }

        public static Component FindFirstComponent(
            GameObject root,
            string typeName,
            bool includeInactive)
        {
            var components = FindComponents(root, typeName, includeInactive);
            return components.Count == 0 ? null : components[0];
        }

        private static List<Component> FindComponents(
            GameObject root,
            string typeName,
            bool includeInactive)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("Component type name is required.", nameof(typeName));
            }

            var result = new List<Component>();
            foreach (var component in root.GetComponentsInChildren<Component>(includeInactive))
            {
                if (component != null && HasTypeName(component.GetType(), typeName))
                {
                    result.Add(component);
                }
            }
            return result;
        }

        private static bool HasTypeName(Type type, string typeName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.Name == typeName)
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureArguments(
            GameObject root,
            IDictionary<string, UnityEngine.Object> bindings)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }
        }
    }
}
