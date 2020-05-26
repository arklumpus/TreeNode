/***********************************************************************
 *  write_binary_tree.cpp    2020-05-20
 *  by Giorgio Bianchini
 *  This file is part of the R package TreeNode, licensed under GPLv3
 *
 *  Methods to write trees provided by R to a file in binary tree
 *  format.
 ***********************************************************************/

// [[Rcpp::plugins(cpp17)]]

#include "common.h"

using namespace Rcpp;

//Less comparer used by std::map.
struct AttributeLess
{
  bool operator()(Attribute const& lhs, Attribute const& rhs) const
  {
    return lhs.AttributeName.compare(rhs.AttributeName) > 0;
  }
};

//Write a single byte to the file stream.
static void writeByte(std::fstream* stream, byte b)
{
  char buf[1];

  buf[0] = (char)b;

  stream->write(buf, 1);
}

//Write multiple bytes to the file stream.
static void writeBytes(std::fstream* stream, byte* bytes, size_t count)
{
  stream->write((char*)bytes, count);
}

//Write a double-precision floating point number to the file stream. The
//numbers should be stored in 64-bit IEEE754 format, hopefully this
//corresponds to the internal format of double on the current platform.
static void writeDouble(std::fstream* stream, double value)
{
  writeBytes(stream, reinterpret_cast<byte*>(&value), sizeof(value));
}

//Write a 32-bit wide integer to the file stream (little-endian).
static void writeInt32(std::fstream* stream, int32_t val)
{
  byte buf[4] = { (byte)(val & 0x000000ff),
                  (byte)((val & 0x0000ff00) >> 8),
                  (byte)((val & 0x00ff0000) >> 16),
                  (byte)((val & 0xff000000) >> 24) };
  writeBytes(stream, buf, 4);
}

//Write a 64-bit wide integer to the file stream (little-endian).
static void writeInt64(std::fstream* stream, int64_t val)
{
  byte buf[8] = { (byte)(val & 0x00000000000000ffLL),
                  (byte)((val & 0x000000000000ff00LL) >> 8),
                  (byte)((val & 0x0000000000ff0000LL) >> 16),
                  (byte)((val & 0x00000000ff000000LL) >> 24),
                  (byte)((val & 0x000000ff00000000LL) >> 32),
                  (byte)((val & 0x0000ff0000000000LL) >> 40),
                  (byte)((val & 0x00ff000000000000LL) >> 48),
                  (byte)((val & 0xff00000000000000LL) >> 56) };
  writeBytes(stream, buf, 8);
}

//Write a variable-width integer to the file stream. If the integer is
//smaller than 254, it is only 1-byte wide; otherwise it is 40-bit
//(5-byte) wide.
static void writeInt(std::fstream* stream, int32_t val)
{
  if (val < 254)
  {
    writeByte(stream, (byte)val);
  }
  else
  {
    writeByte(stream, 254);
    writeInt32(stream, val);
  }
}

//Write a string to the stream. The string should be stored as an
//integer n representing its length followed by n integers that
//constitute the UTF-16 representation of the string. Since codecvt_utf8
//does not apparently work, we are stuck with a straight char->int
//conversion, which will probably only work for ASCII characters.
static void writeMyString(std::fstream* stream, std::string val)
{
  writeInt(stream, val.length());

  for (size_t i = 0; i < val.length(); i++)
  {
    writeInt(stream, (int32_t)val[i]);
  }
}

