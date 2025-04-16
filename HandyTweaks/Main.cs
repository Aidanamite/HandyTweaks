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

namespace HandyTweaks
{
    [BepInPlugin("com.aidanamite.HandyTweaks", "Handy Tweaks", "1.5.10")]
    [BepInDependency("com.aidanamite.ConfigTweaks")]
    public class Main : BaseUnityPlugin
    {
        [ConfigField]
        public static KeyCode DoFarmStuff = true;
        [ConfigField]
        public static bool AutoSpendFarmGems = false;
        [ConfigField]
        public static bool BypassFarmGemCosts = true;
        [ConfigField]
        public static bool DoFarmStuffOnTimer = true;
        [ConfigField]
        public static bool CanPlaceAnywhere = true;
        [ConfigField]
        public static bool SkipTrivia = true;
        [ConfigField]
        public static KeyCode DontApplyGeometry = KeyCode.LeftShift;
        [ConfigField]
        public static KeyCode DontApplyTextures = KeyCode.LeftAlt;
        [ConfigField]
        public static bool SortStableQuestDragonsByValue = false;
        [ConfigField]
        public static bool ShowRacingEquipmentStats = false;
        [ConfigField]
        public static bool InfiniteZoom = false;
        [ConfigField]
        public static float ZoomSpeed = 1;
        [ConfigField]
        public static bool DisableDragonAutomaticSkinUnequip = true;
        [ConfigField]
        public static bool AllowCustomizingSpecialDragons = true;
        [ConfigField]
        public static int StableQuestChanceBoost = 0;
        [ConfigField]
        public static float StableQuestDragonValueMultiplier = 1;
        [ConfigField]
        public static float StableQuestTimeMultiplier = 1;
        [ConfigField]
        public static bool BiggerInputBoxes = true;
        [ConfigField]
        public static bool MoreNameFreedom = true;
        [ConfigField]
        public static bool AutomaticFireballs = true;
        [ConfigField]
        public static bool AlwaysMaxHappiness = true;
        [ConfigField]
        public static Dictionary<string, bool> DisableHappyParticles = new Dictionary<string, bool>();
        [ConfigField]
        public static bool AlwaysShowArmourWings = false;
        [ConfigField]
        public static ColorPickerMode CustomColorPickerMode = ColorPickerMode.RGBHSL;
        [ConfigField]
        public static bool RemoveItemBuyLimits = true;
        //[ConfigField]
        //public static bool ForceTextTagRendering = true;
        [ConfigField]
        public static bool CheckForModUpdates = true;
        [ConfigField]
        public static int UpdateCheckTimeout = 60;
        [ConfigField]
        public static int MaxConcurrentUpdateChecks = 4;
        [ConfigField]
        public static string OverrideBundleCacheLocation = "";
        [ConfigField]
        public static bool ClearBundleCache = false;

        public static Main instance;
        public static ManualLogSource logger;
        public static ManualLogSource unityLogger;
        static List<(BaseUnityPlugin, string)> updatesFound = new List<(BaseUnityPlugin, string)>();
        static ConcurrentDictionary<WebRequest,bool> running = new ConcurrentDictionary<WebRequest, bool>();
        static int currentActive;
        static bool seenLogin = false;
        static GameObject waitingUI;
        static RectTransform textContainer;
        static Text waitingText;
        float waitingTime;
        public void Awake()
        {
            instance = this;
            unityLogger = BepInEx.Logging.Logger.CreateLogSource("Unity");
            Application.logMessageReceived += (x, y, z) =>
            {
                if ((z == LogType.Error || z == LogType.Exception) && !x.StartsWith("[Error  :"))
                    unityLogger.LogError(!string.IsNullOrWhiteSpace(y) ? x + '\n' + y : x);
            };
            Config.ConfigReloaded += OnConfigLoad;
            OnConfigLoad();
            if (CheckForModUpdates)
            {
                waitingUI = new GameObject("Waiting UI", typeof(RectTransform));
                var c = waitingUI.AddComponent<Canvas>();
                DontDestroyOnLoad(waitingUI);
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                var s = c.gameObject.AddComponent<CanvasScaler>();
                s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                s.matchWidthOrHeight = 1;
                s.referenceResolution = new Vector2(Screen.width, Screen.height);
                var backing = new GameObject("back", typeof(RectTransform)).AddComponent<Image>();
                backing.transform.SetParent(c.transform, false);
                backing.color = Color.black;
                backing.gameObject.layer = LayerMask.NameToLayer("UI");
                waitingText = new GameObject("text", typeof(RectTransform)).AddComponent<Text>();
                waitingText.transform.SetParent(backing.transform, false);
                waitingText.text = "Checking for mod updates (??? remaining)";
                waitingText.font = Font.CreateDynamicFontFromOSFont("Consolas", 100);
                waitingText.fontSize = 25;
                waitingText.color = Color.white;
                waitingText.alignment = TextAnchor.MiddleCenter;
                waitingText.material = new Material(Shader.Find("Unlit/Text"));
                waitingText.gameObject.layer = LayerMask.NameToLayer("UI");
                waitingText.supportRichText = true;
                textContainer = backing.GetComponent<RectTransform>();
                textContainer.anchorMin = new Vector2(0, 1);
                textContainer.anchorMax = new Vector2(0, 1);
                textContainer.offsetMin = new Vector2(0, -waitingText.preferredHeight - 40);
                textContainer.offsetMax = new Vector2(waitingText.preferredWidth + 40, 0);
                var tT = waitingText.GetComponent<RectTransform>();
                tT.anchorMin = new Vector2(0, 0);
                tT.anchorMax = new Vector2(1, 1);
                tT.offsetMin = new Vector2(20, 20);
                tT.offsetMax = new Vector2(-20, -20);
                foreach (var plugin in Resources.FindObjectsOfTypeAll<BaseUnityPlugin>())
                    CheckModVersion(plugin);
            }
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("HandyTweaks.handytweaks"))
            {
                var b = AssetBundle.LoadFromStream(s);
                ColorPicker.UIPrefab = b.LoadAsset<GameObject>("ColorPicker");
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
            /*if (ForceTextTagRendering)
                foreach (var lbl in FindObjectsOfType<UILabel>())
                    Patch_ForceEnableEncoding.Prefix(lbl);*/
        }

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
            request.UserAgent = "SoDMod-HandyTweaks-UpdateChecker";
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
                    if (i && i.gameObject.activeInHierarchy && i.pCurrentStage != null && !i.IsWaitingForWsCall())
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
                                            d.SetCurrentUsedConsumableCriteria(consumable);
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
                                            t.SetCurrentUsedConsumableCriteria(consumable);
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
                if (waitingTime >= 1)
                {
                    textContainer.offsetMin = new Vector2(0, -waitingText.preferredHeight - 40);
                    textContainer.offsetMax = new Vector2(waitingText.preferredWidth + 40, 0);
                    waitingTime -= 1;
                }
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
        }

