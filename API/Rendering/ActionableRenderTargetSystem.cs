﻿using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Nitrate.API.Rendering;

/// <summary>
///     Handles the registration of and rendering of render targets that deal
///     with decentralized, arbitrary actions.
/// </summary>
/// <remarks>
///     This system splits rendering logic into two parts: updating ([the
///     execution] of actions) that occurs in <see cref="PostUpdateEverything"/>
///     and rendering (drawing of the render target), which occurs in a detour
///     targeting <see cref="Main.DrawProjectiles"/> (in post).
///     <br />
///     Inheritance from <see cref="ModSystem"/> is not an API guarantee but
///     rather an implementation detail.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public sealed class ActionableRenderTargetSystem : ModSystem
{
    private sealed class DefaultActionableRenderTarget : IActionableRenderTarget
    {
        public List<Action> Actions { get; } = new();

        public RenderTarget2D RenderTarget { get; } = new(
            Main.graphics.GraphicsDevice,
            Main.screenWidth,
            Main.screenHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        public void Finish()
        {
            Actions.Clear();
        }

        public IActionableRenderTarget ReinitForResize() => new DefaultActionableRenderTarget();

        public void Dispose()
        {
            RenderTarget.Dispose();
        }
    }

    /// <summary>
    ///     The dictionary of render targets and their associated rendering
    ///     data.
    /// </summary>
    private static readonly Dictionary<string, IActionableRenderTarget> targets = new();

    public override void Load()
    {
        base.Load();

        On_Main.DrawProjectiles += DrawRenderTargets;
        Main.OnResolutionChanged += TargetsNeedResizing;
    }

    public override void Unload()
    {
        base.Unload();

        On_Main.DrawProjectiles -= DrawRenderTargets;
        Main.OnResolutionChanged -= TargetsNeedResizing;

        Main.RunOnMainThread(() =>
        {
            foreach (IActionableRenderTarget target in targets.Values)
            {
                target.Dispose();
            }
        });
    }

    public override void PostUpdateEverything()
    {
        base.PostUpdateEverything();

        if (Main.gameMenu || Main.dedServ)
        {
            return;
        }

        GraphicsDevice device = Main.graphics.GraphicsDevice;

        foreach (IActionableRenderTarget target in targets.Values)
        {
            RenderTargetBinding[] bindings = device.GetRenderTargets();

            device.SetRenderTarget(target.RenderTarget);
            device.Clear(Color.Transparent);

            foreach (Action action in target.Actions)
            {
                action.Invoke();
            }

            device.SetRenderTargets(bindings);
            target.Finish();
        }
    }

    private static void DrawRenderTargets(On_Main.orig_DrawProjectiles orig, Main self)
    {
        orig(self);

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointWrap,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        foreach (string id in targets.Keys)
        {
            Main.spriteBatch.Draw(targets[id].RenderTarget, Vector2.Zero, Color.White);
        }

        Main.spriteBatch.End();
    }

    private static void TargetsNeedResizing(FnaVector2 _)
    {
        Main.RunOnMainThread(() =>
        {
            foreach (string id in targets.Keys)
            {
                IActionableRenderTarget target = targets[id];
                target.Dispose();
                targets[id] = target.ReinitForResize();
            }
        });
    }

    /// <summary>
    ///     Registers a default render target for use with a drawing action or
    ///     list of drawing actions.
    /// </summary>
    /// <param name="id">The ID of the render target and its layer.</param>
    public static void RegisterRenderTarget(string id)
    {
        RegisterRenderTarget(id, static () => new DefaultActionableRenderTarget());
    }

    /// <summary>
    ///     Registers a render target for use with a drawing action or list of
    ///     drawing actions.
    /// </summary>
    /// <param name="id">The ID of the render target and its layer.</param>
    /// <param name="target">
    ///     A function returning the target to render (to be executed on the
    ///     main thread).
    /// </param>
    public static void RegisterRenderTarget(string id, Func<IActionableRenderTarget> target)
    {
        Main.RunOnMainThread(() =>
        {
            targets[id] = target();
        });
    }

    /// <summary>
    ///     Queues a render action to be executed on the next rendering step.
    /// </summary>
    /// <param name="id">The ID of the render target to render to.</param>
    /// <param name="renderAction">The action to be executed.</param>
    public static void QueueRenderAction(string id, Action renderAction)
    {
        targets[id].Actions.Add(renderAction);
    }
}