//Write a variable-width integer from the stream. If the integer is equal
//to 0, 2 or 3, it is 2-bit wide; if it is 1, 4 or 5, it is 4-bit wide;
//if it is greater than 5, the current byte is padded and the integer is
//represented as an integer of the format written by readInt in the
//following byte(s). The initial value of *currByte and *currIndex should
//be 0. Successive writes should use the same variables, which will have been
//updated by this method. After the last write, if *currIndex is not 0,
//it means that the current byte has not been written to the stream yet.
static int writeShortInt(std::fstream* stream, int32_t value, byte* currByte, int32_t currIndex)
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
      writeByte(stream, *currByte);
      *currByte = 0;
      return 0;
    }
  }
  else if (value == 2)
  {
    //01
    if (currIndex == 0)
    {
      *currByte = (byte)(*currByte | 0b00000001);
      return 2;
    }
    else if (currIndex == 2)
    {
      *currByte = (byte)(*currByte | 0b00000100);
      return 4;
    }
    else if (currIndex == 4)
    {
      *currByte = (byte)(*currByte | 0b00010000);
      return 6;
    }
    else if (currIndex == 6)
    {
      writeByte(stream, (byte)(*currByte | 0b01000000));
      *currByte = 0;
      return 0;
    }
  }
  else if (value == 3)
  {
    //10
    if (currIndex == 0)
    {
      *currByte = (byte)(*currByte | 0b00000010);
      return 2;
    }
    else if (currIndex == 2)
    {
      *currByte = (byte)(*currByte | 0b00001000);
      return 4;
    }
    else if (currIndex == 4)
    {
      *currByte = (byte)(*currByte | 0b00100000);
      return 6;
    }
    else if (currIndex == 6)
    {
      writeByte(stream, (byte)(*currByte | 0b10000000));
      *currByte = 0;
      return 0;
    }
  }
  else if (value == 1)
  {
    //0011
    if (currIndex == 0)
    {
      *currByte = (byte)(*currByte | 0b00000011);
      return 4;
    }
    else if (currIndex == 2)
    {
      *currByte = (byte)(*currByte | 0b00001100);
      return 6;
    }
    else if (currIndex == 4)
    {
      writeByte(stream, (byte)(*currByte | 0b00110000));
      *currByte = 0;
      return 0;
    }
    else if (currIndex == 6)
    {
      writeByte(stream, (byte)(*currByte | 0b11000000));
      *currByte = 0;
      return 2;
    }
  }
  else if (value == 4)
  {
    //0111
    if (currIndex == 0)
    {
      *currByte = (byte)(*currByte | 0b00000111);
      return 4;
    }
    else if (currIndex == 2)
    {
      *currByte = (byte)(*currByte | 0b00011100);
      return 6;
    }
    else if (currIndex == 4)
    {
      writeByte(stream, (byte)(*currByte | 0b01110000));
      *currByte = 0;
      return 0;
    }
    else if (currIndex == 6)
    {
      writeByte(stream, (byte)(*currByte | 0b11000000));
      *currByte = 0b00000001;
      return 2;
    }
  }
  else if (value == 5)
  {
    //1011
    if (currIndex == 0)
    {
      *currByte = (byte)(*currByte | 0b00001011);
      return 4;
    }
    else if (currIndex == 2)
    {
      *currByte = (byte)(*currByte | 0b00101100);
      return 6;
    }
    else if (currIndex == 4)
    {
      writeByte(stream, (byte)(*currByte | 0b10110000));
      *currByte = 0;
      return 0;
    }
    else if (currIndex == 6)
    {
      writeByte(stream, (byte)(*currByte | 0b11000000));
      *currByte = 0b00000010;
      return 2;
    }
  }
  else
  {
    //1111
    if (currIndex == 0)
    {
      writeByte(stream, (byte)(*currByte | 0b00001111));
      writeInt(stream, value);
      *currByte = 0;
      return 0;
    }
    else if (currIndex == 2)
    {
      writeByte(stream, (byte)(*currByte | 0b00111100));
      writeInt(stream, value);
      *currByte = 0;
      return 0;
    }
    else if (currIndex == 4)
    {
      writeByte(stream, (byte)(*currByte | 0b11110000));
      writeInt(stream, value);
      *currByte = 0;
      return 0;
    }
    else if (currIndex == 6)
    {
      writeByte(stream, (byte)(*currByte | 0b11000000));
      writeByte(stream, (byte)0b00000011);
      writeInt(stream, value);
      *currByte = 0;
      return 0;
    }
  }

  Rcpp::stop("Unexpected code path!");
}

