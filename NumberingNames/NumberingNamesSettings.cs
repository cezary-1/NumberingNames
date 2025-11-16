using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace NumberingNames
{
    public sealed class NumberingNamesSettings : AttributeGlobalSettings<NumberingNamesSettings>
    {
        public override string Id => "NumberingNames";
        public override string DisplayName => new TextObject("{=NN_TITLE}Numbering Names").ToString();
        public override string FolderName => "NumberingNames";
        public override string FormatType => "json";


        [SettingPropertyBool(
            "{=NN_OnlyFamily}Only Family",
            Order = 0, RequireRestart = false,
            HintText = "{=NN_OnlyFamily_H}If true, numbering happens between only family members(if false, only clan heroes). (Default: true)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public bool OnlyFamily { get; set; } = true;

        [SettingPropertyBool(
            "{=NN_OnlyCloseFamily}Only Close Family",
            Order = 1, RequireRestart = false,
            HintText = "{=NN_OnlyCloseFamily_H}When true, only include parents, grandparents, siblings and direct descendants. When false, also include aunts/uncles and cousins. Need OnlyFamily be true (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public bool OnlyCloseFamily { get; set; } = false;

        [SettingPropertyInteger(
            "{=NN_GenerationsUp}Generations Up",
            1, 50, "{VALUE}",
            Order = 2, RequireRestart = false,
            HintText = "{=NN_GenerationsUp_H}Generations Up for looking names to number. (Default: 2)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public int GenerationsUp { get; set; } = 2;

        [SettingPropertyInteger(
            "{=NN_GenerationsDown}Generations Down",
            1, 50, "{VALUE}",
            Order = 3, RequireRestart = false,
            HintText = "{=NN_GenerationsDown_H}Generations Down for looking names to number. (Default: 2)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public int GenerationsDown { get; set; } = 2;

        [SettingPropertyBool(
            "{=NN_EnableNicks}Enable Nicknames",
            Order = 4, RequireRestart = false,
            HintText = "{=NN_EnableNicks_H}If true, rulers will have nicknames. (Default: true)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public bool EnableNicks { get; set; } = true;

        [SettingPropertyBool(
            "{=NN_StatInfo}Show Settings",
            Order = 5, RequireRestart = false,
            HintText = "{=NN_StatInfo_H}If true, shows settings value at beginning. (Default: true)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public bool StatInfo { get; set; } = true;

        [SettingPropertyBool(
            "{=NN_Debug}Debug",
            Order = 6, RequireRestart = false,
            HintText = "{=NN_Debug_H}Enables in-game debug messages for troubleshooting (Not needed for you, just me). (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General")]
        public bool Debug { get; set; } = false;

        [SettingPropertyBool(
            "{=NN_Surnames}Surnames",
            Order = 0, RequireRestart = false,
            HintText = "{=NN_Surnames_H}If true, heroes will also have surnames. (Default: true)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool Surnames { get; set; } = true;

        [SettingPropertyBool(
            "{=NN_SurnamesMode}Surnames As Clan Names?",
            Order = 0, RequireRestart = false,
            HintText = "{=NN_SurnamesMode_H}If true, heroes will have surnames as their clan names. If false, surnames will be randomly chosen from culture names (and then saved and kept). (need save reload) (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesClanNames { get; set; } = false;

        [SettingPropertyBool(
            "{=NN_SurnamesRulers}Surnames For Kingdom Rulers",
            Order = 1, RequireRestart = false,
            HintText = "{=NN_SurnamesRulers_H}If true, kingdom rulers will also have surnames. (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesRulers { get; set; } = true;

        [SettingPropertyBool(
            "{=NN_SurnamesMinor}Surnames For Minor Factions",
            Order = 2, RequireRestart = false,
            HintText = "{=NN_SurnamesMinor_H}If true, minor faction members will also have surnames. (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesMinor { get; set; } = false;

        [SettingPropertyBool(
            "{=NN_SurnamesWanderer}Surnames For Wanderers",
            Order = 3, RequireRestart = false,
            HintText = "{=NN_SurnamesWanderer_H}If true, wanderers members will also have surnames. (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesWanderer { get; set; } = false;

        [SettingPropertyBool(
            "{=NN_SurnamesNotables}Surnames For Notables",
            Order = 4, RequireRestart = false,
            HintText = "{=NN_SurnamesNotables_H}If true, Notables will also have surnames. (Default: false)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesNotables { get; set; } = false;

        [SettingPropertyBool(
            "{=NN_SurnamesWife}Wife Change Surname",
            Order = 5, RequireRestart = false,
            HintText = "{=NN_SurnamesWife_H}If true, wifes will change surnames to husbands. (Default: true)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesWife { get; set; } = true;

        [SettingPropertyBool(
            "{=NN_SurnamesChangeMenu}Enable 'Change Surname' option in menu?",
            Order = 6, RequireRestart = false,
            HintText = "{=NN_SurnamesChangeMenu_H}Enable/Disable 'Change Surname' option in menu. (Default: true)")]
        [SettingPropertyGroup("{=MCM_GENERAL}General/{=MCM_SURNAMES}Surnames")]
        public bool SurnamesMenu { get; set; } = true;

    }
}
