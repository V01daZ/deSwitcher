using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DeSwitcher.Runtime
{
    public static class ModInvoker
    {
        private static readonly Traverse ModApi = Traverse.Create(AccessTools.TypeByName("ModAPI"));

        public static bool Invoke(Assembly assembly, object metaData, string entryPoint, string methodName)
        {
            if (assembly == null || metaData == null)
                return false;
            Type type = assembly.GetType(entryPoint);
            MethodInfo method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                return false;

            SetMetadata(metaData, true);
            try
            {
                method.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogError((e as TargetInvocationException)?.InnerException ?? e);
            }
            SetMetadata(metaData, false);
            return true;
        }

        public static void SetMetadata(object metaData, bool valid)
        {
            if (valid)
                ModApi.Property("Metadata").SetValue(metaData);
            ModApi.Field("metaDataIsValid").SetValue(valid);
        }
    }
}
