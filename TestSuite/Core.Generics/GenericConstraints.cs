using System;
using System.Collections.Generic;

namespace Core.Generics;

// Test generic constraints
public interface IComparable<in T>
{
    int CompareTo(T other);
}

public interface IProcessor
{
    void Process();
}

// Class constraint
public class GenericProcessor<T> where T : class
{
    private T? _instance;

    public void SetInstance(T instance)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    public T GetInstance()
    {
        return _instance ?? throw new InvalidOperationException("Instance not set");
    }
}

// Struct constraint
public class GenericCalculator<T> where T : struct
{
    public T Add(T a, T b)
    {
        return (dynamic)a + (dynamic)b;
    }

    public bool IsDefault(T value)
    {
        return EqualityComparer<T>.Default.Equals(value, default);
    }
}

// Interface constraint
public class GenericManager<T> where T : IProcessor
{
    private readonly List<T> _items = new();

    public void AddItem(T item)
    {
        _items.Add(item);
    }

    public void ProcessAll()
    {
        foreach (var item in _items)
        {
            item.Process();
        }
    }
}

// New() constraint
public class GenericFactory<T> where T : new()
{
    public T CreateInstance()
    {
        return new T();
    }

    public T[] CreateArray(int count)
    {
        var array = new T[count];
        for (var i = 0; i < count; i++)
        {
            array[i] = new T();
        }

        return array;
    }
}

// Multiple constraints
public class GenericSorter<T> where T : class, IComparable<T>, new()
{
    public void Sort(List<T> items)
    {
        items.Sort((x, y) => x.CompareTo(y));
    }

    public T CreateDefault()
    {
        return new T();
    }
}

// Base class constraint
public abstract class BaseEntity
{
    public int Id { get; set; }
    public abstract void Save();
}

public class EntityManager<T> where T : BaseEntity
{
    private readonly Dictionary<int, T> _entities = new();

    public void AddEntity(T entity)
    {
        _entities[entity.Id] = entity;
    }

    public T? GetEntity(int id)
    {
        return _entities.TryGetValue(id, out var entity) ? entity : null;
    }

    public void SaveAll()
    {
        foreach (var entity in _entities.Values)
        {
            entity.Save();
        }
    }
}

// Type parameter constraints
public class GenericConverter<TSource, TTarget>
    where TSource : class
    where TTarget : class, new()
{
    private readonly Func<TSource, TTarget> _converter;

    public GenericConverter(Func<TSource, TTarget> converter)
    {
        _converter = converter;
    }

    public TTarget Convert(TSource source)
    {
        return source != null ? _converter(source) : new TTarget();
    }

    public List<TTarget> ConvertMany(IEnumerable<TSource> sources)
    {
        var results = new List<TTarget>();
        foreach (var source in sources)
        {
            results.Add(Convert(source));
        }

        return results;
    }
}

// Test classes for constraints
public class ProcessableItem : IProcessor
{
    public string Name { get; set; } = string.Empty;

    public void Process()
    {
        Console.WriteLine($"Processing {Name}");
    }
}

public class ComparableItem : IComparable<ComparableItem>
{
    public int Value { get; set; }

    public int CompareTo(ComparableItem? other)
    {
        return other == null ? 1 : Value.CompareTo(other.Value);
    }
}

public class DatabaseEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public override void Save()
    {
        Console.WriteLine($"Saving entity {Id}: {Name}");
    }
}