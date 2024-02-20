using System;
using System.Collections.Generic;

namespace KNSoft.C4Lib;

static partial class Rtl
{
    public static T? EnumerableFirstOrNull<T>(IEnumerable<T> Source, Func<T, Boolean> Predicate) where T : class
    {
        foreach (T Item in Source)
        {
            if (Predicate(Item))
            {
                return Item;
            }
        }

        return null;
    }
}
