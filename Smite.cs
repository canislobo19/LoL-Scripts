using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using SharpDX;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using EloBuddy.SDK.Utils;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;

//Smites the closest monster when its in range of Smite damage.

namespace SmiteHelp
{
    public static class Smite
    {
        //Smite's damage formula: 370+(20*Level) at levels 1-4, X+(30*Level) at levels 5-9, X+(40*Level) at levels 10-14, X+(50*Level) at levels 15-18
        
        private static readonly int[] Damage = {390, 410, 430, 450, 480, 510, 540, 570, 600, 640, 680, 720, 760, 800, 850, 900, 950, 1000};

        //Big Jungle Camps
        private static readonly Dictionary<string, Tuple<string, bool>> JungleCamps = new Dictionary<string, Tuple<string, bool>>
        {
            { "Baron", new Tuple<string, bool>("SRU_Baron", true) },
            { "Rift Crab", new Tuple<string, bool>("SRU_RiftHerald", true) },
            { "Blue Buff", new Tuple<string, bool>("SRU_Blue", false) },
            { "Red Buff", new Tuple<string, bool>("SRU_Red", false) },
            { "Gromp", new Tuple<string, bool>("SRU_Gromp", false) },
            { "Wolf", new Tuple<string, bool>("SRU_Murkwolf", false) },
            { "Wraiths", new Tuple<string, bool>("SRU_Razorbeak", false) },
            { "Krugs", new Tuple<string, bool>("SRU_Krug", false) },
            { "Scuttle Crab", new Tuple<string, bool>("Sru_Crab", false) },
            { "Cloud Drag", new Tuple<string, bool>("SRU_Dragon_Air", true) },
            { "Infernal Drag", new Tuple<string, bool>("SRU_Dragon_Fire", true) },
            { "Mountain Drag", new Tuple<string, bool>("SRU_Dragon_Earth", true) },
            { "Ocean Drag", new Tuple<string, bool>("SRU_Dragon_Water", true) },
            { "Elder Drag", new Tuple<string, bool>("SRU_Dragon_Elder", true) },

        };
        
        private const int Range = 500;
        private static readonly Color SmiteColor = Color.Blue;
        private static readonly Color SmiteColorOutOfRange = Color.Black;

        private static Spell.Targeted SmiteSpell { get; set; }

        private static int Damage
        {
            get
            {
                // Return smite damage, max level 18
                return Damage[Math.Min(18, Player.Instance.Level)];

                // Base damage is 370
                var damage = 370;
                
                // Smite damage formula
                for (var i = 1; i <= Math.Min(18, Player.Instance.Level); i++)
                {
                    damage += (int) (10+10*Math.Ceiling((i+1)/5f));
                }
                return damage;
            }
        }

        private static HashSet<Obj_AI_Base> CanSmite { get; set; }
        private static Menu Menu { get; set; }
        private static KeyBind HoldKey { get; set; }
        private static KeyBind ToggleKey { get; set; }
        private static Dictionary<string, CheckBox> EnabledMonsters { get; set; }
        private static CheckBox SmiteRange { get; set; }

        public static void Main(string[] args)
        {
            Loading.FinishLoading += FinishLoading;
        }

        private static void FinishLoading(EventArgs args)
        {
            // Check if Smite is available
            SpellSlot slot;
            try
            {
                slot = Player.Instance.Spellbook.Spells.First(spell => spell.Name.ToLower().Contains("smite") && (spell.Slot == SpellSlot.Summoner1 || spell.Slot == SpellSlot.Summoner2)).Slot;
            }
            catch (Exception)
            {
                Logger.Info("You didn't take Smite");
                return;
            }

            // Menu
            Menu = MainMenu.AddMenu("SmiteHelp", "canisSmiteHelp");

            Menu.AddGroupLabel("Key Settings");
            HoldKey = Menu.Add("holdKey", new KeyBind("Smite (Hold)", false, KeyBind.BindTypes.HoldActive, 'H'));
            ToggleKey = Menu.Add("toggleKey", new KeyBind("Smite (Toggle)", false, KeyBind.BindTypes.PressToggle, 'J'));

            Menu.AddGroupLabel("Enabled Monsters");
            EnabledMonsters = new Dictionary<string, CheckBox>();
            var previousEnabled = true;
            foreach (var monster in JungleCamps)
            {
                if (previousEnabled && !monster.Value.Item2)
                {
                    previousEnabled = false;
                    Menu.AddSeparator();
                }
                EnabledMonsters[monster.Value.Item1] = Menu.Add(monster.Value.Item1, new CheckBox(monster.Key, monster.Value.Item2));
            }

            Menu.AddGroupLabel("Drawings");
            SmiteRange = Menu.Add("SmiteRange", new CheckBox("Draw range around smiteable"));

            SmiteSpell = new Spell.Targeted(slot, Range, DamageType.True);
            CanSmite = new HashSet<Obj_AI_Base>();

            // Listen to for most accurate smite time
            Game.OnUpdate += OnUpdate;

            // Register known objects
            foreach (var obj in ObjectManager.Get<AttackableUnit>())
            {
                OnCreate(obj, EventArgs.Empty);
            }
        }

        private static void OnUpdate(EventArgs args)
        {
            // Check if player is Smiting
            if ((HoldKey.CurrentValue || ToggleKey.CurrentValue) && SmiteSpell.IsReady())
            {
                // Choose monster
                var smiteable =
                    CanSmite.Where(o => Player.Instance.IsInRange(o, Range + Player.Instance.BoundingRadius + o.BoundingRadius)).OrderByDescending(o => o.MaxHealth).FirstOrDefault();
                if (smiteable != null)
                {
                    // Check if that camp is alive
                    var name = smiteable.BaseSkinName.ToLower();
                    if (EnabledMonsters.Any(enabled => name.Equals(enabled.Key.ToLower()) && enabled.Value.CurrentValue))
                    {
                        // Check if monster can be killed
                        if (smiteable.TotalShieldHealth() <= Damage)
                        {
                            // Cast Smite
                            SmiteSpell.Cast(smiteable);
                            }
                        }
                    }
                }
            }
        }
}