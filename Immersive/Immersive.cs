using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Newtonsoft.Json.Bson;
using System;
using System.Runtime.InteropServices;
using static Dalamud.Game.ClientState.Conditions.ConditionFlag;
using static ECommons.DalamudServices.Svc;
namespace Immersive;

/// <summary>
/// Immersive v2
/// </summary>

public class Immersive : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public string Name => "Immersive";
    public static Immersive? Plugin { get; private set; }

    private bool Immersion = true;
    private static unsafe Camera* GameCamera => CameraManager.Instance()->GetActiveCamera();
    private static unsafe bool FirstPerson => GameCamera->ZoomMode == CameraZoomMode.FirstPerson;
    private CameraZoomMode ZoomMode;
    private CameraControlMode ControlMode;
    private static bool InCutscene => Condition[WatchingCutscene] || Condition[OccupiedInCutSceneEvent] || Condition[WatchingCutscene78];
    private static bool InDuty => Condition[BoundByDuty] || Condition[BoundByDuty56] || Condition[BoundByDuty95];
    private delegate void InputKeyDelegate(byte unknown, uint flags, uint keyCode, ulong unknownSource);
    private unsafe static void SetKeyValue(VirtualKey virtualKey, KeyStateFlags keyStateFlag) => (*(int*)(Svc.SigScanner.Module.BaseAddress + Marshal.ReadInt32(Svc.SigScanner.ScanText("48 8D 0C 85 ?? ?? ?? ?? 8B 04 31 85 C2 0F 85") + 0x4) + (4 * (*(byte*)(Svc.SigScanner.Module.BaseAddress + Marshal.ReadInt32(Svc.SigScanner.ScanText("0F B6 94 33 ?? ?? ?? ?? 84 D2") + 0x4) + (int)virtualKey))))) = (int)keyStateFlag;

    public Immersive(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(pluginInterface, this, Module.All, Module.DalamudReflector);

            //Add Command Handler for /immersive to toggle the plugin on or off
            Commands.AddHandler("/immersive", new CommandInfo(OnCommand)
            {
                HelpMessage = "/immersive -> toggle on or off, on by default"
            });

            //Register Addon Lifecycle Listeners for the Talk Addon opening and closing, to switch to first person when the addon opens and back to the previous camera mode when it closes
            AddonLifecycle.RegisterListener(AddonEvent.PostShow, "Talk", OnTalkAddonShow);
            AddonLifecycle.RegisterListener(AddonEvent.PostHide, "Talk", OnTalkAddonClose);
            AddonLifecycle.RegisterListener(AddonEvent.PostShow, "SelectString", OnSelectStringAddonShow);
            AddonLifecycle.RegisterListener(AddonEvent.PostHide, "SelectString", OnSelectStringAddonClose);
        }
        catch (Exception e) { Log.Info($"Failed loading plugin\n{e}"); }
    }

    private void OnTalkAddonShow(AddonEvent type, AddonArgs args)
    {
        //If the Talk Addon is shown, then we will ToggleImmersion true
        ToggleImmersion(true);
    }

    private void OnTalkAddonClose(AddonEvent type, AddonArgs args)
    {
        //If the Talk Addon is hidden, then we will ToggleImmersion false
        ToggleImmersion(false);
    }
    private void OnSelectStringAddonShow(AddonEvent type, AddonArgs args)
    {
        //If the SelectString Addon is shown, then we will ToggleImmersion false
        ToggleImmersion(false);
    }
    
    private unsafe void OnSelectStringAddonClose(AddonEvent type, AddonArgs args)
    {
        //If the SelectString Addon is hidden, then we will ToggleImmersion true if the Talk Addon is ready
        if (GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk) && GenericHelpers.IsAddonReady(addonTalk))
            ToggleImmersion(true);
    }
    private unsafe void ToggleImmersion(bool value )
    {
        if (value)
        {
            //If we are not in a cutscene or duty, then we will switch to first person and hide the UI
            if (!InCutscene && !InDuty)
            {
                //If we are not already in first person, then we will store our current camera mode and switch to first person
                if (!FirstPerson)
                {
                    ZoomMode = GameCamera->ZoomMode;
                    GameCamera->ZoomMode = CameraZoomMode.FirstPerson;
                }

                //If the UI is not already hidden, then we will hide it by pressing the scroll key
                if (!GameGui.GameUiHidden)
                    SetKeyValue(VirtualKey.SCROLL, KeyStateFlags.Pressed);
            }
        }
        else
        {
            //If we are not in a cutscene or duty, then we will switch to back to the previous camera mode and unhide the UI
            if (!InCutscene && !InDuty)
            {
                //If we are in first person, then we will switch back to the previous camera mode
                if (FirstPerson && !InCutscene)
                    GameCamera->ZoomMode = ZoomMode;

                //If the UI is hidden, then we will unhide it by pressing the scroll key
                if (GameGui.GameUiHidden)
                    SetKeyValue(VirtualKey.SCROLL, KeyStateFlags.Pressed);
            }
        }
    }

    public void Dispose()
    {
        //Remove Commands then dispose Ecommons
        Commands.RemoveHandler("/immersive");
        ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        //In response to the slash command, just toggle Immersion
        Immersion = !Immersion;
    }
}