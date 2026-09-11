using System;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    internal static class InputMerge
    {
        internal static float StrongerAxis(float native, float kontrol)
        {
            if (float.IsNaN(native) || float.IsInfinity(native)) native = 0f;
            if (float.IsNaN(kontrol) || float.IsInfinity(kontrol)) kontrol = 0f;
            return Math.Abs(kontrol) > Math.Abs(native) ? kontrol : native;
        }
    }
}
