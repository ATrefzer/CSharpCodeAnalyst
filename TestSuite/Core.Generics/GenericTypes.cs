using System;
using System.Collections.Generic;

namespace Core.Generics;

// Test basic generic types
public class GenericContainer<T>
{
    private T _value;

    public GenericContainer(T value)
    {
        _value = value;
    }

    public T GetValue()
    {
        return _value;
    }

    public void SetValue(T value)
    {
        _value = value;
    }

    public bool IsDefault()
    {
        return EqualityComparer<T>.Default.Equals(_value, default!);
    }
}

// Multiple type parameters
public class GenericPair<TFirst, TSecond>
{

    public GenericPair(TFirst first, TSecond second)
    {
        First = first;
        Second = second;
    }

    public TFirst First { get; set; }
    public TSecond Second { get; set; }

    public void Swap(GenericPair<TSecond, TFirst> other)
    {
        var temp = First;
        First = (TFirst)(object)other.First!;
        other.First = (TSecond)(object)temp!;
    }
}

// Generic inheritance
public class GenericCollection<T> : List<T>
{
    public T? FindFirst(Func<T, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item))
                return item;
        }

        return default;
    }

    public GenericCollection<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var result = new GenericCollection<TResult>();
        foreach (var item in this)
        {
            result.Add(mapper(item));
        }

        return result;
    }
}

// Nested generics
public class GenericTree<T>
{

    public Node<T>? Root { get; set; }

    public void SetRoot(T rootValue)
    {
        Root = new Node<T>(rootValue);
    }

    public class Node<U>
    {

        public Node(U value)
        {
            Value = value;
        }

        public U Value { get; set; }
        public List<Node<U>> Children { get; set; } = new();

        public void AddChild(U childValue)
        {
            Children.Add(new Node<U>(childValue));
        }
    }
}

// Generic creator pattern
public class GenericCreator
{
    public static GenericContainer<T> CreateContainer<T>(T value)
    {
        return new GenericContainer<T>(value);
    }

    public static GenericPair<T, U> CreatePair<T, U>(T first, U second)
    {
        return new GenericPair<T, U>(first, second);
    }

    public static GenericCollection<T> CreateCollectionFromArray<T>(T[] array)
    {
        var collection = new GenericCollection<T>();
        foreach (var item in array)
        {
            collection.Add(item);
        }

        return collection;
    }
}