        public static void TryFixUsername()
        {
            var s = AvatarData.pInstance.DisplayName;
            foreach (var p in Patch_CanInput.replace)
                s = s.Replace(p.Key, p.Value);
            if (AvatarData.pInstance.DisplayName != s)
                WsWebService.SetDisplayName(new SetDisplayNameRequest
                {
                    DisplayName = s,
                    ItemID = 0,
                    StoreID = 0
                }, (a,b,c,d,e) =>
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
            if ( GemCost > Money.pGameCurrency || CoinCost > Money.pCashCurrency)
            {
                GameUtilities.DisplayOKMessage("PfKAUIGenericDB", GemCost > Money.pGameCurrency ? CoinCost > Money.pCashCurrency ? "Not enough gems and coins" : "Not enough gems" : "Not enough coins", null, "");
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
                            if (g == WsServiceEvent.COMPLETE)
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
                            else if (b == WsServiceEvent.ERROR)
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

    public class CustomStatInfo
    {
        public readonly string AttributeName;
        public readonly string DisplayName;
        public readonly string Abreviation;
        public readonly bool Valid;
        public CustomStatInfo(string Att, string Dis, string Abv, bool Val)
        {
            AttributeName = Att;
            DisplayName = Dis;
            Abreviation = Abv;
            Valid = Val;
        }
    }

    public enum AimMode
    {
        Default,
        MouseWhenNoTargets,
        FindTargetNearMouse,
        AlwaysMouse
    }

    [Flags]
    public enum ColorPickerMode
    {
        Disabled,
        RGB,
        HSL,
        RGBHSL
    }

    static class ExtentionMethods
    {
        static MethodInfo _IsCropPlaced = typeof(FarmSlot).GetMethod("IsCropPlaced", ~BindingFlags.Default);
        public static bool IsCropPlaced(this FarmSlot item) => (bool)_IsCropPlaced.Invoke(item, new object[0]);
        static MethodInfo _OnContextAction = typeof(MyRoomItem).GetMethod("OnContextAction", ~BindingFlags.Default);
        public static void OnContextAction(this MyRoomItem item, string actionName) => _OnContextAction.Invoke(item, new[] { actionName });
        static MethodInfo _IsCurrentStageFeedConsumed = typeof(AnimalFarmItem).GetMethod("IsCurrentStageFeedConsumed", ~BindingFlags.Default);
        public static bool IsCurrentStageFeedConsumed(this AnimalFarmItem item) => (bool)_IsCurrentStageFeedConsumed.Invoke(item, new object[0]);
        static MethodInfo _ConsumeFeed = typeof(AnimalFarmItem).GetMethod("ConsumeFeed", ~BindingFlags.Default);
        public static void ConsumeFeed(this AnimalFarmItem item) => _ConsumeFeed.Invoke(item, new object[0]);
        static FieldInfo _mCurrentUsedConsumableCriteria = typeof(ComposterFarmItem).GetField("mCurrentUsedConsumableCriteria", ~BindingFlags.Default);
        public static void SetCurrentUsedConsumableCriteria(this ComposterFarmItem item, ItemStateCriteriaConsumable consumable) => _mCurrentUsedConsumableCriteria.SetValue(item, consumable);
        static FieldInfo _mCurrentUsedConsumableCriteria2 = typeof(FishTrapFarmItem).GetField("mCurrentUsedConsumableCriteria", ~BindingFlags.Default);
        public static void SetCurrentUsedConsumableCriteria(this FishTrapFarmItem item, ItemStateCriteriaConsumable consumable) => _mCurrentUsedConsumableCriteria2.SetValue(item, consumable);
        static MethodInfo _GetSpeedupCost = typeof(FarmItem).GetMethod("GetSpeedupCost", ~BindingFlags.Default);
        public static int GetSpeedupCost(this FarmItem item) => (int)_GetSpeedupCost.Invoke(item, new object[0]);
        static MethodInfo _CheckGemsAvailable = typeof(FarmItem).GetMethod("CheckGemsAvailable", ~BindingFlags.Default);
        public static bool CheckGemsAvailable(this FarmItem item, int count) => (bool)_CheckGemsAvailable.Invoke(item, new object[] { count });
        static FieldInfo _mIsWaitingForWsCall = typeof(FarmItem).GetField("mIsWaitingForWsCall", ~BindingFlags.Default);
        public static bool IsWaitingForWsCall(this FarmItem item) => (bool)_mIsWaitingForWsCall.GetValue(item);
        static MethodInfo _SaveAndExitQuiz = typeof(UiQuizPopupDB).GetMethod("SaveAndExitQuiz", ~BindingFlags.Default);
        public static void SaveAndExitQuiz(this UiQuizPopupDB item) => _SaveAndExitQuiz.Invoke(item, new object[0]);
        static MethodInfo _CreateDragonWiget = typeof(UiStableQuestDragonsMenu).GetMethod("CreateDragonWiget", ~BindingFlags.Default);
        public static void CreateDragonWiget(this UiStableQuestDragonsMenu menu, RaisedPetData rpData) => _CreateDragonWiget.Invoke(menu, new object[] { rpData });
        static MethodInfo _ShowStatInfo = typeof(UiStatsCompareMenu).GetMethod("ShowStatInfo", ~BindingFlags.Default);
        public static void ShowStatInfo(this UiStatsCompareMenu instance, KAWidget widget, string baseStat, string statName, string compareStat, string diffVal, StatCompareResult compareResult = StatCompareResult.Equal, bool showCompare = false) =>
            _ShowStatInfo.Invoke(instance, new object[] { widget, baseStat, statName, compareStat, diffVal, (int)compareResult, showCompare });
        static FieldInfo _mModifierFieldMap = typeof(AvAvatarController).GetField("mModifierFieldMap", ~BindingFlags.Default);
        public static bool TryGetAttributeField(this string att, out string fieldName)
        {
            if (att != null && _mModifierFieldMap.GetValue(null) is Dictionary<string, string> d)
                return d.TryGetValue(att, out fieldName);
            fieldName = null;
            return false;
        }
        public static string GetAttributeField(this string att) => att.TryGetAttributeField(out var f) ? f : null;
        static FieldInfo _mContentMenuCombat = typeof(UiStatPopUp).GetField("mContentMenuCombat", ~BindingFlags.Default);
        public static KAUIMenu GetContentMenuCombat(this UiStatPopUp item) => (KAUIMenu)_mContentMenuCombat.GetValue(item);
        static FieldInfo _mInventory = typeof(CommonInventoryData).GetField("mInventory", ~BindingFlags.Default);
        public static Dictionary<int, List<UserItemData>> FullInventory(this CommonInventoryData inv) => (Dictionary<int, List<UserItemData>>)_mInventory.GetValue(inv);
        static FieldInfo _mCachedItemData = typeof(KAUIStoreChooseMenu).GetField("mCachedItemData", ~BindingFlags.Default);
        public static Dictionary<ItemData, int> GetCached(this KAUIStoreChooseMenu menu) => (Dictionary<ItemData, int>)_mCachedItemData.GetValue(menu);
        static MethodInfo _RemoveDragonSkin = typeof(UiDragonCustomization).GetMethod("RemoveDragonSkin", ~BindingFlags.Default);
        public static void RemoveDragonSkin(this UiDragonCustomization menu) => _RemoveDragonSkin.Invoke(menu, new object[0]);

        public static string ReadContent(this WebResponse response, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var b = new byte[stream.Length];
                stream.Read(b, 0, b.Length);
                return (encoding ?? Encoding.UTF8).GetString(b);
            }
        }
        public static string GetJsonEntry(this WebResponse response, string key, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas() {  });
                while (reader.Name != key && reader.Read())
                { }
                if (reader.Name == key && reader.Read())
                    return reader.Value;
                return null;
            }
        }

        public static bool IsRankLocked(this ItemData data, out int rid, int rankType)
        {
            rid = 0;
            if (data.RewardTypeID > 0)
                rankType = data.RewardTypeID;
            if (data.Points != null && data.Points.Value > 0)
            {
                rid = data.Points.Value;
                UserAchievementInfo userAchievementInfoByType = UserRankData.GetUserAchievementInfoByType(rankType);
                return userAchievementInfoByType == null || userAchievementInfoByType.AchievementPointTotal == null || rid > userAchievementInfoByType.AchievementPointTotal.Value;
            }
            if (data.RankId != null && data.RankId.Value > 0)
            {
                rid = data.RankId.Value;
                UserRank userRank = (rankType == 8) ? PetRankData.GetUserRank(SanctuaryManager.pCurPetData) : UserRankData.GetUserRankByType(rankType);
                return userRank == null || rid > userRank.RankID;
            }
            return false;
        }

        public static bool HasPrereqItem(this ItemData data)
        {
            if (data.Relationship == null)
                return true;
            ItemDataRelationship[] relationship = data.Relationship;
            foreach (var itemDataRelationship in data.Relationship)
                if (itemDataRelationship.Type == "Prereq")
                    return (ParentData.pIsReady && ParentData.pInstance.HasItem(itemDataRelationship.ItemId)) || CommonInventoryData.pInstance.FindItem(itemDataRelationship.ItemId) != null;
            return true;
        }
        public static Dictionary<string, Color> colorPresets = typeof(Color).GetProperties().Where(x => x.PropertyType == x.DeclaringType && x.GetGetMethod(false)?.IsStatic == true).ToDictionary(x => x.Name.ToLowerInvariant(),x => (Color)x.GetValue(null));
        public static bool TryParseColor(this string clr,out Color color)
        {
            clr = clr.ToLowerInvariant();
            color = default;
            if (colorPresets.TryGetValue(clr,out var v))
                color = v;
            else if (uint.TryParse(clr,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var n))
                color = new Color32((byte)(n / 0x10000 & 0xFF), (byte)(n / 0x100 & 0xFF), (byte)(n & 0xFF), 255);
            else
                return false;
            return true;
        }
        public static string ToHex(this Color32 color) => color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(this Color color) => ((Color32)color).ToHex();
        public static Color Shift(this Color oc, Color nc)
        {
            var s = Math.Max(Math.Max(oc[0], oc[1]), oc[2]);
            return new Color(nc.r * s, nc.g * s, nc.b * s, nc.a * oc.a);
        }
        public static ParticleSystem.MinMaxGradient Shift(this ParticleSystem.MinMaxGradient o, Color newColor)
        {
            if (o.mode == ParticleSystemGradientMode.Color)
                return new ParticleSystem.MinMaxGradient(o.color.Shift(newColor));

            if (o.mode == ParticleSystemGradientMode.Gradient)
                return new ParticleSystem.MinMaxGradient(new Gradient()
                {
                    mode = o.gradient.mode,
                    alphaKeys = o.gradient.alphaKeys,
                    colorKeys = o.gradient.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                });

            if (o.mode == ParticleSystemGradientMode.TwoColors)
                return new ParticleSystem.MinMaxGradient(o.colorMin.Shift(newColor), o.colorMax.Shift(newColor));

            if (o.mode == ParticleSystemGradientMode.TwoGradients)
                return new ParticleSystem.MinMaxGradient(new Gradient()
                {
                    mode = o.gradientMin.mode,
                    alphaKeys = o.gradientMin.alphaKeys,
                    colorKeys = o.gradientMin.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                }, new Gradient()
                {
                    mode = o.gradientMax.mode,
                    alphaKeys = o.gradientMax.alphaKeys,
                    colorKeys = o.gradientMax.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                });

            return o;
        }
        public static List<T> GetRandom<T>(this ICollection<T> c, int count)
        {
            var r = new System.Random();
            var n = c.Count;
            if (count >= n)
                return c.ToList();
            var l = new List<T>(count);
            if (count > 0)
                foreach (var i in c)
                    if (r.Next(n--) < count)
                    {
                        count--;
                        l.Add(i);
                        if (count == 0)
                            break;
                    }
            return l;
        }
        public static Vector2 Rotate(this Vector2 v, float delta, Vector2 center = default)
        {
            if (center != default)
                v -= center;
            v = new Vector2(
                v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
                v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
            );
            if (center != default)
                v += center;
            return v;
        }
        public static bool TryParseColor(this string[] values, out Color result, int start = 0)
        {
            if (values != null
                && values.Length >= start + 3
                && values[start] != null && int.TryParse(values[start], out var r)
                && values[start + 1] != null && int.TryParse(values[start + 1], out var g)
                && values[start + 2] != null && int.TryParse(values[start + 2], out var b))
            {
                result = new Color(r / 255f, g / 255f, b / 255f);
                return true;
            }
            result = default;
            return false;
        }
        public static string JoinValues(this Color c, string delimeter = "$") => (int)Math.Round(c.r * 255.0) + delimeter + (int)Math.Round(c.g * 255.0) + delimeter + (int)Math.Round(c.b * 255.0);
        public static T GetOrAddComponent<T>(this GameObject g) where T : Component => g.GetComponent<T>() ?? g.AddComponent<T>();
        public static Y GetValueOrDefault<X, Y>(this IReadOnlyDictionary<X, Y> d, X key) => d.TryGetValue(key, out var value) ? value : default;

        public static T GetSafe<T>(this IList<T> l, int index, T fallback = default)
        {
            if (index < 0 || index > l.Count)
                return fallback;
            return l[index];
        }
    }
    public enum StatCompareResult
    {
        Equal,
        Greater,
        Lesser
    }

    public abstract class ExtendedClass<X, Y> where X : ExtendedClass<X, Y>, new() where Y : class
    {
        static ConditionalWeakTable<Y, X> table = new ConditionalWeakTable<Y, X>();
        public static X Get(Y instance)
        {
            if (table.TryGetValue(instance, out var v))
            {
                v.OnGet(instance);
                return v;
            }
            v = new X();
            table.Add(instance, v);
            v.OnCreate(instance);
            v.OnGet(instance);
            return v;
        }
        protected virtual void OnCreate(Y instance) { }
        protected virtual void OnGet(Y instance) { }
    }

