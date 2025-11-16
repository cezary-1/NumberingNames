using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.PerSave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace NumberingNames
{
    public sealed class NumberingNamesSave : AttributePerSaveSettings<NumberingNamesSave>
    {
        public override string Id => "NumberingNamesSave";
        public override string DisplayName => new TextObject("{=NN_SAVE_TITLE}Numbering Names Save").ToString();
        public override string FolderName => "NumberingNamesSave";

        [SettingPropertyButton(
           "{=NN_CLEAR_SAVE_KINGDOM}Clear All Ruler Data",
           Content = "{=NN_CLEAR_BTN}Clear",
           Order = 1, RequireRestart = false,
           HintText = "{=NN_CLEAR_SAVE_H}Wipe out kingdom history data.")]
        [SettingPropertyGroup("{=MCM_SAVE}Save")]
        public Action ClearSave
        {
            get;
            set;
        } = () =>
        {
            InformationManager.DisplayMessage(
                new InformationMessage("All kingdom history was wiped.", Colors.Green)
            );
            var bh = NumberingNamesBehavior.Instance;
            if (bh != null)
            {
                bh._kingdomRulers = new Dictionary<Kingdom, List<string>>();
                bh._historyEntries.Clear();
            }

        };

        

        [SettingPropertyButton(
           "{=NN_CLEAR_SAVE_SURNAME}Clear All Surname Data",
           Content = "{=NN_CLEAR_BTN}Clear",
           Order = 1, RequireRestart = false,
           HintText = "{=NN_CLEAR_SAVE_SURNAME_H}Wipe out surname history data.")]
        [SettingPropertyGroup("{=MCM_SAVE}Save")]
        public Action ClearSaveSurname
        {
            get;
            set;
        } = () =>
        {
            InformationManager.DisplayMessage(
                new InformationMessage("All surname history was wiped.", Colors.Green)
            );
            var bh = NumberingNamesBehavior.Instance;
            if (bh != null)
            {
                NumberingNamesBehavior.SurnameOtherMap = new ConcurrentDictionary<TextObject, string>();
                bh._surnameEntries.Clear();
            }

        };
        
    }
}
