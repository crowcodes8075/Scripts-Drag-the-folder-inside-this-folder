/*
name: Enhance
description: Auto-enhance the equipped class using custom presets for King's Echo, Lord of Order, Archpaladin, and Stonecrusher. Optionally equip and enhance a specific class by name.
tags: enhancement, autoenhance, class
*/
//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreAdvanced.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Skua.Core.Interfaces;
using Skua.Core.Options;

public class Enhance
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots Core => CoreBots.Instance;

    private static CoreAdvanced Adv
    {
        get => _Adv ??= new CoreAdvanced();
        set => _Adv = value;
    }
    private static CoreAdvanced _Adv;

    public bool DontPreconfigure = true;
    public string OptionsStorage = "Enhance";

    public List<IOption> Options = new()
    {
        new Option<string>("EnhanceClass", "Class to Enhance", "Optional class name to equip and enhance before running.", string.Empty),
        CoreBots.Instance.SkipOptions,
    };

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions();

        string classToEnhance = (Bot.Config.Get<string>("EnhanceClass") ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(classToEnhance))
            EnhanceClass(classToEnhance);
        else
            EnhanceCurrentClass();

        Core.SetOptions(false);
    }

    private void EnhanceCurrentClass()
    {
        string? currentClass = Bot.Player.CurrentClass?.Name;
        if (string.IsNullOrEmpty(currentClass))
        {
            Core.Logger("Enhance failed: no class is currently equipped.");
            return;
        }

        EnhanceClass(currentClass);
    }

    private void EnhanceClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return;

        className = className.Trim();
        string? inventoryName = FindClassName(className);
        if (string.IsNullOrEmpty(inventoryName))
        {
            Core.Logger($"Enhance failed: class '{className}' not found in inventory.");
            return;
        }

        Core.Logger($"Auto-enhancing class: {inventoryName}");

        var preset = GetEnhancementPreset(inventoryName);
        if (preset != null)
        {
            EnhanceWithPreset(inventoryName, preset.Value);
            return;
        }

        if (!EquipClass(inventoryName))
            return;

        new CoreEnhancements().Apply(inventoryName);
    }

    private void EnhanceWithPreset(string className, EnhancementPreset preset)
    {
        if (!Core.CheckInventory(className))
        {
            Core.Logger($"Enhancement failed: class '{className}' not found in inventory.");
            return;
        }

        if (!EquipClass(className))
            return;

        new CoreEnhancements().EnhanceEquipped(
            preset.Type,
            preset.CapeSpecial,
            preset.HelmSpecial,
            preset.WeaponSpecial
        );
    }

    private bool EquipClass(string className)
    {
        var classItem = Bot.Inventory.Items.Find(i =>
            i.Category == ItemCategory.Class
            && NormalizeString(i.Name) == NormalizeString(className)
        );

        if (classItem == null)
        {
            Core.Logger($"Enhancement failed: class '{className}' not found in inventory.");
            return false;
        }

        Core.Equip(classItem.Name);
        Bot.Wait.ForTrue(() => NormalizeString(Bot.Player.CurrentClass?.Name) == NormalizeString(classItem.Name), 40);
        return NormalizeString(Bot.Player.CurrentClass?.Name) == NormalizeString(classItem.Name);
    }

    private string? FindClassName(string className)
    {
        return Bot.Inventory.Items
            .Where(i => i.Category == ItemCategory.Class)
            .FirstOrDefault(i => NormalizeString(i.Name) == NormalizeString(className))
            ?.Name;
    }

    private EnhancementPreset? GetEnhancementPreset(string className)
    {
        switch (NormalizeString(className))
        {
            case "king's echo":
            case "kings echo":
                return new EnhancementPreset(
                    Type: EnhancementType.Healer,
                    CapeSpecial: CapeSpecial.Lament,
                    HelmSpecial: HelmSpecial.Examen,
                    WeaponSpecial: WeaponSpecial.Elysium
                );

            case "lord of order":
                return new EnhancementPreset(
                    Type: EnhancementType.Healer,
                    CapeSpecial: CapeSpecial.Forge,
                    HelmSpecial: HelmSpecial.Forge,
                    WeaponSpecial: WeaponSpecial.Arcanas_Concerto
                );

            case "archpaladin":
                return new EnhancementPreset(
                    Type: EnhancementType.Fighter,
                    CapeSpecial: CapeSpecial.Lament,
                    HelmSpecial: HelmSpecial.Anima,
                    WeaponSpecial: Adv.uPraxis() ? WeaponSpecial.Praxis : WeaponSpecial.Lacerate
                );

            case "stonecrusher":
                return new EnhancementPreset(
                    Type: EnhancementType.Fighter,
                    CapeSpecial: CapeSpecial.Lament,
                    HelmSpecial: HelmSpecial.Forge,
                    WeaponSpecial: WeaponSpecial.Valiance
                );

            case "chaos avenger":
                return new EnhancementPreset(
                    Type: EnhancementType.Lucky,
                    CapeSpecial: CapeSpecial.Vainglory,
                    HelmSpecial: HelmSpecial.Anima,
                    WeaponSpecial: Adv.uDauntless() ? WeaponSpecial.Dauntless : WeaponSpecial.Valiance
                );

            default:
                return null;
        }
    }

    private static string NormalizeString(string? input) => (input ?? string.Empty).Trim().ToLowerInvariant();

    private readonly record struct EnhancementPreset(
        EnhancementType Type,
        CapeSpecial CapeSpecial = CapeSpecial.None,
        HelmSpecial HelmSpecial = HelmSpecial.None,
        WeaponSpecial WeaponSpecial = WeaponSpecial.None
    );
}
