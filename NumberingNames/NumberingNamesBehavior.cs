using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace NumberingNames
{
    public class NumberingNamesBehavior : CampaignBehaviorBase
    {
        public static NumberingNamesBehavior Instance { get; private set; }

        public static bool ClanMode = true;

        public static readonly ConcurrentDictionary<TextObject, string> SurnameMap
          = new ConcurrentDictionary<TextObject, string>();
        // In-memory mapping of kingdoms to ruler name history
        public Dictionary<Kingdom, List<string>> _kingdomRulers = new Dictionary<Kingdom, List<string>>();
        /// Maps a hero’s Name TextObject → the suffix you want to append, e.g. "II"
        public static readonly ConcurrentDictionary<TextObject, string> NameSuffixMap
          = new ConcurrentDictionary<TextObject, string>();
        /// New: maps a hero’s Name TextObject → the nickname (e.g. "the Great")
        public static readonly ConcurrentDictionary<TextObject, string> NicknameMap
            = new ConcurrentDictionary<TextObject, string>();
        // name owner map for gender text
        public static readonly ConcurrentDictionary<TextObject, Hero> NameOwnerMap
    = new ConcurrentDictionary<TextObject, Hero>();
        // Tracks the last seen ruler per kingdom
        private Dictionary<Kingdom, Hero> _currentRulers = new Dictionary<Kingdom, Hero>();

        // this list will be serialized by IDataStore
        public List<KingdomHistoryEntry> _historyEntries
            = new List<KingdomHistoryEntry>();

        //surnames cultures
        public Dictionary<string, List<string>> _names = new Dictionary<string, List<string>>();

        public List<SurnamesOtherEntry> _surnameEntries
            = new List<SurnamesOtherEntry>(); 

        public static ConcurrentDictionary<TextObject, string> SurnameOtherMap
    = new ConcurrentDictionary<TextObject, string>();


        public override void RegisterEvents()
        {
            Instance = this;
            // Show config on load
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinishedEvent);

            // Hook the event dispatcher, passing in your handler method
            CampaignEvents.HeroCreated
                .AddNonSerializedListener(this, OnHeroCreated);

            // New Game created
            CampaignEvents.OnNewGameCreatedEvent
                .AddNonSerializedListener(this, OnNewGameCreatedEvent);

            // whenever any kingdom changes ruler
            CampaignEvents.RulingClanChanged
                .AddNonSerializedListener(this, OnRulingClanChanged);
            //KingdomDestroy
            CampaignEvents.KingdomDestroyedEvent
                .AddNonSerializedListener(this, OnKingdomDestroyed);
            // KingdomCreate
            CampaignEvents.KingdomCreatedEvent
                .AddNonSerializedListener(this, OnKingdomCreated);

            CampaignEvents.HeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);

            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTickEvent);
        }

        private void OnDailyTickEvent()
        {
            var alive = SurnameOtherMap.Where(h=> Hero.AllAliveHeroes.Select(s=> s.Name).Contains(h.Key)).ToList();
            var deadHeroes = SurnameOtherMap.Except(alive).ToList();
            foreach (var dead in deadHeroes)
            {
                SurnameOtherMap.TryRemove(dead.Key, out _);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            var s = NumberingNamesSettings.Instance;
            if (!s.SurnamesMenu || !s.Surnames || s.SurnamesClanNames) return;
            // For each world‑map menu we want to inject into:
            foreach (var parent in new[] { "town", "village", "castle" })
            {
                // 1) Add the “Health Status…” option in the parent menu,
                //    pointing at its own newly‑named submenu:
                var rootMenuId = $"nn_change_surname_root_{parent}_menu";
                starter.AddGameMenuOption(
                    parent,                                   // e.g. "town"
                    $"nn_change_surname_root_{parent}",               // unique option id
                    "{=NN_CHANGE_SURNAME_OPTION}Change Surname",         // text
                    args =>
                    {

                        args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                        return true;
                    },
                    args =>
                    {
                        ChangeSurnameInquiry();
                    },
                    false, 300, false, null
                );
            }

        }

        private void ChangeSurnameInquiry()
        {

            var title = GameTexts.FindText("str_ui_search").ToString();
            var prompt = new TextObject("{=NN_Change_Surname_TEXT}Write your new surname").ToString();

            var data = new TextInquiryData(
                title,
                prompt,
                true,  // isTextBox
                true,  // submitOnRightClick
                GameTexts.FindText("str_next").ToString(),
                GameTexts.FindText("str_cancel").ToString(),

                // when they hit "Next"
                selectedText =>
                {
                    if (string.IsNullOrWhiteSpace(selectedText))
                    {
                        // nothing typed → just bail
                        return;
                    }
                    // Perform the change
                    SurnameOtherMap[Hero.MainHero.Name] = selectedText;
                    var spouse = Hero.MainHero.Spouse;
                    if(spouse != null && spouse.Name != null) SurnameOtherMap[spouse.Name] = selectedText;
                    var children = Hero.MainHero.Children.Where(h => h.IsChild && h.Clan == Clan.PlayerClan).ToList();
                    if (children == null || children.Count == 0) return;
                    foreach(var child in children)
                    {
                        SurnameOtherMap[child.Name] = selectedText;
                    }
                },

                // onCancel
                () => { }
            );

            InformationManager.ShowTextInquiry(data);
        }


        public override void SyncData(IDataStore dataStore)
        {
            // this will keep the same List<T> instance Bannerlord is tracking
            dataStore.SyncData("RulerHistories", ref _historyEntries);

            if (dataStore.IsLoading)
            {
                // rebuild your dictionary from the loaded entries
                _kingdomRulers.Clear();
                foreach (var entry in _historyEntries)
                {
                    var kingdom = Kingdom.All
                        .FirstOrDefault(k => k.StringId == entry.KingdomId && !k.IsEliminated);
                    if (kingdom != null)
                        _kingdomRulers[kingdom] = new List<string>(entry.RulerNames);
                }
            }
            else
            {
                // on saving, **mutate** the existing list rather than reassigning it
                _historyEntries.Clear();
                foreach (var kv in _kingdomRulers.Where(kv => !kv.Key.IsEliminated))
                {
                    _historyEntries.Add(new KingdomHistoryEntry(
                        kv.Key.StringId,
                        new List<string>(kv.Value)
                    ));
                }
            }

            dataStore.SyncData("SurnameHistory", ref _surnameEntries);
            if (NumberingNamesSettings.Instance.Surnames && !NumberingNamesSettings.Instance.SurnamesClanNames)
            {
                if (dataStore.IsLoading)
                {
                    // rebuild your dictionary from the loaded entries
                    SurnameOtherMap.Clear();
                    foreach (var entry in _surnameEntries)
                    {
                        if (entry.HeroName != null && entry.Surname != null)
                            SurnameOtherMap[entry.HeroName] = entry.Surname;
                    }
                }
                else
                {
                    // on saving, **mutate** the existing list rather than reassigning it
                    _surnameEntries.Clear();
                    foreach (var kv in SurnameOtherMap)
                    {
                        if (kv.Key != null && kv.Value != null)
                            _surnameEntries.Add(new SurnamesOtherEntry(
                                kv.Key,
                                kv.Value
                            ));
                    }
                }
            }



        }


        private void OnNewGameCreatedEvent(CampaignGameStarter starter)
        {
            ClanMode = NumberingNamesSettings.Instance.SurnamesClanNames;
            _names.Clear();
            PopulateNames();

            ApplySurnamesToExistingHeroes();
            InitializeRulerHistoryFromCurrent();
            ApplyNumberingToExistingHeroes();
        }

        private void OnGameLoadFinishedEvent()
        {
            ClanMode = NumberingNamesSettings.Instance.SurnamesClanNames;
            _names.Clear();
            PopulateNames();

            // Initialize _currentRulers from the world state
            _currentRulers.Clear();
            foreach (var kingdom in Kingdom.All)
            {
                var ruler = kingdom.RulingClan?.Leader;
                if (ruler != null)
                    _currentRulers[kingdom] = ruler;
            }

            // 2) Rebuild the single suffix map
            SurnameMap.Clear();
            NameSuffixMap.Clear();
            NicknameMap.Clear();
            NameOwnerMap.Clear();

            ApplySurnamesToExistingHeroes();

            InitializeRulerHistoryFromCurrent();

            // 2a) Then overwrite/add the current monarchs’ suffixes
            foreach (var kvp in _kingdomRulers)
            {
                var kingdom = kvp.Key;
                var history = kvp.Value;
                if (history == null || history.Count == 0)
                    continue;

                // Get the last entry, e.g. "Harlaus III"
                var lastEntry = history[history.Count - 1];
                var parts = lastEntry.Split(' ');
                // Only treat it as a suffix if there's more than one token
                if (parts.Length > 1)
                    {
                    var suffix = parts[parts.Length - 1]; // "II", "III", etc.
                    var rulerClan = kingdom.RulingClan;
                    if (rulerClan?.Leader != null)
                    {
                        NameSuffixMap[rulerClan.Leader.Name] = suffix;
                    }
                        
                    }
            }

            
            // 2b) Then, number all non‑rulers 
            ApplyNumberingToExistingHeroes();


            // 4) Now inject nicknames for each current ruler **without** touching the Roman map
            foreach (var kingdom in Kingdom.All)
            {
                
                var ruler = kingdom.RulingClan?.Leader;
                if (ruler != null)
                {
                    ComputeAndStoreNickname(ruler);

                }
            }
        }

        //Surnames

        private bool EligibleForSurnames(Hero hero)
        {
            var s = NumberingNamesSettings.Instance;
            if (hero == null || s == null || hero.Name == null) return false;
            if (!hero.IsAlive) return false;
            if (!s.SurnamesWanderer && hero.IsWanderer) return false;
            if (!s.SurnamesNotables && hero.IsNotable) return false;
            if (!s.SurnamesRulers && hero.IsKingdomLeader) return false;
            if (!s.SurnamesMinor && hero.IsMinorFactionHero) return false;


            return true;
        }


        private void PopulateNames()
        {
            _names.Clear();
            var s = NumberingNamesSettings.Instance;
            if (s.Debug) InformationManager.DisplayMessage(new InformationMessage($"Populate Names Fired!"));
            var cultures = MBObjectManager.Instance.GetObjectTypeList<CultureObject>();
            if (cultures == null) return;

            foreach (var culture in cultures)
            {

                var women = NameGenerator.Current.GetNameListForCulture(culture, true).Select(n => n.ToString()).ToList();
                var men = NameGenerator.Current.GetNameListForCulture(culture, false).Select(n => n.ToString()).ToList();

                List<string> full = new List<string>();
                if(women != null && women.Count > 0)
                {
                    full.AddRange(women);
                }
                if (men != null && men.Count > 0)
                {
                    full.AddRange(men);
                }

                if (s.Debug) InformationManager.DisplayMessage(new InformationMessage($"Are there women: {women.Count}, Are there men: {men.Count}"));

                if ((!_names.TryGetValue(culture.StringId, out var names) || names == null || names.Count == 0)
                    && full.Count > 0)
                {
                    _names.Add(culture.StringId, full);
                }
            }

            // build "All" fallback list (distinct, and only if there is anything)
            var all = _names.Values.SelectMany(x => x).Distinct().ToList();
            if (all != null && all.Count > 0) _names["All"] = all;


            if (s.Debug) InformationManager.DisplayMessage(new InformationMessage($"Amount of names: {_names.Count}"));
        }


        private void ApplySurnamesToExistingHeroes()
        {
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            if (!s.Surnames) return;


            var heroes = Hero.AllAliveHeroes
                .Where(h => EligibleForSurnames(h))
                .OrderByDescending(h=> !h.IsFemale)
                .ToList();
            foreach(var h in heroes)
            {
                if(s.SurnamesClanNames)
                {
                    var name = h?.Clan?.Name;
                    if (name == null) continue;
                    SurnameMap[h.Name] = name.ToString();
                }
                else
                {
                    if(!SurnameOtherMap.TryGetValue(h.Name, out var surname))
                    {
                        if (h.IsFemale && s.SurnamesWife)
                        {
                            var spouse = h.Spouse;
                            if (spouse != null)
                            {
                                if (SurnameOtherMap.TryGetValue(spouse.Name, out var realSurname) && realSurname != null)
                                {
                                    SurnameOtherMap[h.Name] = realSurname;
                                    continue; //skip rest
                                }
                                
                            }
                            else
                            {
                                var exSpouses = h.ExSpouses.Where(a=> SurnameOtherMap.ContainsKey(a.Name)).ToList();
                                if(exSpouses != null && exSpouses.Count > 0)
                                {
                                    var addedName = false;
                                    foreach(var ex in exSpouses)
                                    {
                                        if (SurnameOtherMap.TryGetValue(ex.Name, out var realSurname) && realSurname != null)
                                        {
                                            SurnameOtherMap[h.Name] = realSurname;
                                            addedName = true;
                                            break; //skip rest
                                        }
                                    }
                                    if (addedName) continue;
                                }
                            }
                        }
                        var list = GetBloodRelatives(h, 10, 0).Where(o=> EligibleForSurnames(o)).ToList();
                        if (list != null && list.Count > 1) 
                        {
                            var parent = list.FirstOrDefault(o => h.Father == o) ?? list.FirstOrDefault(o => h.Mother == o);
                            var old = list.OrderByDescending(o=> o.Age).FirstOrDefault(o => !o.IsFemale) ?? list.FirstOrDefault();

                            if(parent != null && SurnameOtherMap.TryGetValue(old.Name, out var parentSurname) && parentSurname != null)
                            {
                                SurnameOtherMap[h.Name] = parentSurname;
                            }
                            else if (old != null && old.Culture != null)
                            {
                                if (SurnameOtherMap.TryGetValue(old.Name, out var realSurname) && realSurname != null)
                                {
                                    SurnameOtherMap[h.Name] = realSurname;
                                }
                                else
                                {
                                    string pick = null;
                                    if (pick == null && _names.TryGetValue(old.Culture.StringId, out var names))
                                    {
                                        if (names != null && names.Count > 0)
                                        {
                                            pick = names[MBRandom.RandomInt(names.Count)];
                                        }
                                    }
                                    if (pick == null && _names.TryGetValue("All", out var AllNames))
                                    {
                                        if (AllNames != null && AllNames.Count > 0)
                                        {
                                            pick = AllNames[MBRandom.RandomInt(AllNames.Count)];
                                        }
                                    }

                                    if (pick == null) continue;
                                    SurnameOtherMap[old.Name] = pick;
                                    SurnameOtherMap[h.Name] = pick;
                                }
                            }
                        }
                        else
                        {
                            string pick = null;
                            if (pick == null && _names.TryGetValue(h.Culture.StringId, out var names))
                            {
                                if (names != null && names.Count > 0)
                                {
                                    pick = names[MBRandom.RandomInt(names.Count)];
                                }
                            }
                            if (pick == null && _names.TryGetValue("All", out var AllNames))
                            {
                                if (AllNames != null && AllNames.Count > 0)
                                {
                                    pick = AllNames[MBRandom.RandomInt(AllNames.Count)];
                                }
                            }
                            if (pick == null) continue;
                            SurnameOtherMap[h.Name] = pick;
                        }

                    }
                }

            }
        }

        private void ApplyClanSurname(Hero hero)
        {
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            if (!s.Surnames) return;
            if(!EligibleForSurnames(hero)) return;


            if (s.SurnamesClanNames)
            {
                if (!SurnameMap.ContainsKey(hero.Name))
                {
                    var name = hero?.Clan?.Name;
                    if (name == null) return;
                    SurnameMap[hero.Name] = name.ToString();
                }
            } else
            {
                if (!SurnameOtherMap.TryGetValue(hero.Name, out var surname))
                {
                    var list = GetBloodRelatives(hero, 10, 0).Where(o=> EligibleForSurnames(o)).ToList();
                    if (list != null && list.Count > 1)
                    {
                        var parent = list.FirstOrDefault(o => hero.Father == o) ?? list.FirstOrDefault(o => hero.Mother == o);
                        var old = list.OrderByDescending(o => o.Age).FirstOrDefault(o => !o.IsFemale) ?? list.FirstOrDefault();

                        if (parent != null && SurnameOtherMap.TryGetValue(parent.Name, out var parentSurname) && parentSurname != null)
                        {
                            SurnameOtherMap[hero.Name] = parentSurname;
                        }
                        else if (old != null && old.Culture != null)
                        {
                            if (SurnameOtherMap.TryGetValue(old.Name, out var realSurname) && realSurname != null)
                            {
                                SurnameOtherMap[hero.Name] = realSurname;
                            }
                            else
                            {
                                string pick = null;
                                if (pick == null && _names.TryGetValue(old.Culture.StringId, out var names))
                                {
                                    if (names != null && names.Count > 0)
                                    {
                                        pick = names[MBRandom.RandomInt(names.Count)];
                                    }
                                }
                                if (pick == null && _names.TryGetValue("All", out var AllNames))
                                {
                                    if (AllNames != null && AllNames.Count > 0)
                                    {
                                        pick = AllNames[MBRandom.RandomInt(AllNames.Count)];
                                    }
                                }

                                if (pick == null) return;
                                SurnameOtherMap[old.Name] = pick;
                                SurnameOtherMap[hero.Name] = pick;
                            }
                        }
                    }
                    else
                    {
                        string pick = null;
                        if (pick == null && _names.TryGetValue(hero.Culture.StringId, out var names))
                        {
                            if (names != null && names.Count > 0)
                            {
                                pick = names[MBRandom.RandomInt(names.Count)];
                            }
                        }
                        if (pick == null && _names.TryGetValue("All", out var AllNames))
                        {
                            if (AllNames != null && AllNames.Count > 0)
                            {
                                pick = AllNames[MBRandom.RandomInt(AllNames.Count)];
                            }
                        }
                        if (pick == null) return;
                        SurnameOtherMap[hero.Name] = pick;
                    }

                }
            }


        }
        //End of surnames
        private void ApplyNumberingToExistingHeroes()
        {
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            // Build a set of all current kingdom rulers so we can skip them
            var currentRulers = new HashSet<Hero>(
                Kingdom.All
                       .Select(k => k.RulingClan?.Leader)
                       .Where(h => h != null)
            );

            foreach (var clan in Clan.All.Where(c => !c.IsBanditFaction))
            {
                // Get the pool of all non-rulers in this clan
                var clanHeroes = clan.Heroes
                                     .Where(h => h != null && !currentRulers.Contains(h))
                                     .ToList();

                if (s.OnlyFamily)
                {
                    // For *each* hero, number within *their* blood relatives
                    foreach (var hero in clanHeroes)
                    {
                        var baseName = hero.Name.ToString().Split(' ')[0];

                        // Build this hero’s family subset (intersect with clanHeroes)
                        var family = GetBloodRelatives(hero,
                                                       s.GenerationsUp,
                                                       s.GenerationsDown)
                                     .Where(h => clanHeroes.Contains(h))
                                     .Where(h => h.Name.ToString().Split(' ')[0] == baseName)
                                     .OrderByDescending(h => h.Age)
                                     .ToList();

                        // If nobody else in the family shares the name, skip
                        if (family.Count < 2)
                            continue;

                        // Find this hero’s position in the ordered family list
                        var index = family.IndexOf(hero);
                        // Roman = index+1
                        NameSuffixMap[hero.Name] = ToRoman(index + 1);
                    }
                }
                else
                {
                    // Number across the *entire clan* by base name
                    var groups = clanHeroes
                        .GroupBy(h => h.Name.ToString().Split(' ')[0])
                        .Where(g => g.Count() > 1);

                    foreach (var group in groups)
                    {
                        var ordered = group
                            .OrderByDescending(h => h.Age)
                            .ToList();
                        for (int i = 0; i < ordered.Count; i++)
                        {
                            NameSuffixMap[ordered[i].Name] = ToRoman(i + 1);
                        }
                    }
                }
            }
        }

        private void InitializeRulerHistoryFromCurrent()
        {
            foreach (var kingdom in Kingdom.All)
            {
                var ruler = kingdom.RulingClan?.Leader;
                if (ruler == null) continue;



                if (!_kingdomRulers.TryGetValue(kingdom, out var history))
                    _kingdomRulers[kingdom] = history = new List<string>();

                // Only add the “I” entry if we have none yet
                if (history.Count == 0)
                {
                    // Base name
                    var baseName = ruler.Name.ToString().Split(' ')[0];
                    history.Add(baseName);
                }
            }
        }

        private void OnHeroesMarried(Hero hero1, Hero hero2, bool showNotification)
        {
            var s = NumberingNamesSettings.Instance;
            if (!s.Surnames || s.SurnamesClanNames || !s.SurnamesWife) return;
            if (!EligibleForSurnames(hero1) || !EligibleForSurnames(hero2)) return;

            if (!hero1.IsFemale && SurnameOtherMap.TryGetValue(hero1.Name, out var surname1) && surname1 != null)
            {
                SurnameOtherMap[hero2.Name] = surname1;
            }
            else if (!hero2.IsFemale && SurnameOtherMap.TryGetValue(hero2.Name, out var surname2) && surname2 != null)
            {
                SurnameOtherMap[hero1.Name] = surname2;
            }
        }

        private void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            if(isBornNaturally)
            {
                ApplyClanSurname(hero);
                ApplyClanNumbering(hero);
            }
            
        }

        private void OnKingdomDestroyed(Kingdom deadKingdom)
        {
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            // 1) Remove from the in‐memory map
            if (_kingdomRulers.Remove(deadKingdom) && s.Debug)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[NumberingNames] Removed history for eliminated kingdom {deadKingdom.Name}",
                        Colors.Red
                    )
                );
            }

            // 1) Get and clean up the old ruler:
            if (_currentRulers.TryGetValue(deadKingdom, out var oldRuler) && oldRuler != null)
            {
                // Remove nickname & number
                SurnameMap.TryRemove(oldRuler.Name, out _);
                NicknameMap.TryRemove(oldRuler.Name, out _);
                NameOwnerMap.TryRemove(oldRuler.Name, out _);
                NameSuffixMap.TryRemove(oldRuler.Name, out _);

                // Re‑apply the exact same clan logic you use on birth:
                ApplyClanSurname(oldRuler);
                ApplyClanNumbering(oldRuler);
            }

            _currentRulers.Remove(deadKingdom);
        }

        private void OnKingdomCreated(Kingdom kingdom)
        {
            // 1) add to the in‐memory map
            var ruler = kingdom.RulingClan?.Leader;
            if (ruler == null) return;

            SurnameMap.TryRemove(ruler.Name, out _);
            ApplyClanSurname(ruler);

            if (!_kingdomRulers.TryGetValue(kingdom, out var history))
                _kingdomRulers[kingdom] = history = new List<string>();

            // Only add the “I” entry if we have none yet
            if (history.Count == 0)
            {
                // Base name
                var baseName = ruler.Name.ToString().Split(' ')[0];
                history.Add(baseName);
            }

            if (!_currentRulers.ContainsKey(kingdom))
                _currentRulers[kingdom] = ruler;

            // 2a) Then overwrite/add the current monarchs’ suffixes
            if (history != null && history.Count > 0)
            {
                // Get the last entry, e.g. "Harlaus III"
                var lastEntry = history[history.Count - 1];
                var parts = lastEntry.Split(' ');
                // Only treat it as a suffix if there's more than one token
                if (parts.Length > 1)
                {
                    var suffix = parts[parts.Length - 1]; // "II", "III", etc.
                    NameSuffixMap[ruler.Name] = suffix;
                }
            }


            ComputeAndStoreNickname(ruler);
        }

        private void OnRulingClanChanged(Kingdom kingdom, Clan newRuler)
        {
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            // 1) Get and clean up the old ruler:
            if (_currentRulers.TryGetValue(kingdom, out var oldRuler) && oldRuler != null)
            {
                // Remove nickname & number
                NicknameMap.TryRemove(oldRuler.Name, out _);
                NameOwnerMap.TryRemove(oldRuler.Name, out _);
                NameSuffixMap.TryRemove(oldRuler.Name, out _);

                SurnameMap.TryRemove(oldRuler.Name, out _);
                ApplyClanSurname(oldRuler);
                // Re‑apply the exact same clan logic you use on birth:
                ApplyClanNumbering(oldRuler);
            }

            var king = newRuler?.Leader;
            if (king == null) return;

            SurnameMap.TryRemove(king.Name, out _);
            ApplyClanSurname(king);

            // 1) Get the base given name
            var baseName = king.Name.ToString().Split(' ')[0];

            // 2) Ensure we have a history list for this kingdom
            if (!_kingdomRulers.TryGetValue(kingdom, out var history))
                _kingdomRulers[kingdom] = history = new List<string>();

            // 3) Count how many times this baseName appears already in history
            //    (we ignore any existing suffixes, just compare the first word)
            int previousCount = history
                .Count(entry => entry.Split(' ')[0] == baseName);

            string fullName;
            if (previousCount == 0)
            {
                // First time this name has ruled: no suffix
                fullName = baseName;
                // Remove any old mapping, just in case
                NameSuffixMap.TryRemove(king.Name, out _);
            }
            else
            {
                // Second or later time: give them the next Roman suffix
                var roman = ToRoman(previousCount + 1);
                fullName = $"{baseName} {roman}";
                NameSuffixMap[king.Name] = roman;
            }

            // 4) Record into history
            history.Add(fullName);
            
            if (s.Debug)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[NumberingNames] New ruler of {kingdom.Name}: {fullName}",
                        Colors.Yellow
                    )
                );
            }


            // 5) Compute nickname from traits
            ComputeAndStoreNickname(king);
        }


        private static readonly Random _rng = new Random();

        private void ComputeAndStoreNickname(Hero hero)
        {

            // Assuming you expose your list of entries as:
            // var entries = NumberingNamesEditor.Instance.Entries;
            // or if you put them in a GlobalSettings:
            // var entries = NumberingNamesSettings.Instance.Entries;
            var e = NumberingNamesEditor.Instance;
            if (e == null) return;
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            if (!s.EnableNicks) return;
            var entries = e.Entries;
            if (entries == null || entries.Count == 0) return;

            // Clear any old nickname
            NicknameMap.TryRemove(hero.Name, out _);
            NameOwnerMap.TryRemove(hero.Name, out _);

            var allMatches = new List<(int threshold, string suffix)>();

            // 1) For each trait, consider all entries for that trait
            foreach (var group in entries.GroupBy(en => en.Trait))
            {
                // Look up the TraitObject by name
                var prop = typeof(DefaultTraits).GetProperty(group.Key);
                if (prop == null) continue;
                var traitObj = (TraitObject)prop.GetValue(null);
                int level = hero.GetTraitLevel(traitObj);

                // Filter to only those entries whose threshold ≤ hero’s level
                var valid = group.Where(h => level >= h.Threshold).ToList();
                if (!valid.Any()) continue;

                // Find the highest threshold in this trait group
                int best = valid.Max(b => b.Threshold);

                // Collect *all* suffixes at that best threshold
                foreach (var a in valid.Where(c => c.Threshold == best))
                    allMatches.Add((a.Threshold, a.Suffix));
            }

            if (allMatches.Count == 0)
                return;  // no nickname

            // 2) Find the global highest threshold
            int globalBest = allMatches.Max(t => t.threshold);

            // 3) Gather all suffixes at that global best threshold
            var topSuffixes = allMatches
                .Where(t => t.threshold == globalBest)
                .Select(t => t.suffix)
                .Distinct()
                .ToList();

            // 4) Randomly pick one
            var chosen = topSuffixes[_rng.Next(topSuffixes.Count)];

            // 5) Store it
            NicknameMap[hero.Name] = chosen;
            NameOwnerMap[hero.Name] = hero;

            // Optional debug
            if (s.Debug)
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[NumberingNames] Nickname for {hero.Name}: {chosen} (threshold {globalBest})",
                        Colors.Magenta
                    )
                );
        }

        /// <summary>
        /// Recomputes the roman suffix for a hero based on their clan membership alone.
        /// Like OnHeroCreated but for an existing hero stepping down.
        /// </summary>
        private void ApplyClanNumbering(Hero hero)
        {
            var s = NumberingNamesSettings.Instance;
            if (s == null) return;
            if (hero == null) return;

            // 1) Extract the base given name
            var parts = hero.Name.ToString().Split(' ');
            var baseName = parts.Length > 0 ? parts[0] : hero.Name.ToString();

            // 2) Build the pool (clan or family)
            IEnumerable<Hero> pool;
            if (s.OnlyFamily)
            {
                pool = GetBloodRelatives(hero, s.GenerationsUp, s.GenerationsDown);
            }
            else
            {
                pool = hero.Clan.Heroes;
            }

            // 3) Filter to same base name and also in that pool
            var sameNameGroup = pool
                .Where(h =>
                    h.Clan == hero.Clan &&                           // same clan
                    h.Name.ToString().Split(' ')[0] == baseName)     // same base name
                .OrderByDescending(h => h.Age)
                .ToList();

            // 4) If you’re the only one, remove any existing suffix and stop
            if (sameNameGroup.Count <= 1)
            {
                NameSuffixMap.TryRemove(hero.Name, out _);
                return;
            }

            // 5) Otherwise, find your index in the age-ordered list
            int idx = sameNameGroup.IndexOf(hero);
            if (idx < 0) idx = sameNameGroup.Count - 1; // fallback

            // 6) Assign numeral = (index + 1)
            var roman = ToRoman(idx + 1);
            NameSuffixMap[hero.Name] = roman;

            if (s.Debug)
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[NumberingNames] ApplyClanNumbering → {hero.Name}: {roman}",
                        Colors.Green));
        }

        private IEnumerable<Hero> GetBloodRelatives(Hero root, int generationsUp = 2, int generationsDown = 2)
        {
            var relatives = new HashSet<Hero>();

            void AddAncestorsAndSiblings(Hero h, int depth)
            {
                var s = NumberingNamesSettings.Instance;
                if (s == null) return;

                if (h == null || depth < 0) return;

                // 1) Parents
                if (h.Father != null && relatives.Add(h.Father))
                    AddAncestorsAndSiblings(h.Father, depth - 1);
                if (h.Mother != null && relatives.Add(h.Mother))
                    AddAncestorsAndSiblings(h.Mother, depth - 1);

                // 2) Siblings (other children of each parent)
                if (h.Father?.Children != null)
                    foreach (var sib in h.Father.Children)
                        if (sib != h) relatives.Add(sib);
                if (h.Mother?.Children != null)
                    foreach (var sib in h.Mother.Children)
                        if (sib != h) relatives.Add(sib);

                // 3) optionally, uncles/aunts + cousins
                if (!s.OnlyCloseFamily)
                {
                    // uncles/aunts = siblings of your parents
                    if (h.Father?.Father != null)
                        foreach (var auntUncle in h.Father.Father.Children)
                            if (auntUncle != h.Father)
                            {
                                relatives.Add(auntUncle);
                                // cousins = their children
                                foreach (var cousin in auntUncle.Children)
                                    relatives.Add(cousin);
                            }
                    if (h.Mother?.Mother != null)
                        foreach (var auntUncle in h.Mother.Mother.Children)
                            if (auntUncle != h.Mother)
                            {
                                relatives.Add(auntUncle);
                                foreach (var cousin in auntUncle.Children)
                                    relatives.Add(cousin);
                            }
                }
            }

            void AddDescendants(Hero h, int depth)
            {
                if (h == null || depth < 0) return;
                foreach (var child in h.Children)
                    if (relatives.Add(child))
                        AddDescendants(child, depth - 1);
            }

            // root themselves
            relatives.Add(root);
            AddAncestorsAndSiblings(root, generationsUp);
            AddDescendants(root, generationsDown);
            return relatives;
        }


        private static string ToRoman(int number)
        {
            if (number < 1) return string.Empty;
            var numerals = new (int Value, string Numeral)[]
            {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
            };
            var result = new StringBuilder();
            foreach (var (value, numeral) in numerals)
            {
                while (number >= value)
                {
                    result.Append(numeral);
                    number -= value;
                }
            }
            return result.ToString();
        }

        

    }


    public static class StringExtensions
    {
        public static string ReplaceInvalidPathChars(this string s)
            => string.Concat(s
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }
}
