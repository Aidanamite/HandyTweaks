using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using UnityEngine;

namespace HandyTweaks
{
    public class SoundOnClick : MonoBehaviour, IPointerClickHandler
    {
        public SnSound Sound;
        public void OnPointerClick(PointerEventData data)
        {
            Sound.Play();
        }
    }
}
