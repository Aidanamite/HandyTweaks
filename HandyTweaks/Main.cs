using BepInEx;
using ConfigTweaks;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using BepInEx.Configuration;
using UnityEngine.EventSystems;
using Unity.Collections;
using BepInEx.Logging;
using System.IO;
using TMPro;


namespace HandyTweaks
{
    [BepInPlugin("com.aidanamite.HandyTweaks", "Handy Tweaks", VERSION)]
    public class Main : BaseUnityPlugin
    {
        public const string VERSION = "1.6.1";

        [ConfigField(Description = "Automatically does all available actions on the farm when pressed")]
        public static KeyCode DoFarmStuff = KeyCode.KeypadMinus;
        [ConfigField(Description = "Allow the automatic farm actions to spend gems to speed things up")]
        public static bool AutoSpendFarmGems = false;
        [ConfigField(Description = "Allow the automatic farm actions to speed things up without spending gems")]
        public static bool BypassFarmGemCosts = false;
        [ConfigField(Description = "Automatically does all available actions on the farm every 0.2 seconds")]
        public static bool DoFarmStuffOnTimer = false;
        [ConfigField(Description = "Disables collision checks when placing stuff in your farms or rooms")]
        public static bool CanPlaceAnywhere = false;
        [ConfigField(Description = "Automatically selects the correct answer for quizzes and trivia thoughout the game.")]
        public static bool SkipTrivia = false;
        [ConfigField(Description = "While held, equipping a different item on your viking will not change the model on your character")]
        public static KeyCode DontApplyGeometry = KeyCode.LeftShift;
        [ConfigField(Description = "While held, equipping a different item on your viking will not change the texture on your character")]
        public static KeyCode DontApplyTextures = KeyCode.LeftAlt;
        [ConfigField(Description = "Sorts the dragon select for stable quests by the dragon's effectiveness for the mission")]
        public static bool SortStableQuestDragonsByValue = false;
        [ConfigField(Description = "Makes items show their racing stat benefits along side their combat stats")]
        public static bool ShowRacingEquipmentStats = false;
        [ConfigField(Description = "Disables the limit on how far you can zoom out")]
        public static bool InfiniteZoom = false;
        [ConfigField(Description = "Multiplies the speed that you zoom in and out")]
        public static float ZoomSpeed = 1;
        [ConfigField(Description = "Makes it so your dragon doesn't unequip its selected skin when you modify the colours")]
        public static bool DisableDragonAutomaticSkinUnequip = true;
        [ConfigField(Description = "Allows you to use name and colour customization on special dragons like Toothless, Lightfury and the Nightlights")]
        public static bool AllowCustomizingSpecialDragons = false;
        [ConfigField(Description = "You shouldn't need to enable this option but it's available just in case")]
        public static bool DisableNameCustomizationPatch = false;
        [ConfigField(Description = "Increases the % chance of stable quest success")]
        public static int StableQuestChanceBoost = 0;
        [ConfigField(Description = "Multiplies the stable quest effectiveness of all dragons")]
        public static float StableQuestDragonValueMultiplier = 1;
        [ConfigField(Description = "Multiplies how long a stable quest takes to complete")]
        public static float StableQuestTimeMultiplier = 1;
        [ConfigField(Description = "Increase the character limit on all input boxes")]
        public static bool BiggerInputBoxes = true;
        [ConfigField(Description = "Removes most restrictions on what characters you're allowed to use in your viking's and dragon's names")]
        public static bool MoreNameFreedom = true;
        [ConfigField(Description = "Makes it so you can hold the fireball key to fire as fast as your firerate allows")]
        public static bool AutomaticFireballs = true;
        [ConfigField(Description = "Makes it so your dragons always have maximum happiness")]
        public static bool AlwaysMaxHappiness = false;
        [ConfigField(Description = "Use to disable the \"happy particles\" of specific dragon species")]
        public static Dictionary<string, bool> DisableHappyParticles = new Dictionary<string, bool>();
        [ConfigField(Description = "Makes it so flight suit wings are always visible even when not gliding")]
        public static bool AlwaysShowArmourWings = false;
        [ConfigField(Description = "Changes the mode and visibility of the custom colour picker")]
        public static ColorPickerMode CustomColorPickerMode = ColorPickerMode.RGBHSL;
        [ConfigField(Description = "Disables the max number of items/dragons you're allowed to buy in the store. You can have as many Toothless' as you want :)")]
        public static bool RemoveItemBuyLimits = false;
        [ConfigField(Description = "Makes it so all text in the game will respect formatting flags")]
        public static bool ForceTextTagRendering = true;
        [ConfigField(Description = "Makes it so ForceTextTagRendering will also affect text inputs")]
        public static bool ForceTagsForInputs = false;
        [ConfigField(Description = "Makes it so you have infinite oxygen in any of the underwater sections")]
        public static bool InfiniteOxygen = false;
        [ConfigField(Description = "Makes it so you are always in range of NPCs in the specified mission types")]
        public static RangeType RangeMissionAlwaysInRange = RangeType.None;
        [ConfigField(Description = "Makes it so if the NPC to check the range of is missing, the mission can be completed anyway")]
        public static RangeType ForceRangeMissionCompleteWithoutNPC = RangeType.Escort | RangeType.Follow | RangeType.Chase;
        [ConfigField(Description = "Allows you to place as many items as you can in the farm and room builders")]
        public static bool InfiniteCreativity = true;
        [ConfigField(Description = "Disables all fog visuals")]
        public static bool DisableFog = false;
        [ConfigField(Description = "Makes it so when renaming your viking or dragon, the input box starts with your current name entered")]
        public static bool KeepPreviousOnRename = true;

