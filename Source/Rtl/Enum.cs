﻿using System;
using System.Collections.Generic;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static Dictionary<String, Int32> EnumToMap(Type enumType)
    {
        Dictionary<String, Int32> Map = [];

        foreach (var Value in Enum.GetValues(enumType))
        {
            Map.Add(Enum.GetName(enumType, Value), Convert.ToInt32(Value));
        }

        return Map;
    }
}