//Writes a tree in binary format to the file stream.
static void writeBinaryTree(phylo* tree, std::fstream* file, bool globalNames = false, bool globalAttributes = false, std::map<std::string, size_t>* names = NULL, std::map<Attribute, size_t, AttributeLess>* attributes = NULL, std::vector<Attribute>* attributesLookupReverse = NULL)
{
  std::map<Attribute, size_t, AttributeLess> newAttributes;
  std::vector<Attribute> newAttributesReverse;

  if (!globalAttributes)
  {
    attributes = &newAttributes;
    attributesLookupReverse = &newAttributesReverse;

    for (size_t j = 0; j < tree->attributes.size(); j++)
    {
      if (attributes->insert(std::pair<Attribute, size_t>(tree->attributes[j], attributes->size())).second)
      {
        attributesLookupReverse->push_back(tree->attributes[j]);
      }
    }

    writeInt(file, (int32_t)attributes->size());

    for (size_t i = 0; i < attributes->size(); i++)
    {
      writeMyString(file, (*attributesLookupReverse)[i].AttributeName);
      writeInt(file, (*attributesLookupReverse)[i].IsNumeric ? 2 : 1);
    }
  }
  else
  {
    writeByte(file, 0);
  }

  std::vector<int32_t> parents(tree->Nnode + tree->tipLabel.size() + 1);
  std::vector<std::vector<int32_t>> children(tree->Nnode + tree->tipLabel.size() + 1);

  std::vector<int32_t> sortedParents(tree->Nnode + tree->tipLabel.size());
  std::vector<std::vector<int32_t>> sortedChildren(tree->Nnode + tree->tipLabel.size());

  std::vector<int32_t> sortedNodes(tree->Nnode + tree->tipLabel.size());

  for (size_t i = 0; i < tree->edge.size(); i++)
  {
    children[tree->edge[i][0]].push_back(tree->edge[i][1]);
    parents[tree->edge[i][1]] = tree->edge[i][0];
  }

  int32_t rootNode = -1;

  for (size_t i = 1; i < parents.size(); i++)
  {
    if (parents[i] == 0)
    {
      rootNode = i;
      break;
    }
  }

  int32_t currIndex = 0;

  addChildren(&children, &sortedParents, &sortedChildren, &sortedNodes, &currIndex, rootNode, -1);

  byte currByte = 0;
  int32_t currPos = 0;

  for (size_t i = 0; i < sortedChildren.size(); i++)
  {
    currPos = writeShortInt(file, (int32_t)sortedChildren[i].size(), &currByte, currPos);
  }

  if (currPos != 0)
  {
    writeByte(file, currByte);
  }

  int32_t tipCount = (int32_t)tree->tipLabel.size();

  for (size_t i = 0; i < sortedParents.size(); i++)
  {
    int32_t currAttributeCount = 0;
    if (sortedNodes[i] <= tipCount)
    {
      for (size_t j = 0; j < attributesLookupReverse->size(); j++)
      {
        if ((*attributesLookupReverse)[j].IsNumeric)
        {
          double value = std::get<std::vector<double>>(tree->tipAttributes[j])[sortedNodes[i] - 1];
          if (!std::isnan(value))
          {
            currAttributeCount++;
          }
        }
        else
        {
          std::string value = std::get<std::vector<std::string>>(tree->tipAttributes[j])[sortedNodes[i] - 1];
          if (!value.empty())
          {
            currAttributeCount++;
          }
        }
      }

      writeInt(file, currAttributeCount);

      for (size_t j = 0; j < attributesLookupReverse->size(); j++)
      {
        int32_t index = (*attributes)[(*attributesLookupReverse)[j]];

        if (!(*attributesLookupReverse)[j].IsNumeric && equalCI((*attributesLookupReverse)[j].AttributeName, NAMEATTRIBUTE) && globalNames)
        {
          std::string value = std::get<std::vector<std::string>>(tree->tipAttributes[j])[sortedNodes[i] - 1];

          if (!value.empty())
          {
            writeInt(file, index);

            /*if (value.length() == 0)
            {
              writeByte(file, 0);
            }
            else
            {*/
              std::map<std::string, size_t>::iterator iter = names->find(value);

              if (iter != names->end())
              {
                writeInt(file, iter->second + 1);
              }
              else
              {
                writeByte(file, 255);
                writeMyString(file, value);
              }
            //}
          }
        }
        else
        {
          if ((*attributesLookupReverse)[j].IsNumeric)
          {
            double value = std::get<std::vector<double>>(tree->tipAttributes[j])[sortedNodes[i] - 1];
            if (!std::isnan(value))
            {
              writeInt(file, index);
              writeDouble(file, value);
            }
          }
          else
          {
            std::string value = std::get<std::vector<std::string>>(tree->tipAttributes[j])[sortedNodes[i] - 1];
            if (!value.empty())
            {
              writeInt(file, index);
              writeMyString(file, value);
            }
          }
        }
      }

    }
    else
    {
      for (size_t j = 0; j < attributesLookupReverse->size(); j++)
      {
        if ((*attributesLookupReverse)[j].IsNumeric)
        {
          double value = std::get<std::vector<double>>(tree->nodeAttributes[j])[sortedNodes[i] - tipCount - 1];
          if (!std::isnan(value))
          {
            currAttributeCount++;
          }
        }
        else
        {
          std::string value = std::get<std::vector<std::string>>(tree->nodeAttributes[j])[sortedNodes[i] - tipCount - 1];
          if (!value.empty())
          {
            currAttributeCount++;
          }
        }
      }

      writeInt(file, currAttributeCount);

      for (size_t j = 0; j < attributesLookupReverse->size(); j++)
      {
        int32_t index = (*attributes)[(*attributesLookupReverse)[j]];

        if (!(*attributesLookupReverse)[j].IsNumeric && equalCI((*attributesLookupReverse)[j].AttributeName, NAMEATTRIBUTE) && globalNames)
        {
          std::string value = std::get<std::vector<std::string>>(tree->nodeAttributes[j])[sortedNodes[i] - tipCount - 1];

          if (!value.empty())
          {
            writeInt(file, index);

            std::map<std::string, size_t>::iterator iter = names->find(value);

            if (iter != names->end())
            {
              writeInt(file, iter->second);
            }
            else
            {
              writeByte(file, 255);
              writeMyString(file, value);
            }
          }
        }
        else
        {
          if ((*attributesLookupReverse)[j].IsNumeric)
          {
            double value = std::get<std::vector<double>>(tree->nodeAttributes[j])[sortedNodes[i] - tipCount - 1];
            if (!std::isnan(value))
            {
              writeInt(file, index);
              writeDouble(file, value);
            }
          }
          else
          {
            std::string value = std::get<std::vector<std::string>>(tree->nodeAttributes[j])[sortedNodes[i] - tipCount - 1];
            if (!value.empty())
            {
              writeInt(file, index);
              writeMyString(file, value);
            }
          }
        }
      }
    }
  }
}

