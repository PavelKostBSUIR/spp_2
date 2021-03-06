using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Faker
{
    public class Faker
    {
        public Random random = new Random();
        public Dictionary<Type, IDTOGenerator> generatorDictionary;
        private Stack<Type> circleStack;
        public Faker()
        {
            generatorDictionary = new Dictionary<Type, IDTOGenerator>();
            circleStack = new Stack<Type>();
            Assembly generatorsAssembly = Assembly.GetAssembly(typeof(IDTOGenerator));
            AddGenerators(generatorsAssembly);
            LoadPlugins();
        }
        public void AddGenerators(Assembly assembly)
        {
            Type[] typeArray = assembly.GetTypes();
            foreach(Type type in typeArray)
            {
                if (typeof(IDTOGenerator).IsAssignableFrom(type) && type.IsClass)
                {
                    ConstructorInfo[] constructors = type.GetConstructors();
                    IDTOGenerator generator = (IDTOGenerator)constructors[0].Invoke(new object[] { random });
                    if (generatorDictionary.Keys.Contains(generator.generatedType))
                        continue;
                    else
                        generatorDictionary.Add(generator.generatedType, generator);
                }
            }
        }
        public void LoadPlugins()
        {
            const string plugDir = "D:\\SPP\\MPP_2\\Faker\\bin\\Debug\\plugins";
            foreach(string path in Directory.GetFiles(plugDir, "*.dll"))
            {
                Assembly assembly = Assembly.LoadFile(new FileInfo(path).FullName);
                AddGenerators(assembly);
            }
        }
        public object CreateWithConstructor(Type type)
        {
            object result;
            IEnumerable<ConstructorInfo> constructors = type.GetConstructors().OrderByDescending(constructor => constructor.GetParameters().Length);
            if (constructors == null) return null;
            foreach (ConstructorInfo constructor in constructors)
            {
                ParameterInfo[] parametersInfo = constructor.GetParameters();
                object[] parameters = new object[parametersInfo.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i] = Create(parametersInfo[i].ParameterType);
                }
                try
                {
                    result = constructor.Invoke(parameters);
                    return result;
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }
        public object FillFields(object obj)
        {
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                field.SetValue(obj, Create(field.FieldType));
            }
            return obj;
        }
        public object FillProperties(object obj)
        {
            PropertyInfo[] properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.CanWrite)
                    property.SetValue(obj, Create(property.PropertyType));
            }
            return obj;
        }
        public T Create<T>()
        {
            return (T)Create(typeof(T));
        }
        public object Create(Type type)
        {
            if (circleStack.Count >= 5)
            {
                throw new Exception("Circle dependency!");
            }
            circleStack.Push(type);
            if (generatorDictionary.TryGetValue(type, out IDTOGenerator generator))
            {
                if (generator != null)
                {
                    circleStack.Pop();
                    return generator.Generate();
                }
            }
            if (type.IsGenericType)
            {
                circleStack.Pop();
                ListGenerator listGenerator = new ListGenerator(random);
                object result = listGenerator.Generate(type.GetGenericArguments()[0]);
                return CastList(type.GetGenericArguments()[0], result);
            }
            if (type.IsClass)
            {
                object result = CreateWithConstructor(type);
                result = FillFields(result);
                result = FillProperties(result);
                circleStack.Pop();
                return result;
            }
            return null;
        }

        private object CastList(Type type, object obj)
        {
            Type newListType = typeof(List<>).MakeGenericType(new Type[] { type });
            IList list = (IList)Activator.CreateInstance(newListType);
            foreach (object elem in (List<object>)obj)
            {
                list.Add(Convert.ChangeType(elem, type));
            }
            return list;
        }
    }
}


