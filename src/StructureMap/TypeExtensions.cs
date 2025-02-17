using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StructureMap.Graph;

namespace StructureMap.TypeRules
{

    public static partial class TypeExtensions
    {
        public static bool IsConcreteWithDefaultCtor(this Type type)
        {
            if (type.IsConcrete())
            {
                return type.GetConstructor(new Type[0]) != null;
            }

            return false;
        }

        public static bool HasAttribute<T>(this MemberInfo provider) where T : Attribute
        {
            var atts = provider.GetCustomAttributes(typeof (T), true);
            return atts.Any();
        }

        

#if !NET45
        public static bool HasAttribute<T>(this Assembly assembly) where T : Attribute
        {
            return assembly.CustomAttributes.OfType<T>().Any();
        }



#endif

#if NETSTANDARD1_5
        public static IEnumerable<Type> GetInterfaces(Type type)
        {
            return type.GetTypeInfo().GetInterfaces();
        }
#endif

        internal static void ForAttribute<T>(this MemberInfo provider, Action<T> action)
            where T : Attribute
        {
            foreach (T attribute in provider.GetCustomAttributes(typeof (T), true))
            {
                action(attribute);
            }
        }

        internal static void ForAttribute<T>(this ParameterInfo provider, Action<T> action)
            where T : Attribute
        {
            foreach (T attribute in provider.GetCustomAttributes(typeof(T), true))
            {
                action(attribute);
            }
        }

        internal static T GetAttribute<T>(this MemberInfo provider)
            where T : Attribute
        {
            return provider.GetCustomAttributes(typeof(T), true).OfType<T>().FirstOrDefault();
        }

        public static T CloseAndBuildAs<T>(this Type openType, params Type[] parameterTypes)
        {
            var closedType = openType.MakeGenericType(parameterTypes);
            return (T) Activator.CreateInstance(closedType);
        }

        public static T CloseAndBuildAs<T>(this Type openType, object ctorArgument, params Type[] parameterTypes)
        {
            var closedType = openType.MakeGenericType(parameterTypes);
            return (T) Activator.CreateInstance(closedType, ctorArgument);
        }


        public static bool Closes(this Type type, Type openType)
        {
            var baseType = type.GetTypeInfo().BaseType;
            if (baseType == null) return false;

            var closes = baseType.GetTypeInfo().IsGenericType && baseType.GetTypeInfo().GetGenericTypeDefinition() == openType;
            return closes || baseType.Closes(openType);
        }

        public static bool IsInNamespace(this Type type, string nameSpace)
        {
            var subNameSpace = nameSpace + ".";
            return type.Namespace != null && (type.Namespace.Equals(nameSpace) || type.Namespace.StartsWith(subNameSpace));
        }

        public static bool IsOpenGeneric(this Type type)
        {
            return type.GetTypeInfo().IsGenericTypeDefinition || type.GetTypeInfo().ContainsGenericParameters;
        }


        public static bool IsConcreteAndAssignableTo(this Type TPluggedType, Type pluginType)
        {
            return TPluggedType.IsConcrete() && pluginType.GetTypeInfo().IsAssignableFrom(TPluggedType.GetTypeInfo());
        }

        public static bool ImplementsInterfaceTemplate(this Type TPluggedType, Type templateType)
        {
            if (!TPluggedType.IsConcrete()) return false;

            return TPluggedType.GetInterfaces().
                Any(itfType => itfType.GetTypeInfo().IsGenericType && itfType.GetGenericTypeDefinition() == templateType);
        }

        public static Type FindFirstInterfaceThatCloses(this Type TPluggedType, Type templateType)
        {
            return TPluggedType.FindInterfacesThatClose(templateType).FirstOrDefault();
        }

        public static IEnumerable<Type> FindInterfacesThatClose(this Type TPluggedType, Type templateType)
        {
            return rawFindInterfacesThatCloses(TPluggedType, templateType).Distinct();
        }

        private static IEnumerable<Type> rawFindInterfacesThatCloses(Type TPluggedType, Type templateType)
        {
            if (!TPluggedType.IsConcrete()) yield break;

            if (templateType.GetTypeInfo().IsInterface)
            {
                foreach (
                    var interfaceType in
                        TPluggedType.GetInterfaces()
                            .Where(type => type.GetTypeInfo().IsGenericType && (type.GetGenericTypeDefinition() == templateType)))
                {
                    yield return interfaceType;
                }
            }
            else if (TPluggedType.GetTypeInfo().BaseType.GetTypeInfo().IsGenericType &&
                     (TPluggedType.GetTypeInfo().BaseType.GetGenericTypeDefinition() == templateType))
            {
                yield return TPluggedType.GetTypeInfo().BaseType;
            }

            if (TPluggedType.GetTypeInfo().BaseType == typeof(object)
                || TPluggedType == typeof(object)
                ) yield break;

            foreach (var interfaceType in rawFindInterfacesThatCloses(TPluggedType.GetTypeInfo().BaseType, templateType))
            {
                yield return interfaceType;
            }
        }

