using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HandyTweaks
{
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
}