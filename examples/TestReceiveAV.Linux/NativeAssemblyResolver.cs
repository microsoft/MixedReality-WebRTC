using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TestReceiveAV.Linux
{
    public static class NativeAssemblyResolver
    {
        public static void RegisterResolver<T>()
        {
            RegisterResolver(typeof(T).Assembly);
        }

        public static void RegisterResolver(Assembly assembly)
        {
            NativeLibrary.SetDllImportResolver(assembly, ImportResolver);
        }

        private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            IntPtr libHandle = IntPtr.Zero;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var filename = $"runtimes/linux-x64/native/lib{libraryName}.so";
                if (!File.Exists(filename))
                {
                    throw new FileNotFoundException(filename);
                }
                NativeLibrary.TryLoad(filename, assembly, searchPath, out libHandle);
            }

            return libHandle;
        }
    }
}
