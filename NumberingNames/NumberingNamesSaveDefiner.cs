using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace NumberingNames
{
    public class KingdomHistoryEntry
    {
        // must have a unique integer ID per field
        [SaveableField(1)]
        public string KingdomId;

        [SaveableField(2)]
        public List<string> RulerNames;

        // parameterless ctor required for deserialization
        public KingdomHistoryEntry() { }

        public KingdomHistoryEntry(string kingdomId, List<string> names)
        {
            KingdomId = kingdomId;
            RulerNames = names;
        }
    }

    
    public class SurnamesOtherEntry
    {
        // must have a unique integer ID per field
        [SaveableField(1)]
        public TextObject HeroName;

        [SaveableField(2)]
        public string Surname;

        // parameterless ctor required for deserialization
        public SurnamesOtherEntry() { }

        public SurnamesOtherEntry(TextObject heroName, string surname)
        {
            HeroName = heroName;
            Surname = surname;
        }
    }
    
    // 4) Register your saveable types
    public class NumberingNamesSaveDefiner : SaveableTypeDefiner
    {
        public NumberingNamesSaveDefiner() : base(98765433) { }

        protected override void DefineClassTypes()
        {
            // class ID 1 = KingdomHistoryEntry
            AddClassDefinition(typeof(KingdomHistoryEntry), 1);
            AddClassDefinition(typeof(SurnamesOtherEntry), 2);
        }

        protected override void DefineContainerDefinitions()
        {
            // container ID 1 = List<KingdomHistoryEntry>
            ConstructContainerDefinition(typeof(List<KingdomHistoryEntry>));
            ConstructContainerDefinition(typeof(List<SurnamesOtherEntry>));
        }
    }
}
