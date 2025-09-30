using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace HandyTweaks
{
    public class ColorSlider : MonoBehaviour
    {
        public ColorSlider(Slider slider, TMP_InputField input, Image back, Image tint)
        {
            this.slider = slider;
            this.input = input;
            this.back = back;
            this.tint = tint;
        }
        public ColorSlider(Transform transform, string slider = "Slider", string input = "Input", string back = "Slider/Background/GradientBackground", string tint = "Slider/Background/GradientBackground/Gradient")
            : this(transform.Find(slider).GetComponent<Slider>(), transform.Find(input).GetComponent<TMP_InputField>(), transform.Find(back).GetComponent<Image>(), transform.Find(tint).GetComponent<Image>())
        { }
        public Slider slider;
        public TMP_InputField input;
        public Image back;
        public Image tint;
    }
}
