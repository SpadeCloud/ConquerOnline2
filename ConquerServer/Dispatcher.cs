﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConquerServer
{
    public interface IDispatcherAttribute<T>
    {
        public T Key { get; }
    }

    public class Dispatcher<TKey, TAttribute, TDelegate>
        where TDelegate : Delegate
        where TAttribute : Attribute, IDispatcherAttribute<TKey>
        where TKey : notnull
    {
        private Dictionary<TKey, TDelegate> m_Actions;

        public Dispatcher(params Type[] types)
        {
            m_Actions = new Dictionary<TKey, TDelegate>();
            foreach (var type in types)
                RegisterMethods(type);
        }

        public void RegisterMethods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            foreach (MethodInfo methodInfo in methods)
            {
                TAttribute? attr = methodInfo.GetCustomAttribute<TAttribute>();
                if (attr != null)
                {
                    TDelegate action = (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), null, methodInfo);
                    m_Actions[attr.Key] = action;
                }
            }
        }

        public void RegisterMethods(object instance) => RegisterMethods(instance.GetType());

        public TDelegate? GetMethod(TKey key)
        {
            TDelegate? action;
            return m_Actions.TryGetValue(key, out action) ? action : null;
        }

        public TDelegate? this[TKey key]
        {
            get { return GetMethod(key); }
        }
    }
}