        [ConfigField(Description = "Will check all mods to see if they have an update available and tell you what the latest version is")]
        public static bool CheckForModUpdates = true;
        [ConfigField(Description = "The max time (in seconds) to wait for an update check to process before assuming the page is not available")]
        public static int UpdateCheckTimeout = 60;
        [ConfigField(Description = "The max number of update checks to try and run at once")]
        public static int MaxConcurrentUpdateChecks = 4;
        [ConfigField(Description = "Change the folder where the game saves its bundle cache to")]
        public static string OverrideBundleCacheLocation = "";
        [ConfigField(Description = "Will make the game clear all of its cache locations. This setting will revert to false when this happens")]
        public static bool ClearBundleCache = false;
        [ConfigField(Description = "Controls how long (in seconds) each item in the cache will be kept before being redownloaded. Max: 12960000 (150 days)")]
        public static int CacheExpiration = 2592000;
        [ConfigField(Description = "Fixes the expiration date updates every time the game uses the cache instead of only when downloading a new cache")]
        public static bool FixCacheExpiration = true;

        [ConfigField(Description = "PLACEHOLDER")]
        public static int PlayerReporterResolutionOverride = -1;
        [ConfigField(Description = "PLACEHOLDER")]
        public static int MaxRecordingFPS = 30;
        [ConfigField(Description = "PLACEHOLDER (seconds)")]
        public static float MaxRecordingLength = 2;

        public static Main instance;
        public static ManualLogSource logger;
        static List<(BaseUnityPlugin, string)> updatesFound = new List<(BaseUnityPlugin, string)>();
        static ConcurrentDictionary<WebRequest,bool> running = new ConcurrentDictionary<WebRequest, bool>();
        static int currentActive;
        static bool seenLogin = false;
        static GameObject waitingUI;
        static TMP_Text waitingText;
        float waitingTime;
        public void Awake()
        {
            instance = this;
            Config.ConfigReloaded += OnConfigLoad;
            OnConfigLoad();
            using (var s = typeof(Main).Assembly.GetManifestResourceStream("HandyTweaks.handytweaks"))
            {
                var b = AssetBundle.LoadFromStream(s);
                Instantiate(b.LoadAsset<GameObject>("UIManager"));
                if (CheckForModUpdates)
                {
                    var ui = Instantiate(b.LoadAsset<GameObject>("ModUpdateUI").GetComponent<UpdateCheckUI>());
                    DontDestroyOnLoad(waitingUI = ui.gameObject);
                    waitingText = ui.label;
                    waitingText.text = "Checking for mod updates (??? remaining)";
                    foreach (var plugin in Resources.FindObjectsOfTypeAll<BaseUnityPlugin>())
                        CheckModVersion(plugin);
                }
                b.Unload(false);
            }
            new Harmony("com.aidanamite.HandyTweaks").PatchAll();
            Logger.LogInfo("Loaded");
        }

