﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class CppCodegenNodeFactory : NodeFactory
    {
        public CppCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup)
            : base(context, compilationModuleGroup, new CompilerGeneratedMetadataManager(compilationModuleGroup, context), new CoreRTNameMangler(true))
        {
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (CompilationModuleGroup.ContainsMethod(method))
            {
                return new CppMethodCodeNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(this, method);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            // TODO: this is wrong: this returns an assembly stub node
            return new UnboxingStubNode(method);
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(Tuple<ReadyToRunHelperId, object> helperCall)
        {
            // TODO: this is wrong: this returns an assembly stub node
            return new ReadyToRunHelperNode(this, helperCall.Item1, helperCall.Item2);
        }

        protected override IMethodNode CreateShadowConcreteMethodNode(MethodKey methodKey)
        {
            return new ShadowConcreteMethodNode<CppMethodCodeNode>(methodKey.Method,
                (CppMethodCodeNode)MethodEntrypoint(
                    methodKey.Method.GetCanonMethodTarget(CanonicalFormKind.Specific),
                    methodKey.IsUnboxingStub));
        }
    }
}
