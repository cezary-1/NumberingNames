using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace NumberingNames
{
    // container for JSON persistence
    public class NicknamesEditorData
    {
        public List<NicknameEntry> Nicknames { get; set; } = new List<NicknameEntry>();
    }

    public class NicknameEntry
    {
        /// <summary>
        /// The name of the DefaultTraits field (e.g. "Honor", "Valor", etc.)
        /// </summary>
        public string Trait { get; set; }

        /// <summary>
        /// The minimum trait level for this suffix to be considered.
        /// </summary>
        public int Threshold { get; set; }

        /// <summary>
        /// The text to append (e.g. "the Great").
        /// </summary>
        public string Suffix { get; set; }

        public NicknameEntry() { }
        public NicknameEntry(string trait, int threshold, string suffix)
        {
            Trait = trait;
            Threshold = threshold;
            Suffix = suffix;
        }

        public override string ToString()
            => new TextObject("{=NN_" + Suffix + "}" + Suffix)
            .ToString();
    }

    public sealed class NumberingNamesEditor
        : AttributeGlobalSettings<NumberingNamesEditor>
    {
        public override string Id => "NumberingNamesEditor";
        public override string DisplayName => new TextObject("{=NN_EDITOR_TITLE}Numbering Names Editor").ToString();
        public override string FolderName => "NumberingNamesEditor";
        public override string FormatType => "json";

        private const string FILE_NAME = "editor_config.json";
        private readonly string _folder, _path;

        // in‑memory list
        public List<NicknameEntry> Entries { get; private set; }

        // ── ctor: load or init defaults ────────────────────────────
        public NumberingNamesEditor()
        {
            var asm = typeof(NumberingNamesModule).Assembly;
            _folder = Path.Combine(Path.GetDirectoryName(asm.Location), "editor");
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, FILE_NAME);

            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var data = JsonConvert.DeserializeObject<NicknamesEditorData>(json)
                           ?? new NicknamesEditorData();
                Entries = data.Nicknames;
            }
            else
            {
                // first‐run: prime with defaults
                Entries = new List<NicknameEntry>
    {
        new NicknameEntry("Honor",     1, "the Honorable"),
        new NicknameEntry("Honor",    2, "the Great"),
        new NicknameEntry("Honor",    -2, "the Damned"),
        new NicknameEntry("Honor",    -1, "the Inglorious"),

        new NicknameEntry("Valor",     1, "the Brave"),
        new NicknameEntry("Valor",     2, "the Valiant"),
        new NicknameEntry("Valor",    -2, "the Treacherous"),
        new NicknameEntry("Valor",    -1, "the Weak"),
    };
                Save();
            }

            RefreshDropdown();
        }

        private void Save()
        {
            var data = new NicknamesEditorData { Nicknames = Entries };
            File.WriteAllText(_path, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        // ── dropdown backing ───────────────────────────────────────
        private Dropdown<NicknameEntry> _selector;
        private NicknameEntry _lastSelected;

        private void RefreshDropdown()
        {
            var list = Entries.ToList();
            var old = _selector?.SelectedValue;
            var idx = list.IndexOf(old ?? list.FirstOrDefault());
            if (idx < 0) idx = 0;
            _selector = new Dropdown<NicknameEntry>(list, idx);
            OnPropertyChanged(nameof(NicknameSelector));
        }

        public NicknameEntry CurrentEntry
            => Entries.ElementAtOrDefault(_selector.SelectedIndex)
               ?? new NicknameEntry();

        // ── UI: selector + buttons ────────────────────────────────

        [SettingPropertyDropdown("{=NN_Select}Select Nickname Entry",
            Order = 0, RequireRestart = false,
            HintText = "{=NN_Select_H}Choose which nickname to edit.")]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames")]
        public Dropdown<NicknameEntry> NicknameSelector
        {
            get
            {
                var cur = _selector.SelectedValue;
                if (cur != _lastSelected)
                {
                    _lastSelected = cur;
                    OnPropertyChanged(nameof(CurrentEntry));
                }
                return _selector;
            }
        }

        [SettingPropertyButton("{=NN_Add}Add Entry", Content = "{=NN_Add}Add Entry",
            Order = 1, RequireRestart = false)]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames")]
        public Action AddEntryButton { get; set; } = () =>
        {
            Instance.Entries.Add(new NicknameEntry("Honor", 1, "new suffix"));
            Instance.RefreshDropdown();
            Instance.Save();
        };

        [SettingPropertyButton("{=NN_Remove}Remove Entry", Content = "{=NN_Remove}Remove Entry",
            Order = 2, RequireRestart = false)]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames")]
        public Action RemoveEntryButton { get; set; } = () =>
        {
            var i = Instance.NicknameSelector.SelectedIndex;
            if (i >= 0 && i < Instance.Entries.Count)
            {
                Instance.Entries.RemoveAt(i);
                Instance.RefreshDropdown();
                Instance.Save();
            }
        };

        [SettingPropertyButton("{=NN_Clear}Clear All", Content = "{=NN_Clear}Clear All",
            Order = 3, RequireRestart = false)]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames")]
        public Action ClearEntriesButton { get; set; } = () =>
        {
            if (Instance.Entries.Count > 1)
            {
                Instance.Entries.RemoveRange(1, Instance.Entries.Count - 1);
                Instance.RefreshDropdown();
                Instance.Save();
                InformationManager.DisplayMessage(
                    new InformationMessage("[NN] Cleared all nicknames (except first)", Colors.Green)
                );
            }
        };

        // ── UI: edit fields for the selected entry ────────────────

        [SettingPropertyText("{=NN_Trait}Trait Name",
            Order = 10, RequireRestart = false)]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames/{=MCM_EDIT}Edit")]
        public string Trait
        {
            get => CurrentEntry.Trait;
            set { CurrentEntry.Trait = value; Save(); }
        }

        [SettingPropertyInteger("{=NN_Threshold}Threshold",
            -20, 20, "{VALUE}",
            Order = 11, RequireRestart = false)]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames/{=MCM_EDIT}Edit")]
        public int Threshold
        {
            get => CurrentEntry.Threshold;
            set { CurrentEntry.Threshold = value; Save(); }
        }

        [SettingPropertyText("{=NN_Suffix}Suffix Text",
            Order = 12, RequireRestart = false)]
        [SettingPropertyGroup("{=MCM_NICKNAMES}Nicknames/{=MCM_EDIT}Edit")]
        public string Suffix
        {
            get => CurrentEntry.Suffix;
            set { CurrentEntry.Suffix = value; Save(); }
        }
    }
}

