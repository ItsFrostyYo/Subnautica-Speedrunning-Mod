using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SubnauticaSpeedrunningMod.Bootstrap
{
    internal static class Ruleset7AssemblyPatcher
    {
        private const string OriginalPrefix = "__SubnauticaSetSeedOriginal_";
        private static Action<string> _write;

        public static void Patch(AssemblyDefinition assembly, Version runtimeVersion, Action<string> write)
        {
            _write = write;
            ModuleDefinition module = assembly.MainModule;
            AssemblyNameReference runtime = module.AssemblyReferences.FirstOrDefault(r => r.Name == "SubnauticaSpeedrunningMod.Runtime");
            if (runtime == null)
            {
                runtime = new AssemblyNameReference("SubnauticaSpeedrunningMod.Runtime", runtimeVersion);
                module.AssemblyReferences.Add(runtime);
            }

            TypeReference hooks = new TypeReference("SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset7", "DirectHooks", module, runtime, false);

            int redirectedCalls = 0;
            redirectedCalls += Wrap(module, hooks, "RandomStart", "GetRandomStartPoint", 0, "GetRandomStartPoint");
            redirectedCalls += Wrap(module, hooks, "CSVEntitySpawner", "GetPrefabForSlot", 2, "GetPrefabForSlot");
            redirectedCalls += Wrap(module, hooks, "EntitySlotsPlaceholder", "Spawn", 0, "SpawnPlaceholder");
            redirectedCalls += Wrap(module, hooks, "EntitySlot", "SpawnVirtualEntities", 0, "SpawnLiveSlot");
            redirectedCalls += Wrap(module, hooks, "TechFragment", "GetRandomTech", 0, "GetFragmentTech");
            redirectedCalls += Wrap(module, hooks, "SpawnRandom", "Start", 0, "StartSpawnRandom");
            redirectedCalls += Wrap(module, hooks, "SpawnRandomChild", "Start", 0, "StartSpawnRandomChild");
            redirectedCalls += Wrap(module, hooks, "RandomModel", "Start", 0, "StartRandomModel");
            redirectedCalls += Wrap(module, hooks, "SpawnStoredLoot", "SpawnRandomStoredItems", 0, "SpawnStoredLoot");
            redirectedCalls += Wrap(module, hooks, "DelayedSpawn", "Spawn", 0, "RunDelayedSpawn");
            redirectedCalls += Wrap(module, hooks, "Creature", "Start", 0, "StartCreature");
            redirectedCalls += Wrap(module, hooks, "ReefbackLife", "Initialize", 0, "InitializeReefbackLife");
            redirectedCalls += Wrap(module, hooks, "BreakableResource", "BreakIntoResources", 0, "BreakResource");
            redirectedCalls += Wrap(module, hooks, "Drillable", "OnDrill", 3, "DrillResource");
            redirectedCalls += Wrap(module, hooks, "Stalker", "CheckLoseTooth", 1, "CheckStalkerTooth");
            redirectedCalls += Wrap(module, hooks, "CrashedShipExploder", "SetExplodeTime", 0, "SetAuroraExplosionTime");
            redirectedCalls += Wrap(module, hooks, "ScaleVariance", "Start", 0, "StartScaleVariance");
            redirectedCalls += Wrap(module, hooks, "EcoRegion", "FindNearestTarget", 5, "FindNearestEcoTarget");

            int redirectedAiRandomCalls = RedirectCreatureAIRandomCalls(module, hooks);

            InjectCallAtStart(module, hooks, "SaveLoadManager", "InitializeNewGame", 0, "BeforeInitializeNewGame");
            InjectCallBeforeReturns(module, hooks, "Utils", "SetSavegameDir", 1, "AfterSetSavegameDir");
            InjectArgumentFilter(module, hooks, "EscapePod", "StartAtPosition", 1, 0, "ForceLifepodPosition");
            if (_write != null) _write("Ruleset 7 rewrote build-247 generation paths; redirected " + redirectedCalls + " method call sites and " + redirectedAiRandomCalls + " creature-AI random calls.");
        }

        private static int RedirectCreatureAIRandomCalls(ModuleDefinition module, TypeReference hooks)
        {
            string[] typeNames =
            {
                "AggressiveToPilotingVehicle", "AggressiveWhenSeeTarget", "AttackCyclops",
                "AvoidObstacles", "AvoidTerrain", "CollectShiny", "CrawlerAttackLastTarget",
                "Creature", "FleeWhenScared", "MoveOnGround", "MoveOnSurface",
                "MoveTowardsTarget", "MushroomAttack", "RangedAttackLastTarget", "Scareable",
                "School", "SeaDragonMeleeAttack", "Stalker", "SwimInGroup", "SwimInSchool",
                "SwimRandom", "TreaderMoveOnSurface"
            };

            TypeReference componentType = module.GetTypeReferences().First(t => t.FullName == "UnityEngine.Component");
            int count = 0;
            for (int typeIndex = 0; typeIndex < typeNames.Length; typeIndex++)
            {
                TypeDefinition type = FindType(module, typeNames[typeIndex]);
                for (int methodIndex = 0; methodIndex < type.Methods.Count; methodIndex++)
                {
                    MethodDefinition method = type.Methods[methodIndex];
                    if (method.IsStatic || !method.HasBody ||
                        (type.Name == "Stalker" && method.Name == OriginalPrefix + "CheckLoseTooth"))
                    {
                        continue;
                    }

                    ILProcessor il = method.Body.GetILProcessor();
                    Instruction[] calls = method.Body.Instructions
                        .Where(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference)
                        .ToArray();
                    for (int callIndex = 0; callIndex < calls.Length; callIndex++)
                    {
                        Instruction instruction = calls[callIndex];
                        MethodReference randomCall = (MethodReference)instruction.Operand;
                        if (randomCall.DeclaringType.FullName != "UnityEngine.Random")
                        {
                            continue;
                        }

                        string hookName;
                        if (randomCall.Name == "get_value") hookName = "NextCreatureAIValue";
                        else if (randomCall.Name == "get_onUnitSphere") hookName = "NextCreatureAIOnUnitSphere";
                        else if (randomCall.Name == "get_insideUnitSphere") hookName = "NextCreatureAIInsideUnitSphere";
                        else if (randomCall.Name == "get_insideUnitCircle") hookName = "NextCreatureAIInsideUnitCircle";
                        else if (randomCall.Name == "Range" && randomCall.Parameters.Count == 2 && randomCall.ReturnType.FullName == "System.Single") hookName = "NextCreatureAIRangeFloat";
                        else if (randomCall.Name == "Range" && randomCall.Parameters.Count == 2 && randomCall.ReturnType.FullName == "System.Int32") hookName = "NextCreatureAIRangeInt";
                        else continue;

                        MethodReference hook = new MethodReference(hookName, module.ImportReference(randomCall.ReturnType), hooks)
                        {
                            HasThis = false,
                            CallingConvention = MethodCallingConvention.Default
                        };
                        for (int parameterIndex = 0; parameterIndex < randomCall.Parameters.Count; parameterIndex++)
                        {
                            hook.Parameters.Add(new ParameterDefinition(module.ImportReference(randomCall.Parameters[parameterIndex].ParameterType)));
                        }
                        hook.Parameters.Add(new ParameterDefinition(module.ImportReference(componentType)));

                        // Rewrite the existing call instruction in place so any branch or
                        // exception-handler boundary targeting it now lands on ldarg.0 too.
                        // Inserting ldarg.0 before the old target lets branches skip the new
                        // argument and produces invalid IL on Unity 5.6's Mono JIT.
                        instruction.OpCode = OpCodes.Ldarg_0;
                        instruction.Operand = null;
                        il.InsertAfter(instruction, il.Create(OpCodes.Call, hook));
                        count++;
                    }
                }
            }
            return count;
        }

        private static int Wrap(ModuleDefinition module, TypeReference hooks, string typeName, string methodName, int parameterCount, string hookName)
        {
            TypeDefinition type = FindType(module, typeName);
            MethodDefinition original = FindMethod(type, methodName, parameterCount);
            if (type.Methods.Any(m => m.Name == OriginalPrefix + methodName && m.Parameters.Count == parameterCount))
            {
                throw new InvalidOperationException("Build-247 method was already wrapped unexpectedly: " + typeName + "." + methodName);
            }

            // Cecil call operands can point directly at a MethodDefinition token. Renaming that
            // definition does not make those existing calls resolve by the wrapper's new name;
            // they keep calling the renamed original. Capture every call site before the rename,
            // then explicitly retarget it to the wrapper below.
            List<Instruction> callSites = FindCallSites(module, original);

            MethodAttributes wrapperAttributes = original.Attributes;
            MethodImplAttributes wrapperImplAttributes = original.ImplAttributes;
            original.Name = OriginalPrefix + methodName;
            original.Attributes &= ~(MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract);

            MethodDefinition wrapper = new MethodDefinition(methodName, wrapperAttributes, module.ImportReference(original.ReturnType));
            wrapper.ImplAttributes = wrapperImplAttributes;
            for (int i = 0; i < original.Parameters.Count; i++)
            {
                ParameterDefinition source = original.Parameters[i];
                ParameterDefinition parameter = new ParameterDefinition(source.Name, source.Attributes, module.ImportReference(source.ParameterType));
                if (source.HasConstant)
                {
                    parameter.Constant = source.Constant;
                }
                wrapper.Parameters.Add(parameter);
            }

            MethodReference hook = new MethodReference(hookName, module.ImportReference(original.ReturnType), hooks)
            {
                HasThis = false,
                CallingConvention = MethodCallingConvention.Default
            };
            if (!original.IsStatic)
            {
                hook.Parameters.Add(new ParameterDefinition(module.ImportReference(type)));
            }
            for (int i = 0; i < wrapper.Parameters.Count; i++)
            {
                hook.Parameters.Add(new ParameterDefinition(module.ImportReference(wrapper.Parameters[i].ParameterType)));
            }

            ILProcessor il = wrapper.Body.GetILProcessor();
            if (!original.IsStatic)
            {
                il.Append(il.Create(OpCodes.Ldarg_0));
            }
            for (int i = 0; i < wrapper.Parameters.Count; i++)
            {
                il.Append(il.Create(OpCodes.Ldarg, wrapper.Parameters[i]));
            }
            il.Append(il.Create(OpCodes.Call, hook));
            il.Append(il.Create(OpCodes.Ret));
            type.Methods.Add(wrapper);

            MethodReference wrapperReference = module.ImportReference(wrapper);
            for (int i = 0; i < callSites.Count; i++)
            {
                callSites[i].Operand = wrapperReference;
            }

            return callSites.Count;
        }

        private static List<Instruction> FindCallSites(ModuleDefinition module, MethodDefinition target)
        {
            List<Instruction> result = new List<Instruction>();
            foreach (TypeDefinition type in EnumerateTypes(module.Types))
            {
                for (int i = 0; i < type.Methods.Count; i++)
                {
                    MethodDefinition method = type.Methods[i];
                    if (!method.HasBody || ReferenceEquals(method, target))
                    {
                        continue;
                    }

                    for (int j = 0; j < method.Body.Instructions.Count; j++)
                    {
                        Instruction instruction = method.Body.Instructions[j];
                        if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                        {
                            continue;
                        }

                        MethodReference called = instruction.Operand as MethodReference;
                        if (called == null)
                        {
                            continue;
                        }

                        if (ReferenceEquals(called, target) || SameSignature(called, target))
                        {
                            result.Add(instruction);
                        }
                    }
                }
            }
            return result;
        }

        private static bool SameSignature(MethodReference candidate, MethodDefinition target)
        {
            if (candidate.Name != target.Name ||
                candidate.DeclaringType.FullName != target.DeclaringType.FullName ||
                candidate.Parameters.Count != target.Parameters.Count)
            {
                return false;
            }

            for (int i = 0; i < target.Parameters.Count; i++)
            {
                if (candidate.Parameters[i].ParameterType.FullName != target.Parameters[i].ParameterType.FullName)
                {
                    return false;
                }
            }
            return true;
        }

        private static IEnumerable<TypeDefinition> EnumerateTypes(IEnumerable<TypeDefinition> roots)
        {
            foreach (TypeDefinition type in roots)
            {
                yield return type;
                foreach (TypeDefinition nested in EnumerateTypes(type.NestedTypes))
                {
                    yield return nested;
                }
            }
        }

        private static void InjectCallAtStart(ModuleDefinition module, TypeReference hooks, string typeName, string methodName, int parameterCount, string hookName)
        {
            MethodDefinition method = FindMethod(FindType(module, typeName), methodName, parameterCount);
            MethodReference hook = StaticHook(module, hooks, hookName, module.TypeSystem.Void);
            method.Body.GetILProcessor().InsertBefore(method.Body.Instructions[0], Instruction.Create(OpCodes.Call, hook));
        }

        private static void InjectCallBeforeReturns(ModuleDefinition module, TypeReference hooks, string typeName, string methodName, int parameterCount, string hookName)
        {
            MethodDefinition method = FindMethod(FindType(module, typeName), methodName, parameterCount);
            MethodReference hook = StaticHook(module, hooks, hookName, module.TypeSystem.Void);
            ILProcessor il = method.Body.GetILProcessor();
            Instruction[] returns = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
            for (int i = 0; i < returns.Length; i++)
            {
                il.InsertBefore(returns[i], Instruction.Create(OpCodes.Call, hook));
            }
        }

        private static void InjectInstanceCallBeforeReturns(ModuleDefinition module, TypeReference hooks, string typeName, string methodName, int parameterCount, string hookName)
        {
            TypeDefinition type = FindType(module, typeName);
            MethodDefinition method = FindMethod(type, methodName, parameterCount);
            if (method.IsStatic)
            {
                throw new InvalidOperationException("Expected instance method: " + typeName + "." + methodName);
            }

            MethodReference hook = StaticHook(module, hooks, hookName, module.TypeSystem.Void, module.ImportReference(type));
            ILProcessor il = method.Body.GetILProcessor();
            Instruction[] returns = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
            for (int i = 0; i < returns.Length; i++)
            {
                il.InsertBefore(returns[i], Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(returns[i], Instruction.Create(OpCodes.Call, hook));
            }
        }

        private static void InjectArgumentFilter(ModuleDefinition module, TypeReference hooks, string typeName, string methodName, int parameterCount, int parameterIndex, string hookName)
        {
            MethodDefinition method = FindMethod(FindType(module, typeName), methodName, parameterCount);
            TypeReference parameterType = module.ImportReference(method.Parameters[parameterIndex].ParameterType);
            MethodReference hook = StaticHook(module, hooks, hookName, parameterType, parameterType);
            ILProcessor il = method.Body.GetILProcessor();
            Instruction first = method.Body.Instructions[0];
            il.InsertBefore(first, il.Create(OpCodes.Ldarg, method.Parameters[parameterIndex]));
            il.InsertBefore(first, il.Create(OpCodes.Call, hook));
            il.InsertBefore(first, il.Create(OpCodes.Starg, method.Parameters[parameterIndex]));
        }

        private static void InjectCallResultFilter(ModuleDefinition module, TypeReference hooks,
            string ownerType, string ownerMethod, int ownerParameters, string calledType,
            string calledMethod, int calledParameters, string hookName)
        {
            MethodDefinition method = FindMethod(FindType(module, ownerType), ownerMethod, ownerParameters);
            Instruction call = method.Body.Instructions.FirstOrDefault(i =>
            {
                if (i.OpCode != OpCodes.Call && i.OpCode != OpCodes.Callvirt) return false;
                MethodReference reference = i.Operand as MethodReference;
                return reference != null && reference.DeclaringType.Name == calledType &&
                    reference.Name == calledMethod && reference.Parameters.Count == calledParameters;
            });
            if (call == null) throw new InvalidOperationException("Call site not found: " + ownerType + "." + ownerMethod + " -> " + calledType + "." + calledMethod);
            MethodReference hook = StaticHook(module, hooks, hookName, module.TypeSystem.String, module.TypeSystem.String);
            method.Body.GetILProcessor().InsertAfter(call, Instruction.Create(OpCodes.Call, hook));
        }

        private static void InjectAfterLabelString(ModuleDefinition module, TypeReference hooks,
            string ownerType, string ownerMethod, int ownerParameters, string labelPrefix, string hookName)
        {
            MethodDefinition method = FindMethod(FindType(module, ownerType), ownerMethod, ownerParameters);
            int labelIndex = -1;
            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                Instruction instruction = method.Body.Instructions[i];
                if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string &&
                    ((string)instruction.Operand).StartsWith(labelPrefix, StringComparison.Ordinal))
                {
                    labelIndex = i;
                    break;
                }
            }
            if (labelIndex < 0) throw new InvalidOperationException("Debug label not found: " + labelPrefix);
            Instruction labelCall = null;
            for (int i = labelIndex + 1; i < method.Body.Instructions.Count; i++)
            {
                Instruction instruction = method.Body.Instructions[i];
                MethodReference reference = instruction.Operand as MethodReference;
                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                    reference != null && reference.DeclaringType.FullName == "UnityEngine.GUILayout" && reference.Name == "Label")
                {
                    labelCall = instruction;
                    break;
                }
            }
            if (labelCall == null) throw new InvalidOperationException("GUILayout.Label call not found after: " + labelPrefix);
            MethodReference hook = StaticHook(module, hooks, hookName, module.TypeSystem.Void);
            method.Body.GetILProcessor().InsertAfter(labelCall, Instruction.Create(OpCodes.Call, hook));
        }

        private static MethodReference StaticHook(ModuleDefinition module, TypeReference hooks, string name, TypeReference returnType, params TypeReference[] parameters)
        {
            MethodReference hook = new MethodReference(name, module.ImportReference(returnType), hooks)
            {
                HasThis = false,
                CallingConvention = MethodCallingConvention.Default
            };
            for (int i = 0; i < parameters.Length; i++)
            {
                hook.Parameters.Add(new ParameterDefinition(module.ImportReference(parameters[i])));
            }
            return hook;
        }

        private static TypeDefinition FindType(ModuleDefinition module, string name)
        {
            TypeDefinition type = module.Types.FirstOrDefault(t => t.Name == name);
            if (type == null)
            {
                throw new InvalidOperationException("Build-247 type not found: " + name);
            }
            return type;
        }

        private static MethodDefinition FindMethod(TypeDefinition type, string name, int parameterCount)
        {
            MethodDefinition method = type.Methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == parameterCount);
            if (method == null)
            {
                throw new InvalidOperationException("Build-247 method not found: " + type.Name + "." + name + "/" + parameterCount);
            }
            return method;
        }
    }
}

