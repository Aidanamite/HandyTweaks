using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandyTweaks
{
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

    public enum StatCompareResult
    {
        Equal,
        Greater,
        Lesser
    }

    enum Side
    {
        Good,
        Middle,
        Bad
    }
}
