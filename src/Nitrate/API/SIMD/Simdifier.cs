﻿using System.Collections.Generic;
using MonoMod.Cil;
using TeamCatalyst.Nitrate.SIMD;

namespace TeamCatalyst.Nitrate.API.SIMD;

public static class Simdifier {
    private static readonly List<ISimdifier> simdifiers = new() {
        new Vector2Simdifier(),
    };

    /// <summary>
    ///     Registers a "simdifier".
    /// </summary>
    public static void RegisterSimdifier(ISimdifier simdifier) {
        simdifiers.Add(simdifier);
    }

    /// <summary>
    ///     "Simdifies" a method.
    /// </summary>
    /// <param name="c">The cursor of the method to "simdify."</param>
    public static void Simdify(ILCursor c) {
        foreach (var simdifier in simdifiers) {
            c.Index = 0;
            simdifier.Simdify(c);
        }
    }
}
