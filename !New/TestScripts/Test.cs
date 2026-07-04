//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine2.cs

using System;
using System.Linq;
using Skua.Core.Interfaces;

public class Test
{
    private CoreBots2 C => CoreBots2.Instance;
    private CoreEngine2 Core => CoreEngine2.Instance;
    public IScriptInterface Bot => IScriptInterface.Instance;

    public void ScriptMain(IScriptInterface bot)
    {
        C.SetOptions(disableCoreSkills: true);
        Core.Boot();

        LogEquippedBoosts();

        C.SetOptions(false);
        Bot.StopSync();
    }

    private void LogEquippedBoosts()
    {
        var weapon = Bot.Inventory.Items.FirstOrDefault(i => i.Equipped && i.Category == ItemCategory.Sword)
                  ?? Bot.Inventory.Items.FirstOrDefault(i => i.Equipped && (i.Category == ItemCategory.Axe || i.Category == ItemCategory.Dagger || i.Category == ItemCategory.Gun || i.Category == ItemCategory.Staff || i.Category == ItemCategory.Wand || i.Category == ItemCategory.Polearm || i.Category == ItemCategory.Mace || i.Category == ItemCategory.Gauntlet || i.Category == ItemCategory.Bow || i.Category == ItemCategory.Whip || i.Category == ItemCategory.HandGun || i.Category == ItemCategory.Rifle));

        var armor = Bot.Inventory.Items.FirstOrDefault(i => i.Equipped && i.Category == ItemCategory.Armor);

        Bot.Log("--- Equipped Weapon Boosts ---");
        if (weapon != null)
        {
            Bot.Log($"  Weapon: {weapon.Name} (ID: {weapon.ID})");
            Bot.Log($"  Meta: {weapon.Meta}");
            LogBoostItem(weapon);
        }
        else
            Bot.Log("  No weapon equipped.");

        Bot.Log("--- Equipped Armor Boosts ---");
        if (armor != null)
        {
            Bot.Log($"  Armor: {armor.Name} (ID: {armor.ID})");
            Bot.Log($"  Meta: {armor.Meta}");
            LogBoostItem(armor);
        }
        else
            Bot.Log("  No armor equipped.");

        Bot.Log("--- Checking Pet (TempInv + Inventory) ---");
        var pet = Bot.Inventory.Items.FirstOrDefault(i => i.Equipped && (i.CategoryString?.Equals("Pet", StringComparison.OrdinalIgnoreCase) == true));
        // Also check house items for pet
        if (pet == null)
        {
            // Try the active pet from game object
            try
            {
                int petId = Bot.Flash.CallGameFunction<int>("world.myAvatar.intPetID");
                if (petId > 0)
                    pet = Bot.Inventory.Items.FirstOrDefault(i => i.ID == petId);
            }
            catch { }
        }
        if (pet != null)
        {
            Bot.Log($"  Pet: {pet.Name} (ID: {pet.ID})");
            Bot.Log($"  Meta: {pet.Meta}");
            LogBoostItem(pet);
        }
        else
            Bot.Log("  No pet found.");
    }

    private void LogBoostItem(InventoryItem item)
    {
        if (string.IsNullOrEmpty(item.Meta))
        {
            Bot.Log("  No boost data in Meta.");
            return;
        }

        Bot.Log("  Raw Meta: " + item.Meta);

        // Try common boost keys
        string[] checkTypes = ["dmgAll", "Human", "Undead", "Dragon", "Chaos", "Elemental", "Mortal", "All"];
        foreach (string t in checkTypes)
        {
            float val = C.GetBoostFloat(item, t);
            if (val > 0)
                Bot.Log($"  {t}: {val}");
        }
    }
}
