using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DadsEZContainers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public sealed class DadsEZContainersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dadisbored.dadsezcontainers";
        public const string PluginName = "DadsEZContainers";
        public const string PluginVersion = "0.1.0";

        internal static ConfigEntry<bool> ModEnabled;
        internal static ConfigEntry<bool> DebugLogging;
        internal static ConfigEntry<float> ContainerRange;
        internal static ConfigEntry<bool> LeaveOneItem;
        internal static ConfigEntry<string> ResourceCostFormat;
        internal static ConfigEntry<string> ContainerColor;
        internal static ConfigEntry<string> AvailableColor;
        internal static ConfigEntry<string> UnavailableColor;
        internal static ConfigEntry<KeyCode> FillAllModifier;
        internal static ConfigEntry<KeyboardShortcut> ToggleShortcut;
        internal static ConfigEntry<bool> ShowToggleMessage;
        internal static ManualLogSource ModLogger;
        internal static string RulesPath;
        internal static bool PullingSuspended;

        private Harmony _harmony;
        private DateTime _rulesWriteTime;
        private float _rulesCheckAt;

        private void Awake()
        {
            ModLogger = Logger;
            ModEnabled = Config.Bind("General", "Enabled", true, "Enable nearby-container access.");
            DebugLogging = Config.Bind("General", "DebugLogging", false, "Write container-selection details to the BepInEx log.");
            ContainerRange = Config.Bind("Containers", "Range", 20f, "Maximum distance in metres from the player or target station.");
            LeaveOneItem = Config.Bind("Containers", "LeaveOneItem", true, "Keep one matching item in each source container.");
            ResourceCostFormat = Config.Bind("Interface", "ResourceCostFormat", "{0}/{1}", "Requirement format where {0} is available and {1} is required.");
            ContainerColor = Config.Bind("Interface", "ContainerResourceColor", "FFEB04FF", "Requirement color when containers contribute, as RRGGBBAA.");
            AvailableColor = Config.Bind("Interface", "AvailableColor", "FFFFFFFF", "Requirement color when inventory alone is sufficient, as RRGGBBAA.");
            UnavailableColor = Config.Bind("Interface", "UnavailableColor", "FF0000FF", "Requirement color when resources are insufficient, as RRGGBBAA.");
            FillAllModifier = Config.Bind("Keys", "FillAllModifier", KeyCode.LeftShift, "Modifier used to fill fuel and processing stations.");
            ToggleShortcut = Config.Bind("Keys", "ToggleContainerAccess", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt), "Toggle nearby-container access for this client.");
            ShowToggleMessage = Config.Bind("Keys", "ShowToggleMessage", true, "Display the current container-access state after using the toggle shortcut.");

            RulesPath = Path.Combine(Paths.ConfigPath, "Dad_Is_Bored.DadsEZContainers.yml");
            RulesConfig.EnsureFile(RulesPath);
            RulesConfig.Load(RulesPath);
            _rulesWriteTime = File.GetLastWriteTimeUtc(RulesPath);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(DadsEZContainersPlugin).Assembly);
            Logger.LogInfo($"{PluginName} {PluginVersion} initialized for Valheim 1.0.12 container APIs.");
        }

        private void Update()
        {
            if (ToggleShortcut.Value.IsDown())
            {
                PullingSuspended = !PullingSuspended;
                if (ShowToggleMessage.Value && Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                        PullingSuspended ? "Nearby container access: Off" : "Nearby container access: On");
                }
            }

            if (Time.unscaledTime < _rulesCheckAt)
            {
                return;
            }
            _rulesCheckAt = Time.unscaledTime + 1f;
            DateTime current = File.GetLastWriteTimeUtc(RulesPath);
            if (current != _rulesWriteTime)
            {
                RulesConfig.Load(RulesPath);
                _rulesWriteTime = current;
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        internal static bool Active => ModEnabled?.Value == true && !PullingSuspended;

        internal static Color ParseColor(string value, Color fallback)
        {
            return ColorUtility.TryParseHtmlString("#" + (value ?? string.Empty).TrimStart('#'), out Color parsed)
                ? parsed
                : fallback;
        }
    }
}
