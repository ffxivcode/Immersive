using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using System;
using System.Runtime.InteropServices;
using static ECommons.DalamudServices.Svc;

namespace Immersive;

/// <summary>
/// Immersive v1
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
    private unsafe bool InCutscene => Conditions.Instance()->WatchingCutscene || Conditions.Instance()->OccupiedInCutSceneEvent || Conditions.Instance()->WatchingCutscene78;
    private delegate void InputKeyDelegate(byte unknown, uint flags, uint keyCode, ulong unknownSource);
    private unsafe static void SetKeyValue(VirtualKey virtualKey, KeyStateFlags keyStateFlag) => (*(int*)(Svc.SigScanner.Module.BaseAddress + Marshal.ReadInt32(Svc.SigScanner.ScanText("48 8D 0C 85 ?? ?? ?? ?? 8B 04 31 85 C2 0F 85") + 0x4) + (4 * (*(byte*)(Svc.SigScanner.Module.BaseAddress + Marshal.ReadInt32(Svc.SigScanner.ScanText("0F B6 94 33 ?? ?? ?? ?? 84 D2") + 0x4) + (int)virtualKey))))) = (int)keyStateFlag;

    public Immersive(
        IDalamudPluginInterface pluginInterface, IAddonLifecycle addonLifecycle)
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
            addonLifecycle.RegisterListener(AddonEvent.PostShow, "Talk", OnTalkAddonShow);
            addonLifecycle.RegisterListener(AddonEvent.PostHide, "Talk", OnTalkAddonClose);
        }
        catch (Exception e) { Log.Info($"Failed loading plugin\n{e}"); }
    }

    private unsafe void OnTalkAddonShow(AddonEvent type, AddonArgs args)
    {
        //If we are not already in first person and not in a cutscene, then we will switch to first person 
        if (!FirstPerson && !InCutscene)
        {
            ControlMode = GameCamera->ControlMode;
            ZoomMode = GameCamera->ZoomMode;
            
            GameCamera->ControlMode = CameraControlMode.LockonFirstPerson;
            GameCamera->ZoomMode = CameraZoomMode.FirstPerson;

            //press the scroll key to hide the UI, since we are now in first person mode
            SetKeyValue(VirtualKey.SCROLL, KeyStateFlags.Pressed);
        }
    }

    private unsafe void OnTalkAddonClose(AddonEvent type, AddonArgs args)
    {
        //If we are in first person and not in a cutscene, then we will switch back to the previous camera mode
        if (FirstPerson && !InCutscene)
        {
            GameCamera->ControlMode = ControlMode;
            GameCamera->ZoomMode = ZoomMode;

            //press the scroll key to unhide the UI
            SetKeyValue(VirtualKey.SCROLL, KeyStateFlags.Pressed);
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