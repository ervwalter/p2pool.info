using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace P2Pool
{

    /// <summary>
    /// Summary description for FifoBuffer
    /// </summary>
    public class FifoBuffer<T> : ICollection<T>
    {
        private List<T> _list = new List<T>();
        private int _startIndex = -1;


        public int Size { get; set; }
        public int Count { get { return _list.Count; } }

        public FifoBuffer(int size)
        {
            Size = size;
            _list = new List<T>(size);
        }

        public T Add(T item)
        {
            NextIndex();

            if (_startIndex >= 0)
            {
                if (_list.Count < Size)
                    _list.Insert(_startIndex, item);
                else
                    _list[_startIndex] = item;
            }
            else
                _list.Add(item);

            return item;
        }

        public T this[int index]
        {
            get { return ItemAt(index); }
        }

        #region Private Methods

        private int NextIndex()
        {
            if (_startIndex >= 0 || _list.Count == Size) //Now rotating
            {
                _startIndex = (_startIndex + 1) % Size;
                if (_startIndex > _list.Count)
                    _startIndex = _list.Count;

                return _startIndex;
            }
            else
                return 0;
        }

        private int ResolveIndex(int index)
        {
            if (_startIndex < 0)
                return index;
            else
                return (_startIndex + 1 + index) % _list.Count;
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _list.Count)
                throw new IndexOutOfRangeException();
        }

        #endregion

        #region ICollection<T> Members

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public void Clear()
        {
            _list.Clear();
            _startIndex = 0;
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            //Can't use _list.CopyTo, since the 
            //items are in a circular order with moving index
            foreach (var t in this)
            {
                array[arrayIndex++] = t;
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            int i = _list.IndexOf(item);
            if (i >= 0)
            {
                _list.Remove(item);
                if (i <= _startIndex)
                    _startIndex--;

                return true;
            }
            else
                return false;
        }

        public T ItemAt(int index)
        {
            ValidateIndex(index);
            int i = ResolveIndex((_list.Count - 1) - index);
            return _list[i];
        }

        #endregion

        #region IEnumerable<T> Members
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
                yield return _list[ResolveIndex(i)];
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
                yield return _list[ResolveIndex(i)];
        }

        #endregion

    }
}