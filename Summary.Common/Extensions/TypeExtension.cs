using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Summary.Common.Extensions
{
    public static class TypeExtensions
    {
        private static readonly ConcurrentDictionary<(Type, Type), bool> _assignableToGenericTypeCache = new ConcurrentDictionary<(Type, Type), bool>();

        /// <summary>
        /// (Cached) Checks if a type can be assigned to a specified open generic interface type.
        /// </summary>
        /// <param name="typeToCheck">The type to check.</param>
        /// <param name="genericInterface">The open generic interface type (e.g., typeof(IEntity<>)).</param>
        /// <returns>True if the type implements the generic interface; otherwise, false.</returns>
        public static bool IsAssignableToGenericType(this Type typeToCheck, Type genericInterface)
        {
            var cacheKey = (typeToCheck, genericInterface);

            if (_assignableToGenericTypeCache.TryGetValue(cacheKey, out var isAssignable))
            {
                return isAssignable;
            }

            var result = typeToCheck.GetInterfaces().Any(it => it.IsGenericType && it.GetGenericTypeDefinition() == genericInterface)
                         || typeToCheck.BaseType != null && typeToCheck.BaseType.IsAssignableToGenericType(genericInterface);

            _assignableToGenericTypeCache[cacheKey] = result;

            return result;
        }
    }
}