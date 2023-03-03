using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using PhyloTree.Formats;

namespace PhyloTree
{
    /// <summary>
    /// Represents a collection of <see cref="TreeNode"/> objects.
    /// If the full representations of the <see cref="TreeNode"/> objects reside in memory, this offers the best performance at the expense of memory usage.
    /// Alternatively, the trees may be read on demand from a stream in binary format. In this case, accessing any of the trees will require the tree to be parsed. This reduces memory usage, but worsens performance.
    /// The internal storage model of the collection is transparent to consumers (except for the difference in performance/memory usage).
    /// </summary>
    public class TreeCollection : IList<TreeNode>, IReadOnlyList<TreeNode>, IDisposable
    {
        /// <summary>
        /// A list containing the <see cref="TreeNode"/> objects, if they are stored in memory.
        /// </summary>
        private List<TreeNode> InternalStorage = null;

        /// <summary>
        /// A stream containing the tree data in binary format, if this is the chosen storage model. This can be either a <see cref="MemoryStream"/> or a <see cref="FileStream"/>.
        /// </summary>
        public Stream UnderlyingStream { get; private set; } = null;

        /// <summary>
        /// A <see cref="BinaryReader"/> that reads the <see cref="UnderlyingStream"/>
        /// </summary>
        private BinaryReader UnderlyingReader = null;

        /// <summary>
        /// If the trees are stored in binary format, this contains the addresses of the trees (i.e. byte offsets from the start of the stream).
        /// </summary>
        private List<long> TreeAddresses = null;

        /// <summary>
        /// If the collection is manipulated when the trees are stored in the <see cref="UnderlyingStream"/>, entries in <see cref="TreeIndexCorrespondence"/> are used to keep track of which indices have had their meaning change.
        /// </summary>
        private List<int> TreeIndexCorrespondence = null;

        /// <summary>
        /// If the trees are stored in binary format, this determines whether there are global names that are used in parsing the trees.
        /// </summary>
        private readonly bool GlobalNames = false;

        /// <summary>
        /// If the trees are stored in binary format, this contains any global names that are used in parsing the trees.
        /// </summary>
        private IReadOnlyList<string> AllNames = null;

        /// <summary>
        /// If the trees are stored in binary format, this contains any global attributes that are used in parsing the trees.
        /// </summary>
        private IReadOnlyList<Formats.Attribute> AllAttributes = null;

        /// <summary>
        /// Describes the internal storage model of the collection.
        /// </summary>
        enum StorageTypes
        {
            /// <summary>
            /// The trees are stored in a <see cref="List"/>.
            /// </summary>
            List,

            /// <summary>
            /// The trees are stored in binary format in a <see cref="FileStream"/> or <see cref="MemoryStream"/>.
            /// </summary>
            Stream
        }

        /// <summary>
        /// Determines the internal storage model of the collection.
        /// </summary>
        private StorageTypes StorageType;

        /// <summary>
        /// If the trees are stored on disk in a temporary file, you should assign this property to the full path of the file. The file will be deleted when the <see cref="TreeCollection"/> is <see cref="Dispose()"/>d.
        /// </summary>
        public string TemporaryFile { get; set; } = null;

        /// <summary>
        /// The number of trees in the collection.
        /// </summary>
        public int Count
        {
            get
            {
                if (StorageType == StorageTypes.List)
                {
                    return InternalStorage.Count;
                }
                else
                {
                    return TreeIndexCorrespondence.Count;
                }
            }
        }

