/*
name: AutoAttack
description: Auto-attacks on the spot without changing class or using CoreSkills.
tags: autoattack, farm, attack
*/
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs

using Skua.Core.Interfaces;

public class AutoAttack
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots2 C => CoreBots2.Instance;
    public CoreEngine2 Core = new();

    public void ScriptMain(IScriptInterface bot)
    {
        C.SetOptions(disableCoreSkills: true);
        Core.Boot();

        Bot.Options.AttackWithoutTarget = true;
        Bot.Combat.StopAttacking = false;

        try
        {
            C.Logger("Starting auto-attack on the spot with CoreEngine skill automation...");

            while (!bot.ShouldExit)
            {
                if (!Bot.Player.Alive)
                {
                    Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                    continue;
                }

                if (!Bot.Player.HasTarget)
                    Bot.Combat.Attack("*");

                bot.Sleep(500);
            }
        }
        finally
        {
            Core.DisableSkills();
            Bot.Options.AttackWithoutTarget = false;
            Bot.Combat.StopAttacking = false;
            C.SetOptions(false);
        }
    }
}
