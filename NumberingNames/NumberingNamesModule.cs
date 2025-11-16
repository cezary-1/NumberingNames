using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace NumberingNames
{
    public class NumberingNamesModule : MBSubModuleBase 
    {
        private Harmony _harmony;
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            InformationManager.DisplayMessage(
                new InformationMessage("Numbering Names Mod loaded successfully."));
            // Create a Harmony instance with a unique ID
            _harmony = new Harmony("NumberingNames");
            // Tell Harmony to scan your assembly for [HarmonyPatch] classes
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
        }

        protected override void OnGameStart(Game game, IGameStarter starter)
        {
            base.OnGameStart(game, starter);
            if (game.GameType is Campaign)
            {
                ((CampaignGameStarter)starter).AddBehavior(new NumberingNamesBehavior());
            }
        }

    }
}
