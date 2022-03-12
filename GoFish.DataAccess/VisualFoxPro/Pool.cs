using System;
using System.Collections.Concurrent;

namespace GoFish.DataAccess.VisualFoxPro;

internal class Pool<T>
{
    private readonly int size;
    private readonly Func<T> factory;
    private readonly ConcurrentQueue<T> pool;

    public Pool(int size, Func<T> factory)
    {
        pool = new ConcurrentQueue<T>();
        for (int i = 0; i < size; i++)
        {
            pool.Enqueue(factory());
        }

        this.size = size;
        this.factory = factory;
    }

    public T Rent()
    {
        if (!pool.TryDequeue(out var item))
        {
            item = factory();
        }
        return item;
    }

    public void Return(T item)
    {
        if (pool.Count < size)
        {
            pool.Enqueue(item);
        }
    }
}
