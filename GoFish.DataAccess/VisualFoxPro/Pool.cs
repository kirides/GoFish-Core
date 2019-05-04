using System;
using System.Collections.Generic;

namespace GoFish.DataAccess.VisualFoxPro
{
    internal class Pool<T>
    {
        private readonly int size;
        private readonly Func<T> factory;
        private readonly Queue<T> pool;

        public Pool(int size, Func<T> factory)
        {
            pool = new Queue<T>(size);
            for (int i = 0; i < size; i++)
            {
                pool.Enqueue(factory());
            }

            this.size = size;
            this.factory = factory;
        }

        public T Rent()
        {
            lock (pool)
            {
                var item = pool.Dequeue();
                if (item == default)
                {
                    item = factory();
                }
                return item;
            }
        }

        public void Return(T item)
        {
            lock (pool)
            {
                if (pool.Count < size)
                {
                    pool.Enqueue(item);
                }
            }
        }
    }
}
