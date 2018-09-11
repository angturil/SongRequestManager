// Decompiled with JetBrains decompiler
// Type: Hidden_Notes.ReflectionUtil
// Assembly: Hidden Notes, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 019C193F-D0E0-4265-9184-825838CDBB64
// Assembly location: C:\Users\billybob\Documents\Downloads\Hidden_Notes.dll

using System.Reflection;

namespace HiddenBlocks
{
  public static class ReflectionUtil
  {
    public static void SetPrivateField(object obj, string fieldName, object value)
    {
      obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(obj, value);
    }

    public static T GetPrivateField<T>(object obj, string fieldName)
    {
      return (T) obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    }

    public static object GPF(object obj, string fieldName)
    {
      return obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    }

    public static void SetPrivateProperty(object obj, string propertyName, object value)
    {
      obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(obj, value, (object[]) null);
    }

    public static void InvokePrivateMethod(object obj, string methodName, object[] methodParams)
    {
      obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, methodParams);
    }
  }
}
