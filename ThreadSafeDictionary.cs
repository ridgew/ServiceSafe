using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ServiceSafe
{
    /// <summary>
    /// 线程安全词典接口
    /// 创建者：wangqj@WANGQJ
    /// 创建时间：2010-12-1113:12
    /// <para>http://dotnetcommandos.com/blogs/brianr/archive/2008/09/29/thread-safe-dictionary-update.aspx</para>
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public interface IThreadSafeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// Merge is similar to the SQL merge or upsert statement.  
        /// </summary>
        /// <param name="key">Key to lookup</param>
        /// <param name="newValue">New Value</param>
        void MergeSafe(TKey key, TValue newValue);


        /// <summary>
        /// This is a blind remove. Prevents the need to check for existence first.
        /// </summary>
        /// <param name="key">Key to Remove</param>
        void RemoveSafe(TKey key);
    }

    /// <summary>
    /// 线程安全词典实现
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    [Serializable]
    public class ThreadSafeDictionary<TKey, TValue> : IThreadSafeDictionary<TKey, TValue>
    {
        /// <summary>
        /// 初始化 <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"/> class.
        /// </summary>
        public ThreadSafeDictionary()
        {
            dict = new Dictionary<TKey, TValue>();
        }

        /// <summary>
        /// 初始化一个 <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"/> class 实例。
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        public ThreadSafeDictionary(IEqualityComparer<TKey> comparer)
        {
            dict = new Dictionary<TKey, TValue>(comparer);
        }

        //This is the internal dictionary that we are wrapping
        IDictionary<TKey, TValue> dict = null;


        [NonSerialized]
        ReaderWriterLockSlim dictionaryLock = Locks.GetLockInstance(LockRecursionPolicy.NoRecursion); //setup the lock;


        /// <summary>
        /// This is a blind remove. Prevents the need to check for existence first.
        /// </summary>
        /// <param name="key">Key to remove</param>
        public void RemoveSafe(TKey key)
        {
            using (new ReadLock(this.dictionaryLock))
            {
                if (this.dict.ContainsKey(key))
                {
                    using (new WriteLock(this.dictionaryLock))
                    {
                        this.dict.Remove(key);
                    }
                }
            }
        }


        /// <summary>
        /// Merge does a blind remove, and then add.  Basically a blind Upsert.  
        /// </summary>
        /// <param name="key">Key to lookup</param>
        /// <param name="newValue">New Value</param>
        public void MergeSafe(TKey key, TValue newValue)
        {
            using (new WriteLock(this.dictionaryLock)) // take a writelock immediately since we will always be writing
            {
                if (dict.ContainsKey(key))
                {
                    dict.Remove(key);
                }
                this.dict.Add(key, newValue);
            }
        }


        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.IDictionary`2"/> 中移除带有指定键的元素。
        /// </summary>
        /// <param name="key">要移除的元素的键。</param>
        /// <returns>
        /// 如果该元素已成功移除，则为 true；否则为 false。如果在原始 <see cref="T:System.Collections.Generic.IDictionary`2"/> 中没有找到 <paramref name="key"/>，该方法也会返回 false。
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="key"/> 为 null。</exception>
        /// <exception cref="T:System.NotSupportedException">
        /// 	<see cref="T:System.Collections.Generic.IDictionary`2"/> 为只读。</exception>
        public virtual bool Remove(TKey key)
        {
            using (new WriteLock(this.dictionaryLock))
            {
                return this.dict.Remove(key);
            }
        }


        /// <summary>
        /// 确定 <see cref="T:System.Collections.Generic.IDictionary`2"/> 是否包含具有指定键的元素。
        /// </summary>
        /// <param name="key">要在 <see cref="T:System.Collections.Generic.IDictionary`2"/> 中定位的键。</param>
        /// <returns>
        /// 如果 <see cref="T:System.Collections.Generic.IDictionary`2"/> 包含带有该键的元素，则为 true；否则，为 false。
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="key"/> 为 null。</exception>
        public virtual bool ContainsKey(TKey key)
        {
            using (new ReadOnlyLock(this.dictionaryLock))
            {
                return this.dict.ContainsKey(key);
            }
        }


        /// <summary>
        /// 获取与指定的键相关联的值。
        /// </summary>
        /// <param name="key">要获取其值的键。</param>
        /// <param name="value">当此方法返回时，如果找到指定键，则返回与该键相关联的值；否则，将返回 <paramref name="value"/> 参数的类型的默认值。该参数未经初始化即被传递。</param>
        /// <returns>
        /// 如果实现 <see cref="T:System.Collections.Generic.IDictionary`2"/> 的对象包含具有指定键的元素，则为 true；否则，为 false。
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="key"/> 为 null。</exception>
        public virtual bool TryGetValue(TKey key, out TValue value)
        {
            using (new ReadOnlyLock(this.dictionaryLock))
            {
                return this.dict.TryGetValue(key, out value);
            }
        }


        /// <summary>
        /// Gets or sets the <see cref="TValue"/> with the specified key.
        /// </summary>
        /// <value></value>
        public virtual TValue this[TKey key]
        {
            get
            {
                using (new ReadOnlyLock(this.dictionaryLock))
                {
                    return this.dict[key];
                }
            }
            set
            {
                using (new WriteLock(this.dictionaryLock))
                {
                    this.dict[key] = value;
                }
            }
        }


        /// <summary>
        /// 获取包含 <see cref="T:System.Collections.Generic.IDictionary`2"/> 的键的 <see cref="T:System.Collections.Generic.ICollection`1"/>。
        /// </summary>
        /// <value></value>
        /// <returns>一个 <see cref="T:System.Collections.Generic.ICollection`1"/>，它包含实现 <see cref="T:System.Collections.Generic.IDictionary`2"/> 的对象的键。</returns>
        public virtual ICollection<TKey> Keys
        {
            get
            {
                using (new ReadOnlyLock(this.dictionaryLock))
                {
                    return new List<TKey>(this.dict.Keys);
                }
            }
        }


        /// <summary>
        /// 获取包含 <see cref="T:System.Collections.Generic.IDictionary`2"/> 中的值的 <see cref="T:System.Collections.Generic.ICollection`1"/>。
        /// </summary>
        /// <value></value>
        /// <returns>一个 <see cref="T:System.Collections.Generic.ICollection`1"/>，它包含实现 <see cref="T:System.Collections.Generic.IDictionary`2"/> 的对象中的值。</returns>
        public virtual ICollection<TValue> Values
        {
            get
            {
                using (new ReadOnlyLock(this.dictionaryLock))
                {
                    return new List<TValue>(this.dict.Values);
                }
            }
        }


        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除所有项。
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">
        /// 	<see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。 </exception>
        public virtual void Clear()
        {
            using (new WriteLock(this.dictionaryLock))
            {
                this.dict.Clear();
            }
        }


        /// <summary>
        /// 获取 <see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素数。
        /// </summary>
        /// <value></value>
        /// <returns>
        /// 	<see cref="T:System.Collections.Generic.ICollection`1"/> 中包含的元素数。</returns>
        public virtual int Count
        {
            get
            {
                using (new ReadOnlyLock(this.dictionaryLock))
                {
                    return this.dict.Count;
                }
            }
        }


        /// <summary>
        /// 确定 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否包含特定值。
        /// </summary>
        /// <param name="item">要在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中定位的对象。</param>
        /// <returns>
        /// 如果在 <see cref="T:System.Collections.Generic.ICollection`1"/> 中找到 <paramref name="item"/>，则为 true；否则为 false。
        /// </returns>
        public virtual bool Contains(KeyValuePair<TKey, TValue> item)
        {
            using (new ReadOnlyLock(this.dictionaryLock))
            {
                return this.dict.Contains(item);
            }
        }


        /// <summary>
        /// 将某项添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 中。
        /// </summary>
        /// <param name="item">要添加到 <see cref="T:System.Collections.Generic.ICollection`1"/> 的对象。</param>
        /// <exception cref="T:System.NotSupportedException">
        /// 	<see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        public virtual void Add(KeyValuePair<TKey, TValue> item)
        {
            using (new WriteLock(this.dictionaryLock))
            {
                this.dict.Add(item);
            }
        }


        /// <summary>
        /// 在 <see cref="T:System.Collections.Generic.IDictionary`2"/> 中添加一个带有所提供的键和值的元素。
        /// </summary>
        /// <param name="key">用作要添加的元素的键的对象。</param>
        /// <param name="value">用作要添加的元素的值的对象。</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="key"/> 为 null。</exception>
        /// <exception cref="T:System.ArgumentException">
        /// 	<see cref="T:System.Collections.Generic.IDictionary`2"/> 中已存在具有相同键的元素。</exception>
        /// <exception cref="T:System.NotSupportedException">
        /// 	<see cref="T:System.Collections.Generic.IDictionary`2"/> 为只读。</exception>
        public virtual void Add(TKey key, TValue value)
        {
            using (new WriteLock(this.dictionaryLock))
            {
                this.dict.Add(key, value);
            }
        }


        /// <summary>
        /// 从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除特定对象的第一个匹配项。
        /// </summary>
        /// <param name="item">要从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中移除的对象。</param>
        /// <returns>
        /// 如果已从 <see cref="T:System.Collections.Generic.ICollection`1"/> 中成功移除 <paramref name="item"/>，则为 true；否则为 false。如果在原始 <see cref="T:System.Collections.Generic.ICollection`1"/> 中没有找到 <paramref name="item"/>，该方法也会返回 false。
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">
        /// 	<see cref="T:System.Collections.Generic.ICollection`1"/> 为只读。</exception>
        public virtual bool Remove(KeyValuePair<TKey, TValue> item)
        {
            using (new WriteLock(this.dictionaryLock))
            {
                return this.dict.Remove(item);
            }
        }


        /// <summary>
        /// 从特定的 <see cref="T:System.Array"/> 索引开始，将 <see cref="T:System.Collections.Generic.ICollection`1"/> 的元素复制到一个 <see cref="T:System.Array"/> 中。
        /// </summary>
        /// <param name="array">作为从 <see cref="T:System.Collections.Generic.ICollection`1"/> 复制的元素的目标位置的一维 <see cref="T:System.Array"/>。<see cref="T:System.Array"/> 必须具有从零开始的索引。</param>
        /// <param name="arrayIndex"><paramref name="array"/> 中从零开始的索引，从此处开始复制。</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="array"/> 为 null。</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="arrayIndex"/> 小于 0。</exception>
        /// <exception cref="T:System.ArgumentException">
        /// 	<paramref name="array"/> 是多维的。- 或 -<paramref name="arrayIndex"/> 等于或大于 <paramref name="array"/> 的长度。- 或 -源 <see cref="T:System.Collections.Generic.ICollection`1"/> 中的元素数目大于从 <paramref name="arrayIndex"/> 到目标 <paramref name="array"/> 末尾之间的可用空间。- 或 -无法自动将类型 <paramref name="T"/> 强制转换为目标 <paramref name="array"/> 的类型。</exception>
        public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            using (new ReadOnlyLock(this.dictionaryLock))
            {
                this.dict.CopyTo(array, arrayIndex);
            }
        }


        /// <summary>
        /// 获取一个值，该值指示 <see cref="T:System.Collections.Generic.ICollection`1"/> 是否为只读。
        /// </summary>
        /// <value></value>
        /// <returns>
        /// 如果 <see cref="T:System.Collections.Generic.ICollection`1"/> 为只读，则为 true；否则为 false。</returns>
        public virtual bool IsReadOnly
        {
            get
            {
                using (new ReadOnlyLock(this.dictionaryLock))
                {
                    return this.dict.IsReadOnly;
                }
            }
        }


        /// <summary>
        /// 返回一个循环访问集合的枚举数。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.Generic.IEnumerator`1"/>。
        /// </returns>
        public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotSupportedException("Cannot enumerate a threadsafe dictionary.  Instead, enumerate the keys or values collection");
        }


        /// <summary>
        /// 返回一个循环访问集合的枚举数。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.IEnumerator"/> 对象。
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotSupportedException("Cannot enumerate a threadsafe dictionary.  Instead, enumerate the keys or values collection");
        }
    }


    public static class Locks
    {
        public static void GetReadLock(ReaderWriterLockSlim locks)
        {
            bool lockAcquired = false;
            while (!lockAcquired)
                lockAcquired = locks.TryEnterUpgradeableReadLock(1);
        }


        public static void GetReadOnlyLock(ReaderWriterLockSlim locks)
        {
            bool lockAcquired = false;
            while (!lockAcquired)
                lockAcquired = locks.TryEnterReadLock(1);
        }


        public static void GetWriteLock(ReaderWriterLockSlim locks)
        {
            bool lockAcquired = false;
            while (!lockAcquired)
                lockAcquired = locks.TryEnterWriteLock(1);
        }


        public static void ReleaseReadOnlyLock(ReaderWriterLockSlim locks)
        {
            if (locks.IsReadLockHeld)
                locks.ExitReadLock();
        }


        public static void ReleaseReadLock(ReaderWriterLockSlim locks)
        {
            if (locks.IsUpgradeableReadLockHeld)
                locks.ExitUpgradeableReadLock();
        }


        public static void ReleaseWriteLock(ReaderWriterLockSlim locks)
        {
            if (locks.IsWriteLockHeld)
                locks.ExitWriteLock();
        }


        public static void ReleaseLock(ReaderWriterLockSlim locks)
        {
            ReleaseWriteLock(locks);
            ReleaseReadLock(locks);
            ReleaseReadOnlyLock(locks);
        }


        public static ReaderWriterLockSlim GetLockInstance()
        {
            return GetLockInstance(LockRecursionPolicy.SupportsRecursion);
        }


        public static ReaderWriterLockSlim GetLockInstance(LockRecursionPolicy recursionPolicy)
        {
            return new ReaderWriterLockSlim(recursionPolicy);
        }
    }


    public abstract class BaseLock : IDisposable
    {
        protected ReaderWriterLockSlim _Locks;

        public BaseLock(ReaderWriterLockSlim locks)
        {
            _Locks = locks;
        }
        public abstract void Dispose();
    }


    public class ReadLock : BaseLock
    {
        public ReadLock(ReaderWriterLockSlim locks)
            : base(locks)
        {
            Locks.GetReadLock(this._Locks);
        }

        public override void Dispose()
        {
            Locks.ReleaseReadLock(this._Locks);
        }
    }


    public class ReadOnlyLock : BaseLock
    {
        public ReadOnlyLock(ReaderWriterLockSlim locks)
            : base(locks)
        {
            Locks.GetReadOnlyLock(this._Locks);
        }


        public override void Dispose()
        {
            Locks.ReleaseReadOnlyLock(this._Locks);
        }
    }


    public class WriteLock : BaseLock
    {
        public WriteLock(ReaderWriterLockSlim locks)
            : base(locks)
        {
            Locks.GetWriteLock(this._Locks);
        }

        public override void Dispose()
        {
            Locks.ReleaseWriteLock(this._Locks);
        }
    }
}
