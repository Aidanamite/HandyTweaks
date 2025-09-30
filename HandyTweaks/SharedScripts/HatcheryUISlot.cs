using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class HatcheryUISlot : MonoBehaviour
    {
        [NonSerialized]
        public Incubator incubator;
        public GameObject empty;
        public GameObject locked;
        public GameObject buyable;
        public GameObject hatching;
        public GameObject ready;
        public GameObject hatched;
        public TMP_Text buyCost;
        public TMP_Text timeRemaining;
        public Image hatchingEggIcon;
        public Image readyEggIcon;
        public Image hatchedEggIcon;

        public void Init(Incubator Incubator)
        {
            incubator = Incubator;
            UpdateState();
        }
#if !GAME
        public DateTime end;
        public void UpdateState()
        {
            if (hatching.activeSelf)
            {
                var len = end - DateTime.UtcNow;
                timeRemaining.text = $"{(int)len.TotalHours:D2}:{len.Minutes:D2}:{len.Seconds:D2}";
            }
        }
#else
        public void UpdateState()
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Do(GameObject go, bool target) { if (go.activeSelf != target) go.SetActive(target); }

            Do(empty, incubator.pMyState == Incubator.IncubatorStates.WAITING_FOR_EGG);
            Do(ready, incubator.pMyState == Incubator.IncubatorStates.HATCHED);
            Do(hatched, incubator.pMyState == Incubator.IncubatorStates.IDLE);
            Do(hatching, incubator.pMyState == Incubator.IncubatorStates.HATCHING);
            Do(buyable, incubator.pMyState == Incubator.IncubatorStates.LOCKED && incubator.pReadyToBuy);
            Do(locked, incubator.pMyState == Incubator.IncubatorStates.LOCKED && !incubator.pReadyToBuy);
            if (buyable.activeSelf)
                buyCost.text = incubator._HatcheryManager.pSlotUnlockCost.ToString();
            if (hatching.activeSelf)
                timeRemaining.text = incubator.GetStatusText(incubator.GetHatchTimeLeft());
        }
#endif
        public void Update()
        {
            UpdateState();
        }
    }
}
