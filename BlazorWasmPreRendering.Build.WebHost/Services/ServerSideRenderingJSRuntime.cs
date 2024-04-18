using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using static System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost.Services
{
    internal class ServerSideRenderingJSRuntime : IJSRuntime
#if !NET9_0_OR_GREATER
        , IJSUnmarshalledRuntime
#endif
    {
        private const string GetLazyAssemblies = "window.Blazor._internal.getLazyAssemblies";
        private const string ReadLazyAssemblies = "window.Blazor._internal.readLazyAssemblies";
        private const string ReadLazyPDBs = "window.Blazor._internal.readLazyPdbs";

        private const string ExceptionMessage =
                   "JavaScript interop calls cannot be issued at this time. This is because the component is being " +
                   "statically rendered. When prerendering is enabled, JavaScript interop calls can only be performed " +
                   "during the OnAfterRenderAsync lifecycle method.";
        /*

                         //throw new InvalidOperationException(ExceptionMessage);
         
         */
        private readonly CustomAssemblyLoader _AssemblyLoader;

        private readonly List<byte[]> _LazyAssemblyBytes = new();

        public ServerSideRenderingJSRuntime(CustomAssemblyLoader assemblyLoader)
        {
            this._AssemblyLoader = assemblyLoader;
        }

        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(PublicConstructors | PublicFields | PublicProperties)] TValue>(string identifier, object?[]? args)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(PublicConstructors | PublicFields | PublicProperties)] TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        public TResult InvokeUnmarshalled<TResult>(string identifier)
        {
            if (identifier == ReadLazyAssemblies)
            {
                var assemblyBytes = this.ReadLazyAssembliesImplements();
                return (TResult)((object)assemblyBytes);
            }
            if (identifier == ReadLazyPDBs)
            {
                var pdbBytes = this.ReadLazyPDBsImplements();
                return (TResult)((object)pdbBytes);
            }
            throw new InvalidOperationException(ExceptionMessage);
        }

        public TResult InvokeUnmarshalled<T0, TResult>(string identifier, T0 arg0)
        {
            if (identifier == GetLazyAssemblies)
            {
                object count = this.GetLazyAssembliesImplements(arg0 as string[]);
                return (TResult)((object)Task.FromResult(count));
            }
            throw new InvalidOperationException(ExceptionMessage);
        }

        public TResult InvokeUnmarshalled<T0, T1, TResult>(string identifier, T0 arg0, T1 arg1)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        public TResult InvokeUnmarshalled<T0, T1, T2, TResult>(string identifier, T0 arg0, T1 arg1, T2 arg2)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        private int GetLazyAssembliesImplements(string[]? newAssembliesToLoad)
        {
            this._LazyAssemblyBytes.Clear();
            if (newAssembliesToLoad == null) return 0;

            foreach (var assemblyName in newAssembliesToLoad)
            {
                if (this._AssemblyLoader.TryGetAssemblyBytes(new AssemblyName(assemblyName), out var assemblyBytes))
                {
                    this._LazyAssemblyBytes.Add(assemblyBytes);
                }
            }
            return this._LazyAssemblyBytes.Count;
        }

        private byte[][] ReadLazyAssembliesImplements()
        {
            return this._LazyAssemblyBytes.ToArray();
        }

        private byte[][] ReadLazyPDBsImplements()
        {
            return this._LazyAssemblyBytes.Select(_ => new byte[0]).ToArray();
        }
    }
}