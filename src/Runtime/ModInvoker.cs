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

            try
            {
                WithMetadata(metaData, () => method.Invoke(null, null));
            }
            catch (Exception e)
            {
                Debug.LogError((e as TargetInvocationException)?.InnerException ?? e);
            }
            return true;
        }

        public static void WithMetadata(object metaData, Action action)
        {
            object previous = ModApi.Field("metadata").GetValue();
            bool wasValid = ModApi.Field("metaDataIsValid").GetValue() is bool b && b;
            ModApi.Field("metadata").SetValue(metaData);
            ModApi.Field("metaDataIsValid").SetValue(true);
            try
            {
                action();
            }
            finally
            {
                ModApi.Field("metadata").SetValue(previous);
                ModApi.Field("metaDataIsValid").SetValue(wasValid);
            }
        }
    }
}
