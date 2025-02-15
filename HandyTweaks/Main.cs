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
        public static KeyCode DoFarmStuff = KeyCode.KeypadMinus;
        [ConfigField]
        public static bool AutoSpendFarmGems = false;
        [ConfigField]
        public static bool BypassFarmGemCosts = false;
        [ConfigField]
        public static bool DoFarmStuffOnTimer = false;
        [ConfigField]
        public static bool CanPlaceAnywhere = false;
        [ConfigField]
        public static bool SkipTrivia = false;
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
        public static bool AllowCustomizingSpecialDragons = false;
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
        public static bool AlwaysMaxHappiness = false;
        [ConfigField]
        public static Dictionary<string, bool> DisableHappyParticles = new Dictionary<string, bool>();
        [ConfigField]
        public static bool AlwaysShowArmourWings = false;
        [ConfigField]
        public static ColorPickerMode CustomColorPickerMode = ColorPickerMode.RGBHSL;
        [ConfigField]
        public static bool RemoveItemBuyLimits = false;
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
}