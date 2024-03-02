﻿using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace TeamCatalyst.Nitrate.Utilities;

/// <summary>
///     Basic utilities for dealing with IL.
/// </summary>
internal static class IntermediateLanguageUtil {
    /// <summary>
    ///     Clones the given method body to the cursor, wholesale copying all
    ///     relevant contents (instructions, parameters, variables, etc.).
    /// </summary>
    /// <param name="body">The body to copy from.</param>
    /// <param name="c">The cursor to copy to.</param>
    public static void CloneMethodBodyToCursor(MethodBody body, ILCursor c) {
        c.Index = 0;

        c.Body.MaxStackSize = body.MaxStackSize;
        c.Body.InitLocals = body.InitLocals;
        c.Body.LocalVarToken = body.LocalVarToken;

        foreach (var instr in body.Instructions) {
            c.Emit(instr.OpCode, instr.Operand);
        }

        for (var i = 0; i < body.Instructions.Count; i++) {
            c.Instrs[i].Offset = body.Instructions[i].Offset;
        }

        foreach (var instr in c.Body.Instructions) {
            instr.Operand = instr.Operand switch {
                Instruction target => c.Body.Instructions[body.Instructions.IndexOf(target)],
                Instruction[] targets => targets.Select(x => c.Body.Instructions[body.Instructions.IndexOf(x)]).ToArray(),
                _ => instr.Operand,
            };
        }

        c.Body.ExceptionHandlers.AddRange(
            body.ExceptionHandlers.Select(
                x => new ExceptionHandler(x.HandlerType) {
                    TryStart = x.TryStart is null ? null : c.Body.Instructions[body.Instructions.IndexOf(x.TryStart)],
                    TryEnd = x.TryEnd is null ? null : c.Body.Instructions[body.Instructions.IndexOf(x.TryEnd)],
                    FilterStart = x.FilterStart is null ? null : c.Body.Instructions[body.Instructions.IndexOf(x.FilterStart)],
                    HandlerStart = x.HandlerStart is null ? null : c.Body.Instructions[body.Instructions.IndexOf(x.HandlerStart)],
                    HandlerEnd = x.HandlerEnd is null ? null : c.Body.Instructions[body.Instructions.IndexOf(x.HandlerEnd)],
                    CatchType = x.CatchType is null ? null : c.Body.Method.Module.ImportReference(x.CatchType),
                }
            )
        );

        c.Body.Variables.AddRange(body.Variables.Select(x => new VariableDefinition(x.VariableType)));

        c.Method.CustomDebugInformations.AddRange(
            body.Method.CustomDebugInformations.Select(
                x => {
                    switch (x) {
                        case AsyncMethodBodyDebugInformation asyncInfo: {
                            AsyncMethodBodyDebugInformation info = new();

                            if (asyncInfo.CatchHandler.Offset >= 0) {
                                info.CatchHandler = asyncInfo.CatchHandler.IsEndOfMethod ? new InstructionOffset() : new InstructionOffset(resolveInstrOff(info.CatchHandler.Offset));
                            }

                            info.Yields.AddRange(asyncInfo.Yields.Select(y => y.IsEndOfMethod ? new InstructionOffset() : new InstructionOffset(resolveInstrOff(y.Offset))));
                            info.Resumes.AddRange(asyncInfo.Resumes.Select(y => y.IsEndOfMethod ? new InstructionOffset() : new InstructionOffset(resolveInstrOff(y.Offset))));

                            return info;
                        }

                        case StateMachineScopeDebugInformation stateInfo: {
                            StateMachineScopeDebugInformation info = new();
                            info.Scopes.AddRange(stateInfo.Scopes.Select(y => new StateMachineScope(resolveInstrOff(y.Start.Offset), y.End.IsEndOfMethod ? null : resolveInstrOff(y.End.Offset))));

                            return info;
                        }

                        default:
                            return x;
                    }
                }
            )
        );

        c.Method.DebugInformation.SequencePoints.AddRange(
            body.Method.DebugInformation.SequencePoints.Select(
                x => new SequencePoint(resolveInstrOff(x.Offset), x.Document) {
                    StartLine = x.StartLine,
                    StartColumn = x.StartColumn,
                    EndLine = x.EndLine,
                    EndColumn = x.EndColumn,
                }
            )
        );

        c.Index = 0;

        return;

        Instruction resolveInstrOff(int off) {
            for (var i = 0; i < body.Instructions.Count; i++) {
                if (body.Instructions[i].Offset == off) {
                    return c.Body.Instructions[i];
                }
            }

            throw new Exception("Could not resolve instruction offset");
        }
    }
}
