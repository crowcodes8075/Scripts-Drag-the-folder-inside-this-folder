/*
name: AutoAttack
description: Auto-attacks on the spot without changing class or using CoreSkills.
tags: autoattack, farm, attack
*/
//cs_include Scripts/CoreBots.cs

using Skua.Core.Interfaces;

public class AutoAttack
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots Core => CoreBots.Instance;

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions();

        Bot.Options.AttackWithoutTarget = true;
        Bot.Combat.StopAttacking = false;

        try
        {
            Core.Logger("Starting auto-attack on the spot...");

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
            Bot.Options.AttackWithoutTarget = false;
            Bot.Combat.StopAttacking = false;
            Core.SetOptions(false);
        }
    }
}