//Writes the tree(s) contained in a multiPhylo object to the file stream.
static void writeBinaryTrees(multiPhylo* trees, std::fstream* file, byte* additionalDataToCopy, size_t additionalDataToCopySize)
{
  std::map<std::string, size_t> allNamesLookup;
  std::vector<std::string> allNamesLookupReverse;

  std::map<Attribute, size_t, AttributeLess> allAttributesLookup;
  std::vector<Attribute> allAttributesLookupReverse;

  bool includeNamesPerTree = false;
  bool includeAttributesPerTree = false;

  for (size_t i = 0; i < trees->trees.size(); i++)
  {
    size_t prevNameCount = allNamesLookup.size();
    size_t prevAttributeCount = allAttributesLookup.size();

    size_t count = 0;
    size_t maxAttributeCount = 0;

    int nameIndex = -1;

    for (size_t j = 0; j < trees->trees[i].attributes.size(); j++)
    {
      if (equalCI(trees->trees[i].attributes[j].AttributeName, NAMEATTRIBUTE) && !trees->trees[i].attributes[j].IsNumeric)
      {
        nameIndex = j;
      }

      if (allAttributesLookup.insert(std::pair<Attribute, size_t>(trees->trees[i].attributes[j], allAttributesLookup.size())).second)
      {
        allAttributesLookupReverse.push_back(trees->trees[i].attributes[j]);
      }
    }

    std::vector<std::string> nodeNames = std::get<std::vector<std::string>>(trees->trees[i].nodeAttributes[nameIndex]);
    std::vector<std::string> tipNames = std::get<std::vector<std::string>>(trees->trees[i].tipAttributes[nameIndex]);

    for (size_t j = 0; j < nodeNames.size(); j++)
    {
      if (nodeNames[j].length() > 0)
      {
        count++;
        if (allNamesLookup.insert(std::pair<std::string, size_t>(nodeNames[j], allNamesLookup.size())).second)
        {
          allNamesLookupReverse.push_back(nodeNames[j]);
        }
      }
    }




    for (size_t j = 0; j < tipNames.size(); j++)
    {
      if (tipNames[j].length() > 0)
      {
        count++;
        if (allNamesLookup.insert(std::pair<std::string, size_t>(tipNames[j], allNamesLookup.size())).second)
        {
          allNamesLookupReverse.push_back(tipNames[j]);
        }
      }
    }

    maxAttributeCount = std::max(maxAttributeCount, std::max(trees->trees[i].nodeAttributes.size(), trees->trees[i].tipAttributes.size()));

    if (prevNameCount != 0 && (allNamesLookup.size() - prevNameCount) * 2 > count)
    {
      includeNamesPerTree = true;
    }

    if (prevAttributeCount != 0 && (allAttributesLookup.size() - prevAttributeCount) * 2 > maxAttributeCount)
    {
      includeAttributesPerTree = true;
    }

    if (includeNamesPerTree && includeAttributesPerTree)
    {
      break;
    }
  }

  byte header[4] = { 0x23, 0x54, 0x52, 0x45 };
  writeBytes(file, header, 4);

  if (!includeNamesPerTree && !includeAttributesPerTree)
  {
    writeByte(file, 0b00000011);
  }
  else if (!includeNamesPerTree && includeAttributesPerTree)
  {
    writeByte(file, 0b00000001);
  }
  else if (includeNamesPerTree && !includeAttributesPerTree)
  {
    writeByte(file, 0b00000010);
  }
  else
  {
    writeByte(file, 0b00000000);
  }

  if (!includeNamesPerTree)
  {
    writeInt(file, (int32_t)allNamesLookup.size());

    for (size_t i = 0; i < allNamesLookup.size(); i++)
    {
      writeMyString(file, allNamesLookupReverse[i]);
    }
  }

  if (!includeAttributesPerTree)
  {
    writeInt(file, (int32_t)allAttributesLookup.size());

    for (size_t i = 0; i < allAttributesLookup.size(); i++)
    {
      writeMyString(file, allAttributesLookupReverse[i].AttributeName);
      writeInt(file, allAttributesLookupReverse[i].IsNumeric ? 2 : 1);
    }
  }

  std::vector<int64_t> addresses(trees->trees.size());

  for (size_t i = 0; i < trees->trees.size(); i++)
  {
    addresses[i] = file->tellp();
    writeBinaryTree(&(trees->trees[i]), file, !includeNamesPerTree, !includeAttributesPerTree, &allNamesLookup, &allAttributesLookup, &allAttributesLookupReverse);
  }

  if (additionalDataToCopySize > 0)
  {
    writeBytes(file, additionalDataToCopy, additionalDataToCopySize);
  }

  int64_t labelAddress = file->tellp();

  writeInt(file, (int32_t)addresses.size());

  for (size_t i = 0; i < addresses.size(); i++)
  {
    writeInt64(file, addresses[i]);
  }

  writeInt64(file, labelAddress);

  byte trailer[4] = { 0x45, 0x4e, 0x44, 0xff };
  writeBytes(file, trailer, 4);
}

