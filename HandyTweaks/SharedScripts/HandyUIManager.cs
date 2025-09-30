using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HandyTweaks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class HandyUIManager : MonoBehaviour
    {
        Canvas canvas;
        [SerializeField]
        HatcheryUI hatchPrefab;
        [SerializeField]
        ColorPicker pickerPrefab;
        static List<HandyUI> open = new List<HandyUI>();

        static HandyUIManager instance;
        void Awake()
        {
            if (instance)
            {
                DestroyImmediate(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            canvas = GetComponent<Canvas>();
            canvas.enabled = false;
        }

        public static HatcheryUI HatcheryUIPrefab => instance.hatchPrefab;
        public static ColorPicker ColorPickerUIPrefab => instance.pickerPrefab;
        public static T OpenUI<T>(T prefab) where T : HandyUI => (T)OpenUI((HandyUI)prefab);
        static HandyUI OpenUI(HandyUI prefab) => (HandyUI)Instantiate((Object)prefab, instance.canvas.transform);
        public static void NotifyOpen(HandyUI ui, bool checkOpen = false)
        {
            if (checkOpen && !open.Contains(ui))
                return;
            instance.canvas.enabled = true;
            if (open.Count > 0) open[open.Count - 1].interactable = false;
            open.Remove(ui);
            open.Add(ui);
            ui.transform.SetAsLastSibling();
            ui.interactable = true;
        }
        public static void Close(HandyUI ui)
        {
            if (open.Remove(ui))
            {
                ui.Close();
                if (open.Count > 0)
                    open[open.Count - 1].interactable = true;
            }
        }

        public static HandyUI CurrentUI => open.Count > 0 ? open[open.Count - 1] : null;
    }
}
