using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Generic types, constraints (where T : ...), generic methods, nested generics and the resulting
///     call/constraint edges. The whole module is parsed as one self-contained snippet (migrated from the
///     former Core.Generics approval fixture). Note: the constraint interfaces here (IComparable, ...) are
///     the module's own, not the System ones.
/// </summary>
[TestFixture]
public class GenericsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                using System;
                                using System.Collections.Generic;

                                namespace Demo;

                                public class GenericContainer<T>
                                {
                                    private T _value;
                                    public GenericContainer(T value) { _value = value; }
                                    public T GetValue() { return _value; }
                                    public void SetValue(T value) { _value = value; }
                                    public bool IsDefault() { return EqualityComparer<T>.Default.Equals(_value, default!); }
                                }

                                public class GenericPair<TFirst, TSecond>
                                {
                                    public GenericPair(TFirst first, TSecond second) { First = first; Second = second; }
                                    public TFirst First { get; set; }
                                    public TSecond Second { get; set; }
                                    public void Swap(GenericPair<TSecond, TFirst> other)
                                    {
                                        var temp = First;
                                        First = (TFirst)(object)other.First!;
                                        other.First = (TSecond)(object)temp!;
                                    }
                                }

                                public class GenericCollection<T> : List<T>
                                {
                                    public T? FindFirst(Func<T, bool> predicate)
                                    {
                                        foreach (var item in this) { if (predicate(item)) return item; }
                                        return default;
                                    }
                                    public GenericCollection<TResult> Map<TResult>(Func<T, TResult> mapper)
                                    {
                                        var result = new GenericCollection<TResult>();
                                        foreach (var item in this) { result.Add(mapper(item)); }
                                        return result;
                                    }
                                }

                                public class GenericTree<T>
                                {
                                    public Node<T>? Root { get; set; }
                                    public void SetRoot(T rootValue) { Root = new Node<T>(rootValue); }
                                    public class Node<U>
                                    {
                                        public Node(U value) { Value = value; }
                                        public U Value { get; set; }
                                        public List<Node<U>> Children { get; set; } = new();
                                        public void AddChild(U childValue) { Children.Add(new Node<U>(childValue)); }
                                    }
                                }

                                public class GenericCreator
                                {
                                    public static GenericContainer<T> CreateContainer<T>(T value) { return new GenericContainer<T>(value); }
                                    public static GenericPair<T, U> CreatePair<T, U>(T first, U second) { return new GenericPair<T, U>(first, second); }
                                    public static GenericCollection<T> CreateCollectionFromArray<T>(T[] array)
                                    {
                                        var collection = new GenericCollection<T>();
                                        foreach (var item in array) { collection.Add(item); }
                                        return collection;
                                    }
                                }

                                public interface IComparable<in T> { int CompareTo(T other); }
                                public interface IProcessor { void Process(); }

                                public class GenericProcessor<T> where T : class
                                {
                                    private T? _instance;
                                    public void SetInstance(T instance) { _instance = instance ?? throw new ArgumentNullException(nameof(instance)); }
                                    public T GetInstance() { return _instance ?? throw new InvalidOperationException("Instance not set"); }
                                }

                                public class GenericCalculator<T> where T : struct
                                {
                                    public T Add(T a, T b) { return (dynamic)a + (dynamic)b; }
                                    public bool IsDefault(T value) { return EqualityComparer<T>.Default.Equals(value, default); }
                                }

                                public class GenericManager<T> where T : IProcessor
                                {
                                    private readonly List<T> _items = new();
                                    public void AddItem(T item) { _items.Add(item); }
                                    public void ProcessAll() { foreach (var item in _items) { item.Process(); } }
                                }

                                public class GenericFactory<T> where T : new()
                                {
                                    public T CreateInstance() { return new T(); }
                                    public T[] CreateArray(int count)
                                    {
                                        var array = new T[count];
                                        for (var i = 0; i < count; i++) { array[i] = new T(); }
                                        return array;
                                    }
                                }

                                public class GenericSorter<T> where T : class, IComparable<T>, new()
                                {
                                    public void Sort(List<T> items) { items.Sort((x, y) => x.CompareTo(y)); }
                                    public T CreateDefault() { return new T(); }
                                }

                                public abstract class BaseEntity
                                {
                                    public int Id { get; set; }
                                    public abstract void Save();
                                }

                                public class EntityManager<T> where T : BaseEntity
                                {
                                    private readonly Dictionary<int, T> _entities = new();
                                    public void AddEntity(T entity) { _entities[entity.Id] = entity; }
                                    public T? GetEntity(int id) { return _entities.TryGetValue(id, out var entity) ? entity : null; }
                                    public void SaveAll() { foreach (var entity in _entities.Values) { entity.Save(); } }
                                }

                                public class GenericConverter<TSource, TTarget>
                                    where TSource : class
                                    where TTarget : class, new()
                                {
                                    private readonly Func<TSource, TTarget> _converter;
                                    public GenericConverter(Func<TSource, TTarget> converter) { _converter = converter; }
                                    public TTarget Convert(TSource source) { return source != null ? _converter(source) : new TTarget(); }
                                    public List<TTarget> ConvertMany(IEnumerable<TSource> sources)
                                    {
                                        var results = new List<TTarget>();
                                        foreach (var source in sources) { results.Add(Convert(source)); }
                                        return results;
                                    }
                                }

                                public class ProcessableItem : IProcessor
                                {
                                    public string Name { get; set; } = string.Empty;
                                    public void Process() { Console.WriteLine($"Processing {Name}"); }
                                }

                                public class ComparableItem : IComparable<ComparableItem>
                                {
                                    public int Value { get; set; }
                                    public int CompareTo(ComparableItem? other) { return other == null ? 1 : Value.CompareTo(other.Value); }
                                }

                                public class DatabaseEntity : BaseEntity
                                {
                                    public string Name { get; set; } = string.Empty;
                                    public override void Save() { Console.WriteLine($"Saving entity {Id}: {Name}"); }
                                }

                                public class GenericMethodsClass
                                {
                                    public static T Identity<T>(T value) { return value; }
                                    public static T CreateInstance<T>() where T : new() { return new T(); }
                                    public static TResult Transform<TSource, TResult>(TSource source, Func<TSource, TResult> transformer) { return transformer(source); }
                                    public static bool AreEqual<T>(T first, T second) where T : class { return ReferenceEquals(first, second) || (first?.Equals(second) ?? false); }
                                    public static void ProcessItems<T>(IEnumerable<T> items) where T : IProcessor { foreach (var item in items) { item.Process(); } }
                                    public static void SaveEntities<T>(IEnumerable<T> entities) where T : BaseEntity { foreach (var entity in entities) { entity.Save(); } }
                                    public static List<TResult> ConvertAll<TSource, TResult>(IEnumerable<TSource> sources, Func<TSource, TResult> converter)
                                    {
                                        var results = new List<TResult>();
                                        foreach (var source in sources) { results.Add(converter(source)); }
                                        return results;
                                    }
                                    public static void Swap<T>(ref T first, ref T second) { var temp = first; first = second; second = temp; }
                                }

                                public class GenericService<TEntity> where TEntity : class
                                {
                                    private readonly List<TEntity> _entities = new();
                                    public void Add(TEntity entity) { _entities.Add(entity); }
                                    public TResult ProcessEntity<TResult>(TEntity entity, Func<TEntity, TResult> processor) { return processor(entity); }
                                    public void ProcessWithValidator<TValidator>(TEntity entity, TValidator validator) where TValidator : IValidator<TEntity>
                                    {
                                        if (validator.IsValid(entity)) { Console.WriteLine("Entity is valid"); }
                                    }
                                    public GenericContainer<TResult> WrapResult<TResult>(TResult result) { return new GenericContainer<TResult>(result); }
                                }

                                public interface IValidator<T> { bool IsValid(T item); }

                                public class StringValidator : IValidator<string> { public bool IsValid(string item) { return !string.IsNullOrEmpty(item); } }
                                public class NumberValidator : IValidator<int> { public bool IsValid(int item) { return item >= 0; } }

                                public static class GenericUtilities
                                {
                                    public static bool IsNull<T>(T value) where T : class { return value == null; }
                                    public static T GetDefault<T>() { return default!; }
                                    public static Type GetGenericType<T>() { return typeof(T); }
                                    public static GenericPair<T, U> MakePair<T, U>(T first, U second) { return new GenericPair<T, U>(first, second); }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[]
        {
            "GenericProcessor", "GenericCalculator", "GenericManager", "GenericFactory", "GenericSorter",
            "BaseEntity", "EntityManager", "GenericConverter", "ProcessableItem", "ComparableItem",
            "DatabaseEntity", "GenericCollection", "GenericContainer", "GenericCreator", "GenericMethodsClass",
            "GenericPair", "GenericService", "GenericTree", "GenericTree.Node", "GenericUtilities",
            "NumberValidator", "StringValidator"
        };

        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void GenericConstraintTypes_AreRecordedAsUses()
    {
        // "where T : Foo" records the constraint type as a Uses relationship, for both class and method
        // type parameters. IComparable here is the module's own interface.
        var expected = new[]
        {
            "GenericManager -> IProcessor",
            "GenericSorter -> IComparable",
            "EntityManager -> BaseEntity",
            "GenericMethodsClass.ProcessItems -> IProcessor",
            "GenericMethodsClass.SaveEntities -> BaseEntity",
            "GenericService.ProcessWithValidator -> IValidator"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.SupersetOf(expected));
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "GenericManager.ProcessAll -> IProcessor.Process",
            "EntityManager.AddEntity -> BaseEntity.Id",
            "EntityManager.SaveAll -> BaseEntity.Save",
            "GenericConverter.ConvertMany -> GenericConverter.Convert",
            "ProcessableItem.Process -> ProcessableItem.Name",
            "ComparableItem.CompareTo -> ComparableItem.Value",
            "DatabaseEntity.Save -> BaseEntity.Id",
            "DatabaseEntity.Save -> DatabaseEntity.Name",
            "GenericMethodsClass.ProcessItems -> IProcessor.Process",
            "GenericMethodsClass.SaveEntities -> BaseEntity.Save",
            "GenericPair..ctor -> GenericPair.First",
            "GenericPair..ctor -> GenericPair.Second",
            "GenericPair.Swap -> GenericPair.First",
            "GenericTree.Node..ctor -> GenericTree.Node.Value",
            "GenericTree.Node.AddChild -> GenericTree.Node.Children",
            "GenericTree.SetRoot -> GenericTree.Root",
            "GenericCreator.CreateContainer -> GenericContainer..ctor",
            "GenericCreator.CreatePair -> GenericPair..ctor",
            "GenericService.WrapResult -> GenericContainer..ctor",
            "GenericTree.Node.AddChild -> GenericTree.Node..ctor",
            "GenericTree.SetRoot -> GenericTree.Node..ctor",
            "GenericUtilities.MakePair -> GenericPair..ctor",
            "GenericService.ProcessWithValidator -> IValidator.IsValid"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
