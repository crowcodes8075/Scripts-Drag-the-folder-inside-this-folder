/*
name: TestEnhance
description: Test script that uses SmartEnhance from CoreAdvanced to enhance the currently equipped class.
tags: test, smart, enhancement, enhance
*/
//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreAdvanced.cs

using Skua.Core.Interfaces;

public class TestEnhance
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots Core => CoreBots.Instance;
    private static CoreAdvanced Adv
    {
        get => _Adv ??= new CoreAdvanced();
        set => _Adv = value;
    }
    private static CoreAdvanced _Adv;

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);

        string? className = Bot.Player.CurrentClass?.Name;
        if (string.IsNullOrEmpty(className))
        {
            Core.Logger("TestEnhance: No class is currently equipped.");
            Core.SetOptions(false);
            return;
        }

        Core.Logger($"TestEnhance: SmartEnhancing {className}...");
        Adv.SmartEnhance(className);

        Core.SetOptions(false);
    }
}
