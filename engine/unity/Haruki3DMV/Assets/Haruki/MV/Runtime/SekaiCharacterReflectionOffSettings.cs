using System.Collections.Generic;

namespace Sekai.Rendering
{
    public interface ISekaiCharacterReflectionOff
    {
        bool IsReflectionHiding { get; }

        int FormationId { get; }
    }

    /// <summary>
    /// Recovered global registry used to suppress the character-reflection
    /// draw passes when all formation members are hidden by Timeline clips.
    /// </summary>
    public static class SekaiCharacterReflectionOffSettings
    {
        private static readonly List<ISekaiCharacterReflectionOff>
            CurrentCharacterReflectionOffs = new List<ISekaiCharacterReflectionOff>();

        private static int memberCount;
        private static bool isHidingAll;

        public static IReadOnlyList<ISekaiCharacterReflectionOff> CharacterReflectionOffs =>
            CurrentCharacterReflectionOffs;

        public static bool IsHidingAll => isHidingAll;

        public static void Clear()
        {
            isHidingAll = false;
            ClearCharacterReflection();
            ClearMemberNum();
        }

        public static void ClearCharacterReflection()
        {
            CurrentCharacterReflectionOffs.Clear();
        }

        public static void SetMemberNum(int num)
        {
            memberCount = num;
        }

        public static void ClearMemberNum()
        {
            memberCount = 0;
        }

        public static void SetIsHidingAll(bool useAnyClip)
        {
            isHidingAll = useAnyClip;
        }

        public static void RegisterCharacterReflectionOff(
            ISekaiCharacterReflectionOff characterReflectionOff)
        {
            if (characterReflectionOff == null ||
                !characterReflectionOff.IsReflectionHiding ||
                CurrentCharacterReflectionOffs.Contains(characterReflectionOff))
            {
                return;
            }

            CurrentCharacterReflectionOffs.Add(characterReflectionOff);
        }

        public static void UnregisterCharacterReflectionOff(
            ISekaiCharacterReflectionOff characterReflectionOff)
        {
            if (characterReflectionOff != null)
            {
                CurrentCharacterReflectionOffs.Remove(characterReflectionOff);
            }
        }

        public static bool ExistCharacterReflection()
        {
            return CurrentCharacterReflectionOffs.Count == memberCount;
        }
    }
}