    [HarmonyPatch(typeof(UiMyRoomBuilder), "Update")]
    static class Patch_RoomBuilder
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].opcode == OpCodes.Stfld && code[i].operand is FieldInfo f && f.Name == "mCanPlace")
                    code.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_RoomBuilder), nameof(EditCanPlace))));
            return code;
        }
        static bool EditCanPlace(bool original) => original || Main.CanPlaceAnywhere;
    }

    [HarmonyPatch(typeof(UiQuizPopupDB))]
    static class Patch_InstantAnswer
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(UiQuizPopupDB __instance, ref bool ___mIsQuestionAttemped, ref bool ___mCheckForTaskCompletion)
        {
            if (Main.SkipTrivia)
            {
                ___mCheckForTaskCompletion = true;
                ___mIsQuestionAttemped = true;
                __instance._MessageObject.SendMessage(__instance._QuizAnsweredMessage, true, SendMessageOptions.DontRequireReceiver);
                __instance.SaveAndExitQuiz();
            }
        }
        [HarmonyPatch("IsQuizAnsweredCorrect")]
        [HarmonyPostfix]
        static void IsQuizAnsweredCorrect(ref bool __result)
        {
            if (Main.SkipTrivia)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    static class Patch_ApplyTexture
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AvatarData), "SetStyleTexture", new[] { typeof(AvatarData.InstanceInfo), typeof(string), typeof(string), typeof(int) });
            yield return AccessTools.Method(typeof(CustomAvatarState), "SetPartTexture");
            yield return AccessTools.Method(typeof(CustomAvatarState), "SetTextureData");
            yield return AccessTools.Method(typeof(UiAvatarCustomizationMenu), "SetPartTextureByIndex");
            yield return AccessTools.Method(typeof(UiAvatarCustomizationMenu), "UpdatePartTexture");
        }
        static bool Prefix() => !Input.GetKey(Main.DontApplyTextures);
    }

    [HarmonyPatch(typeof(AvatarData), "SetGeometry", typeof(AvatarData.InstanceInfo), typeof(string), typeof(string), typeof(int))]
    static class Patch_ApplyGeometry
    {
        static bool Prefix() => !Input.GetKey(Main.DontApplyGeometry);
    }

    [HarmonyPatch(typeof(UiStableQuestDragonsMenu), "LoadDragonsList")]
    static class Patch_LoadStableQuestDragonsList
    {
        static bool Prefix(UiStableQuestDragonsMenu __instance)
        {
            if (!Main.SortStableQuestDragonsByValue)
                return true;
            __instance.ClearItems();
            
            var l = new SortedSet<(float, RaisedPetData)>(new ComparePetValue());
            if (RaisedPetData.pActivePets != null)
                foreach (RaisedPetData[] array in RaisedPetData.pActivePets.Values)
                    if (array != null)
                        foreach (RaisedPetData pet in array)
                            if (StableData.GetByPetID(pet.RaisedPetID) != null && pet.pStage >= RaisedPetStage.BABY && pet.IsPetCustomized())
                                l.Add((TimedMissionManager.pInstance.GetWinProbabilityForPet(UiStableQuestMain.pInstance._StableQuestDetailsUI.pCurrentMissionData, pet.RaisedPetID),pet));
            foreach (var p in l)
                __instance.CreateDragonWiget(p.Item2);
            __instance.pMenuGrid.repositionNow = true;
            return false;
        }
    }

    class ComparePetValue : IComparer<(float, RaisedPetData)>
    {
        public int Compare((float, RaisedPetData) a, (float, RaisedPetData) b)
        {
            var c = b.Item1.CompareTo(a.Item1);
            return c == 0 ? 1 : c;
        }
    }

    [HarmonyPatch]
    static class Patch_ShowFlightCompare
    {
        static UiStatCompareDB.ItemCompareDetails equipped;
        static UiStatCompareDB.ItemCompareDetails unequipped;
        [HarmonyPatch(typeof(UiStatCompareDB), "Initialize")]
        [HarmonyPrefix]
        static void UiStatCompareDB_Initialize(UiStatCompareDB.ItemCompareDetails inLeftItem, UiStatCompareDB.ItemCompareDetails inRightItem)
        {
            equipped = inLeftItem;
            unequipped = inRightItem;
        }
        [HarmonyPatch(typeof(UiStatsCompareMenu), "Populate")]
        [HarmonyPostfix]
        static void UiStatsCompareMenu_Populate(UiStatsCompareMenu __instance, bool showCompare, ItemStat[] equippedStats, ItemStat[] unequippedStats)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            bool shouldClear = !(equippedStats?.Length > 0 || unequippedStats?.Length > 0);
            void Show(string name, string stat1, string stat2)
            {
                if (stat1 == null && stat2 == null)
                    return;
                if (shouldClear)
                {
                    __instance.ClearItems();
                    shouldClear = false;
                }
                KAWidget kawidget2 = __instance.DuplicateWidget(__instance._Template, UIAnchor.Side.Center);
                __instance.AddWidget(kawidget2);
                kawidget2.SetVisibility(true);
                string text = null;
                string text2 = null;
                string diffVal = null;
                var num = 0f;
                var num2 = 0f;
                if (stat1 != null)
                {
                    float.TryParse(stat1, out num);
                    text = Math.Round(num * 100) + "%";
                }
                if (stat2 != null)
                {
                    float.TryParse(stat2, out num2);
                    text2 = Math.Round(num2 * 100) + "%";
                }
                var statCompareResult = (num == num2) ? StatCompareResult.Equal : (num2 > num) ? StatCompareResult.Greater : StatCompareResult.Lesser;
                if (statCompareResult != StatCompareResult.Equal)
                    diffVal = Math.Round(Math.Abs(num - num2) * 100) + "%";
                __instance.ShowStatInfo(kawidget2, text, name, text2, diffVal, statCompareResult, showCompare);
            }
            var s = new SortedSet<string>();
            foreach (var att in new[] { equipped?._ItemData?.Attribute, unequipped?._ItemData?.Attribute })
                if (att != null)
                    foreach (var a in att)
                    {
                        if (a == null || s.Contains(a.Key))
                            continue;
                        var n = Main.GetCustomStatInfo( a.Key);
                        if (n != null && n.Valid)
                            s.Add(a.Key);
                    }
            foreach (var f in s)
                Show(
                    Main.GetCustomStatInfo(f).DisplayName,
                    equipped?._ItemData?.GetAttribute<string>(f, null),
                    unequipped?._ItemData?.GetAttribute<string>(f, null));
        }
        [HarmonyPatch(typeof(UiStoreStatCompare), "UpdateStatsCompareData")]
        [HarmonyPostfix]
        static void UiStoreStatCompare_UpdateStatsCompareData(UiStoreStatCompare __instance, List<UiStoreStatCompare.StatDataContainer> ___mStatDataList, KAUIMenu ___mContentMenu, int previewIndex, List<PreviewItemData> previewList)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            ___mStatDataList.RemoveAll(x => x._EquippedStat == x._ModifiedStat);
            void Show(string name, string abv, float equipped, float unequipped)
            {
                var statDataContainer = new UiStoreStatCompare.StatDataContainer();
                statDataContainer._StatName = name;
                statDataContainer._AbvStatName = abv;
                statDataContainer._EquippedStat = equipped;
                statDataContainer._ModifiedStat = unequipped;
                statDataContainer._DiffStat = statDataContainer._ModifiedStat - statDataContainer._EquippedStat;
                ___mStatDataList.Add(statDataContainer);
                if (equipped != unequipped)
                {
                    var kawidget = ___mContentMenu.AddWidget(___mContentMenu._Template.name);
                    kawidget.FindChildItem("AbvStatWidget", true).SetText(statDataContainer._AbvStatName);
                    kawidget.FindChildItem("StatDiffWidget", true).SetText(Math.Round(Math.Abs(equipped - unequipped)) + "%");
                    var arrowWidget = kawidget.FindChildItem("ArrowWidget", true);
                    arrowWidget.SetVisibility(true);
                    arrowWidget.SetRotation(Quaternion.Euler(0f, 0f, 0f));
                    if (statDataContainer._DiffStat == 0f)
                    {
                        arrowWidget.SetVisibility(false);
                    }
                    else if (statDataContainer._DiffStat < 0f)
                    {
                        arrowWidget.pBackground.color = Color.red;
                        arrowWidget.SetRotation(Quaternion.Euler(0f, 0f, 180f));
                    }
                    else
                    {
                        arrowWidget.pBackground.color = Color.green;
                    }
                    kawidget.SetVisibility(true);
                }
            }
            var s = new SortedSet<string>();
            var d = new Dictionary<string, (float, float)>();
            var e = new Dictionary<string, (ItemData, ItemData)>();
            foreach (var part in AvatarData.pInstance.Part)
                if (part != null)
                {
                    var equipped = part.UserInventoryId > 0 ? CommonInventoryData.pInstance.FindItemByUserInventoryID(part.UserInventoryId.Value)?.Item : null;
                    if (equipped != null)
                    {
                        var key = part.PartType;
                        if (key.StartsWith("DEFAULT_"))
                            key = key.Remove(0, 8);
                        var t = e.GetOrCreate(key);
                        e[key] = (equipped, t.Item2);
                    }
                }
            foreach (var preview in previewIndex == -1 ? previewList as IEnumerable<PreviewItemData> : new[] { previewList[previewIndex] })
                if (preview.pItemData != null)
                {
                    var key = AvatarData.GetPartName(preview.pItemData);
                    if (key.StartsWith("DEFAULT_"))
                        key = key.Remove(0, 8);
                    var t = e.GetOrCreate(key);
                    if (t.Item2 == null)
                        e[key] = (t.Item1, preview.pItemData);
                }
            foreach (var p in e)
            {
                var item2 = p.Value.Item2 ?? p.Value.Item1;
                //Debug.Log($"\n{p.Key}\n - [{p.Value.Item1?.Attribute?.Join(x => x.Key + "=" + x.Value)}]\n - [{item2?.Attribute?.Join(x => x.Key + "=" + x.Value)}]");
                if (p.Value.Item1?.Attribute != null)
                    foreach (var a in p.Value.Item1.Attribute)
                    {
                        if (a == null)
                            continue;
                        var cs = Main.GetCustomStatInfo(a.Key);
                        if (cs == null || !cs.Valid)
                            continue;
                        if (!float.TryParse(a.Value, out var value))
                            continue;
                        s.Add(a.Key);
                        var t = d.GetOrCreate(a.Key);
                        d[a.Key] = (t.Item1 + value, t.Item2);
                    }
                if (item2?.Attribute != null)
                    foreach (var a in item2.Attribute)
                    {
                        if (a == null)
                            continue;
                        var cs = Main.GetCustomStatInfo(a.Key);
                        if (cs == null || !cs.Valid)
                            continue;
                        if (!float.TryParse(a.Value, out var value))
                            continue;
                        s.Add(a.Key);
                        var t = d.GetOrCreate(a.Key);
                        d[a.Key] = (t.Item1, t.Item2 + value);
                    }
            }
            foreach (var i in s)
            {
                var t = d[i];
                var c = Main.GetCustomStatInfo(i);
                if (t.Item1 != t.Item2)
                    Show(c.DisplayName, c.Abreviation, t.Item1 * 100, t.Item2 * 100);
            }
        }

        [HarmonyPatch(typeof(UiAvatarCustomization), "ShowAvatarStats")]
        [HarmonyPostfix]
        static void UiAvatarCustomization_ShowAvatarStats(UiAvatarCustomization __instance, UiStatPopUp ___mUiStats)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            void Show(string name, string value)
            {
                KAWidget kawidget = ___mUiStats.GetContentMenuCombat().AddWidget(___mUiStats.GetContentMenuCombat()._Template.name);
                kawidget.FindChildItem("CombatStatWidget", true).SetText(name);
                kawidget.FindChildItem("CombatStatValueWidget", true).SetText(value);
            }
            var custom = __instance.pCustomAvatar;
            var e = new HashSet<string>();
            var s = new SortedSet<string>();
            var d = new Dictionary<string, float>();
            foreach (var part in AvatarData.pInstance.Part)
                if (part != null)
                {
                    var equipped = custom == null
                        ? part.UserInventoryId > 0
                            ? CommonInventoryData.pInstance.FindItemByUserInventoryID(part.UserInventoryId.Value)?.Item
                            : null
                        : CommonInventoryData.pInstance.FindItemByUserInventoryID(custom.GetInventoryId(part.PartType))?.Item;
                    if (equipped != null)
                    {
                        var key = part.PartType;
                        if (key.StartsWith("DEFAULT_"))
                            key = key.Remove(0, 8);
                        if (!e.Add(key))
                            continue;
                        if (equipped.Attribute != null)
                            foreach (var a in equipped.Attribute)
                            {
                                if (a == null)
                                    continue;
                                var cs = Main.GetCustomStatInfo(a.Key);
                                if (cs == null || !cs.Valid)
                                    continue;
                                if (!float.TryParse(a.Value, out var value))
                                    continue;
                                s.Add(a.Key);
                                d[a.Key] = d.GetOrCreate(a.Key) + value;
                            }
                    }
                }
            foreach (var k in s)
                Show(Main.GetCustomStatInfo(k).DisplayName, Math.Round(d[k] * 100) + "%");
        }
    }

    [HarmonyPatch(typeof(BaseUnityPlugin), MethodType.Constructor, new Type[0])]
    static class Patch_CreatePluginObj
    {
        static void Postfix(BaseUnityPlugin __instance)
        {
            if (Main.CheckForModUpdates)
                Main.instance.CheckModVersion(__instance);
        }
    }

    [HarmonyPatch(typeof(KAUIStore))]
    static class Patch_Store
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(KAUIStore __instance, KAWidget ___mBtnPreviewBuy)
        {
            var n = __instance.DuplicateWidget(___mBtnPreviewBuy, UIAnchor.Side.BottomLeft);
            n.name = "btnBuyAll";
            n.SetText("Buy All");
            n.SetVisibility(true);
            n.SetInteractive(true);
            var p = ___mBtnPreviewBuy.transform.position;
            p.x = -p.x * 0.7f;
            n.transform.position = p;
        }

        [HarmonyPatch("OnClick")]
        [HarmonyPostfix]
        static void OnClick(KAUIStore __instance, KAWidget item)
        {
            if (item.name == "btnBuyAll")
            {
                var byCatergory = CommonInventoryData.pInstance.FullInventory();
                
                var all = new List<ItemData>();
                var check = new HashSet<int>();
                var gems = 0;
                var coins = 0;
                var cache = KAUIStore.pInstance.pChooseMenu.GetCached();
                foreach (var ite in cache.Keys)
                    if (ite != null
                        && !ite.IsBundleItem()
                        && !ite.HasCategory(Category.MysteryBox)
                        && !ite.HasCategory(Category.DragonTickets)
                        && !ite.HasCategory(Category.DragonAgeUp)
                        && (!ite.Locked || SubscriptionInfo.pIsMember)
                        && (__instance.pCategoryMenu.pDisableRankCheck || !ite.IsRankLocked(out _, __instance.pStoreInfo._RankTypeID))
                        && ite.HasPrereqItem()
                        && CommonInventoryData.pInstance.GetQuantity(ite.ItemID) <= 0
                        && check.Add(ite.ItemID))
                    {
                        all.Add(ite);
                        if (ite.GetPurchaseType() == 1)
                            coins += ite.GetFinalCost();
                        else
                            gems += ite.GetFinalCost();
                    }
                if (all.Count == 0)
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", "No items left to buy", null, "");
                else
                {
                    Main.CoinCost = coins;
                    Main.GemCost = gems;
                    Main.Buying = all;
                    GameUtilities.DisplayGenericDB("PfKAUIGenericDB", $"Buying these {Main.Buying.Count} items will cost {(gems > 0 ? coins > 0 ? $"{coins} coins and {gems} gems" : $"{gems} gems" : coins > 0 ? $"{coins} coins" : "nothing")}. Are you sure you want to buy these?", "Buy All", Main.instance.gameObject, nameof(Main.ConfirmBuyAll), nameof(Main.DoNothing), null, null, true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaAvatarCam), "LateUpdate")]
    static class Patch_AvatarCam
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].opcode == OpCodes.Ldfld && code[i].operand is FieldInfo f && f.Name == "mMaxCameraDistance")
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_AvatarCam), nameof(EditMaxZoom))));
                else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 0.25f)
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_AvatarCam), nameof(EditZoomSpeed))));
            return code;
        }
        static float EditMaxZoom(float original) => Main.InfiniteZoom ? float.PositiveInfinity : original;
        static float EditZoomSpeed(float original) => original * Main.ZoomSpeed;
    }

    [HarmonyPatch(typeof(UiDragonCustomization), "RemoveDragonSkin")]
    static class Patch_ChangeDragonColor
    {
        static bool Prefix() => !Main.DisableDragonAutomaticSkinUnequip;
    }

    [HarmonyPatch(typeof(SanctuaryPet), "UpdateShaders")]
    static class Patch_UpdatePetShaders
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Ldloc_S && ((x.operand is LocalBuilder l && l.LocalIndex == 6) || (x.operand is IConvertible i && i.ToInt32(CultureInfo.InvariantCulture) == 6))) + 1,
                new[] 
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_UpdatePetShaders), nameof(EditMat)))
                });
            return code;
        }
        static Material EditMat(Material material, SanctuaryPet pet)
        {
            if (material.HasProperty("_EmissiveColor"))
            {
                var pe = ExtendedPetData.Get(pet.pData);
                if (pe.EmissionColor != null)
                {
                    var e = MaterialEdit.Get(material).OriginalEmissive;
                    material.SetColor("_EmissiveColor", new Color(pe.EmissionColor.Value.r * e.strength, pe.EmissionColor.Value.g * e.strength, pe.EmissionColor.Value.b * e.strength, pe.EmissionColor.Value.a * e.alpha));
                }
                else
                    material.SetColor("_EmissiveColor", MaterialEdit.Get(material).OriginalEmissive.original);
            }
            return material;
        }
    }

    public class MaterialEdit
    {
        static ConditionalWeakTable<Material, MaterialEdit> data = new ConditionalWeakTable<Material, MaterialEdit>();
        public static MaterialEdit Get(Material material)
        {
            if (data.TryGetValue(material, out var edit)) return edit;
            edit = data.GetOrCreateValue(material);
            if (material.HasProperty("_EmissiveColor"))
            {
                var c = material.GetColor("_EmissiveColor");
                edit.OriginalEmissive = (Math.Max(Math.Max(c.r,c.g),c.b),c.a, c);
            }
            return edit;
        }
        public (float strength, float alpha, Color original) OriginalEmissive;
    }

    [HarmonyPatch(typeof(SanctuaryData), "GetPetCustomizationType", typeof(int))]
    static class Patch_PetCustomization
    {
        static bool Prefix(ref PetCustomizationType __result)
        {
            if (Main.AllowCustomizingSpecialDragons)
            {
                __result = PetCustomizationType.Default;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UserNotifyDragonTicket))]
    static class Patch_OpenCloseCustomization
    {
        public static (string, string)? closed;
        [HarmonyPatch("ActivateDragonCreationUIObj")]
        [HarmonyPrefix]
        static void ActivateDragonCreationUIObj()
        {
            if (KAUIStore.pInstance)
            {
                closed = (KAUIStore.pInstance.pCategory, KAUIStore.pInstance.pStoreInfo._Name);
                KAUIStore.pInstance.ExitStore();
            }
        }
        [HarmonyPatch("OnStableUIClosed")]
        [HarmonyPostfix]
        static void OnStableUIClosed()
        {
            if (closed != null)
            {
                var t = closed.Value;
                closed = null;
                StoreLoader.Load(true, t.Item1, t.Item2, null, UILoadOptions.AUTO, "", null);
            }
        }
    }

    [HarmonyPatch(typeof(SanctuaryData), "GetLocalizedPetName")]
    static class Patch_GetPetName
    {
        static void Postfix(RaisedPetData raisedPetData, ref string __result)
        {
            if (__result.Length == 15 && __result.StartsWith("Dragon-") && uint.TryParse(__result.Remove(0, 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                __result = SanctuaryData.GetPetDefaultName(raisedPetData.PetTypeID);
        }
    }

    [HarmonyPatch]
    static class Patch_GetStableQuestDuration
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "HandleAdButtons");
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "StateChangeInit");
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "Update");
            yield return AccessTools.Method(typeof(TimedMissionManager), "CheckMissionCompleted", new[] { typeof(TimedMissionSlotData) });
            yield return AccessTools.Method(typeof(TimedMissionManager), "CheckMissionSuccess");
            yield return AccessTools.Method(typeof(TimedMissionManager), "GetCompletionTime");
            yield return AccessTools.Method(typeof(TimedMissionManager), "GetPetEngageTime");
            yield return AccessTools.Method(typeof(TimedMissionManager), "StartMission");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "HandleAdButton");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "MissionLogIndex");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "SetSlotData");
            yield return AccessTools.Method(typeof(UiStableQuestMissionStart), "RefreshUi");
            yield return AccessTools.Method(typeof(UiStableQuestSlotsMenu), "OnAdWatched");
            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "Duration" && f.DeclaringType == typeof(TimedMission))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetStableQuestDuration).GetMethod(nameof(EditDuration), ~BindingFlags.Default)));
            return code;
        }

        static int EditDuration(int original) => (int)Math.Round(original * Main.StableQuestTimeMultiplier);
    }

    [HarmonyPatch]
    static class Patch_GetStableQuestBaseChance
    {
        static IEnumerable<MethodBase> TargetMethods() => from m in typeof(TimedMissionManager).GetMethods(~BindingFlags.Default) where m.Name == "GetWinProbability" select m;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "WinFactor" && f.DeclaringType == typeof(TimedMission))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetStableQuestBaseChance).GetMethod(nameof(EditChance), ~BindingFlags.Default)));
            return code;
        }

        static int EditChance(int original) => original + Main.StableQuestChanceBoost;
    }

    [HarmonyPatch(typeof(TimedMissionManager), "GetWinProbabilityForPet")]
    static class Patch_GetStableQuestPetChance
    {
        static void Postfix(ref float __result) => __result *= Main.StableQuestDragonValueMultiplier;
    }

    [HarmonyPatch]
    static class Patch_GetInputLength
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(KAUIStoreBuyPopUp), "RefreshValues");
            yield return AccessTools.Method(typeof(UIInput), "Insert");
            yield return AccessTools.Method(typeof(UIInput), "Validate", new[] { typeof(string) });
            yield return AccessTools.Method(typeof(UiItemTradeGenericDB), "RefreshQuantity");
            yield return AccessTools.Method(typeof(UiPrizeCodeEnterDB), "Start");
            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "characterLimit" && f.DeclaringType == typeof(UIInput))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetInputLength).GetMethod(nameof(EditLength), ~BindingFlags.Default)));
            return code;
        }

        static int EditLength(int original) => Main.BiggerInputBoxes ? (int)Math.Min((long)original * original, int.MaxValue) : original;
    }

    [HarmonyPatch(typeof(UIInput),"Validate",typeof(string),typeof(int),typeof(char))]
    static class Patch_CanInput
    {
        public static Dictionary<char, char> replace = new Dictionary<char, char>
        {
            {',','‚' },
            {':','꞉' },
            {'$','＄' },
            {'*','∗' },
            {'/','∕' },
            { '|','∣' }
        };
        static bool Prefix(UIInput __instance, string text, int pos, char ch, ref char __result)
        {
            if (Main.MoreNameFreedom && (__instance.validation == UIInput.Validation.Alphanumeric || __instance.validation == UIInput.Validation.Username || __instance.validation == UIInput.Validation.Name))
            {
                var cat = char.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format || cat == UnicodeCategory.OtherNotAssigned)
                    __result = '\0';
                else if (replace.TryGetValue(ch, out var n))
                    __result = n;
                else
                    __result = ch;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(KAEditBox), "ValidateText", typeof(string), typeof(int), typeof(char))]
    static class Patch_CanInput2
    {
        static bool Prefix(KAEditBox __instance, string text, int charIndex, char addedChar, ref char __result)
        {
            if (Main.MoreNameFreedom && (__instance._CheckValidityOnInput && __instance._RegularExpression != null && __instance._RegularExpression.Contains("a-z")))
            {
                var cat = char.GetUnicodeCategory(addedChar);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format || cat == UnicodeCategory.OtherNotAssigned)
                    __result = '\0';
                else if (Patch_CanInput.replace.TryGetValue(addedChar, out var n))
                    __result = n;
                else
                    __result = addedChar;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UiSelectProfile), "InitProfile")]
    static class Patch_AvatarDataLoad
    {
        static void Postfix()
        {
                Main.TryFixUsername();
        }
    }

    [HarmonyPatch(typeof(UiAvatarControls), "Update")]
    static class Patch_ControlsUpdate
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            var flag = false;
            for (int i = 0; i < code.Count; i++)
                if (code[i].operand is MethodInfo m && flag)
                {
                    if (m.Name == "GetButtonDown")
                        code[i] = new CodeInstruction(OpCodes.Call, typeof(Patch_ControlsUpdate).GetMethod(nameof(ButtonDown), ~BindingFlags.Default));
                    else if (m.Name == "GetButtonUp")
                        code[i] = new CodeInstruction(OpCodes.Call, typeof(Patch_ControlsUpdate).GetMethod(nameof(ButtonUp), ~BindingFlags.Default));
                    flag = false;
                }
                else if (code[i].operand is string str)
                    flag = str == "DragonFire";
            return code;
        }
        static bool ButtonDown(string button) => Main.AutomaticFireballs ? KAInput.GetButton(button) : KAInput.GetButtonDown(button);
        static bool ButtonUp(string button) => Main.AutomaticFireballs ? KAInput.GetButton(button) : KAInput.GetButtonUp(button);
    }

    [HarmonyPatch(typeof(RacingManager),"AddPenalty")]
    static class Patch_AddRacingCooldown
    {
        public static bool Prefix() => RacingManager.Instance.State >= RacingManagerState.RaceCountdown;
    }

    [HarmonyPatch]
    static class Patch_ColorDragonShot
    {
        [HarmonyPatch(typeof(ObAmmo),"Activate")]
        [HarmonyPrefix]
        static void ActivateAmmo_Pre(ObAmmo __instance, WeaponManager inManager)
        {
            var a = ExtendedAmmo.Get(__instance);
            a.manager = inManager;
            ExtendedAmmo.EditColors(__instance.gameObject, __instance);
        }

        [HarmonyPatch(typeof(ObAmmo), "PlayHitParticle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ObAmmo_PlayHitParticle(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var ind = code.FindIndex(x => x.opcode == OpCodes.Stloc_0);
            var lbl = code[ind].labels;
            code[ind].labels = new List<Label>();
            code.InsertRange(ind, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0) { labels = lbl },
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedAmmo),nameof(ExtendedAmmo.EditColors)))
            });
            return code;
        }

        [HarmonyPatch(typeof(ObBlastAmmo), "PlayHitParticle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ObBlastAmmo_PlayHitParticle(IEnumerable<CodeInstruction> instructions) => ObAmmo_PlayHitParticle(instructions);

        [HarmonyPatch(typeof(ObCatapultAmmo), "PlayHitParticle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ObCatapultAmmo_PlayHitParticle(IEnumerable<CodeInstruction> instructions) => ObAmmo_PlayHitParticle(instructions);
    }

    [HarmonyPatch(typeof(RaisedPetData))]
    static class Patch_PetData
    {
        [HarmonyPatch("ParseResStringEx")]
        static void Postfix(string s, RaisedPetData __instance)
        {
            ExtendedPetData.Get(__instance).isIntact = false;
            foreach (var i in s.Split('*'))
                if (i == ExtendedPetData.ISINTACT_KEY)
                    ExtendedPetData.Get(__instance).isIntact = true;
            else
            {
                var values = i.Split('$');
                if (values.Length >= 2 && values[0] == ExtendedPetData.FIREBALLCOLOR_KEY)
                {
                    if (values.TryParseColor(out var c, 1))
                        ExtendedPetData.Get(__instance).FireballColor = c;
                    else if (values[1].TryParseColor(out c))
                        ExtendedPetData.Get(__instance).FireballColor = c;
                }
                else if (values.TryParseColor(out var c, 1))
                        ExtendedPetData.Get(__instance).EmissionColor = c;
            }
        }
        [HarmonyPatch("SaveToResStringEx")]
        static void Postfix(RaisedPetData __instance, ref string __result)
        {
            var d = ExtendedPetData.Get(__instance);
            if (d.FireballColor != null)
                __result += ExtendedPetData.FIREBALLCOLOR_KEY + "$" + d.FireballColor.Value.JoinValues() + "*";
            if (d.EmissionColor != null)
                __result += ExtendedPetData.EMISSIONCOLOR_KEY + "$" + d.EmissionColor.Value.JoinValues() + "*";
            if (d.isIntact)
                __result += ExtendedPetData.ISINTACT_KEY + "*";
        }
        [HarmonyPatch("SaveDataReal")]
        static void Prefix(RaisedPetData __instance)
        {
            var d = ExtendedPetData.Get(__instance);
            if (d.EmissionColor == null)
                __instance.SetAttrData(ExtendedPetData.EMISSIONCOLOR_KEY, "false", DataType.BOOL);
            else
                __instance.SetAttrData(ExtendedPetData.EMISSIONCOLOR_KEY, d.EmissionColor.Value.JoinValues(), DataType.STRING);

            if (d.FireballColor == null)
                __instance.SetAttrData(ExtendedPetData.FIREBALLCOLOR_KEY, "false", DataType.BOOL);
            else
                __instance.SetAttrData(ExtendedPetData.FIREBALLCOLOR_KEY, d.FireballColor.Value.JoinValues(), DataType.STRING);

            if (d.isIntact)
                __instance.SetAttrData(ExtendedPetData.ISINTACT_KEY, "true", DataType.BOOL);
            else
                __instance.SetAttrData(ExtendedPetData.ISINTACT_KEY, "false", DataType.BOOL);
        }
        [HarmonyPatch("ResolveLoadedData")]
        static void Postfix(RaisedPetData __instance)
        {
            var d = ExtendedPetData.Get(__instance);
            var a = __instance.FindAttrData(ExtendedPetData.FIREBALLCOLOR_KEY);
            if (a?.Value != null && a.Type == DataType.STRING)
            {
                var values = a.Value.Split('$');
                if (values.TryParseColor(out var c))
                    d.FireballColor = c;
            }
            a = __instance.FindAttrData(ExtendedPetData.EMISSIONCOLOR_KEY);
            if (a?.Value != null && a.Type == DataType.STRING)
            {
                var values = a.Value.Split('$');
                if (values.TryParseColor(out var c))
                    d.EmissionColor = c;
            }
            a = __instance.FindAttrData(ExtendedPetData.ISINTACT_KEY);
            d.isIntact = a?.Value == "true" && a.Type == DataType.BOOL;
        }
    }

    public class ExtendedAmmo : ExtendedClass<ExtendedAmmo,ObAmmo>
    {
        public WeaponManager manager;

        static MaterialPropertyBlock props = new MaterialPropertyBlock();
        public static GameObject EditColors(GameObject obj, ObAmmo fireball)
        {
            if (!fireball)
                return obj;
            var manager = Get(fireball).manager;
            if (!manager || !manager.IsLocal || !(manager is PetWeaponManager p) || !p.SanctuaryPet)
                return obj;
            var d = ExtendedPetData.Get(p.SanctuaryPet.pData);
            if (d.FireballColor == null)
                return obj;
            var color = d.FireballColor.Value;
            //Debug.Log($"Changing fireball {obj.name} to {color}");
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(props);
                foreach (var m in r.sharedMaterials)
                    if (m && m.shader)
                    {
                        var c = m.shader.GetPropertyCount();
                        for (var i = 0; i < c; i++)
                            if (m.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
                            {
                                var n = m.shader.GetPropertyNameId(i);
                                props.SetColor(n, m.GetColor(n).Shift(color));
                            }
                    }
                r.SetPropertyBlock(props);
            }
            foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.startColor = m.startColor.Shift(color);
                var s = ps.colorBySpeed;
                s.color = s.color.Shift(color);
                var l = ps.colorOverLifetime;
                l.color = l.color.Shift(color);
            }
            return obj;
        }
    }

    public class ExtendedPetData : ExtendedClass<ExtendedPetData,RaisedPetData>
    {
        public const string FIREBALLCOLOR_KEY = "HTFC"; // Handy Tweaks Fireball Colour
        public Color? FireballColor;
        public const string EMISSIONCOLOR_KEY = "HTEC"; // Handy Tweaks Emission Colour
        public Color? EmissionColor;
        public const string ISINTACT_KEY = "HTIFI"; // Handy Tweaks Is Fury Intact
        public bool isIntact;
    }

    [HarmonyPatch(typeof(UiSelectName),"OnClick")]
    static class Patch_SelectName
    {
        public static bool SkipNameChecks = false;
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            var lbl = iL.DefineLabel();
            code[code.FindLastIndex(code.FindIndex(x => x.operand is MethodInfo m && m.Name == "get_Independent"), x => x.opcode == OpCodes.Ldarg_0)].labels.Add(lbl);
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Stloc_0) + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldsfld,AccessTools.Field(typeof(Patch_SelectName),nameof(SkipNameChecks))),
                new CodeInstruction(OpCodes.Brtrue,lbl)
            });
            return code;
        }
    }

    [HarmonyPatch(typeof(SanctuaryPet), "PetMoodParticleAllowed")]
    static class Patch_MoodParticleAllowed
    {
        static void Postfix(SanctuaryPet __instance, ref bool __result)
        {
            var n = __instance.GetTypeInfo()._Name;
            if (Main.DisableHappyParticles.TryGetValue(n,out var v))
            {
                if (v)
                    __result = false;
            }
            else
            {
                Main.DisableHappyParticles[n] = false;
                Main.instance.Config.Save();
            }
        }
    }

    [HarmonyPatch(typeof(AvAvatarController), "ShowArmorWing")]
    static class Patch_ArmorWingsVisible
    {
        static void Prefix(ref bool show)
        {
            if (Main.AlwaysShowArmourWings)
                show = true;
        }
    }

    [HarmonyPatch(typeof(UiDragonCustomization))]
    static class Patch_DragonCustomization
    {
        [HarmonyPatch("SetColorSelector")]
        [HarmonyPostfix]
        static void SetColorSelector(UiDragonCustomization __instance)
        {
            if (Main.CustomColorPickerMode == ColorPickerMode.Disabled)
                return;
            if (__instance.mIsUsedInJournal && !ExtendedDragonCustomization.FreeCustomization(__instance.mFreeCustomization, __instance) && CommonInventoryData.pInstance.GetQuantity(__instance.mUiJournalCustomization._DragonTicketItemID) <= 0)
                return;
            var ui = ColorPicker.OpenUI((x) =>
            {
                if (__instance.mSelectedColorBtn == __instance.mPrimaryColorBtn)
                    __instance.mPrimaryColor = x;
                else if (__instance.mSelectedColorBtn == __instance.mSecondaryColorBtn)
                    __instance.mSecondaryColor = x;
                else if (__instance.mSelectedColorBtn == __instance.mTertiaryColorBtn)
                    __instance.mTertiaryColor = x;
                else
                {
                    var e = ExtendedDragonCustomization.Get(__instance);
                    if (__instance.mSelectedColorBtn == e.emissionColorBtn)
                        e.emissionColor = x;
                    else if (__instance.mSelectedColorBtn == e.fireballColorBtn)
                        e.fireballColor = x;
                }
                __instance.mSelectedColorBtn.pBackground.color = x;
                __instance.mRebuildTexture = true;
                __instance.RemoveDragonSkin();
                __instance.mIsResetAvailable = true;
                __instance.RefreshResetBtn();
                __instance.mMenu.mModified = true;
            });
            ui.Requires = () => __instance && __instance.isActiveAndEnabled && __instance.GetVisibility();
            ui.current = __instance.mSelectedColorBtn.pBackground.color;
        }

        [HarmonyPatch("SetColorSelector")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SetColorSelector(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.operand is MethodInfo m && m.Name == "get_white")+ 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.GetSelectedColor)))
            });
            return code;
        }

        [HarmonyPatch("UpdateCustomizationUI")]
        [HarmonyPrefix]
        static void UpdateCustomizationUI(UiDragonCustomization __instance) => ExtendedDragonCustomization.Get(__instance);

        [HarmonyPatch("OnClick")]
        [HarmonyPostfix]
        static void OnClick(UiDragonCustomization __instance, KAWidget inItem)
        {
            var e = ExtendedDragonCustomization.Get(__instance);
            if (inItem == e.emissionColorBtn || inItem == e.fireballColorBtn)
            {
                __instance.mSelectedColorBtn = inItem;
                __instance.SetColorSelector();
            }
            if (e.ToggleBtnRepaired && inItem == e.ToggleBtnRepaired)
            {
                __instance.mMenu.mModified = true;
                ExtendedPetData.Get(__instance.pPetData).isIntact = e.ToggleBtnRepaired.IsChecked();
                MeshConversion.EnforceModel(__instance.pPetData, __instance.mPet.mRendererMap.Values);
            }
        }

        [HarmonyPatch("RefreshUI")]
        [HarmonyPostfix]
        static void RefreshUI(UiDragonCustomization __instance)
        {
            bool flag = SanctuaryData.GetPetCustomizationType(__instance.pPetData) == PetCustomizationType.Default;
            __instance.mToggleBtnMale.SetVisibility(flag);
            __instance.mToggleBtnFemale.SetVisibility(flag);
        }

        [HarmonyPatch("OnPressRepeated")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> OnPressRepeated(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.operand is FieldInfo f && f.Name == "mFreeCustomization") + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.FreeCustomization)))
            });
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo f && f.Name == "mRebuildTexture"), new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S,10),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.OnPaletteClick)))
            });
            return code;
        }

        [HarmonyPatch("OnCloseCustomization")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> OnCloseCustomization(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is MethodInfo m && m.Name == "SetColors" && m.DeclaringType == typeof(SanctuaryPet))
                {
                    code.RemoveAt(i);
                    code.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExtendedDragonCustomization),nameof(ExtendedDragonCustomization.StoreValues)))
                    });
                }
            return code;
        }
        [HarmonyPatch("Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Update(IEnumerable<CodeInstruction> instructions) => OnCloseCustomization(instructions);

        [HarmonyPatch("Update")]
        [HarmonyPostfix] 
        static void Update(UiDragonCustomization __instance)
        {
            if (Input.GetMouseButtonDown(1))
            {
                var e = ExtendedDragonCustomization.Get(__instance);
                var flag = false;
                if (KAUI.GetGlobalMouseOverItem() == e.emissionColorBtn)
                {
                    e.emissionColor = null;
                    e.emissionColorBtn.pBackground.color = ExtendedDragonCustomization.NullColorFallback;
                    flag = true;
                }
                else if (KAUI.GetGlobalMouseOverItem() == e.fireballColorBtn)
                {
                    e.fireballColor = null;
                    e.fireballColorBtn.pBackground.color = ExtendedDragonCustomization.NullColorFallback;
                    flag = true;
                }
                if (flag)
                {
                    __instance.mRebuildTexture = true;
                    __instance.RemoveDragonSkin();
                    __instance.mMenu.mModified = true;
                }
            }
        }
    }

    public class ExtendedDragonCustomization : ExtendedClass<ExtendedDragonCustomization,UiDragonCustomization>
    {
        public static Color NullColorFallback = new Color(0, 0, 0, 0.5f);
        UiDragonCustomization ui;
        public KAWidget emissionColorBtn;
        public KAWidget fireballColorBtn;
        public KAToggleButton ToggleBtnRepaired;
        public Color? emissionColor;
        public Color? fireballColor;
        protected override void OnCreate(UiDragonCustomization instance)
        {
            ui = instance;
            var e = ExtendedPetData.Get(instance.pPetData);
            emissionColor = e.EmissionColor;
            fireballColor = e.FireballColor;
            var p1 = (Vector2)instance.mPrimaryColorBtn.GetPosition();
            var p2 = (Vector2)instance.mSecondaryColorBtn.GetPosition();
            var p3 = (Vector2)instance.mTertiaryColorBtn.GetPosition();
            var p4 = p2.Rotate(-60 * Mathf.Deg2Rad, p1);
            var p5 = p3.Rotate(-60 * Mathf.Deg2Rad, p2);

            emissionColorBtn = instance.DuplicateWidget(instance.mPrimaryColorBtn, instance.mPrimaryColorBtn.pAnchor.side);
            emissionColorBtn.transform.SetParent( instance.mPrimaryColorBtn.transform.parent);
            emissionColorBtn.SetPosition(p4.x, p4.y);
            emissionColorBtn.SetVisibility(true);
            emissionColorBtn.SetState(KAUIState.INTERACTIVE);

            fireballColorBtn = instance.DuplicateWidget(instance.mPrimaryColorBtn, instance.mPrimaryColorBtn.pAnchor.side);
            fireballColorBtn.transform.SetParent(instance.mPrimaryColorBtn.transform.parent);
            fireballColorBtn.SetPosition(p5.x, p5.y);
            fireballColorBtn.SetVisibility(true);
            fireballColorBtn.SetState(KAUIState.INTERACTIVE);

            if (!instance.mIsCreationUI && !(string.IsNullOrEmpty(instance.pPetData.FindAttrData("_LastCustomizedStage")?.Value)))
            {
                var o = -instance.mToggleBtnMale.pBackground.height * 1.5f;
                var p = instance.mToggleBtnMale.GetPosition();
                instance.mToggleBtnMale.SetPosition( p.x + o, p.y + o);
                p = instance.mToggleBtnFemale.GetPosition();
                instance.mToggleBtnFemale.SetPosition(p.x + o, p.y + o);
            }

            emissionColorBtn.SetText("Glow");
            emissionColorBtn.pBackground.color = emissionColor ?? NullColorFallback;
            fireballColorBtn.SetText("Fireball");
            fireballColorBtn.pBackground.color = fireballColor ?? NullColorFallback;

            if (!instance.mIsCreationUI && instance.mUiJournalCustomization && MeshConversion.ShouldAffect(instance.mPetData.PetTypeID))
            {
                var b = ToggleBtnRepaired = (KAToggleButton)instance.DuplicateWidget(instance.mToggleBtnFemale, instance.mToggleBtnFemale.pAnchor.side);
                b.name = "IntactNightfuries.ToggleBtnRepaired";
                instance.mToggleBtnFemale.pParentWidget?.AddChild(b);
                var icon = b.transform.Find("Icon").GetComponent<UISlicedSprite>();
                icon.spriteName = icon.pOrgSprite = "IcoDWDragonsJournalDecals";
                b._Grouped = false;
                b.mToggleButtons = new KAToggleButton[0];
                b.mCachedTooltipInfo._Text = new LocaleString("Toothless Tail");
                b._CheckedTooltipInfo._Text = new LocaleString("Natural Tail");
                foreach (var a in b._CheckedInfo._ColorInfo._ApplyTo)
                    a._Color = new Color(0.1f, 0.9f, 0.1f);
                var p = instance.mUiJournalCustomization.mAvatarBtn.GetPosition();
                b.SetPosition(p.x - (b.pBackground.width + instance.mUiJournalCustomization.mAvatarBtn.pBackground.width) * 0.65f, p.y);
                b.SetChecked(b._StartChecked = ExtendedPetData.Get(instance.mPetData).isIntact);
            }
        } 

        public static Color GetSelectedColor(Color fallback, UiDragonCustomization instance)
        {
            var e = Get(instance);
            if (instance.mSelectedColorBtn == e.emissionColorBtn)
                return e.emissionColor ?? Color.black;
            if (instance.mSelectedColorBtn == e.fireballColorBtn)
                return e.fireballColor ?? Color.black;
            return fallback;
        }

        public static bool FreeCustomization(bool fallback, UiDragonCustomization instance)
        {
            if (fallback)
                return true;
            var e = Get(instance);
            return instance.mSelectedColorBtn == e.emissionColorBtn || instance.mSelectedColorBtn == e.fireballColorBtn;
        }

        public static UiDragonCustomization OnPaletteClick(UiDragonCustomization instance, Color color)
        {
            var e = Get(instance);
            if (instance.mSelectedColorBtn == e.emissionColorBtn)
            {
                e.emissionColor = color;
                instance.mRebuildTexture = true;
            }
            else if (instance.mSelectedColorBtn == e.fireballColorBtn)
            {
                e.fireballColor = color;
                instance.mRebuildTexture = true;
            }
            ColorPicker.TrySetColor(color);
            return instance;
        }

        public static void StoreValues(SanctuaryPet pet, Color a, Color b, Color c, bool save, UiDragonCustomization ui)
        {
            var uie = Get(ui);
            var pe = ExtendedPetData.Get(ui.pPetData);
            pe.EmissionColor = uie.emissionColor;
            pe.FireballColor = uie.fireballColor;
            pe = ExtendedPetData.Get(pet.pData);
            pe.EmissionColor = uie.emissionColor;
            pe.FireballColor = uie.fireballColor;
            pet.SetColors(a,b,c,save);
        }
    }

    [HarmonyPatch(typeof(UiAvatarCustomization), "SetSkinColorPickersVisibility")]
    static class Patch_AvatarCustomization
    {
        static void Postfix(UiAvatarCustomization __instance)
        {
            if (Main.CustomColorPickerMode == ColorPickerMode.Disabled)
                return;
            var ui = ColorPicker.OpenUI((x) =>
            {
                __instance.mSelectedColorBtn.pBackground.color = x;
                __instance.SetColor(x);
            });
            ui.Requires = () =>
                __instance
                && __instance.isActiveAndEnabled
                && __instance.GetVisibility()
                && (
                    (__instance.pColorPalette && __instance.pColorPalette.GetVisibility())
                    ||
                    (__instance.mSkinColorPalette && __instance.mSkinColorPalette.GetVisibility())
                );
            ui.current = __instance.mSelectedColorBtn.pBackground.color;
        }
    }

    public class ColorPicker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Display;
        public class ColorSlider
        {
            public ColorSlider(Slider slider, InputField input, Image back, Image tint)
            {
                this.slider = slider;
                this.input = input;
                this.back = back;
                this.tint = tint;
            }
            public ColorSlider(Transform transform, string slider = "Slider", string input = "Input", string back = "Slider/Background/GradientBackground", string tint = "Slider/Background/GradientBackground/Gradient")
                : this(transform.Find(slider).GetComponent<Slider>(), transform.Find(input).GetComponent<InputField>(), transform.Find(back).GetComponent<Image>(), transform.Find(tint).GetComponent<Image>())
            { }
            public Slider slider;
            public InputField input;
            public Image back;
            public Image tint;
        }
        public ColorSlider Red;
        public ColorSlider Green;
        public ColorSlider Blue;
        public ColorSlider Hue;
        public ColorSlider Saturation;
        public ColorSlider Luminosity;
        (float H, float S, float L) HSL;
        ColorSlider this[int channel] => channel == 0 ? Red : channel == 1 ? Green : channel == 2 ? Blue : default;
        ColorSlider this[string channel] {
            get
            {
                if (channel == "R") return Red;
                if (channel == "G") return Green;
                if (channel == "B") return Blue;
                if (channel == "H") return Hue;
                if (channel == "S") return Saturation;
                if (channel == "L") return Luminosity;
                return null;
            }
        }
        public Button Close;
        public event Action<Color> OnChange;
        public event Action OnClose;
        public Func<bool> Requires;

        KAUI handle;
        Color _c;
        public Color current
        {
            get => _c;
            set
            {
                value.a = 1;
                if (value == _c)
                    return;
                UpdateSliders(value);
            }
        }
        T GetComponent<T>(string path) where T : Component => Find(path).GetComponent<T>();
        Transform Find(string path) => transform.Find(path);
        void Awake()
        {
            handle = gameObject.AddComponent<KAUI>();

            Display = GetComponent<Image>("LeftContainer/ColorView/Color");
            Red = new ColorSlider(Find("R"));
            Green = new ColorSlider(Find("G"));
            Blue = new ColorSlider(Find("B"));
            Hue = new ColorSlider(Find("H"),back: "Slider/Background/GradientBackground/Gradient",tint: "Slider/Background/GradientBackground/Tint");
            Saturation = new ColorSlider(Find("S"));
            Luminosity = new ColorSlider(Find("L"));
            UpdateSliderVisibility();

            Close = GetComponent<Button>("LeftContainer/CloseButton");

            Close.onClick.AddListener(CloseUI);
            foreach (var t in new[] { "R","G","B","H","S","L" })
            {
                var tag = t;
                this[tag].slider.onValueChanged.AddListener((x) => UpdateValue(tag, x));
                if (tag == "H")
                    this[tag].input.onValueChanged.AddListener((x) => UpdateValue(tag, long.TryParse(x, out var v) ? v / 360f : 0, true));
                else
                    this[tag].input.onValueChanged.AddListener((x) => UpdateValue(tag, long.TryParse(x, out var v) ? v / 255f : 0, true));
            }
        }

        public static void TryUpdateSliderVisibility()
        {
            if (open)
                open.UpdateSliderVisibility();
        }
        public void UpdateSliderVisibility()
        {
            var rgb = (Main.CustomColorPickerMode & ColorPickerMode.RGB) != 0;
            var hsl = (Main.CustomColorPickerMode & ColorPickerMode.HSL) != 0;
            foreach (var t in new[] { "R", "G", "B" })
                Find(t).gameObject.SetActive(rgb);
            foreach (var t in new[] { "H", "S", "L" })
                Find(t).gameObject.SetActive(hsl);
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            KAUI.SetExclusive(handle);
        }
        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            KAUI.RemoveExclusive(handle);
        }
        void CloseUI()
        {
            Destroy(GetComponentInParent<Canvas>().gameObject);
        }
        void OnDestroy()
        {
            KAUI.RemoveExclusive(GetComponent<KAUI>());
            OnClose?.Invoke();
        }

        void UpdateSliders(Color nColor, string called = null, float value = 0, bool fromInput = false)
        {
            _c = nColor;
            if (called == "H" || called == "L" || called == "S")
            {
                if (called == "H")
                    HSL.H = value;
                else if (called == "S")
                    HSL.S = value;
                else
                    HSL.L = value;
                UpdateSlider(called, value, fromInput);
                UpdateSlider("R", _c.r);
                UpdateSlider("G", _c.g);
                UpdateSlider("B", _c.b);
                UpdateGradients();
            }
            else
            {
                UpdateSlider("R", _c.r, fromInput && called == "R");
                UpdateSlider("G", _c.g, fromInput && called == "G");
                UpdateSlider("B", _c.b, fromInput && called == "B");
                _c.ToHSL(out var h, out var s, out var l);
                HSL = (h, s, l);
                UpdateSlider("H", h);
                UpdateSlider("S", s);
                UpdateSlider("L", l);
                UpdateGradients();
            }
            Display.color = _c;
            OnChange?.Invoke(_c);
        }

        void UpdateSlider(string tag, float value, bool fromInput = false)
        {
            if (this[tag] != null)
            {
                updating = true;
                this[tag].slider.value = value;
                if (!fromInput)
                    this[tag].input.text = ((long)Math.Round(value * (tag == "H" ? 360 : 255))).ToString();
                updating = false;
            }
        }

        void UpdateGradients()
        {
            Red.back.color = new Color(1, _c.g, _c.b);
            Red.tint.color = new Color(0, _c.g, _c.b);
            Green.back.color = new Color(_c.r, 1, _c.b);
            Green.tint.color = new Color(_c.r, 0, _c.b);
            Blue.back.color = new Color(_c.r, _c.g, 1);
            Blue.tint.color = new Color(_c.r, _c.g, 0);
            var nhsl = HSL;
            ColorConvert.Normalized(ref nhsl.H, ref nhsl.S, ref nhsl.L);
            var h = ColorConvert.FromHSL(0, 0, nhsl.L);
            h.a = 1 - nhsl.S;
            Hue.tint.color = h;
            Saturation.back.color = ColorConvert.FromHSL(HSL.H, 1, HSL.L);
            Saturation.tint.color = ColorConvert.FromHSL(HSL.H, 0, HSL.L);
            Luminosity.back.color = ColorConvert.FromHSL(HSL.H, HSL.S, 1);
            Luminosity.tint.color = ColorConvert.FromHSL(HSL.H, HSL.S, 0);
        }

        bool updating = false;
        void UpdateValue(string tag, float value, bool fromInput = false)
        {
            if (updating)
                return;
            Color nc = _c;
            if (tag == "R")
                nc.r = value;
            else if (tag == "G")
                nc.g = value;
            else if (tag == "B")
                nc.b = value;
            else if (tag == "H")
                nc = ColorConvert.FromHSL(value, HSL.S, HSL.L);
            else if (tag == "S")
                nc = ColorConvert.FromHSL(HSL.H, value, HSL.L);
            else if (tag == "L")
                nc = ColorConvert.FromHSL(HSL.H, HSL.S, value);
            else
                throw new ArgumentOutOfRangeException();
            
            UpdateSliders(nc, tag, value, fromInput);
        }
        void Update()
        {
            if (Input.GetKeyUp(KeyCode.Escape) && KAUI._GlobalExclusiveUI == handle)
                CloseUI();
            if (Requires != null && !Requires())
                CloseUI();
        }

        public static GameObject UIPrefab;
        static ColorPicker open;
        public static void TrySetColor(Color color)
        {
            if (open)
                open.current = color;
        }
        public static ColorPicker OpenUI(Action<Color> onChange = null, Action onClose = null)
        {
            if (!open)
                open = Instantiate(UIPrefab).transform.Find("Picker").gameObject.AddComponent<ColorPicker>();
            open.OnChange = onChange;
            open.OnClose = onClose;
            return open;
        }
    }

    public static class ColorConvert
    {
        public static void ToHSL(this Color c, out float hue, out float saturation, out float luminosity) => ToHSL(c.r, c.g, c.b, out hue, out saturation, out luminosity);
        public static void ToHSL(float R, float G, float B, out float hue, out float saturation, out float luminosity)
        {
            var max = Math.Max(Math.Max(R, G), B);
            var min = Math.Min(Math.Min(R, G), B);
            luminosity = max;
            if (min == max)
            {
                hue = 0;
                saturation = 0;
                return;
            }
            saturation = (max - min) / max;
            if (R == max)
            {
                if (G >= B)
                    hue = (G - min) * H60 / (max - min);
                else
                    hue = H360 - (B - min) * H60 / (max - min);
            }
            else if (G == max)
            {
                if (B >= R)
                    hue = H120 + (B - min) * H60 / (max - min);
                else
                    hue = H120 - (R - min) * H60 / (max - min);
            }
            else
            {
                if (R >= G)
                    hue = H240 + (R - min) * H60 / (max - min);
                else
                    hue = H240 - (G - min) * H60 / (max - min);
            }
        }
        public static Color FromHSL(float hue, float saturation, float luminosity)
        {
            FromHSL(hue, saturation, luminosity, out var R, out var G, out var B);
            return new Color(R, G, B);
        }
        const float H60 = 1f / 6;
        const float H120 = 2f / 6;
        const float H180 = 3f / 6;
        const float H240 = 4f / 6;
        const float H300 = 5f / 6;
        const float H360 = 1;
        public static void FromHSL(float hue, float saturation, float luminosity, out float R, out float G, out float B)
        {
            hue %= 1;
            if (hue < 0)
                hue += 1;
            var max = luminosity;
            if (saturation == 0)
            {
                R = G = B = max;
                return;
            }
            var min = max - (saturation * max);
            if (hue <= H60)
            {
                B = min;
                R = max;
                G = min + hue * (max - min) / H60;
            }
            else if (hue <= H120)
            {
                B = min;
                G = max;
                R = min + (H120 - hue) * (max - min) / H60;
            }
            else if (hue <= H180)
            {
                R = min;
                G = max;
                B = min + (hue - H120) * (max - min) / H60;
            }
            else if (hue <= H240)
            {
                R = min;
                B = max;
                G = min + (H240 - hue) * (max - min) / H60;
            }
            else if (hue <= H300)
            {
                G = min;
                B = max;
                R = min + (hue - H240) * (max - min) / H60;
            }
            else
            {
                G = min;
                R = max;
                B = min + (H360 - hue) * (max - min) / H60;
            }
        }

        public static Color Normalized(this Color c)
        {
            c.ToHSL(out var h, out var s, out var l);
            Normalized(ref h, ref s, ref l);
            return FromHSL(h, s, l);
        }

        public static Color Clamped(this Color c) => new Color(Math.Min(1, Math.Max(0, c.r)), Math.Min(1, Math.Max(0, c.g)), Math.Min(1, Math.Max(0, c.b)), Math.Min(1, Math.Max(0, c.a)));
        public static void Normalized(ref float hue, ref float saturation, ref float luminosity)
        {
            saturation = Math.Min(1, Math.Max(0, saturation));
            luminosity = Math.Min(1, Math.Max(0, luminosity));
        }
    }

    [HarmonyPatch(typeof(UiDragonsInfoCardItem))]
    static class Patch_DragonInfoCard
    {
        [HarmonyPatch("RefreshUI")]
        [HarmonyPostfix]
        static void RefreshUI(UiDragonsInfoCardItem __instance) => ExtendedInfoCard.Get(__instance).Refresh();

        [HarmonyPatch("OnClick",typeof(KAWidget))]
        [HarmonyPostfix]
        static void OnClick(UiDragonsInfoCardItem __instance, KAWidget inWidget) => ExtendedInfoCard.Get(__instance).OnClick(inWidget);
    }

    public class ExtendedInfoCard : ExtendedClass<ExtendedInfoCard,UiDragonsInfoCardItem>
    {
        public UiDragonsInfoCardItem instance;
        public KAWidget ReleaseBtn;
        protected override void OnCreate(UiDragonsInfoCardItem instance)
        {
            base.OnCreate(instance);
            this.instance = instance;
            if (instance.mBtnChangeName)
            {
                ReleaseBtn = instance.pUI.DuplicateWidget(instance.mBtnChangeName, instance.mBtnChangeName.pAnchor.side);
                instance.mBtnChangeName.pParentWidget?.AddChild(ReleaseBtn);
                ReleaseBtn.transform.position = instance.mBtnChangeName.transform.position + new Vector3(-50, 0, 0);
                ReleaseBtn.SetToolTipText("Release Dragon");
                ReleaseBtn.RemoveChildItem(ReleaseBtn.FindChildItem("Gems"), true);
                var back = ReleaseBtn.FindChildItem("Icon").transform.Find("Background").GetComponent<UISprite>();
                back.spriteName = "IconIgnore";
                back.pOrgSprite = "IconIgnore";
                back = ReleaseBtn.transform.Find("Background").GetComponent<UISprite>();
                back.pOrgColorTint = back.pOrgColorTint.Shift(Color.red);
                back.color = back.color.Shift(Color.red);
            }
        }
        public void Refresh()
        {
            if (ReleaseBtn)
                ReleaseBtn.SetVisibility(instance.pSelectedPetData != SanctuaryManager.pCurPetData);
        }
        public void OnClick(KAWidget widget)
        {
            if (ReleaseBtn && widget == ReleaseBtn)
            {
                var selected = instance.pSelectedPetID;
                var instances = Resources.FindObjectsOfTypeAll<SanctuaryPet>().Where(x => x.pData?.RaisedPetID == selected).ToArray();
                Main.TryDestroyDragon(instance, () =>
                {
                    foreach (var i in instances)
                        if (i)
                            Object.Destroy(i.gameObject);
                    var info = instance.mMsgObject.GetComponent<UiDragonsInfoCard>();
                    var list = Object.FindObjectOfType<UiDragonsListCard>();
                    if (list && list.GetVisibility())
                    {
                        list.RefreshUI();
                        list.SelectDragon(SanctuaryManager.pCurPetData?.RaisedPetID ?? 0);
                        info.RefreshUI();
                    }
                    else
                    {
                        info.PopOutCard();
                        Object.FindObjectOfType<UiStablesInfoCard>()?.RefreshUI();
                    }
                });
            }
        }
    }

    [HarmonyPatch(typeof(StoreData), "SetStoreData")]
    static class Patch_SetStoreItemData
    {
        public static Dictionary<int, int> originalMaxes = new Dictionary<int, int>();
        public static void Postfix(StoreData __instance)
        {
            if (Main.RemoveItemBuyLimits)
                foreach (var i in __instance._Items)
                    if (i.InventoryMax != -1)
                    {
                        originalMaxes[i.ItemID] = i.InventoryMax;
                        i.InventoryMax = -1;
                    }
        }
    }

    [HarmonyPatch(typeof(KAUIStoreBuyPopUp), "SetItemData")]
    static class Patch_SetBuyPopupItem
    {
        static (string text,int textid,Color color,int width)? originalText;
        static void Postfix(KAUIStoreBuyPopUp __instance, KAStoreItemData itemData)
        {
            var label = __instance.mBattleSlots.GetLabel();
            if (itemData._ItemData.HasCategory(Category.DragonTickets) && itemData._ItemData.InventoryMax < 0)
            {
                if (originalText == null)
                {
                    originalText = (label.text, label.textID, label.pOrgColorTint, label.width);
                    label.textID = 0;
                    label.text = "Buying many of this item is not recommended";
                    label.ResetEnglishText();
                    label.color = label.pOrgColorTint = originalText.Value.color.Shift(Color.red);
                    label.width = (int)(originalText.Value.width * 1.8);
                    __instance.mBattleSlots.transform.position += new Vector3((label.width - originalText.Value.width), 0, 0);
                    __instance.mOccupiedBattleSlots.SetText("");
                }
                __instance.mBattleSlots.SetVisibility(true);
            }
            else if (originalText != null)
            {
                label.text = originalText.Value.text;
                label.textID = originalText.Value.textid;
                label.ResetEnglishText();
                label.color = label.pOrgColorTint = originalText.Value.color;
                __instance.mBattleSlots.transform.position -= new Vector3((label.width - originalText.Value.width), 0, 0);
                label.width = originalText.Value.width;
                originalText = null;
            }

        }
    }

    [HarmonyPatch]
    static class Patch_ApplyPetCustomization
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ApplyPetCustomization), "SetSkinData");
            yield return AccessTools.Method(typeof(ApplyPetCustomization), "OnMeshLoaded");
            yield break;
        }
        static void Postfix(ApplyPetCustomization __instance) => MeshConversion.EnforceModel(__instance.mRaisedPetData, __instance.mRendererMap.Values);
    }

    [HarmonyPatch]
    static class Patch_SanctuaryPet
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SanctuaryPet), "SetSkinData");
            yield return AccessTools.Method(typeof(SanctuaryPet), "ResetSkinData");
            yield return AccessTools.Method(typeof(SanctuaryPet), "UpdateData");
            yield return AccessTools.Method(typeof(SanctuaryPet), "Init");
            yield break;
        }
        static void Postfix(SanctuaryPet __instance) => MeshConversion.EnforceModel(__instance.pData, __instance.mRendererMap.Values);
    }

    public class MeshConversion : ExtendedClass<MeshConversion, Mesh>
    {
        public Mesh Other;
        public bool IsGenerated = false;
        static bool Disable = false;
        static Dictionary<int, int> mirrorBones = new Dictionary<int, int> { { 2, 5 }, { 5, 2 }, { 3, 6 }, { 6, 3 }, { 4, 7 }, { 7, 4 }, { 15, 17 }, { 17, 15 }, { 16, 18 }, { 18, 16 }, { 19, 21 }, { 21, 19 }, { 20, 22 }, { 22, 20 }, { 29, 30 }, { 30, 29 }, { 34, 36 }, { 36, 34 }, { 35, 37 }, { 37, 35 }, { 38, 46 }, { 46, 38 }, { 39, 47 }, { 47, 39 }, { 40, 48 }, { 48, 40 }, { 41, 49 }, { 49, 41 }, { 42, 50 }, { 50, 42 }, { 43, 51 }, { 51, 43 }, { 44, 52 }, { 52, 44 }, { 45, 53 }, { 53, 45 }, { 54, 56 }, { 56, 54 }, { 55, 57 }, { 57, 55 }, { 59, 62 }, { 62, 59 }, { 60, 63 }, { 63, 60 }, { 61, 64 }, { 64, 61 }, { 65, 90 }, { 90, 65 }, { 66, 91 }, { 91, 66 }, { 67, 92 }, { 92, 67 }, { 68, 93 }, { 93, 68 }, { 69, 94 }, { 94, 69 }, { 70, 95 }, { 95, 70 }, { 71, 96 }, { 96, 71 }, { 72, 97 }, { 97, 72 }, { 73, 98 }, { 98, 73 }, { 74, 99 }, { 99, 74 }, { 75, 100 }, { 100, 75 }, { 76, 101 }, { 101, 76 }, { 77, 102 }, { 102, 77 }, { 78, 103 }, { 103, 78 }, { 79, 104 }, { 104, 79 }, { 80, 105 }, { 105, 80 }, { 81, 106 }, { 106, 81 }, { 82, 107 }, { 107, 82 }, { 83, 108 }, { 108, 83 }, { 84, 109 }, { 109, 84 }, { 85, 110 }, { 110, 85 }, { 86, 111 }, { 111, 86 }, { 87, 112 }, { 112, 87 }, { 88, 113 }, { 113, 88 }, { 89, 114 }, { 114, 89 } };

        protected override void OnGet(Mesh instance)
        {
            base.OnGet(instance);
            if (Disable || Other)
                return;
            try
            {
                if (!instance.isReadable)
                {
                    Main.logger.LogError("Mesh must have read/write enabled >> "+ instance.name);
                    return;
                }

                var keep = new Dictionary<int, bool>();

                var tDup = new List<int>();
                var tKeep = new List<int>();
                var verts = instance.vertices;
                var tris = instance.triangles;

                Side GetSide(int vInd) => verts[vInd].x > 0.001 ? Side.Good : verts[vInd].x < -0.001 ? Side.Bad : Side.Middle;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var t1 = tris[i];
                    var t2 = tris[i + 1];
                    var t3 = tris[i + 2];
                    var s1 = GetSide(t1);
                    var s2 = GetSide(t2);
                    var s3 = GetSide(t3);
                    if (s1 == Side.Good || s2 == Side.Good || s3 == Side.Good || (s1 == Side.Middle && s2 == Side.Middle && s3 == Side.Middle))
                    {
                        var noneBad = s1 != Side.Bad && s2 != Side.Bad && s3 != Side.Bad;
                        keep[t1] = keep.GetValueOrDefault(t1) || noneBad;
                        keep[t2] = keep.GetValueOrDefault(t2) || noneBad;
                        keep[t3] = keep.GetValueOrDefault(t3) || noneBad;
                        if (noneBad)
                            tDup.Add(i);
                        else
                            tKeep.Add(i);
                    }
                }
                var uv = instance.uv;
                var norms = instance.normals;
                var tangs = instance.tangents;
                var bones = GetBoneWeights(instance);
                var nVerts = new List<Vector3>(verts.Length * 2);
                var nUV = new List<Vector2>(uv.Length * 2);
                var nNorms = new List<Vector3>(norms.Length * 2);
                var nTangs = tangs.Length == 0 ? null : new List<Vector4>(verts.Length * 2);
                var nBones = new List<List<BoneWeight1>>(bones.Count * 2);
                var indRemap = new Dictionary<int, int>();
                for (int i = 0; i < verts.Length; i++)
                    if (keep.TryGetValue(i, out var dup))
                    {
                        indRemap[i] = indRemap.Count;
                        nVerts.Add(verts[i]);
                        nUV.Add(uv[i]);
                        nNorms.Add(norms[i]);
                        nTangs?.Add(tangs.GetSafe(i));
                        nBones.Add(bones[i]);
                        if (dup)
                        {
                            indRemap[i + verts.Length] = indRemap.Count;
                            nVerts.Add(Mirror(verts[i]));
                            nUV.Add(uv[i]);
                            nNorms.Add(Mirror(norms[i]));
                            nTangs?.Add(Mirror(tangs.GetSafe(i)));
                            nBones.Add(bones[i].Select(x => new BoneWeight1() { boneIndex = mirrorBones.TryGetValue(x.boneIndex, out var y) ? y : x.boneIndex, weight = x.weight }).ToList());
                        }
                    }
                var subs = GetSubmeshes(instance);
                var nTris = new List<int>();
                var nSubs = new List<(int start, int count)>();
                foreach (var s in subs)
                {
                    var count = 0;
                    foreach (var i in tKeep)
                        if (i >= s.start && i < s.start + s.count)
                        {
                            nTris.Add(indRemap[tris[i]]);
                            nTris.Add(indRemap[tris[i + 1]]);
                            nTris.Add(indRemap[tris[i + 2]]);
                            count += 3;
                        }
                    foreach (var i in tDup)
                        if (i >= s.start && i < s.start + s.count)
                        {
                            nTris.Add(indRemap[tris[i]]);
                            nTris.Add(indRemap[tris[i + 1]]);
                            nTris.Add(indRemap[tris[i + 2]]);
                            nTris.Add(indRemap.TryGetValue(tris[i] + verts.Length, out var ni) ? ni : indRemap[tris[i]]);
                            nTris.Add(indRemap.TryGetValue(tris[i + 2] + verts.Length, out var ni2) ? ni2 : indRemap[tris[i + 2]]);
                            nTris.Add(indRemap.TryGetValue(tris[i + 1] + verts.Length, out var ni1) ? ni1 : indRemap[tris[i + 1]]);
                            count += 6;
                        }
                    nSubs.Add((nSubs.Count != 0 ? nSubs[nSubs.Count - 1].start + nSubs[nSubs.Count - 1].count : 0, count));
                }
                var nm = new Mesh();
                nm.name = "Intact" + instance.name;
                nm.vertices = nVerts.ToArray();
                nm.uv = nUV.ToArray();
                nm.normals = nNorms.ToArray();
                if (nTangs != null)
                    nm.tangents = nTangs.ToArray();
                SetBoneWeights(nm, nBones);
                nm.triangles = nTris.ToArray();
                SetSubmeshes(nm, nSubs);
                nm.bindposes = instance.bindposes;
                nm.RecalculateBounds();

                Disable = true;
                var other = Get(nm);
                Other = nm;
                other.Other = instance;
                other.IsGenerated = true;
            }
            finally
            {
                Disable = false;
            }
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        static Vector4 Mirror(Vector4 v) => new Vector4(-v.x, v.y, v.z, -v.w);

        static List<List<BoneWeight1>> GetBoneWeights(Mesh mesh)
        {
            using (var all = mesh.GetAllBoneWeights())
            using (var per = mesh.GetBonesPerVertex())
            {
                var r = new List<List<BoneWeight1>>();
                var cur = 0;
                foreach (var n in per)
                {
                    var l = new List<BoneWeight1>();
                    r.Add(l);
                    for (int o = 0; o < n; o++)
                        l.Add(all[o + cur]);
                    cur += n;
                }
                return r;
            }
        }

        static void SetBoneWeights(Mesh mesh, List<List<BoneWeight1>> weights)
        {
            var per = new List<byte>();
            var all = new List<BoneWeight1>();
            foreach (var l in weights)
            {
                per.Add((byte)l.Count);
                all.AddRange(l);
            }
            using (var per2 = new NativeArray<byte>(per.ToArray(), Allocator.Temp))
            using (var all2 = new NativeArray<BoneWeight1>(all.ToArray(), Allocator.Temp))
                mesh.SetBoneWeights(per2, all2);
        }

        static List<(int start, int count)> GetSubmeshes(Mesh mesh)
        {
            if (mesh.subMeshCount <= 0)
                return new List<(int, int)>();
            var l = new List<(int, int)>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var s = mesh.GetSubMesh(i);
                l.Add((s.indexStart, s.indexCount));
            }
            return l;
        }

        static void SetSubmeshes(Mesh mesh, List<(int start, int count)> submeshes)
        {
            if (submeshes == null)
                mesh.SetSubMeshes(new UnityEngine.Rendering.SubMeshDescriptor[0], ~UnityEngine.Rendering.MeshUpdateFlags.Default);
            else
                mesh.SetSubMeshes(submeshes.Select(x => new UnityEngine.Rendering.SubMeshDescriptor(x.start, x.count)).ToArray(), ~UnityEngine.Rendering.MeshUpdateFlags.Default);
        }

        public static void EnforceModel(RaisedPetData pet, IEnumerable<SkinnedMeshRenderer> renderers)
        {
            if (ShouldAffect(pet.PetTypeID))
                foreach (var r in renderers)
                    if (r && r.name == "NightFuryMesh" && r.sharedMesh)
                    {
                        var d = Get(r.sharedMesh);
                        if (d.Other && ExtendedPetData.Get(pet).isIntact != d.IsGenerated)
                    {
                            r.sharedMesh = d.Other;
                        }
                    }
        }

        static int _nightfury;
        public static bool ShouldAffect(int TypeId)
        {
            if (_nightfury == 0)
                _nightfury = SanctuaryData.FindSanctuaryPetTypeInfo("NightFury")._TypeID;
            return TypeId == _nightfury;
        }
    }

    enum Side
    {
        Good,
        Middle,
        Bad
    }

    /*[HarmonyPatch(typeof(UILabel))]
    static class Patch_ForceEnableEncoding
    {
        [HarmonyPatch("OnEnable")]
        public static void Prefix(UILabel __instance)
        {
            if (Main.ForceTextTagRendering)
            {
                __instance.supportEncoding = true;
                __instance.symbolStyle = NGUIText.SymbolStyle.Colored;
            }
        }
        [HarmonyPatch("set_supportEncoding")]
        static void Prefix(ref bool value)
        {
            if (Main.ForceTextTagRendering)
                value = true;
        }
    }*/

    public class DragonFilterMenu : MonoBehaviour
    {
        static Transform prefabHolder;
        static DragonFilterMenu prefab;
        static DragonFilterItem prefabFilter;
        static DragonOrderItem prefabOrder;
        public static void SetPrefabs(GameObject main,GameObject filteritem, GameObject orderitem)
        {
            if (prefabHolder)
                Destroy(prefabHolder.gameObject);
            prefabHolder = new GameObject("PrefabHolder").transform;
            prefabHolder.gameObject.SetActive(false);
            DontDestroyOnLoad(prefabHolder.gameObject);
            prefab = Instantiate(main, prefabHolder).AddComponent<DragonFilterMenu>();
            prefabFilter = Instantiate(main, prefabHolder).AddComponent<DragonFilterItem>();
            prefabOrder = Instantiate(main, prefabHolder).AddComponent<DragonOrderItem>();
        }
        public static void Include(string filter)
        {

        }
        public static void Exclude(string filter)
        {

        }
        public static void SetOrder(string order)
        {

        }

        public Button OpenButton;
        public GameObject Menu;
        public Transform FilterGroup;
        public Transform OrderGroup;
        public Button EnableAll;
        public Button DisableAll;

    }
    public class DragonFilterItem : MonoBehaviour
    {
        public Text Label;
        public Image Icon;
        public Toggle OnOff;
        public string Filter;
        void Awake()
        {
            OnOff.onValueChanged.AddListener(OnValueChanged);
            OnValueChanged(OnOff);
        }
        void OnValueChanged(bool value)
        {
            if (value)
                DragonFilterMenu.Include(Filter);
            else
                DragonFilterMenu.Exclude(Filter);
        }
    }
    public class DragonOrderItem : MonoBehaviour, IPointerClickHandler
    {
        public Text Label;
        public Image Ascending;
        public Image Descending;
        public string Order;

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            DragonFilterMenu.SetOrder(Order);
        }
    }
}