        void OnConfigLoad(object sender = null, EventArgs args = null)
        {
            if (!RemoveItemBuyLimits && Patch_SetStoreItemData.originalMaxes.Count != 0)
            {
                foreach (var s in ItemStoreDataLoader.GetAllStores())
                    foreach (var d in s._Items)
                        if (Patch_SetStoreItemData.originalMaxes.TryGetValue(d.ItemID, out var orig))
                            d.InventoryMax = orig;
                Patch_SetStoreItemData.originalMaxes.Clear();
            }
            else if (RemoveItemBuyLimits)
            {
                foreach (var s in ItemStoreDataLoader.GetAllStores())
                    Patch_SetStoreItemData.Postfix(s);
            }
            ColorPicker.TryUpdateSliderVisibility();
            var prev = PlayerPrefs.GetString("AIDANAMITEHANDYTWEAKSMOD_PREVCACHE", "");
            if (!PathEquals(prev, OverrideBundleCacheLocation))
            {
                if (!string.IsNullOrEmpty(OverrideBundleCacheLocation))
                    ChangeCache(OverrideBundleCacheLocation);
                if (!string.IsNullOrEmpty(prev))
                {
                    if (GetCache(prev,out var prevC,false))
                        Caching.RemoveCache(prevC);
                    try
                    {
                        if (Directory.Exists(prev))
                            Directory.Delete(prev, true);
                    }
                    catch { }
                }
                PlayerPrefs.GetString("AIDANAMITEHANDYTWEAKSMOD_PREVCACHE", OverrideBundleCacheLocation);
            }
            if (ClearBundleCache)
            {
                Caching.ClearCache();
                ClearBundleCache = false;
                Config.Save();
            }
            for (int i = 0; i < Caching.cacheCount; i++)
            {
                var cache = Caching.GetCacheAt(i);
                cache.expirationDelay = CacheExpiration;
            }
            if (ForceTextTagRendering)
            {
                foreach (var lbl in FindObjectsOfType<UILabel>())
                    Patch_ForceEnableEncoding.Prefix(lbl);
                foreach (var lbl in FindObjectsOfType<TMP_Text>())
                    lbl.text = lbl.text;
                foreach (var lbl in FindObjectsOfType<TextMesh>())
                    if (lbl.text.SoDtoUnityRich(out var txt, lbl.color))
                    {
                        lbl.richText = true;
                        lbl.text = txt;
                    }
            }
        }

        static ConditionalWeakTable<TextMesh, object> textCache = new ConditionalWeakTable<TextMesh, object>();

        public static void ChangeCache(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            GetCache(path, out var cache);
            Caching.currentCacheForWriting = cache;
        }

        public static bool GetCache(string path, out Cache cache, bool createIfMissing = true)
        {
            var c = Caching.cacheCount;
            for (int i = 0; i < c; i++)
            {
                var c2 = Caching.GetCacheAt(i);
                if (PathEquals(path, c2.path))
                {
                    cache = c2;
                    return true;
                }
            }
            if (createIfMissing)
            {
                cache = Caching.AddCache(path);
                return true;
            }
            cache = default;
            return false;
        }

        public static bool PathEquals(string a, string b) => string.Equals(GetFullPathSafe(a), GetFullPathSafe(b), StringComparison.OrdinalIgnoreCase);

