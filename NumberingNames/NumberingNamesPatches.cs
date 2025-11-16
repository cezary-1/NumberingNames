using HarmonyLib;
using Helpers;
using TaleWorlds.Localization;

namespace NumberingNames
{
    [HarmonyPatch(typeof(TextObject), nameof(TextObject.ToString))]
    public static class NumberingNamesPatches
    {

        [HarmonyPostfix]
        public static void ToString_Postfix(TextObject __instance, ref string __result)
        {
            // 1) Roman?
            if (NumberingNamesBehavior.NameSuffixMap.TryGetValue(__instance, out var suffix))
            {
                if (!__result.EndsWith(" " + suffix))
                    __result += " " + suffix;
            }

            // Surname?
            if (NumberingNamesBehavior.ClanMode)
            {
                if (NumberingNamesBehavior.SurnameMap.TryGetValue(__instance, out var surname))
                {
                    if (!string.IsNullOrEmpty(surname))
                    {
                        // Append clan if it's not already at the end
                        var clanSuffix = " " + surname;
                        if (!__result.EndsWith(clanSuffix))
                            __result += clanSuffix;
                    }
                }
            }
            else
            {
                if (NumberingNamesBehavior.SurnameOtherMap.TryGetValue(__instance, out var OtherSurname))
                {
                    if (!string.IsNullOrEmpty(OtherSurname))
                    {
                        // Append surname if it's not already at the end
                        var surnameSuffix = " " + OtherSurname;
                        if (!__result.EndsWith(surnameSuffix))
                            __result += surnameSuffix;
                    }
                }
            }


            // 2) Nickname?
            if (NumberingNamesBehavior.NicknameMap.TryGetValue(__instance, out var nick) && NumberingNamesBehavior.NameOwnerMap.TryGetValue(__instance, out var hero))
            {
                var nickTextObj = new TextObject("{=NN_" + nick + "}" + nick);
                StringHelpers.SetCharacterProperties("CHARACTER", hero.CharacterObject, nickTextObj, true);
                var nicknameStr = nickTextObj.ToString();
                if (!__result.EndsWith(" " + nicknameStr))
                    __result += " " + nicknameStr;
            }
            
        }
    }
}
