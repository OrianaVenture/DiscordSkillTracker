using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using DiscordMessenger;
using ServerSync;

namespace DiscordSkillTracker
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class SkillTracker : BaseUnityPlugin
    {
        private const string PluginGUID = "com.tonyism1.DiscordSkillTracker";
        private const string PluginName = "DiscordSkillTracker";
        private const string PluginVersion = "0.1.2";

        private static readonly ConfigSync configSync = new(PluginGUID)
        { DisplayName = PluginName, CurrentVersion = PluginVersion, MinimumRequiredVersion = PluginVersion };

        private readonly Harmony harmony = new(PluginGUID);
        
        public static readonly ManualLogSource SkillTrackerLogger = BepInEx.Logging.Logger.CreateLogSource(PluginName);

        private static string ConfigFileName = PluginGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static ConfigEntry<string> webhookAddress = null!;
        internal static ConfigEntry<string> botAvatar = null!;
        internal static ConfigEntry<string> botName = null!;

        static string botAvatar1 = "https://thumbs.dreamstime.com/b/scandinavian-viking-design-ancient-decorative-dragon-celtic-style-knot-work-illustration-northern-runes-vector-214616877.jpg";

        private static Dictionary<string, string> customSkillList = new Dictionary<string, string>();

        public void Awake()
        {
            SkillTrackerLogger.LogInfo("Starting up!");
            #region Configuration

            AddConfig("webhookAddress", "General", "The Discord Webhook Address.",
                true, "", ref webhookAddress);
            AddConfig("botAvatar", "General", "Avatar to use for the bot.",
                true, "", ref botAvatar);
            AddConfig("botName", "General", "Name to use for the bot.",
                true, "The Watcher", ref botName);

            if (webhookAddress.Value.IsNullOrWhiteSpace())
            {
                SkillTrackerLogger.LogWarning("No Webhook Address defined.");
            }

            if (botAvatar.Value.IsNullOrWhiteSpace())
            {
                botAvatar.BoxedValue = botAvatar1;
            }

            #endregion

            loadJson();

            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void OnDestroy()
        {
            Config.Save();
            harmony.UnpatchSelf();
        }

        private void AddConfig<T>(string key, string section, string description, bool synced, T value, ref ConfigEntry<T> configEntry)
        {
            string extendedDescription = GetExtendedDescription(description, synced);
            configEntry = Config.Bind(section, key, value, extendedDescription);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synced;
        }

        public string GetExtendedDescription(string description, bool synchronizedSetting)
        {
            return description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]");
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                SkillTrackerLogger.LogInfo("SkillAlert Reloading Config!");
                Config.Reload();
            }
            catch
            {
                SkillTrackerLogger.LogError("SkillAlert Reloading Config FAILED!!");
            }
        }

        private void loadJson()
        {
            try
            {
                using (StreamReader r = new StreamReader(Paths.ConfigPath + Path.DirectorySeparatorChar + "customSkills.json"))
                {
                    string jsonString = r.ReadToEnd();
                    customSkillList = JsonUtility.FromJson<Dictionary<string, string>>(jsonString);
                    SkillTrackerLogger.LogInfo($"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                    SkillTrackerLogger.LogInfo($"XXX                                                       XXX");
                    SkillTrackerLogger.LogInfo($"XXX                  DiscordSkillTracker                  XXX");
                    SkillTrackerLogger.LogInfo($"XXX                    Loaded!  v0.0.2                    XXX");
                    SkillTrackerLogger.LogInfo($"XXX                                                       XXX");
                    SkillTrackerLogger.LogInfo($"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                }
            }
            catch
            {
                SkillTrackerLogger.LogError("Could not load customSkills.json, ensure it is in your config path!");
            }
        }

        public static async Task pushDiscord(string pn, string skn, float skl)
        {
            await Task.Run(() =>
            {
                // Send message to discord
                var rnk = "";
                int col = 2303786;
                if (skl > 90) { rnk = "Master"; col = 15277667; }
                else if (skl > 70) { rnk = "Expert"; col = 11342935; }
                else if (skl > 50) { rnk = "Proficient"; col = 10181046; }
                else if (skl > 30) { rnk = "Competent"; col = 3447003; }
                else if (skl > 15) { rnk = "Advanced Beginner"; col = 5763719; }
                else { rnk = "Novice"; col = 16776960; }

                new DiscordMessage()
                    .SetUsername(botName.Value)
                    .SetAvatar(botAvatar.Value)
                    .AddEmbed()
                        .SetTimestamp(DateTime.Now)
                        .SetTitle("Skill Increase!")
                        .SetDescription($"**{pn}** has grown a {skn} level!  [{skn}: {skl}]")
                        .SetColor(col)
                        .SetFooter($"Rank: {rnk}")
                        .Build()
                        .SendMessage(webhookAddress.Value);
                Task.Delay(100).Wait();
            });
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSkillLevelup))]
        public static class Patch_Player_OnSkillLevelup
        {
            private static void Prefix(Player __instance, Skills.SkillType skill, float level)
            {
                var playerName = __instance.GetPlayerName();
                var skillName = skill.ToString();
                var skillLevel = (float)Math.Floor(level);

                // Check if skillname is a digit, this indicates a custom skill
                if (skillName.All(char.IsDigit))
                {
                    foreach (var item in customSkillList)
                    {
                        string _id = item.Key;
                        string _name = item.Value;

                        if (skillName.Equals(_id))
                        { 
                            skillName = _name; 
                            break; 
                        }
                    }
                }

                pushDiscord(playerName, skillName, skillLevel).ConfigureAwait(false);
            }
        }
    }
}