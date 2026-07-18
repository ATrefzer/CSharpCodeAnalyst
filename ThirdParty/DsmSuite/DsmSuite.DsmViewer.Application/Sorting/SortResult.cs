// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;

namespace DsmSuite.DsmViewer.Application.Sorting
{
    public class SortResult : ISortResult
    {
        /// <summary>
        /// _list contains the ordering as a permutation of indices. If the sorting
        /// puts an element on position i that was on position j, then _list[i] == j; 
        /// </summary>
        private readonly List<int> _list = new List<int>();

        public SortResult(List<int> list)
        {
            _list = new List<int>(list);
        }

        public SortResult(int numberOfElements)
        {
            _list.Clear();

            for (int i = 0; i < numberOfElements; i++)
            {
                _list.Add(i);
            }
        }

        public int GetNumberOfElements()
        {
            return _list.Count;
        }

        public void InvertOrder()
        {
            List<KeyValuePair<int, int>> order = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < _list.Count; i++)
            {
                order.Add(new KeyValuePair<int, int>(i, _list[i]));
            }

            foreach (var v in order)
            {
                _list[v.Value] = v.Key;
            }
        }

        public void Swap(int index1, int index2)
        {
            CheckIndex(index1);
            CheckIndex(index2);
            int temp = _list[index1];
            _list[index1] = _list[index2];
            _list[index2] = temp;
        }

        public void SetIndex(int index, int value)
        {
            CheckIndex(index);
            _list[index] = value;
        }

        public int GetIndex(int index)
        {
            CheckIndex(index);
            return _list[index];
        }

        /// <summary>
        /// Return the sorting order as a permutation of indices. If the sorting
        /// puts an element on position i that was on position j, then order[i] == j; 
        /// </summary>
        public List<int> GetOrder()
        {
            return new List<int>(_list);
        }


        public bool IsValid
        {
            get
            {
                HashSet<int> set = new HashSet<int>(_list);

                for (int i = 0; i < _list.Count; i++)
                {
                    if (!set.Contains(i))
                        return false;
                }
                return _list.Count > 0;
            }
        }

        private void CheckIndex(int index)
        {
            if ((index < 0) || (index >= _list.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