//Initialises a file in binary tree format by writing an empty header.
static void beginWritingBinaryTrees(std::fstream* file)
{
  byte header[4] = { 0x23, 0x54, 0x52, 0x45 };
  writeBytes(file, header, 4);
  writeByte(file, 0b00000000);
}

//Finalises a file in binary tree format by writing a trailer containing the
//tree addresses.
static void finishWritingBinaryTrees(std::fstream* file, std::vector<int64_t>* addresses, byte* additionalDataToCopy, size_t additionalDataToCopySize)
{
  if (additionalDataToCopySize > 0)
  {
    writeBytes(file, additionalDataToCopy, additionalDataToCopySize);
  }

  int64_t labelAddress = (*addresses)[addresses->size() - 1] + additionalDataToCopySize;

  writeInt(file, (int32_t)addresses->size() - 1);

  for (size_t i = 0; i < addresses->size() - 1; i++)
  {
    writeInt64(file, (*addresses)[i]);
  }

  writeInt64(file, labelAddress);

  byte trailer[4] = { 0x45, 0x4e, 0x44, 0xff };
  writeBytes(file, trailer, 4);
}

//Writes tree(s) provided by R into a file in binary format.
//[[Rcpp::export]]
void Rcpp_write_binary_trees(Rcpp::List trees, std::string fileName, std::vector<Rbyte> additionalData)
{
  multiPhylo convertedTrees = convertTrees(&trees);

  std::fstream file(fileName, std::fstream::binary | std::fstream::out);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for writing.");
  }

  writeBinaryTrees(&convertedTrees, &file, additionalData.data(), additionalData.size());

  file.close();
}

