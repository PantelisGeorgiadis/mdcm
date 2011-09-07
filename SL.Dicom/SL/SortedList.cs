// ReSharper disable CheckNamespace
using System.Linq;

namespace System.Collections.Generic
// ReSharper restore CheckNamespace
{
    public class SortedList<TKey, TValue> : Dictionary<TKey, TValue>
    {
        #region INSTANCE MEMBERS

        private readonly IComparer<TKey> mComparer;

        #endregion

        #region CONSTRUCTORS

        public SortedList()
        {
            mComparer = null;
        }

        public SortedList(IComparer<TKey> iComparer)
        {
            mComparer = iComparer;
        }

        #endregion

        #region PROPERTIES

        public new IList<TKey> Keys
        {
            get { return base.Keys.ToList(); }
        }

        public new IList<TValue> Values
        {
            get { return base.Values.ToList(); }
        }

        #endregion
        
        #region METHODS

        public new void Add(TKey key, TValue value)
        {
            base.Add(key, value);
#if !WINDOWS_PHONE
            this.OrderBy(kv => kv.Key, mComparer);
#else
            Sort(this, mComparer);
#endif
        }

        public void RemoveAt(int i)
        {
            Remove(this.ElementAt(i).Key);
        }

        private void Sort(Dictionary<TKey, TValue> dictionary, IComparer<TKey> iComparer)
        {
            TKey[] keys = new TKey[Count];
            TValue[] vals = new TValue[Count];
            Keys.CopyTo(keys, 0);
            Values.CopyTo(vals, 0);

            Array.Sort<TKey, TValue>(keys, vals, iComparer);

            dictionary.Clear();
            for (int i = 0; i < keys.Length; i++)
            {
                dictionary.Add(keys[i], vals[i]);
            }
        }

        #endregion
    }
}
