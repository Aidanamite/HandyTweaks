using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class HatcheryUI : HandyUI
    {
        public HatcheryUISlot slotPrefab;
        public RectTransform slotArea;
        public Button teleportToHatchery;
        public int rows = 2;
        
        public void Start()
        {
#if GAME
            foreach (var i in StableManager.pInstance._HatcheryManager.pIncubators)
            {
                var n = Instantiate(slotPrefab, slotArea);
                n.Init(i);
            }
#else
            while (slotArea.childCount > 0)
                DestroyImmediate(slotArea.GetChild(0).gameObject);
            for (int i = 0; i < 12; i++)
            {
                var n = Instantiate(slotPrefab, slotArea);
                n.name = slotPrefab.name + " " + i;
                n.hatched.SetActive(i == 0);
                n.ready.SetActive(i == 1);
                n.hatching.SetActive(i >= 2 && i <= 5);
                if (i == 2)
                    n.end = DateTime.UtcNow.AddDays(2.123154);
                else if (i == 3)
                    n.end = DateTime.UtcNow.AddHours(12.123154);
                else if (i == 4)
                    n.end = DateTime.UtcNow.AddMinutes(32.123154);
                else if (i == 5)
                    n.end = DateTime.UtcNow.AddSeconds(32.123154);
                n.empty.SetActive(i == 6);
                n.buyable.SetActive(i == 7);
                if (i == 7)
                    n.buyCost.text = "800";
                n.locked.SetActive(i > 7);
            }
#endif
            var slots = slotArea.GetComponentsInChildren<HatcheryUISlot>();
            for (var i = 0; i < slots.Length; i++)
            {
                var select = slots[i].GetComponent<Selectable>();
                select.navigation = new Navigation()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnDown = slots[(i + 1) % slots.Length].GetComponent<Selectable>(),
                    selectOnRight = i + rows >= slots.Length ? teleportToHatchery : slots[i + rows].GetComponent<Selectable>(),
                    selectOnUp = slots[(i + slots.Length - 1) % slots.Length].GetComponent<Selectable>(),
                    selectOnLeft = i - rows < 0 ? teleportToHatchery : slots[i - rows].GetComponent<Selectable>()
                };
            }
            teleportToHatchery.navigation = new Navigation()
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = slots[0].GetComponent<Selectable>(),
                selectOnLeft = slots[slots.Length - 1].GetComponent<Selectable>()
            };
            slots[0].GetComponent<Selectable>().Select();
            slots[0].GetComponent<Selectable>().OnDeselect(null);
        }
    }
}