//Initialises a file in binary tree format by writing an empty header.
//Returns a vector of integers intended to store the addresses of the trees
//that will be written to the file.
//[[Rcpp::export]]
std::vector<int64_t> Rcpp_begin_writing_binary_trees(std::string fileName)
{
  std::vector<int64_t> addresses;

  std::fstream file(fileName, std::fstream::binary | std::fstream::out);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for writing.");
  }

  beginWritingBinaryTrees(&file);

  int64_t address = file.tellp();
  addresses.push_back(address);

  file.close();
  return addresses;
}

//Writes a single tree in binary format to a file. Requires a vector of
//integers containing the addresses of trees previously written to the file.
//This vector is updated and returned to R. The last entry in this vector
//represents what will be the address of the trailer.
//[[Rcpp::export]]
std::vector<int64_t> Rcpp_write_binary_tree(Rcpp::List tree, std::string fileName, std::vector<int64_t> addresses)
{
  phylo convertedTree = convertTree(&tree);
  std::fstream file(fileName, std::fstream::binary | std::fstream::app);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for writing.");
  }

  writeBinaryTree(&convertedTree, &file);
  int64_t address = file.tellp();
  addresses.push_back(address);
  file.close();
  return addresses;
}

//Finalises a file in binary tree format by writing a trailer containing the
//tree addresses (provided by R).
//[[Rcpp::export]]
void Rcpp_finish_writing_binary_trees(std::string fileName, std::vector<int64_t> addresses, std::vector<Rbyte> additionalData)
{
  std::fstream file(fileName, std::fstream::binary | std::fstream::app);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for writing.");
  }

  finishWritingBinaryTrees(&file, &addresses, additionalData.data(), additionalData.size());
  file.close();
}
