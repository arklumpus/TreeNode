using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Contains classes and methods to read and write phylogenetic trees in multiple formats
/// </summary>
namespace PhyloTree.Formats
{
    /// <summary>
    /// Contains methods to read and write tree files in binary format.
    /// </summary>
    public static class BinaryTree
    {
        /// <summary>
        /// Determines whether the tree file stream has a valid trailer.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read. Its <see cref="Stream.CanSeek"/> must be <c>true</c>. It does not have to be a <see cref="FileStream"/>.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <returns><c>true</c> if the <paramref name="inputStream"/> has a valid trailer, <c>false</c> otherwise.</returns>
        public static bool HasValidTrailer(Stream inputStream, bool keepOpen = false)
        {
            Contract.Requires(inputStream != null);

            BinaryReader reader = new BinaryReader(inputStream, Encoding.UTF8, true);

            try
            {
                long position = inputStream.Position;
                inputStream.Seek(-4, SeekOrigin.End);

                if (reader.ReadByte() != (byte)'E' || reader.ReadByte() != (byte)'N' || reader.ReadByte() != (byte)'D' || reader.ReadByte() != (byte)255)
                {
                    inputStream.Position = position;
                    return false;
                }

                inputStream.Position = position;
                return true;
            }
            finally
            {
                reader.Dispose();

                if (!keepOpen)
                {
                    inputStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Determines whether the tree file stream is valid (i.e. it has a valid header).
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read. Its <see cref="Stream.CanSeek"/> must be <c>true</c>. It does not have to be a <see cref="FileStream"/>.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <returns><c>true</c> if the <paramref name="inputStream"/> has a valid header, <c>false</c> otherwise.</returns>
        public static bool IsValidStream(Stream inputStream, bool keepOpen = false)
        {
            Contract.Requires(inputStream != null);

            BinaryReader reader = new BinaryReader(inputStream, Encoding.UTF8, true);

            try
            {
                long position = inputStream.Position;

                if (reader.ReadByte() != (byte)'#' || reader.ReadByte() != (byte)'T' || reader.ReadByte() != (byte)'R' || reader.ReadByte() != (byte)'E')
                {
                    inputStream.Position = position;
                    return false;
                }

                byte headerByte = reader.ReadByte();

                if ((headerByte & 0b11111100) != 0)
                {
                    inputStream.Position = position;
                    return false;
                }

                return true;
            }
            finally
            {
                reader.Dispose();

                if (!keepOpen)
                {
                    inputStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Reads the metadata from a file containing trees in binary format.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read. Its <see cref="Stream.CanSeek"/> must be <c>true</c>. It does not have to be a <see cref="FileStream"/>.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="reader">A <see cref="BinaryReader"/> to read from the <paramref name="inputStream"/>. If this is <c>null</c>, a new <see cref="BinaryReader"/> will be initialised and disposed within this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that may be invoked while parsing the tree file, with an argument ranging from 0 to 1 describing the progress made in reading the file (determined by the position in the stream).</param>
        /// <returns>A <see cref="BinaryTreeMetadata"/> object containing metadata information about the tree file.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000")]
        public static BinaryTreeMetadata ParseMetadata(Stream inputStream, bool keepOpen = false, BinaryReader reader = null, Action<double> progressAction = null)
        {
            Contract.Requires(inputStream != null);

            bool wasExternalReader = true;

            try
            {
                if (reader == null)
                {
                    wasExternalReader = false;
                    reader = new BinaryReader(inputStream, Encoding.UTF8, true);
                }

                BinaryTreeMetadata tbr = new BinaryTreeMetadata();

                if (reader.ReadByte() != (byte)'#' || reader.ReadByte() != (byte)'T' || reader.ReadByte() != (byte)'R' || reader.ReadByte() != (byte)'E')
                {
                    throw new FormatException("Invalid file header!");
                }

                byte headerByte = reader.ReadByte();

                if ((headerByte & 0b11111100) != 0)
                {
                    throw new FormatException("Invalid file header!");
                }

                bool globalNames = (headerByte & 0b1) != 0;
                bool globalAttributes = (headerByte & 0b10) != 0;

                tbr.GlobalNames = globalNames;

                inputStream.Seek(-4, SeekOrigin.End);

                bool validTrailer = true;

                if (reader.ReadByte() != (byte)'E' || reader.ReadByte() != (byte)'N' || reader.ReadByte() != (byte)'D' || reader.ReadByte() != (byte)255)
                {
                    validTrailer = false;
                }

                if (validTrailer)
                {
                    inputStream.Seek(-12, SeekOrigin.End);
                    long labelAddress = reader.ReadInt64();

                    IEnumerable<long> getEnumerable()
                    {
                        inputStream.Seek(labelAddress, SeekOrigin.Begin);
                        int numOfTrees = reader.ReadInt();

                        for (int i = 0; i < numOfTrees; i++)
                        {
                            yield return reader.ReadInt64();
                        }
                    };

                    tbr.TreeAddresses = getEnumerable();
                }

                inputStream.Seek(5, SeekOrigin.Begin);

                string[] allNames = null;

                if (globalNames)
                {
                    allNames = new string[reader.ReadInt()];
                    for (int i = 0; i < allNames.Length; i++)
                    {
                        allNames[i] = reader.ReadMyString();
                    }
                    tbr.Names = allNames;
                }

                Attribute[] allAttributes = null;

                if (globalAttributes)
                {
                    allAttributes = new Attribute[reader.ReadInt()];

                    for (int i = 0; i < allAttributes.Length; i++)
                    {
                        allAttributes[i] = new Attribute(reader.ReadMyString(), reader.ReadInt() == 2);
                    }
                    tbr.AllAttributes = allAttributes;
                }

                if (!validTrailer)
                {
                    IEnumerable<long> getEnumerable()
                    {
                        bool error = false;

                        while (!error)
                        {
                            long position = inputStream.Position;

                            TreeNode tree = null;
                            try
                            {
                                tree = reader.ReadTree(globalNames, allNames, allAttributes);
                            }
                            catch
                            {
                                error = true;
                            }

                            if (!error)
                            {
                                yield return position;
                                double progress = Math.Max(0, Math.Min(1, (double)position / inputStream.Length));
                                progressAction?.Invoke(progress);
                            }
                        }
                    }

                    tbr.TreeAddresses = getEnumerable();
                }

                return tbr;
            }
            finally
            {
                if (!wasExternalReader || !keepOpen)
                {
                    reader.Dispose();
                }
                if (!keepOpen)
                {
                    inputStream.Dispose();
                }
            }
        }


        /// <summary>
        /// Lazily parses trees from a file in binary format. Each tree in the file is not read and parsed until it is requested.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read. Its <see cref="Stream.CanSeek"/> must be <c>true</c>. It does not have to be a <see cref="FileStream"/>.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the file.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031")]
        public static IEnumerable<TreeNode> ParseTrees(Stream inputStream, bool keepOpen = false, Action<double> progressAction = null)
        {
            Contract.Requires(inputStream != null);

            BinaryReader reader = new BinaryReader(inputStream, Encoding.UTF8, true);

            try
            {
                if (reader.ReadByte() != (byte)'#' || reader.ReadByte() != (byte)'T' || reader.ReadByte() != (byte)'R' || reader.ReadByte() != (byte)'E')
                {
                    throw new FormatException("Invalid file header!");
                }

                byte headerByte = reader.ReadByte();

                if ((headerByte & 0b11111100) != 0)
                {
                    throw new FormatException("Invalid file header!");
                }

                bool globalNames = (headerByte & 0b1) != 0;
                bool globalAttributes = (headerByte & 0b10) != 0;


                inputStream.Seek(-4, SeekOrigin.End);

                bool validTrailer = true;

                if (reader.ReadByte() != (byte)'E' || reader.ReadByte() != (byte)'N' || reader.ReadByte() != (byte)'D' || reader.ReadByte() != (byte)255)
                {
                    validTrailer = false;
                }

                List<long> treeAddresses;

                if (validTrailer)
                {
                    inputStream.Seek(-12, SeekOrigin.End);
                    long labelAddress = reader.ReadInt64();

                    inputStream.Seek(labelAddress, SeekOrigin.Begin);
                    int numOfTrees = reader.ReadInt();
                    treeAddresses = new List<long>(numOfTrees);

                    for (int i = 0; i < numOfTrees; i++)
                    {
                        treeAddresses.Add(reader.ReadInt64());
                    }
                }
                else
                {
                    treeAddresses = new List<long>();
                }

                inputStream.Seek(5, SeekOrigin.Begin);

                string[] allNames = null;

                if (globalNames)
                {
                    allNames = new string[reader.ReadInt()];
                    for (int i = 0; i < allNames.Length; i++)
                    {
                        allNames[i] = reader.ReadMyString();
                    }
                }

                Attribute[] allAttributes = null;

                if (globalAttributes)
                {
                    allAttributes = new Attribute[reader.ReadInt()];

                    for (int i = 0; i < allAttributes.Length; i++)
                    {
                        allAttributes[i] = new Attribute(reader.ReadMyString(), reader.ReadInt() == 2);
                    }
                }

                if (validTrailer)
                {
                    for (int i = 0; i < treeAddresses.Count; i++)
                    {
                        inputStream.Seek(treeAddresses[i], SeekOrigin.Begin);
                        yield return reader.ReadTree(globalNames, allNames, allAttributes);
                        double progress = Math.Max(0, Math.Min(1, (double)(i + 1) / treeAddresses.Count));
                        progressAction?.Invoke(progress);
                    }
                }
                else
                {
                    bool error = false;

                    while (!error)
                    {
                        TreeNode tree = null;
                        try
                        {
                            tree = reader.ReadTree(globalNames, allNames, allAttributes);
                        }
                        catch
                        {
                            error = true;
                        }

                        if (!error)
                        {
                            yield return tree;
                            double progress = Math.Max(0, Math.Min(1, (double)(inputStream.Position) / inputStream.Length));
                            progressAction?.Invoke(progress);
                        }
                    }
                }
            }
            finally
            {
                reader.Dispose();

                if (!keepOpen)
                {
                    inputStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Parses trees from a file in binary format and completely loads them in memory.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read. Its <see cref="Stream.CanSeek"/> must be <c>true</c>. It does not have to be a <see cref="FileStream"/>.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the file.</returns>
        public static List<TreeNode> ParseAllTrees(Stream inputStream, bool keepOpen = false, Action<double> progressAction = null)
        {
            return ParseTrees(inputStream, keepOpen, progressAction).ToList();
        }

        /// <summary>
        /// Lazily parses trees from a file in binary format. Each tree in the file is not read and parsed until it is requested.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the file.</returns>
        public static IEnumerable<TreeNode> ParseTrees(string inputFile, Action<double> progressAction = null)
        {
            FileStream inputStream = File.OpenRead(inputFile);
            return ParseTrees(inputStream, false, progressAction);
        }

        /// <summary>
        /// Parses trees from a file in binary format and completely loads them in memory.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the file.</returns>
        public static List<TreeNode> ParseAllTrees(string inputFile, Action<double> progressAction = null)
        {
            using FileStream inputStream = File.OpenRead(inputFile);
            return ParseAllTrees(inputStream, false, progressAction);
        }

        /// <summary>
        /// Writes a single tree in Binary format.
        /// </summary>
        /// <param name="tree">The tree to be written.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="additionalDataToCopy">A stream containing additional data that will be copied into the binary file.</param>
        public static void WriteTree(TreeNode tree, Stream outputStream, bool keepOpen = false, Stream additionalDataToCopy = null)
        {
            WriteAllTrees(new List<TreeNode> { tree }, outputStream, keepOpen, null, additionalDataToCopy);
        }

        /// <summary>
        /// Writes a single tree in Binary format.
        /// </summary>
        /// <param name="tree">The tree to be written.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="additionalDataToCopy">A stream containing additional data that will be copied into the binary file.</param>
        public static void WriteTree(TreeNode tree, string outputFile, bool append = false, Stream additionalDataToCopy = null)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(new List<TreeNode>() { tree }, outputStream, false, null, additionalDataToCopy);
        }

        /// <summary>
        /// Writes trees in binary format.
        /// </summary>
        /// <param name="trees">An <see cref="IEnumerable{T}"/> containing the trees to be written. It will ony be enumerated once.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with the number of trees written so far.</param>
        /// <param name="additionalDataToCopy">A stream containing additional data that will be copied into the binary file.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, string outputFile, bool append = false, Action<int> progressAction = null, Stream additionalDataToCopy = null)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(trees, outputStream, false, progressAction, additionalDataToCopy);
        }

        /// <summary>
        /// Writes trees in binary format.
        /// </summary>
        /// <param name="trees">An <see cref="IEnumerable{T}"/> containing the trees to be written. It will ony be enumerated once.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the trees should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with the number of trees written so far.</param>
        /// <param name="additionalDataToCopy">A stream containing additional data that will be copied into the binary file.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, Stream outputStream, bool keepOpen = false, Action<int> progressAction = null, Stream additionalDataToCopy = null)
        {
            Contract.Requires(trees != null);

            using BinaryWriter writer = new BinaryWriter(outputStream, Encoding.UTF8, keepOpen);

            writer.Write(new byte[] { (byte)'#', (byte)'T', (byte)'R', (byte)'E', 0 });


            List<long> addresses = new List<long>();

            foreach (TreeNode tree in trees)
            {
                writer.Flush();
                addresses.Add(outputStream.Position);
                writer.WriteTree(tree);
                progressAction?.Invoke(addresses.Count);
            }

            if (additionalDataToCopy != null)
            {
                additionalDataToCopy.CopyTo(outputStream);
            }

            writer.Flush();

            long labelAddress = outputStream.Position;

            writer.WriteInt(addresses.Count);

            for (int i = 0; i < addresses.Count; i++)
            {
                writer.Write(addresses[i]);
            }

            writer.Write(labelAddress);

            writer.Write(new byte[] { (byte)'E', (byte)'N', (byte)'D', (byte)255 });

        }

        /// <summary>
        /// Writes trees in binary format.
        /// </summary>
        /// <param name="trees">A collection of trees to be written. Each tree will be accessed twice.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with a value between 0 and 1 depending on how many trees have been written so far.</param>
        /// <param name="additionalDataToCopy">A stream containing additional data that will be copied into the binary file.</param>
        public static void WriteAllTrees(IList<TreeNode> trees, string outputFile, bool append = false, Action<double> progressAction = null, Stream additionalDataToCopy = null)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(trees, outputStream, false, progressAction, additionalDataToCopy);
        }


        /// <summary>
        /// Writes trees in binary format.
        /// </summary>
        /// <param name="trees">A collection of trees to be written. Each tree will be accessed twice.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the trees should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with a value between 0 and 1 depending on how many trees have been written so far.</param>
        /// <param name="additionalDataToCopy">A stream containing additional data that will be copied into the binary file.</param>
        public static void WriteAllTrees(IList<TreeNode> trees, Stream outputStream, bool keepOpen = false, Action<double> progressAction = null, Stream additionalDataToCopy = null)
        {
            Contract.Requires(trees != null);

            Dictionary<string, int> allNamesLookup = new Dictionary<string, int>();
            List<string> allNamesLookupReverse = new List<string>();

            Dictionary<(string, bool), int> allAttributesLookup = new Dictionary<(string, bool), int>();
            List<(string, bool)> allAttributesLookupReverse = new List<(string, bool)>();

            bool includeNamesPerTree = false;
            bool includeAttributesPerTree = false;

            for (int i = 0; i < trees.Count; i++)
            {
                int prevNameCount = allNamesLookup.Count;
                int prevAttributeCount = allAttributesLookup.Count;

                int count = 0;

                int maxAttributeCount = 0;

                foreach (TreeNode node in trees[i].GetChildrenRecursiveLazy())
                {
                    if (!string.IsNullOrEmpty(node.Name))
                    {
                        count++;
                        if (allNamesLookup.TryAdd(node.Name, allNamesLookup.Count))
                        {
                            allNamesLookupReverse.Add(node.Name);
                        }
                    }

                    maxAttributeCount = Math.Max(maxAttributeCount, node.Attributes.Count);

                    foreach (KeyValuePair<string, object> kvp in node.Attributes)
                    {
                        bool isDouble = kvp.Value is double;
                        if (allAttributesLookup.TryAdd((kvp.Key, isDouble), allAttributesLookup.Count))
                        {
                            allAttributesLookupReverse.Add((kvp.Key, isDouble));
                        }
                    }
                }

                if (prevNameCount != 0 && (allNamesLookup.Count - prevNameCount) * 2 > count)
                {
                    includeNamesPerTree = true;
                }

                if (prevAttributeCount != 0 && (allAttributesLookup.Count - prevAttributeCount) * 2 > maxAttributeCount)
                {
                    includeAttributesPerTree = true;
                }

                if (includeNamesPerTree && includeAttributesPerTree)
                {
                    break;
                }
            }

            using BinaryWriter writer = new BinaryWriter(outputStream, Encoding.UTF8, keepOpen);

            writer.Write(new byte[] { (byte)'#', (byte)'T', (byte)'R', (byte)'E' });

            if (!includeNamesPerTree && !includeAttributesPerTree)
            {
                writer.Write((byte)0b00000011);
            }
            else if (!includeNamesPerTree && includeAttributesPerTree)
            {
                writer.Write((byte)0b00000001);
            }
            else if (includeNamesPerTree && !includeAttributesPerTree)
            {
                writer.Write((byte)0b00000010);
            }
            else
            {
                writer.Write((byte)0b00000000);
            }

            if (!includeNamesPerTree)
            {
                writer.WriteInt(allNamesLookup.Count);

                for (int i = 0; i < allNamesLookup.Count; i++)
                {
                    writer.WriteMyString(allNamesLookupReverse[i]);
                }
            }

            if (!includeAttributesPerTree)
            {
                writer.WriteInt(allAttributesLookup.Count);

                for (int i = 0; i < allAttributesLookup.Count; i++)
                {
                    writer.WriteMyString(allAttributesLookupReverse[i].Item1);
                    writer.WriteInt(allAttributesLookupReverse[i].Item2 ? 2 : 1);
                }
            }

            long[] addresses = new long[trees.Count];

            for (int i = 0; i < trees.Count; i++)
            {
                writer.Flush();
                addresses[i] = outputStream.Position;
                writer.WriteTree(trees[i], !includeNamesPerTree, !includeAttributesPerTree, allNamesLookup, allAttributesLookup);

                double progress = Math.Max(0, Math.Min(1, (double)(i + 1) / trees.Count));

                progressAction?.Invoke(progress);
            }

            if (additionalDataToCopy != null)
            {
                additionalDataToCopy.CopyTo(outputStream);
            }

            writer.Flush();

            long labelAddress = outputStream.Position;

            writer.WriteInt(addresses.Length);

            for (int i = 0; i < addresses.Length; i++)
            {
                writer.Write(addresses[i]);
            }

            writer.Write(labelAddress);

            writer.Write(new byte[] { (byte)'E', (byte)'N', (byte)'D', (byte)255 });

        }
    }

    /// <summary>
    /// Holds metadata information about a file containing trees in binary format.
    /// </summary>
    public class BinaryTreeMetadata
    {
        /// <summary>
        /// The addresses of the trees (i.e. byte offsets from the start of the file).
        /// </summary>
        public IEnumerable<long> TreeAddresses { get; set; }

        /// <summary>
        /// Determines whether there are any global names stored in the file's header that are used when parsing the trees.
        /// </summary>
        public bool GlobalNames { get; set; }

        /// <summary>
        /// Contains any global names stored in the file's header that are used when parsing the trees.
        /// </summary>
        public IReadOnlyList<string> Names { get; set; }

        /// <summary>
        /// Contains any global attributes stored in the file's header that are used when parsing the trees.
        /// </summary>
        public IReadOnlyList<Attribute> AllAttributes { get; set; }
    }

    /// <summary>
    /// Describes an attribute of a node.
    /// </summary>
    public struct Attribute : IEquatable<Attribute>
    {
        /// <summary>
        /// The name of the attribute.
        /// </summary>
        public string AttributeName { get; }

        /// <summary>
        /// Whether the attribute is represented by a numeric value or a string.
        /// </summary>
        public bool IsNumeric { get; }

        /// <summary>
        /// Constructs a new <see cref="Attribute"/>.
        /// </summary>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <param name="isNumeric">Whether the attribute is represented by a numeric value or a string.</param>
        public Attribute(string attributeName, bool isNumeric)
        {
            this.AttributeName = attributeName;
            this.IsNumeric = isNumeric;
        }

        /// <summary>
        /// Compares an <see cref="Attribute"/> and another <see cref="object"/>.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare to.</param>
        /// <returns><c>true</c> if <paramref name="obj"/> is an <see cref="Attribute"/> and it has the same <see cref="AttributeName"/> (case insensitive) and value for <see cref="IsNumeric"/> as the current instance. <c>false</c> otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Attribute attr)
            {
                return this.AttributeName.Equals(attr.AttributeName, StringComparison.OrdinalIgnoreCase) && this.IsNumeric == attr.IsNumeric;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the hash code for this <see cref="Attribute"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="Attribute"/>.</returns>
        public override int GetHashCode()
        {
            return this.AttributeName.GetHashCode(StringComparison.OrdinalIgnoreCase) + this.IsNumeric.GetHashCode();
        }

        /// <summary>
        /// Compares two <see cref="Attribute"/>s.
        /// </summary>
        /// <param name="left">The first <see cref="Attribute"/> to compare.</param>
        /// <param name="right">The second <see cref="Attribute"/> to compare.</param>
        /// <returns><c>true</c> if both <see cref="Attribute"/>s have the same <see cref="AttributeName"/> (case insensitive) and value for <see cref="IsNumeric"/>. <c>false</c> otherwise.</returns>
        public static bool operator ==(Attribute left, Attribute right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Attribute"/>s (negated).
        /// </summary>
        /// <param name="left">The first <see cref="Attribute"/> to compare.</param>
        /// <param name="right">The second <see cref="Attribute"/> to compare.</param>
        /// <returns><c>false</c> if both <see cref="Attribute"/>s have the same <see cref="AttributeName"/> (case insensitive) and value for <see cref="IsNumeric"/>. <c>true</c> otherwise.</returns>
        public static bool operator !=(Attribute left, Attribute right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Compares two <see cref="Attribute"/>s.
        /// </summary>
        /// <param name="other">The <see cref="Attribute"/> to compare to.</param>
        /// <returns><c>true</c> if <paramref name="other"/> has the same <see cref="AttributeName"/> (case insensitive) and value for <see cref="IsNumeric"/> as the current instance. <c>false</c> otherwise.</returns>
        public bool Equals(Attribute other)
        {
            return this.AttributeName.Equals(other.AttributeName, StringComparison.OrdinalIgnoreCase) && this.IsNumeric == other.IsNumeric;
        }
    }

    /// <summary>
    /// Extension methods for <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
    /// </summary>
    internal static class BinaryExtensions
    {
        /// <summary>
        /// Writes a variable-width integer to the stream. If the integer is smaller than 254, it is only 1-byte wide; otherwise it is 40-bit (5-byte) wide.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which to write.</param>
        /// <param name="value">The value to be written.</param>
        public static void WriteInt(this BinaryWriter writer, int value)
        {
            if (value < 254)
            {
                writer.Write((byte)value);
            }
            else
            {
                writer.Write((byte)254);
                writer.Write(value);
            }
        }

        /// <summary>
        /// Reads a variable-width integer from the stream. If the integer is smaller than 254, it is only 1-byte wide; otherwise it is 40-bit (5-byte) wide.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which to read.</param>
        /// <returns>The value read.</returns>
        public static int ReadInt(this BinaryReader reader)
        {
            byte b = reader.ReadByte();

            if (b < 254)
            {
                return b;
            }
            else
            {
                return reader.ReadInt32();
            }
        }

        /// <summary>
        /// Writes a string to the stream. The string is stored as an integer n representing its length followed by n integers that constitute the UTF-16 representation of the string.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which to write.</param>
        /// <param name="value">The value to be written.</param>
        public static void WriteMyString(this BinaryWriter writer, string value)
        {
            writer.WriteInt(value.Length);

            foreach (char c in value)
            {
                writer.WriteInt(c);
            }
        }

        /// <summary>
        /// Reads a string from the stream. The string is stored as an integer n representing its length followed by n integers that constitute the UTF-16 representation of the string.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which to read.</param>
        /// <returns>The value read.</returns>
        public static string ReadMyString(this BinaryReader reader)
        {
            int length = reader.ReadInt();
            StringBuilder bld = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                bld.Append((char)reader.ReadInt());
            }

            return bld.ToString();
        }

        /// <summary>
        /// Read a variable-width integer from the stream. If the integer is equal to 0, 2 or 3, it is 2-bit wide; if it is 1, 4 or 5, it is 4-bit wide; if it is greater than 5, the current byte is padded and the integer is represented as an integer of the format read by <see cref="ReadInt(BinaryReader)"/> in the following byte(s).
        /// The initial value of currByte should be read using <see cref="BinaryReader.ReadByte"/> and the initial value of currIndex should be 0. Successive reads should use the same variables, which will have been updated by this method.
        /// After the last read, if *currIndex is equal to 0, it means that currByte has not been processed (thus you should seek back by 1).
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which to read.</param>
        /// <param name="currByte">The current byte that is being read</param>
        /// <param name="currIndex">The current index within the byte.</param>
        /// <returns>The value read.</returns>
        public static int ReadShortInt(this BinaryReader reader, ref byte currByte, ref int currIndex)
        {
            if (currIndex == 0)
            {
                int twoBits = currByte & 0b00000011;

                currIndex = 2;

                if (twoBits == 0b00)
                {
                    return 0;
                }
                else if (twoBits == 0b01)
                {
                    return 2;
                }
                else if (twoBits == 0b10)
                {
                    return 3;
                }
                else// if (twoBits == 0b11)
                {
                    int fourBits = currByte & 0b00001111;
                    currIndex = 4;

                    if (fourBits == 0b0011)
                    {
                        return 1;
                    }
                    else if (fourBits == 0b0111)
                    {
                        return 4;
                    }
                    else if (fourBits == 0b1011)
                    {
                        return 5;
                    }
                    else// if (fourBits == 0b1111)
                    {
                        int tbr = reader.ReadInt();
                        currByte = reader.ReadByte();
                        currIndex = 0;
                        return tbr;
                    }
                }
            }
            else if (currIndex == 2)
            {
                int twoBits = (currByte & 0b00001100) >> 2;

                currIndex = 4;

                if (twoBits == 0b00)
                {
                    return 0;
                }
                else if (twoBits == 0b01)
                {
                    return 2;
                }
                else if (twoBits == 0b10)
                {
                    return 3;
                }
                else// if (twoBits == 0b11)
                {
                    int fourBits = (currByte & 0b00111100) >> 2;
                    currIndex = 6;

                    if (fourBits == 0b0011)
                    {
                        return 1;
                    }
                    else if (fourBits == 0b0111)
                    {
                        return 4;
                    }
                    else if (fourBits == 0b1011)
                    {
                        return 5;
                    }
                    else// if (fourBits == 0b1111)
                    {
                        int tbr = reader.ReadInt();
                        currByte = reader.ReadByte();
                        currIndex = 0;
                        return tbr;
                    }
                }
            }
            else if (currIndex == 4)
            {
                int twoBits = (currByte & 0b00110000) >> 4;

                currIndex = 6;

                if (twoBits == 0b00)
                {
                    return 0;
                }
                else if (twoBits == 0b01)
                {
                    return 2;
                }
                else if (twoBits == 0b10)
                {
                    return 3;
                }
                else// if (twoBits == 0b11)
                {
                    int fourBits = (currByte & 0b11110000) >> 4;
                    currIndex = 0;

                    if (fourBits == 0b0011)
                    {
                        currByte = reader.ReadByte();
                        return 1;
                    }
                    else if (fourBits == 0b0111)
                    {
                        currByte = reader.ReadByte();
                        return 4;
                    }
                    else if (fourBits == 0b1011)
                    {
                        currByte = reader.ReadByte();
                        return 5;
                    }
                    else// if (fourBits == 0b1111)
                    {
                        int tbr = reader.ReadInt();
                        currByte = reader.ReadByte();
                        currIndex = 0;
                        return tbr;
                    }
                }
            }
            else //if (currIndex == 6)
            {
                int twoBits = (currByte & 0b11000000) >> 6;

                currIndex = 0;
                currByte = reader.ReadByte();

                if (twoBits == 0b00)
                {
                    return 0;
                }
                else if (twoBits == 0b01)
                {
                    return 2;
                }
                else if (twoBits == 0b10)
                {
                    return 3;
                }
                else// if (twoBits == 0b11)
                {
                    int fourBits = twoBits | ((currByte & 0b00000011) << 2);
                    currIndex = 2;

                    if (fourBits == 0b0011)
                    {
                        return 1;
                    }
                    else if (fourBits == 0b0111)
                    {
                        return 4;
                    }
                    else if (fourBits == 0b1011)
                    {
                        return 5;
                    }
                    else// if (fourBits == 0b1111)
                    {
                        int tbr = reader.ReadInt();
                        currByte = reader.ReadByte();
                        currIndex = 0;
                        return tbr;
                    }
                }
            }
        }

        /// <summary>
        /// Write a variable-width integer from the stream. If the integer is equal to 0, 2 or 3, it is 2-bit wide; if it is 1, 4 or 5, it is 4-bit wide; if it is greater than 5, the current byte is padded and the integer is represented as an integer of the format written by readInt in the following byte(s).
        /// The initial value of currByte and currIndex should be 0. Successive writes should use the same variables, which will have been updated by this method.
        /// After the last write, if *currIndex is not 0, it means that the current byte has not been written to the stream yet.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which to write.</param>
        /// <param name="value">The value to be written.</param>
        /// <param name="currByte">The value of the byte that is being written.</param>
        /// <param name="currIndex">The current index within the byte.</param>
        /// <returns></returns>
        public static int WriteShortInt(this BinaryWriter writer, int value, ref byte currByte, int currIndex)
        {
            if (value == 0)
            {
                //00
                if (currIndex == 0)
                {
                    return 2;
                }
                else if (currIndex == 2)
                {
                    return 4;
                }
                else if (currIndex == 4)
                {
                    return 6;
                }
                else if (currIndex == 6)
                {
                    writer.Write(currByte);
                    currByte = 0;
                    return 0;
                }
            }
            else if (value == 2)
            {
                //01
                if (currIndex == 0)
                {
                    currByte = (byte)(currByte | 0b00000001);
                    return 2;
                }
                else if (currIndex == 2)
                {
                    currByte = (byte)(currByte | 0b00000100);
                    return 4;
                }
                else if (currIndex == 4)
                {
                    currByte = (byte)(currByte | 0b00010000);
                    return 6;
                }
                else if (currIndex == 6)
                {
                    writer.Write((byte)(currByte | 0b01000000));
                    currByte = 0;
                    return 0;
                }
            }
            else if (value == 3)
            {
                //10
                if (currIndex == 0)
                {
                    currByte = (byte)(currByte | 0b00000010);
                    return 2;
                }
                else if (currIndex == 2)
                {
                    currByte = (byte)(currByte | 0b00001000);
                    return 4;
                }
                else if (currIndex == 4)
                {
                    currByte = (byte)(currByte | 0b00100000);
                    return 6;
                }
                else if (currIndex == 6)
                {
                    writer.Write((byte)(currByte | 0b10000000));
                    currByte = 0;
                    return 0;
                }
            }
            else if (value == 1)
            {
                //0011
                if (currIndex == 0)
                {
                    currByte = (byte)(currByte | 0b00000011);
                    return 4;
                }
                else if (currIndex == 2)
                {
                    currByte = (byte)(currByte | 0b00001100);
                    return 6;
                }
                else if (currIndex == 4)
                {
                    writer.Write((byte)(currByte | 0b00110000));
                    currByte = 0;
                    return 0;
                }
                else if (currIndex == 6)
                {
                    writer.Write((byte)(currByte | 0b11000000));
                    currByte = 0;
                    return 2;
                }
            }
            else if (value == 4)
            {
                //0111
                if (currIndex == 0)
                {
                    currByte = (byte)(currByte | 0b00000111);
                    return 4;
                }
                else if (currIndex == 2)
                {
                    currByte = (byte)(currByte | 0b00011100);
                    return 6;
                }
                else if (currIndex == 4)
                {
                    writer.Write((byte)(currByte | 0b01110000));
                    currByte = 0;
                    return 0;
                }
                else if (currIndex == 6)
                {
                    writer.Write((byte)(currByte | 0b11000000));
                    currByte = 0b00000001;
                    return 2;
                }
            }
            else if (value == 5)
            {
                //1011
                if (currIndex == 0)
                {
                    currByte = (byte)(currByte | 0b00001011);
                    return 4;
                }
                else if (currIndex == 2)
                {
                    currByte = (byte)(currByte | 0b00101100);
                    return 6;
                }
                else if (currIndex == 4)
                {
                    writer.Write((byte)(currByte | 0b10110000));
                    currByte = 0;
                    return 0;
                }
                else if (currIndex == 6)
                {
                    writer.Write((byte)(currByte | 0b11000000));
                    currByte = 0b00000010;
                    return 2;
                }
            }
            else
            {
                //1111
                if (currIndex == 0)
                {
                    writer.Write((byte)(currByte | 0b00001111));
                    writer.WriteInt(value);
                    currByte = 0;
                    return 0;
                }
                else if (currIndex == 2)
                {
                    writer.Write((byte)(currByte | 0b00111100));
                    writer.WriteInt(value);
                    currByte = 0;
                    return 0;
                }
                else if (currIndex == 4)
                {
                    writer.Write((byte)(currByte | 0b11110000));
                    writer.WriteInt(value);
                    currByte = 0;
                    return 0;
                }
                else if (currIndex == 6)
                {
                    writer.Write((byte)(currByte | 0b11000000));
                    writer.Write((byte)0b00000011);
                    writer.WriteInt(value);
                    currByte = 0;
                    return 0;
                }
            }

            throw new IndexOutOfRangeException("Unexpected position!");
        }

        /// <summary>
        /// Writes a tree in binary format to the stream.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which to write.</param>
        /// <param name="tree">The <see cref="TreeNode"/> to be written.</param>
        /// <param name="globalNames">Specifies whether global names are stored in the file's header.</param>
        /// <param name="globalAttributes">Specified whether global attributes are stored in the file's header.</param>
        /// <param name="names">The global names specified in the file's header.</param>
        /// <param name="attributes">The global attributes specified in the file's header.</param>
        public static void WriteTree(this BinaryWriter writer, TreeNode tree, bool globalNames = false, bool globalAttributes = false, Dictionary<string, int> names = null, Dictionary<(string, bool), int> attributes = null)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();


            if (!globalAttributes)
            {
                attributes = new Dictionary<(string, bool), int>();
                List<(string, bool)> attributesLookupReverse = new List<(string, bool)>();

                for (int i = 0; i < nodes.Count; i++)
                {
                    foreach (KeyValuePair<string, object> kvp in nodes[i].Attributes)
                    {
                        bool isDouble = kvp.Value is double;
                        if (attributes.TryAdd((kvp.Key, isDouble), attributes.Count))
                        {
                            attributesLookupReverse.Add((kvp.Key, isDouble));
                        }
                    }
                }

                writer.WriteInt(attributes.Count);

                for (int i = 0; i < attributes.Count; i++)
                {
                    writer.WriteMyString(attributesLookupReverse[i].Item1);
                    writer.WriteInt(attributesLookupReverse[i].Item2 ? 2 : 1);
                }
            }
            else
            {
                writer.Write((byte)0);
            }

            //Topology
            byte currByte = 0;
            int currPos = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                currPos = writer.WriteShortInt(nodes[i].Children.Count, ref currByte, currPos);
            }

            if (currPos != 0)
            {
                writer.Write(currByte);
            }


            //Attributes
            for (int i = 0; i < nodes.Count; i++)
            {
                int attributeCount = 0;

                foreach (KeyValuePair<string, object> kvp in nodes[i].Attributes)
                {
                    bool isDouble = kvp.Value is double;

                    if (!isDouble && kvp.Key == "Name" && globalNames)
                    {
                        string value = (kvp.Value as string) ?? kvp.Value?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(value))
                        {
                            attributeCount++;
                        }
                    }
                    else
                    {
                        if (isDouble)
                        {
                            double value = (double)kvp.Value;
                            if (!double.IsNaN(value))
                            {
                                attributeCount++;
                            }
                        }
                        else
                        {
                            string value = (kvp.Value as string) ?? kvp.Value?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(value))
                            {
                                attributeCount++;
                            }
                        }
                    }
                }

                writer.WriteInt(attributeCount);
                foreach (KeyValuePair<string, object> kvp in nodes[i].Attributes)
                {
                    bool isDouble = kvp.Value is double;
                    int index = attributes[(kvp.Key, isDouble)];

                    if (!isDouble && kvp.Key == "Name" && globalNames)
                    {
                        string value = (kvp.Value as string) ?? kvp.Value?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(value))
                        {
                            writer.WriteInt(index);
                            if (names.TryGetValue(value, out int nameIndex))
                            {
                                writer.WriteInt(nameIndex + 1);
                            }
                            else
                            {
                                writer.Write((byte)255);
                                writer.WriteMyString(value);
                            }
                        }
                    }
                    else
                    {
                        if (isDouble)
                        {
                            double value = (double)kvp.Value;
                            if (!double.IsNaN(value))
                            {
                                writer.WriteInt(index);
                                writer.Write(value);
                            }
                        }
                        else
                        {
                            string value = (kvp.Value as string) ?? kvp.Value?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(value))
                            {
                                writer.WriteInt(index);
                                writer.WriteMyString(value);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads a tree in binary format from the stream.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which to read.</param>
        /// <param name="globalNames">Specifies whether global names are stored in the file's header.</param>
        /// <param name="names">The global names specified in the file's header.</param>
        /// <param name="attributes">The global attributes specified in the file's header.</param>
        /// <returns>The <see cref="TreeNode"/> that has been read.</returns>
        public static TreeNode ReadTree(this BinaryReader reader, bool globalNames = false, IReadOnlyList<string> names = null, IReadOnlyList<Attribute> attributes = null)
        {
            int numAttributes = reader.ReadInt();

            if (numAttributes > 0)
            {
                Attribute[] actualAttributes = new Attribute[numAttributes];

                for (int i = 0; i < actualAttributes.Length; i++)
                {
                    actualAttributes[i] = new Attribute(reader.ReadMyString(), reader.ReadInt() == 2);
                }

                attributes = actualAttributes;
            }

            //Topology
            TreeNode rootNode = new TreeNode(null);

            TreeNode currParent = rootNode;

            Dictionary<string, int> childCounts = new Dictionary<string, int>();

            byte currByte = reader.ReadByte();
            int currIndex = 0;

            while (currParent != null)
            {
                int currCount = reader.ReadShortInt(ref currByte, ref currIndex);

                childCounts.Add(currParent.Id, currCount);

                while (currParent != null && currParent.Children.Count == childCounts[currParent.Id])
                {
                    currParent = currParent.Parent;
                }

                if (currParent != null)
                {
                    TreeNode newNode = new TreeNode(currParent);
                    currParent.Children.Add(newNode);
                    currParent = newNode;
                }
            }

            if (currIndex == 0)
            {
                reader.BaseStream.Seek(-1, SeekOrigin.Current);
            }

            List<TreeNode> nodes = rootNode.GetChildrenRecursive();

            //Attributes
            for (int i = 0; i < nodes.Count; i++)
            {
                int attributeCount = reader.ReadInt();

                for (int j = 0; j < attributeCount; j++)
                {
                    int attributeIndex = reader.ReadInt();
                    string attributeName = attributes[attributeIndex].AttributeName;
                    bool isDouble = attributes[attributeIndex].IsNumeric;

                    if (isDouble)
                    {
                        if (string.Equals(attributeName, "Length", StringComparison.OrdinalIgnoreCase))
                        {
                            nodes[i].Length = reader.ReadDouble();
                        }
                        else if (string.Equals(attributeName, "Support", StringComparison.OrdinalIgnoreCase))
                        {
                            nodes[i].Support = reader.ReadDouble();
                        }
                        else
                        {
                            nodes[i].Attributes[attributeName] = reader.ReadDouble();
                        }
                    }
                    else if (!string.Equals(attributeName, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        nodes[i].Attributes[attributeName] = reader.ReadMyString();
                    }
                    else
                    {
                        if (!globalNames)
                        {
                            nodes[i].Name = reader.ReadMyString();
                        }
                        else
                        {
                            byte b = (byte)reader.BaseStream.ReadByte();

                            if (b == 0)
                            {
                                nodes[i].Name = "";
                            }
                            else if (b <= 254)
                            {
                                reader.BaseStream.Position--;
                                int index = reader.ReadInt();
                                nodes[i].Name = names[index - 1];
                            }
                            else //if(b == 255)
                            {
                                nodes[i].Name = reader.ReadMyString();
                            }
                        }
                    }
                }
            }

            return rootNode;
        }
    }
}
