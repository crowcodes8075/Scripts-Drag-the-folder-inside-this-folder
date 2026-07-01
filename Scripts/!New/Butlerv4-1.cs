/*
name: Butler version 4.5
description: Follows a player using Goto; sync-file fallback for locked zones; transition-safe goto; reduced blinking; combat-aware syncing; server-aware sleeping.
tags: butler, follow, sync, locked-zone, quickdeaggro, lowlog
*/

//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreFarms.cs
//cs_include Scripts/CoreAdvanced.cs

using System;
using System.IO;
using System.Linq;
using Skua.Core.Interfaces;
using Skua.Core.Options;

public class Butler
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
    public string OptionsStorage = "ButlerV4-1";

    public List<IOption> Options = new()
    {
        new Option<string>("playerName", "Player Name", "Username to follow.", "InsertPlayerNamehere"),
        new Option<bool>("AutoEnhance", "Enable Auto-Enhance", "Automatically enhance your class when starting.", true),
        CoreBots.Instance.SkipOptions,
    };

    volatile bool LockedZoneWarning;
    volatile bool GotoIsOff;
    volatile bool RoomFull;
    volatile bool IsParked;

    string? playerName;
    string syncFilePath = "";

    private int noSyncFileCounter = 0;
    private const int noSyncFileThreshold = 5;

    private bool IsButlerAlive()
    {
        return Bot.Player?.Alive == true;
    }

    private void AntiAutoAggro(string oldCell, string oldPad)
    {
        if (!IsButlerAlive())
            return;

        Core.Sleep(50);

        if (Bot.Player.Cell == oldCell && Bot.Player.Pad == oldPad &&
            (Bot.Player.InCombat || Bot.Player.HasTarget))
        {
            Core.JumpWait();
        }
    }

    void QuickDeaggro()
    {
        if (!IsButlerAlive())
            return;

        if (Bot.Player.InCombat || (Bot.Player.HasTarget && (Bot.Player.Target?.HP ?? 0) > 0))
        {
            DontAttack();
            Bot.Map.Jump(Bot.Player.Cell ?? "Enter", Bot.Player.Pad ?? "Left");
        }
    }

    void DontAttack()
    {
        Bot.Combat.CancelTarget();
        Bot.Options.AttackWithoutTarget = false;
        Bot.Options.AggroAllMonsters = false;
        Bot.Options.AggroMonsters = false;
    }

    void EnterSafeState(string reason)
    {
        if (!IsParked)
        {
            Core.Logger(reason);

            Core.Join("whitemap-100000");
            Bot.Wait.ForMapLoad("whitemap");
            JoinHouse();

            IsParked = true;
        }
        else
        {
            Core.Logger("Safe parked. Sleeping 10 seconds.");
        }

        Core.Sleep(10000);
    }

    void JoinHouse()
    {
        try
        {
            if (Bot.House.Items.Any(h => h.Equipped))
            {
                string? toSend = null;

                void modifyPacket(dynamic packet)
                {
                    try
                    {
                        string pkt = Convert.ToString(packet) ?? "";
                        if (pkt.Contains("%xt%zm%house%")) toSend = pkt;
                    }
                    catch { }
                }

                Bot.Events.ExtensionPacketReceived += modifyPacket;
                Bot.Send.Packet($"%xt%zm%house%1%{Bot.Player.Username}%");
                Bot.Wait.ForMapLoad("house");

                if (Bot.Wait.ForTrue(() => toSend != null, 20))
                    Bot.Send.ClientPacket(toSend!, "json");

                Bot.Events.ExtensionPacketReceived -= modifyPacket;

                for (int i = 0; i < 7; i++) Bot.Send.ClientServer(" ", "");
            }
            else Core.Join("yulgar-100000");
        }
        catch { Core.Join("yulgar-100000"); }
    }

    private static string? ReadSyncFileSafe(string path, int retries = 5, int delayMs = 50)
    {
        if (!File.Exists(path)) return null;

        for (int i = 0; i < retries; i++)
        {
            try
            {
                using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader sr = new(fs);
                string? text = sr.ReadToEnd();

                if (!string.IsNullOrWhiteSpace(text) && text.Split('|').Length >= 10)
                    return text.Trim();
            }
            catch { }

            Core.Sleep(delayMs);
        }

        return null;
    }

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);
        Core.LoggerInChat = false;

        Bot.Events.ExtensionPacketReceived += ChatListener;
        Execute();
        Bot.Events.ExtensionPacketReceived -= ChatListener;

        Core.SetOptions(false);
    }

    public void Execute()
    {
        Core.Join("whitemap-100000");
        playerName = Bot.Config!.Get<string>("playerName");

        if (string.IsNullOrEmpty(playerName))
        {
            Core.Logger("PlayerName empty.", messageBox: true, stopBot: true);
            return;
        }

        if (playerName == Bot.Player.Username)
        {
            Core.Logger("Cannot follow yourself.", messageBox: true, stopBot: true);
            return;
        }

        // --- AutoEnhance Option B ---
        bool autoEnhanceToggle = Bot.Config!.Get<bool>("AutoEnhance");
        Core.CBOBool("DisableAutoEnhance", out bool disableAutoEnhance);

        if (autoEnhanceToggle && !disableAutoEnhance)
        {
            string currentClass = Bot.Player.CurrentClass?.Name ?? "";
            if (!string.IsNullOrEmpty(currentClass))
            {
                Core.Logger($"Enhancing class: {currentClass}");
                Adv.SmartEnhance(currentClass);
            }
        }
        // --- End AutoEnhance check ---

        string charLocDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Skua",
            "character_locations"
        );
        Directory.CreateDirectory(charLocDir);

        syncFilePath = Path.Combine(charLocDir, $"{playerName}_location.sync");

        long lastTicks = 0;
        int staleCounter = 0;
        const int staleThreshold = 30;

        while (!Bot.ShouldExit)
        {
            while (!Bot.ShouldExit && Bot.Player?.Alive != true) Core.Sleep(200);

            string? content = ReadSyncFileSafe(syncFilePath);
            if (content == null)
            {
                noSyncFileCounter++;
                if (noSyncFileCounter >= 5)
                {
                    EnterSafeState("No sync file detected.");
                    noSyncFileCounter = 0;
                }
                Core.Sleep(500);
                continue;
            }
            noSyncFileCounter = 0;

            var parts = content.Split('|');
            string targetMap  = parts.ElementAtOrDefault(0)?.ToLower() ?? "unknown";
            string targetRoom = parts.ElementAtOrDefault(1) ?? "0";
            string targetCell = parts.ElementAtOrDefault(2) ?? "Enter";
            string targetPad  = parts.ElementAtOrDefault(3) ?? "Left";

            bool leaderLoggedIn  = parts.ElementAtOrDefault(4) == "1";
            string leaderServer  = parts.ElementAtOrDefault(5) ?? "";
            long ticks           = long.TryParse(parts.ElementAtOrDefault(6), out var t) ? t : 0;
            bool leaderInCombat  = parts.ElementAtOrDefault(7) == "1";
            bool leaderHasTarget = parts.ElementAtOrDefault(8) == "1";
            bool leaderAlive     = parts.ElementAtOrDefault(9) == "1";

            // --- Stale Detector ---
            if (ticks == lastTicks)
            {
                staleCounter++;
                if (staleCounter >= staleThreshold)
                {
                    EnterSafeState("Leader offline (stale ticks).");
                    staleCounter = 0;
                }
            }
            else
            {
                staleCounter = 0;
                lastTicks = ticks;
            }
            // --- End Stale Detector ---

            if (!leaderLoggedIn) { EnterSafeState("Leader offline."); continue; }

            string myServer = Bot.Player.ServerIP ?? "";
            if (!string.Equals(leaderServer, myServer, StringComparison.OrdinalIgnoreCase))
            {
                EnterSafeState("Leader is on a different server.");
                continue;
            }

            string currentMap = Bot.Map.Name?.ToLower() ?? "";
            string currentCell = Bot.Player?.Cell ?? "Enter";

            bool mapMismatch = !string.Equals(targetMap, currentMap, StringComparison.OrdinalIgnoreCase);
            bool cellMismatch = !string.Equals(currentCell, targetCell, StringComparison.OrdinalIgnoreCase);

            if (mapMismatch || cellMismatch)
            {
                QuickDeaggro();

                if (LockedZoneWarning)
                {
                    Core.Logger("Locked zone - joining via sync file.");
                    Bot.Events.ExtensionPacketReceived -= ChatListener;
                    Core.Join("whitemap-100000");

                    string? syncContent = ReadSyncFileSafe(syncFilePath);
                    if (syncContent != null)
                    {
                        var syncParts = syncContent.Split('|');
                        string lockedMap = syncParts.ElementAtOrDefault(0)?.ToLower() ?? "unknown";
                        string lockedRoom = syncParts.ElementAtOrDefault(1) ?? "0";

                        Core.Join($"{lockedMap}-{lockedRoom}");
                        Bot.Wait.ForMapLoad(lockedMap);

                        string oldCell = Bot.Player.Cell ?? "Enter";
                        string oldPad = Bot.Player.Pad ?? "Left";
                        Bot.Player.Goto(playerName);
                        AntiAutoAggro(oldCell, oldPad);

                        DontAttack();
                        Core.Sleep(50);
                    }

                    LockedZoneWarning = false;
                    Bot.Events.ExtensionPacketReceived += ChatListener;
                }
                else
                {
                    string? finalCheck = ReadSyncFileSafe(syncFilePath);
                    if (finalCheck != null)
                    {
                        var finalParts = finalCheck.Split('|');
                        string finalRoom = finalParts.ElementAtOrDefault(1) ?? "0";

                        if (!int.TryParse(finalRoom, out int finalRoomNumber) ||
                            finalRoomNumber == -1 || finalRoomNumber < 10000)
                        {
                            EnterSafeState($"Detected non-private room, sleeping: {targetMap}-{finalRoom}");
                            continue;
                        }
                    }

                    if (IsParked)
                    {
                        Core.Logger("Leader valid again. Rejoining.");
                        Core.Join("whitemap-100000");
                        Bot.Wait.ForMapLoad("whitemap");
                        IsParked = false;
                    }

                    string oldCell = Bot.Player.Cell ?? "Enter";
                    string oldPad = Bot.Player.Pad ?? "Left";
                    Bot.Player.Goto(playerName);
                    AntiAutoAggro(oldCell, oldPad);

                    Bot.Wait.ForMapLoad(targetMap);
                    DontAttack();
                }
            }

            if (GotoIsOff)
            {
                Core.Logger($"{playerName} goto disabled. Stopping.");
                break;
            }

            if (RoomFull)
            {
                int sleepTimer = 1000;
                bool entered = false;

                while (!Bot.ShouldExit && !entered)
                {
                    Core.Sleep(sleepTimer);
                    string oldCell = Bot.Player.Cell ?? "Enter";
                    string oldPad = Bot.Player.Pad ?? "Left";
                    Bot.Player.Goto(playerName);
                    AntiAutoAggro(oldCell, oldPad);

                    if (Bot.Map.PlayerExists(playerName))
                    {
                        entered = true;
                        RoomFull = false;
                    }

                    sleepTimer = Math.Min(sleepTimer + 1000, 5000);
                }

                continue;
            }

            if (!mapMismatch)
            {
                string myPad = Bot.Player?.Pad ?? "Left";
                bool wrongPad = !string.Equals(myPad, targetPad, StringComparison.OrdinalIgnoreCase);

                if (wrongPad)
                {
                    string oldCell = Bot.Player.Cell ?? "Enter";
                    string oldPad = Bot.Player.Pad ?? "Left";
                    Bot.Player.Goto(playerName);
                    AntiAutoAggro(oldCell, oldPad);

                    DontAttack();
                    Core.Sleep(50);
                }
            }

            bool shouldAttack = leaderAlive && (leaderHasTarget || leaderInCombat);
            if (shouldAttack) Bot.Combat.Attack("*");
            else if (Bot.Player.InCombat || Bot.Player.HasTarget) QuickDeaggro();

            Core.Sleep(500);
        }

        Bot.Events.ExtensionPacketReceived -= ChatListener;
        QuickDeaggro();
    }

    void ChatListener(dynamic packet)
    {
        try
        {
            if (packet == null) return;

            var paramsObj = packet["params"];
            if (paramsObj == null || paramsObj.type != "str") return;

            dynamic? dataObj = paramsObj.dataObj;
            if (dataObj == null) return;

            string? cmd = dataObj[0];
            if (string.IsNullOrEmpty(cmd)) return;

            if (cmd == "server")
            {
                string? text = dataObj[2]?.ToString();
                if (text.Contains("ignoring goto")) GotoIsOff = true;
            }
            else if (cmd == "warning")
            {
                string chat = Convert.ToString(packet);
                if (chat.Contains("Locked zone") || chat.Contains("not available")) LockedZoneWarning = true;
                if (chat.Contains("full")) RoomFull = true;
                if (chat.Contains("ignoring goto")) GotoIsOff = true;
            }
        }
        catch { }
    }
}