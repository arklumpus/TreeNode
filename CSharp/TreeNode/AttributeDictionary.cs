using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace PhyloTree
{
    /// <summary>
    /// Represents the attributes of a node. Attributes <see cref="Name"/>, <see cref="Length"/> and <see cref="Support"/> are always included. See the respective properties for default values.
    /// </summary>
    [Serializable]
    public class AttributeDictionary : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> InternalStorage;

        private string _name;

        /// <summary>
        /// The name of this node (e.g. the species name for leaf nodes). Default is <c>""</c>. Getting the value of this property does not require a dictionary lookup.
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                InternalStorage["Name"] = value;
            }
        }

        private double _length;

        /// <summary>
        /// The length of the branch leading to this node. This is <c>double.NaN</c> for branches whose length is not specified (e.g. the root node). Getting the value of this property does not require a dictionary lookup.
        /// </summary>
        public double Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
                InternalStorage["Length"] = value;
            }
        }

        private double _support;

        /// <summary>
        /// The support value of this node. This is <c>double.NaN</c> for branches whose support is not specified. The interpretation of the support value depends on how the tree was built. Getting the value of this property does not require a dictionary lookup.
        /// </summary>
        public double Support
        {
            get
            {
                return _support;
            }
            set
            {
                _support = value;
                InternalStorage["Support"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the attribute with the specified <paramref name="name"/>. Getting the value of attributes <c>"Name"</c>, <c>"Length"</c> and <c>"Support"</c> does not require a dictionary lookup.
        /// </summary>
        /// <param name="name">The name of the attribute to get/set.</param>
        /// <returns>The value of the attribute, boxed into an <c>object</c>.</returns>
        public object this[string name]
        {
            get
            {
                Contract.Requires(name != null);
                if (name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    return _name;
                }
                else if (name.Equals("Length", StringComparison.OrdinalIgnoreCase))
                {
                    return _length;
                }
                else if (name.Equals("Support", StringComparison.OrdinalIgnoreCase))
                {
                    return _support;
                }
                else
                {
                    return InternalStorage[name];
                }
            }

            set
            {
                Contract.Requires(name != null);
                InternalStorage[name] = value;

                if (name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    _name = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (name.Equals("Length", StringComparison.OrdinalIgnoreCase))
                {
                    _length = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (name.Equals("Support", StringComparison.OrdinalIgnoreCase))
                {
                    _support = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>
        /// Gets a collection containing the names of the attributes in the <see cref="AttributeDictionary"/>.
        /// </summary>
        public ICollection<string> Keys => InternalStorage.Keys;

        /// <summary>
        /// Gets a collection containing the values of the attributes in the <see cref="AttributeDictionary"/>.
        /// </summary>
        public ICollection<object> Values => InternalStorage.Values;

        /// <summary>
        /// Gets the number of attributes contained in the <see cref="AttributeDictionary"/>.
        /// </summary>
        public int Count => InternalStorage.Count;

        /// <summary>
        /// Determine whether the <see cref="AttributeDictionary"/> is read-only. This is always <c>false</c> in the current implementation.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Adds an attribute with the specified <paramref name="name"/> and <paramref name="value"/> to the <see cref="AttributeDictionary"/>. Throws an exception if the <see cref="AttributeDictionary"/> already contains an attribute with the same <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        public void Add(string name, object value)
        {
            InternalStorage.Add(name, value);
        }

        /// <summary>
        /// Adds an attribute with the specified name and value to the <see cref="AttributeDictionary"/>. Throws an exception if the <see cref="AttributeDictionary"/> already contains an attribute with the same name.
        /// </summary>
        /// <param name="item">The item to be added to the dictionary.</param>
        public void Add(KeyValuePair<string, object> item)
        {
            InternalStorage.Add(item.Key, item.Value);
        }

        /// <summary>
        /// Removes all attributes from the dictionary, except the <c>"Name"</c>, <c>"Length"</c> and <c>"Support"</c> attributes.
        /// </summary>
        public void Clear()
        {
            InternalStorage.Clear();

            _name = "";
            _length = double.NaN;
            _support = double.NaN;

            InternalStorage.Add("Name", _name);
            InternalStorage.Add("Length", _length);
            InternalStorage.Add("Support", _support);
        }

        /// <summary>
        /// Determines whether the <see cref="AttributeDictionary"/> contains the specified <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The item to locate in the <see cref="AttributeDictionary"/></param>
        /// <returns><c>true</c> if the <see cref="AttributeDictionary"/> contains the specified <paramref name="item"/>, <c>false</c> otherwise.</returns>
        public bool Contains(KeyValuePair<string, object> item)
        {
            return InternalStorage.Contains(item);
        }

        /// <summary>
        /// Determines whether the <see cref="AttributeDictionary"/> contains an attribute with the specified name <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the attribute to locate.</param>
        /// <returns><c>true</c> if the <see cref="AttributeDictionary"/> contains an attribute with the specified <paramref name="name"/>, <c>false</c> otherwise.</returns>
        public bool ContainsKey(string name)
        {
            return InternalStorage.ContainsKey(name);
        }

        /// <summary>
        /// Copies the elements of the <see cref="AttributeDictionary"/> to an array, starting at a specific array index.
        /// </summary>
        /// <param name="array">The array to which the elements will be copied.</param>
        /// <param name="arrayIndex">The index at which to start copying.</param>
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            Contract.Requires(array != null);

            foreach (KeyValuePair<string, object> item in InternalStorage)
            {
                array[arrayIndex] = item;
                arrayIndex++;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="AttributeDictionary"/>.
        /// </summary>
        /// <returns>An enumerator that iterates through the <see cref="AttributeDictionary"/>.</returns>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return InternalStorage.GetEnumerator();
        }

        /// <summary>
        /// Removes the attribute with the specified name from the <see cref="AttributeDictionary"/>. Attributes <c>"Name"</c>, <c>"Length"</c> and <c>"Support"</c> cannot be removed.
        /// </summary>
        /// <param name="name">The name of the attribute to remove.</param>
        /// <returns>A <c>bool</c> indicating whether the attribute was succesfully removed.</returns>
        public bool Remove(string name)
        {
            if (name == null || !name.Equals("Name", StringComparison.OrdinalIgnoreCase) && !name.Equals("Length", StringComparison.OrdinalIgnoreCase) && !name.Equals("Support", StringComparison.OrdinalIgnoreCase))
            {
                return InternalStorage.Remove(name);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the attribute with the specified name from the <see cref="AttributeDictionary"/>. Attributes <c>"Name"</c>, <c>"Length"</c> and <c>"Support"</c> cannot be removed.
        /// </summary>
        /// <param name="item">The attribute to remove (only the name will be used).</param>
        /// <returns>A <c>bool</c> indicating whether the attribute was succesfully removed.</returns>
        public bool Remove(KeyValuePair<string, object> item)
        {
            if (!item.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) && !item.Key.Equals("Length", StringComparison.OrdinalIgnoreCase) && !item.Key.Equals("Support", StringComparison.OrdinalIgnoreCase))
            {
                return InternalStorage.Remove(item.Key);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the value of the attribute with the specified <paramref name="name"/>. Getting the value of attributes <c>"Name"</c>, <c>"Length"</c> and <c>"Support"</c> does not require a dictionary lookup.
        /// </summary>
        /// <param name="name">The name of the attribute to get.</param>
        /// <param name="value">When this method returns, contains the value of the attribute with the specified <paramref name="name"/>, if this is found in the <see cref="AttributeDictionary"/>, or <c>null</c> otherwise.</param>
        /// <returns>A <c>bool</c> indicating whether an attribute with the specified <paramref name="name"/> was found in the <see cref="AttributeDictionary"/>.</returns>
        public bool TryGetValue(string name, out object value)
        {
            if (name?.Equals("Name", StringComparison.OrdinalIgnoreCase) == true)
            {
                value = _name;
                return true;
            }
            else if (name?.Equals("Length", StringComparison.OrdinalIgnoreCase) == true)
            {
                value = _length;
                return true;
            }
            else if (name?.Equals("Support", StringComparison.OrdinalIgnoreCase) == true)
            {
                value = _support;
                return true;
            }
            else
            {
                return InternalStorage.TryGetValue(name, out value);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="AttributeDictionary"/>.
        /// </summary>
        /// <returns>An enumerator that iterates through the <see cref="AttributeDictionary"/>.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return InternalStorage.GetEnumerator();
        }

        /// <summary>
        /// Constructs an <see cref="AttributeDictionary"/> containing only the <c>"Name"</c>, <c>"Length"</c> and <c>"Support"</c> attributes.
        /// </summary>
        public AttributeDictionary()
        {
            this._name = "";
            this._length = double.NaN;
            this._support = double.NaN;
            this.InternalStorage = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "Name", _name }, { "Length", _length }, { "Support", _support } };
        }
    }
}