        public static string GetFullPathSafe(string path)
        {
            try
            {
                return new FileInfo(path).FullName;
            } catch
            {
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        bool CanStartCheck()
        {
            if (currentActive < MaxConcurrentUpdateChecks)
            {
                currentActive++;
                return true;
            }
            return false;
        }
        void CheckStopped() => currentActive--;
        public async void CheckModVersion(BaseUnityPlugin plugin)
        {
            string url = null;
            bool isGit = true;
            var f = plugin.GetType().GetField("UpdateUrl", ~BindingFlags.Default);
            if (f != null)
            {
                var v = f.GetValue(plugin);
                if (v is string s)
                {
                    url = s;
                    isGit = false;
                }
            }
            f = plugin.GetType().GetField("GitKey", ~BindingFlags.Default);
            if (f != null)
            {
                var v = f.GetValue(plugin);
                if (v is string s)
                    url = "https://api.github.com/repos/" + s + "/releases/latest";
            }
            if (url == null)
            {
                var split = plugin.Info.Metadata.GUID.Split('.');
                if (split.Length >= 2)
                {
                    if (split[0] == "com" && split.Length >= 3)
                        url = $"https://api.github.com/repos/{split[1]}/{split[split.Length - 1]}/releases/latest";
                    else
                        url = $"https://api.github.com/repos/{split[0]}/{split[split.Length - 1]}/releases/latest";
                }
            }
            if (url == null)
            {
                Logger.LogInfo($"No update url found for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})");
                return;
            }
            var request = WebRequest.CreateHttp(url);
            request.Timeout = UpdateCheckTimeout * 1000;
            request.UserAgent = "SoDMod-HandyTweaks-UpdateChecker-" + plugin.Info.Metadata.GUID;
            request.Accept = isGit ? "application/vnd.github+json" : "raw";
            request.Method = "GET";
            running[request] = true;
            try
            {
                while (!CanStartCheck())
                    await System.Threading.Tasks.Task.Delay(100);
                using (var req = request.GetResponseAsync())
                {
                    await req;
                    if (req.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        var res = req.Result;
                        var v = isGit ? res.GetJsonEntry("tag_name") : res.ReadContent();
                        if (string.IsNullOrEmpty(v))
                            Logger.LogInfo($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: Responce was null");
                        if (Version.TryParse(v, out var newVersion))
                        {
                            if (plugin.Info.Metadata.Version == newVersion)
                                Logger.LogInfo($"{plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID}) is up-to-date");
                            else if (plugin.Info.Metadata.Version > newVersion)
                                Logger.LogInfo($"{plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID}) is newer than the latest release. Release is {newVersion}, current is {plugin.Info.Metadata.Version}");
                            else
                            {
                                Logger.LogInfo($"{plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID}) has an update available. Latest is {newVersion}, current is {plugin.Info.Metadata.Version}");
                                updatesFound.Add((plugin, newVersion.ToString()));
                            }
                        }
                        else
                            Logger.LogInfo($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: Responce could not be parsed {(v.Length > 100 ? $"\"{v.Remove(100)}...\" (FullLength={v.Length})" : $"\"{v}\"")}");
                    }
                    else
                        Logger.LogInfo($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: No responce");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: {e.GetType().FullName}: {e.Message}");
                if (!(e is WebException))
                    Logger.LogError(e);
            } finally
            {
                CheckStopped();
                running.TryRemove(request, out _);
            }
        }

        float timer;
        public void Update()
        {
            //if (Camera.main)
            //    Camera.main.gameObject.GetOrAddComponent<ReportManager>();
            if (!seenLogin && UiLogin.pInstance)
                seenLogin = true;
            if (running != null && running.Count == 0 && seenLogin)
            {
                running = null;
                Destroy(waitingUI);
                if (updatesFound.Count == 1)
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", $"Mod {updatesFound[0].Item1.Info.Metadata.Name} has an update available\nCurrent: {updatesFound[0].Item1.Info.Metadata.Version}\nLatest: {updatesFound[0].Item2}", null, "");
                else if (updatesFound.Count > 1)
                {
                    var s = new StringBuilder();
                    s.Append(updatesFound.Count);
                    s.Append(" mod updates available:");
                    for (int i = 0; i < updatesFound.Count; i++)
                    {
                        s.Append("\n");
                        if (i == 4)
                        {
                            s.Append("(");
                            s.Append(updatesFound.Count - 4);
                            s.Append(" more) ...");
                            break;
                        }
                        s.Append(updatesFound[i].Item1.Info.Metadata.Name);
                        s.Append(" ");
                        s.Append(updatesFound[i].Item1.Info.Metadata.Version);
                        s.Append(" > ");
                        s.Append(updatesFound[i].Item2);
                    }
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", s.ToString(), null, "");
                }
            }
            if ((timer -= Time.deltaTime) <= 0 && (Input.GetKeyDown(DoFarmStuff) || DoFarmStuffOnTimer) && MyRoomsIntMain.pInstance is FarmManager f)
            {
                timer = 0.2f;
                foreach (var i in Resources.FindObjectsOfTypeAll<FarmItem>())
                    if (i && i.gameObject.activeInHierarchy && i.pCurrentStage != null && !i.mIsWaitingForWsCall)
                    {
                        if (i is CropFarmItem c)
                        {
                            if (c.pCurrentStage._Name == "NoInteraction")
                            {
                                if (BypassFarmGemCosts)
                                    c.GotoNextStage();
                                else if (AutoSpendFarmGems && c.CheckGemsAvailable(c.GetSpeedupCost()))
                                    c.GotoNextStage(true);
                            }
                            else
                                c.GotoNextStage();
                        }
                        else if (i is FarmSlot s)
                        {
                            if (!s.IsCropPlaced())
                            {
                                var items = CommonInventoryData.pInstance.GetItems(s._SeedsCategory);
                                if (items != null)
                                    foreach (var seed in items)
                                        if (seed != null && seed.Quantity > 0)
                                            s.OnContextAction(seed.Item.ItemName);
                                break;
                            }
                        }
                        else if (i is AnimalFarmItem a)
                        {
                            if (a.pCurrentStage._Name.Contains("Feed"))
                            {
                                a.ConsumeFeed();
                                if (a.IsCurrentStageFeedConsumed())
                                    a.GotoNextStage(false);
                            }
                            else if (a.pCurrentStage._Name.Contains("Harvest"))
                                a.GotoNextStage(false);
                            else
                            {
                                if (BypassFarmGemCosts)
                                    a.GotoNextStage();
                                else if (AutoSpendFarmGems && a.CheckGemsAvailable(a.GetSpeedupCost()))
                                    a.GotoNextStage(true);
                            }
                        }
                        else if (i is ComposterFarmItem d)
                        {
                            if (d.pCurrentStage._Name.Contains("Harvest"))
                                d.GotoNextStage();
                            else if (d.pCurrentStage._Name.Contains("Feed"))
                                foreach (var consumable in d._CompostConsumables)
                                    if (consumable != null)
                                    {
                                        var userItemData = CommonInventoryData.pInstance.FindItem(consumable.ItemID);
                                        if (userItemData != null && consumable.Amount <= userItemData.Quantity)
                                        {
                                            d.mCurrentUsedConsumableCriteria = consumable;
                                            d.GotoNextStage(false);
                                            break;
                                        }
                                    }
                            
                        }
                        else if (i is FishTrapFarmItem t)
                        {
                            if (t.pCurrentStage._Name.Contains("Harvest"))
                                t.GotoNextStage();
                            else if (t.pCurrentStage._Name.Contains("Feed"))
                                foreach (var consumable in t._FishTrapConsumables)
                                    if (consumable != null)
                                    {
                                        var userItemData = CommonInventoryData.pInstance.FindItem(consumable.ItemID);
                                        if (userItemData != null && consumable.Amount <= userItemData.Quantity)
                                        {
                                            t.mCurrentUsedConsumableCriteria = consumable;
                                            t.GotoNextStage(false);
                                            break;
                                        }
                                    }
                        }
                    }
            }
            waitingTime += Time.deltaTime;
            if (waitingText)
            {
                var t = $"Checking for mod updates ({running?.Count ?? 0} remaining)";
                var s = new StringBuilder();
                for (int i = 0; i < t.Length; i++)
                {
                    s.Append("<color=#");
                    s.Append(ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(0, 0, (float)(Math.Sin((i / (double)t.Length - waitingTime) * Math.PI * 2) / 4 + 0.75))));
                    s.Append(">");
                    s.Append(t[i]);
                    s.Append("</color>");
                }
                waitingText.text = s.ToString();
            }
            if (AlwaysMaxHappiness && SanctuaryManager.pCurPetInstance)
            {
                var cur = SanctuaryManager.pCurPetInstance.GetPetMeter(SanctuaryPetMeterType.HAPPINESS).mMeterValData.Value;
                var max = SanctuaryData.GetMaxMeter(SanctuaryPetMeterType.HAPPINESS, SanctuaryManager.pCurPetInstance.pData);
                if (cur < max)
                    SanctuaryManager.pCurPetInstance.UpdateMeter(SanctuaryPetMeterType.HAPPINESS, max - cur);
            }

            if (ForceTextTagRendering)
            {
                textMeshInterval -= Time.deltaTime;
                if (textMeshInterval <= 0)
                {
                    textMeshInterval = 0.2f;
                    foreach (var lbl in FindObjectsOfType<TextMesh>())
                        if (!textCache.TryGetValue(lbl, out var oldTxt) || oldTxt != (object)lbl.text)
                        {
                            if (lbl.text.SoDtoUnityRich(out var txt, lbl.color))
                            {
                                lbl.richText = true;
                                lbl.text = txt;
                            }
                            textCache.Remove(lbl);
                            textCache.Add(lbl, lbl.text);
                        }
                }
            }
        }
        static float textMeshInterval;

        public static void TryFixUsername()
        {
            var s = AvatarData.pInstance.DisplayName;
            var modified = false;
            var builder = new StringBuilder(s.Length);
            foreach (var c in s)
                builder.Append(Patch_CanInput.replace.TryGetValue(c, out var nc) && (modified = true) ? nc : c);
            var state = new RichTextState(Color.white);
            for (int i = 0; i < s.Length;)
                if (!state.ParseSymbol(s, ref i))
                    i++;
            if (state.bold && (modified = true))
                builder.Append("[/b]");
            if (state.italic && (modified = true))
                builder.Append("[/i]");
            if (state.under && (modified = true))
                builder.Append("[/u]");
            if (state.strike && (modified = true))
                builder.Append("[/s]");
            if (state.sub != 0 && (modified = true))
                builder.Append("[/sub]");
            for (int i = state.colors.size - 1; i > 1 && (modified = true); i--)
                builder.Append("[-]");
            if (state.ignore && (modified = true))
                builder.Append("[/c]");
            if (modified)
            {
                s = builder.ToString();
                WsWebService.SetDisplayName(new SetDisplayNameRequest
                {
                    DisplayName = s,
                    ItemID = 0,
                    StoreID = 0
                }, (a, b, c, d, e) =>
                {
                    if (b == WsServiceEvent.COMPLETE)
                    {
                        SetAvatarResult setAvatarResult = (SetAvatarResult)d;
                        if (setAvatarResult.Success)
                        {
                            AvatarData.SetDisplayName(s);
                            UserInfo.pInstance.Username = s;
                        }
                    }
                }, null);
            }
        }

        void OnPopupClose()
        {
            AvAvatar.pState = AvAvatarState.IDLE;
            AvAvatar.SetUIActive(true);
        }

        static Dictionary<string, (PetStatType, string, string)> FlightFieldToType = new Dictionary<string, (PetStatType, string, string)>
        {
            { "_RollTurnRate",(PetStatType.TURNRATE,"TRN","") },
            { "_PitchTurnRate",(PetStatType.PITCHRATE,"PCH", "") },
            { "_Acceleration",(PetStatType.ACCELERATION,"ACL", "") },
            { "_Speed",(PetStatType.MAXSPEED,"FSP", "Pet ") }
        };
        static Dictionary<string, (string, string)> PlayerFieldToType = new Dictionary<string, (string, string)>
        {
            { "_MaxForwardSpeed",("Walk Speed","WSP") },
            { "_Gravity",("Gravity","GRV") },
            { "_Height",("Height","HGT") },
            { "_PushPower",("Push Power","PSH") }
        };
        static Dictionary<SanctuaryPetMeterType, (string,string)> MeterToName = new Dictionary<SanctuaryPetMeterType, (string, string)>
        {
            { SanctuaryPetMeterType.ENERGY, ("Energy","NRG") },
            { SanctuaryPetMeterType.HAPPINESS, ("Happiness","HAP") },
            { SanctuaryPetMeterType.HEALTH, ("Health","DHP") },
            { SanctuaryPetMeterType.RACING_ENERGY, ("Racing Energy","RNR") },
            { SanctuaryPetMeterType.RACING_FIRE, ("Racing Fire","RFR") }
        };
        static Dictionary<string, CustomStatInfo> statCache = new Dictionary<string, CustomStatInfo>();
        public static CustomStatInfo GetCustomStatInfo(string AttributeName)
        {
            if (AttributeName == null)
                return null;
            if (!statCache.TryGetValue(AttributeName, out var v))
            {
                var name = AttributeName;
                var abv = "???";
                var found = false;
                if (AttributeName.TryGetAttributeField(out var field))
                {
                    if (FlightFieldToType.TryGetValue(field, out var type))
                    {
                        found = true;
                        name = type.Item3 + SanctuaryData.GetDisplayTextFromPetStat(type.Item1);
                        abv = type.Item2;
                    }
                    else if (PlayerFieldToType.TryGetValue(field,out var type3))
                    {
                        found = true;
                        (name, abv) = type3;
                    }
                }
                if (!found && Enum.TryParse<SanctuaryPetMeterType>(AttributeName, true, out var type2) && MeterToName.TryGetValue(type2, out var meterName))
                {
                    found = true;
                    (name, abv) = meterName;
                }
                statCache[AttributeName] = v = new CustomStatInfo(AttributeName,name,abv,found);
            }
            return v;
        }

        public static int GemCost;
        public static int CoinCost;
        public static List<ItemData> Buying;
        public void ConfirmBuyAll()
        {
            if ( GemCost > Money.pCashCurrency || CoinCost > Money.pGameCurrency)
            {
                GameUtilities.DisplayOKMessage("PfKAUIGenericDB", GemCost > Money.pCashCurrency ? CoinCost > Money.pGameCurrency ? "Not enough gems and coins" : "Not enough gems" : "Not enough coins", null, "");
                return;
            }
            foreach (var i in Buying)
                CommonInventoryData.pInstance.AddPurchaseItem(i.ItemID, 1, "HandyTweaks.BuyAll");
            KAUICursorManager.SetExclusiveLoadingGear(true);
            CommonInventoryData.pInstance.DoPurchase(0,0,x =>
            {
                KAUICursorManager.SetExclusiveLoadingGear(false);
                GameUtilities.DisplayOKMessage("PfKAUIGenericDB", x.Success ? "Purchase complete" : "Purchase failed", null, "");
                if (x.Success)
                    KAUIStore.pInstance.pChooseMenu.ChangeCategory(KAUIStore.pInstance.pFilter, true);

            });
        }

        public void DoNothing() { }

        public void CancelDestroyDragon()
        {
            destroyCard = default;
            destroyCount = 0;
        }

        static Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (x, y) =>
            {
                var n = new AssemblyName(y.Name);
                if (n.Name.StartsWith("Assembly-CSharq",StringComparison.OrdinalIgnoreCase))
                {
                    n.Name = new StringBuilder(n.Name) { [14] = 'p' }.ToString();
                    return AppDomain.CurrentDomain.Load(n);
                }
                var s = typeof(Main).Assembly.GetManifestResourceStream("HandyTweaks." + n.Name + ".dll");
                if (s != null)
                {
                    try
                    {
                        var data = new byte[s.Length];
                        s.Read(data, 0, data.Length);
                        return Assembly.Load(data);
                    }
                    finally
                    {
                        s.Dispose();
                    }
                }
                return null;
            };
            if (!TomlTypeConverter.CanConvert(typeof(Dictionary<string, bool>)))
                TomlTypeConverter.AddConverter(typeof(Dictionary<string, bool>), new TypeConverter()
                {
                    ConvertToObject = (str, type) =>
                    {
                        var d = new Dictionary<string, bool>();
                        if (str == null)
                            return d;
                        var split = str.Split('|');
                        foreach (var i in split)
                            if (i.Length != 0)
                            {
                                var parts = i.Split(',');
                                if (parts.Length != 2)
                                    Debug.LogWarning($"Could not load entry \"{i}\". Entries must have exactly 2 values divided by commas");
                                else
                                {
                                    if (d.ContainsKey(parts[0]))
                                        Debug.LogWarning($"Duplicate entry name \"{parts[0]}\" from \"{i}\". Only last entry will be kept");
                                    var value = false;
                                    if (bool.TryParse(parts[1], out var v))
                                            value = v;
                                        else
                                            Debug.LogWarning($"Value \"{parts[1]}\" in \"{i}\". Could not be parsed as a bool");
                                    d[parts[0]] = value;
                                }
                            }
                        return d;
                    },
                    ConvertToString = (obj, type) =>
                    {
                        if (!(obj is Dictionary<string, bool> d))
                            return "";
                        var str = new StringBuilder();
                        var k = d.Keys.ToList();
                        k.Sort();
                        foreach (var key in k)
                        {
                            if (str.Length > 0)
                                str.Append("|");
                            str.Append(key);
                            str.Append(",");
                            str.Append(d[key].ToString(CultureInfo.InvariantCulture));
                        }
                        return str.ToString();
                    }
                });
        }
        const int ReAskCount = 2;
        public static void TryDestroyDragon(UiDragonsInfoCardItem card, Action OnSuccess = null, Action OnFail = null)
        {
            if (destroyCard.ui == card)
                destroyCount++;
            else
            {
                destroyCard = (card,OnSuccess,OnFail);
                destroyCount = 0;
            }
            if (destroyCount <= ReAskCount)
            {
                var str = "Are you";
                for (int i = 0; i < destroyCount; i++)
                    str += " really";
                str += " sure?";
                if (destroyCount == ReAskCount)
                    str += "\n\nThis is your last warning. This really cannot be undone";
                else if (destroyCount == 0)
                    str += "\n\nYou will permanently lose this dragon. This can't be undone";
                else
                    str += "\n\nThis can't be undone";
                GameUtilities.DisplayGenericDB("PfKAUIGenericDB", str, "Release Dragon", instance.gameObject, nameof(ConfirmDestroyDragon), nameof(CancelDestroyDragon), null, null, true);
                return;
            }

            KAUICursorManager.SetDefaultCursor("Loading", true);
            card.pUI.SetState(KAUIState.DISABLED);
            void OnEnd(string message, bool success)
            {
                KAUICursorManager.SetDefaultCursor("Arrow", true);
                card.pUI.SetState(KAUIState.INTERACTIVE);
                destroyCard = default;
                GameUtilities.DisplayOKMessage("PfKAUIGenericDB", message, null, "");
                (success ? OnSuccess : OnFail)?.Invoke();
            }

            IEnumerator DoRemove()
            {
                var originalData = card.pSelectedPetData;
                var petid = originalData.RaisedPetID;
                var itemid = originalData.FindAttrData("TicketID")?.Value;
                if (itemid != null && int.TryParse(itemid, out var realId))
                {
                    var count = 0;
                    var common = 0;
                    if (ParentData.pIsReady)
                        count += ParentData.pInstance.pInventory.GetQuantity(realId);
                    if (CommonInventoryData.pIsReady)
                        count += common = CommonInventoryData.pInstance.GetQuantity(realId);
                    if (count > 0)
                    {
                        var request = new CommonInventoryRequest()
                        {
                            ItemID = realId,
                            Quantity = -1
                        };
                        var state = 0;
                        (common > 0 ? CommonInventoryData.pInstance : ParentData.pInstance.pInventory.pData).RemoveItem(realId, true, 1, (success, _) =>
                        {
                            if (success)
                                state = 1;
                            else
                                state = 2;
                        });
                        while (state == 0)
                            yield return null;
                        if (state == 2)
                        {
                            OnEnd("Failed to release dragon.\nFailed to remove dragon ticket", false);
                            yield break;
                        }
                    }
                }
                WsWebService.SetRaisedPet(new RaisedPetData()
                {
                    RaisedPetID = petid,
                    PetTypeID = 2,
                    IsSelected = false,
                    IsReleased = false,
                    Gender = Gender.Unknown,
                    UpdateDate = DateTime.MinValue
                }, Array.Empty<CommonInventoryRequest>(), (a, b, c, d, e) =>
                {
                    if (b == WsServiceEvent.COMPLETE)
                    {
                        WsWebService.SetRaisedPetInactive(petid, (f, g, h, i, j) =>
                        {
                            if (g != WsServiceEvent.COMPLETE && g != WsServiceEvent.ERROR)
                                return;
                            if (g == WsServiceEvent.COMPLETE && (bool)i)
                            {
                                originalData.RemoveFromActivePet();
                                var nest = StableData.GetByPetID(petid)?.GetNestByPetID(petid);
                                if (nest != null)
                                {
                                    nest.PetID = 0;
                                    StableData.SaveData();
                                }
                                OnEnd("Dragon released", true);
                            }
                            else
                                WsWebService.SetRaisedPet(originalData, Array.Empty<CommonInventoryRequest>(), (k, l, m, n, o) =>
                                {
                                    if (l == WsServiceEvent.COMPLETE)
                                        OnEnd("Failed to release dragon.\nChanges reversed", false);
                                    if (l == WsServiceEvent.ERROR)
                                        OnEnd("Failed to release dragon.\nAlso failed to reverse changes, there may be some unexpected results", false);
                                }, null);
                        }, null);
                    }
                    else if (b == WsServiceEvent.ERROR)
                        OnEnd("Failed to release dragon", false);
                }, null);
                yield break;
            }
            instance.StartCoroutine(DoRemove());
        }
        static int destroyCount = 0;
        static (UiDragonsInfoCardItem ui,Action succ,Action fail) destroyCard;
        public void ConfirmDestroyDragon()
        {
            TryDestroyDragon(destroyCard.ui,destroyCard.succ,destroyCard.fail);
        }
    }
}

public static class ModuleInitializer
{
    public static void Initialize()
    {

        try
        {
            TestHarmony();
        }
        catch
        {
            ErrorMessageHandler.Show("[FF0000]Harmony is not working![FFFFFF]\nTry moving the game to a different folder in your computer");
            throw;
        }
        try
        {
            TestConfigTweaks();
        }
        catch
        {
            ErrorMessageHandler.Show("[FF0000]Required mod missing![FFFFFF]\nConfig Tweaks mod not found");
            throw;
        }
        try
        {
            TestConfigTweaksVersion();
        }
        catch
        {
            ErrorMessageHandler.Show("[FF0000]Required mod outdated![FFFFFF]\nConfig Tweaks mod is outdated");
            throw;
        }
    }

    static void TestConfigTweaks()
    {
        typeof(ConfigFieldAttribute).ToString();
    }

    static void TestConfigTweaksVersion()
    {
        _ = new ConfigFieldAttribute().Description;
    }
    static void TestHarmony()
    {
        new Harmony("com.aidanamite.HandyTweaks_TestPatch").Patch(AccessTools.Method(typeof(ModuleInitializer), nameof(TestHarmony)), new HarmonyMethod(typeof(ModuleInitializer), nameof(_Prefix)));
    }
    static void _Prefix() { }

    class ErrorMessageHandler : MonoBehaviour
    {
        string message;
        Component msgBox;
        void Update()
        {
            if (!msgBox)
                msgBox = GameUtilities.DisplayOKMessage("PfKAUIGenericDB", message, gameObject, nameof(OnOk));
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        void OnOk()
        {
            Application.Quit(0);
        }

        public static void Show(string message)
        {
            var go = new GameObject("ERRMSG");
            DontDestroyOnLoad(go);
            go.AddComponent<ErrorMessageHandler>().message = message;
        }
    }
}