        public static bool IsNullable(this Type type)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static Type GetInnerTypeFromNullable(this Type nullableType)
        {
            return nullableType.GetGenericArguments()[0];
        }

        public static string GetName(this Type type)
        {
            return type.GetTypeInfo().IsGenericType ? GetGenericName(type) : type.Name;
        }

        public static string GetFullName(this Type type)
        {
            return type.GetTypeInfo().IsGenericType ? GetGenericName(type) : type.FullName;
        }

        public static string GetTypeName(this Type type)
        {
            return type.GetTypeInfo().IsGenericType ? GetGenericName(type) : type.Name;
        }

        private static string GetGenericName(Type type)
        {
            var parameters = type.GetGenericArguments().Select(t => t.GetName());
            string parameterList = String.Join(", ", parameters);
            return "{0}<{1}>".ToFormat(type.Name.Split('`').First(), parameterList);
        }

        public static bool CanBeCreated(this Type type)
        {
            return type.IsConcrete() && type.HasConstructors();
        }

        public static IEnumerable<Type> AllInterfaces(this Type type)
        {
            foreach (var @interface in type.GetInterfaces())
            {
                yield return @interface;
            }
        }

        /// <summary>
        /// Determines if the PluggedType can be upcast to the pluginType
        /// </summary>
        /// <param name="pluginType"></param>
        /// <param name="pluggedType"></param>
        /// <returns></returns>
        public static bool CanBeCastTo(this Type pluggedType, Type pluginType)
        {
            if (pluggedType == null) return false;

            if (pluggedType == pluginType) return true;


            if (pluginType.IsOpenGeneric())
            {
                return GenericsPluginGraph.CanBeCast(pluginType, pluggedType);
            }

            if (IsOpenGeneric(pluggedType))
            {
                return false;
            }


            return pluginType.GetTypeInfo().IsAssignableFrom(pluggedType.GetTypeInfo());
        }

        public static bool CanBeCastTo<T>(this Type pluggedType)
        {
            return pluggedType.CanBeCastTo(typeof (T));
        }

        public static bool CouldCloseTo(this Type openConcretion, Type closedInterface)
        {
            var openInterface = closedInterface.GetGenericTypeDefinition();
            var arguments = closedInterface.GetGenericArguments();

            var concreteArguments = openConcretion.GetGenericArguments();
            return arguments.Length == concreteArguments.Length && openConcretion.CanBeCastTo(openInterface);
        }

        public static bool IsString(this Type type)
        {
            return type == typeof (string);
        }

        public static bool IsPrimitive(this Type type)
        {
            return type.GetTypeInfo().IsPrimitive && !IsString(type) && type != typeof(IntPtr);
        }

        public static bool IsSimple(this Type type)
        {
            if (type == null) return false;

            return type.GetTypeInfo().IsPrimitive || IsString(type) || type.GetTypeInfo().IsEnum || IsNullable(type);
        }

        public static bool IsInterfaceOrAbstract(this Type type)
        {
            return type.GetTypeInfo().IsInterface || type.GetTypeInfo().IsAbstract;
        }

        public static bool IsChild(this Type type)
        {
            return IsPrimitiveArray(type) || (!type.IsArray && !IsSimple(type));
        }

        public static bool IsChildArray(this Type type)
        {
            return type.IsArray && !IsSimple(type.GetElementType());
        }

        public static bool IsPrimitiveArray(this Type type)
        {
            return type.IsArray && IsSimple(type.GetElementType());
        }

        public static bool IsConcrete(this Type type)
        {
            return !type.GetTypeInfo().IsAbstract && !type.GetTypeInfo().IsInterface;
        }

        public static bool IsAutoFillable(this Type type)
        {
            return IsChild(type) || IsChildArray(type);
        }

        public static bool HasConstructors(this Type type)
        {
            return type.GetPublicAndInternalConstructors().Any();
        }
        
        /// <summary>
        /// Returns all public and internal (but not internal protected) constructors
        /// for <paramref name="type"/>,
        /// but only if <paramref name="type"/> is a non-abstract class.
        /// </summary>
        /// <returns>
        /// An array of zero, or more, <see cref="ConstructorInfo"/> instances.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="type"/> is null.
        /// </exception>
        public static ConstructorInfo[] GetPublicAndInternalConstructors(this Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            
            if (type.IsInterfaceOrAbstract()) return new ConstructorInfo[0];
            
            return type.GetConstructors(BindingFlags.Public
                                        | BindingFlags.NonPublic
                                        | BindingFlags.Instance).Where(ci =>
                ci.IsPublic /* public */
                || ci.IsAssembly /* internal, but not internal protected */).ToArray();
        }

        public static bool IsVoidReturn(this Type type)
        {
            return type == null || type == typeof (void);
        }
    }
}