        /// <summary>
        /// Determine whether the collection is read-only. This is always <c>false</c> in the current implementation.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Obtains an element from the collection.
        /// </summary>
        /// <param name="index">The index of the element to extract.</param>
        /// <returns>The requested element from the collection.</returns>
        public TreeNode this[int index]
        {
            get
            {
                if (StorageType == StorageTypes.List)
                {
                    return InternalStorage[index];
                }
                else
                {
                    int correspIndex = TreeIndexCorrespondence[index];
                    if (correspIndex >= 0)
                    {
                        UnderlyingStream.Seek(TreeAddresses[correspIndex], SeekOrigin.Begin);
                        return UnderlyingReader.ReadTree(GlobalNames, AllNames, AllAttributes);
                    }
                    else
                    {
                        return InternalStorage[-correspIndex - 1];
                    }
                }
            }

            set
            {
                if (StorageType == StorageTypes.List)
                {
                    InternalStorage[index] = value;
                }
                else
                {
                    int correspIndex = TreeIndexCorrespondence[index];
                    if (correspIndex >= 0)
                    {
                        InternalStorage.Add(value);
                        TreeIndexCorrespondence[index] = -InternalStorage.Count;
                    }
                    else
                    {
                        InternalStorage[-correspIndex - 1] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Adds an element to the collection. This is stored in memory, even if the internal storage model of the collection is a <see cref="Stream"/>.
        /// </summary>
        /// <param name="item">The element to add.</param>
        public void Add(TreeNode item)
        {
            InternalStorage.Add(item);
            if (StorageType == StorageTypes.Stream)
            {
                TreeIndexCorrespondence.Add(-InternalStorage.Count);
            }
        }

        /// <summary>
        /// Adds multiple elements to the collection. These are stored in memory, even if the internal storage model of the collection is a <see cref="Stream"/>.
        /// </summary>
        /// <param name="items">The elements to add.</param>
        public void AddRange(IEnumerable<TreeNode> items)
        {
            Contract.Requires(items != null);
            foreach (TreeNode item in items)
            {
                InternalStorage.Add(item);
                if (StorageType == StorageTypes.Stream)
                {
                    TreeIndexCorrespondence.Add(-InternalStorage.Count + 1);
                }
            }
        }

        /// <summary>
        /// Get an <see cref="IEnumerator"/> over the collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> over the collection.</returns>
        public IEnumerator<TreeNode> GetEnumerator()
        {
            if (StorageType == StorageTypes.List)
            {
                return InternalStorage.GetEnumerator();
            }
            else
            {
                TreeCollection coll = this;

                IEnumerable<TreeNode> GetEnumerable()
                {
                    for (int i = 0; i < coll.Count; i++)
                    {
                        yield return coll[i];
                    }
                };

                return GetEnumerable().GetEnumerator();
            }
        }

        /// <summary>
        /// Get an <see cref="IEnumerator"/> over the collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> over the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            if (StorageType == StorageTypes.List)
            {
                return InternalStorage.GetEnumerator();
            }
            else
            {
                TreeCollection coll = this;

                IEnumerable<TreeNode> GetEnumerable()
                {
                    for (int i = 0; i < coll.Count; i++)
                    {
                        yield return coll[i];
                    }
                };

                return GetEnumerable().GetEnumerator();
            }
        }

        /// <summary>
        /// Finds the index of the first occurrence of an element in the collection.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The index of the item in the collection.</returns>
        public int IndexOf(TreeNode item)
        {
            if (StorageType == StorageTypes.List)
            {
                return InternalStorage.IndexOf(item);
            }
            else
            {
                for (int i = 0; i < this.TreeIndexCorrespondence.Count; i++)
                {
                    if (this.TreeIndexCorrespondence[i] < 0)
                    {
                        if (InternalStorage[-this.TreeIndexCorrespondence[i] - 1] == item)
                        {
                            return i;
                        }
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// Inserts an element in the collection at the specified index.
        /// </summary>
        /// <param name="index">The index at which to insert the element.</param>
        /// <param name="item">The element to insert.</param>
        public void Insert(int index, TreeNode item)
        {
            if (StorageType == StorageTypes.List)
            {
                InternalStorage.Insert(index, item);
            }
            else
            {
                if (index < 0 || index >= this.Count)
                {
                    throw new ArgumentOutOfRangeException(paramName: nameof(index));
                }

                InternalStorage.Add(item);
                TreeIndexCorrespondence.Add(TreeIndexCorrespondence[^-1]);
                for (int i = TreeIndexCorrespondence.Count - 2; i > index; i--)
                {
                    TreeIndexCorrespondence[i] = TreeIndexCorrespondence[i - 1];
                }
                TreeIndexCorrespondence[index] = -InternalStorage.Count;
            }
        }

        /// <summary>
        /// Removes from the collection the element at the specified index.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            if (StorageType == StorageTypes.List)
            {
                InternalStorage.RemoveAt(index);
            }
            else
            {
                if (TreeIndexCorrespondence[index] >= 0)
                {
                    TreeIndexCorrespondence.RemoveAt(index);
                }
                else
                {
                    int underlyingIndex = -TreeIndexCorrespondence[index] - 1;
                    TreeIndexCorrespondence.RemoveAt(index);
                    InternalStorage.RemoveAt(underlyingIndex);
                    for (int i = 0; i < TreeIndexCorrespondence.Count; i++)
                    {
                        if (TreeIndexCorrespondence[i] < 0 && (-TreeIndexCorrespondence[i] - 1) > underlyingIndex)
                        {
                            TreeIndexCorrespondence[i]++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes all elements from the collection. If the internal storage model is a <see cref="Stream"/>, it is disposed and the internal storage model is converted to a <see cref="List{TreeNode}"/>.
        /// </summary>
        public void Clear()
        {
            if (StorageType == StorageTypes.List)
            {
                this.InternalStorage.Clear();
            }
            else
            {
                this.InternalStorage.Clear();
                UnderlyingReader.Dispose();
                UnderlyingStream.Dispose();
                UnderlyingReader = null;
                UnderlyingStream = null;
                TreeAddresses = null;
                TreeIndexCorrespondence = null;
                AllNames = null;
                AllAttributes = null;

                this.StorageType = StorageTypes.List;
            }
        }

        /// <summary>
        /// Determines whether the collection contains the specified element.
        /// </summary>
        /// <param name="item">The element to search for.</param>
        /// <returns><c>true</c> if the collection contains the specified element, <c>false</c> otherwise.</returns>
        public bool Contains(TreeNode item)
        {
            if (StorageType == StorageTypes.List)
            {
                return InternalStorage.Contains(item);
            }
            else
            {
                for (int i = 0; i < this.TreeIndexCorrespondence.Count; i++)
                {
                    if (this.TreeIndexCorrespondence[i] > 0)
                    {
                        if (InternalStorage[-this.TreeIndexCorrespondence[i] - 1] == item)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Copies the collection to an array.
        /// </summary>
        /// <param name="array">The array in which to copy the collection.</param>
        /// <param name="arrayIndex">The index at which to start the copy.</param>
        public void CopyTo(TreeNode[] array, int arrayIndex)
        {
            Contract.Requires(array != null);
            if (StorageType == StorageTypes.List)
            {
                InternalStorage.CopyTo(array, arrayIndex);
            }
            else
            {
                for (int i = 0; i < this.Count; i++)
                {
                    array[arrayIndex + i] = this[i];
                }
            }
        }

        /// <summary>
        /// Removes the specified element from the collection.
        /// </summary>
        /// <param name="item">The element to remove.</param>
        /// <returns><c>true</c> if the removal was successful (i.e. the list contained the element in the first place), <c>false</c> otherwise.</returns>
        public bool Remove(TreeNode item)
        {
            if (StorageType == StorageTypes.List)
            {
                return InternalStorage.Remove(item);
            }
            else
            {
                int index = this.IndexOf(item);
                if (index >= 0)
                {
                    this.RemoveAt(index);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Constructs a <see cref="TreeCollection"/> object from a <see cref="List{TreeNode}"/>.
        /// </summary>
        /// <param name="internalStorage">The <see cref="List{TreeNode}"/> containing the trees to store in the collection. Note that this list is not copied, but used as-is.</param>
        public TreeCollection(List<TreeNode> internalStorage)
        {
            InternalStorage = internalStorage;
            StorageType = StorageTypes.List;
        }

        /// <summary>
        /// Constructs a <see cref="TreeCollection"/> object from a stream of trees in binary format.
        /// </summary>
        /// <param name="binaryTreeStream">The stream of trees in binary format to use. The stream will be disposed when the <see cref="TreeCollection"/> is disposed. It should not be disposed earlier by external code.</param>
        public TreeCollection(Stream binaryTreeStream)
        {
            UnderlyingStream = binaryTreeStream;
            UnderlyingReader = new BinaryReader(UnderlyingStream);

            BinaryTreeMetadata metadata = BinaryTree.ParseMetadata(binaryTreeStream, true, UnderlyingReader);

            TreeAddresses = new List<long>(metadata.TreeAddresses);

            GlobalNames = metadata.GlobalNames;
            AllNames = metadata.Names;
            AllAttributes = metadata.AllAttributes;

            TreeIndexCorrespondence = new List<int>();
            for (int i = 0; i < TreeAddresses.Count; i++)
            {
                TreeIndexCorrespondence.Add(i);
            }
            StorageType = StorageTypes.Stream;
        }

        /// <summary>
        /// Determines whether the <see cref="TreeCollection"/> has already been disposed.
        /// </summary>
        private bool disposedValue = false;

        /// <summary>
        /// Disposes the <see cref="TreeCollection"/>.
        /// </summary>
        /// <param name="disposing">Determines whether the method has been called by user code or by the destructor.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031")]
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    UnderlyingReader?.Dispose();
                    UnderlyingStream?.Dispose();
                }

                InternalStorage = null;
                UnderlyingReader = null;
                UnderlyingStream = null;
                TreeAddresses = null;
                TreeIndexCorrespondence = null;
                AllNames = null;
                AllAttributes = null;

                if (!string.IsNullOrEmpty(TemporaryFile))
                {
                    try
                    {
                        File.Delete(TemporaryFile);
                    }
                    catch { }
                    TemporaryFile = null;
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~TreeCollection()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the <see cref="TreeCollection"/>, the underlying <see cref="Stream"/> and <see cref="StreamReader"/>, and deletes the <see cref="TemporaryFile"/> (if applicable).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

}
