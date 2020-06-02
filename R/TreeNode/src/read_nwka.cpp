/***********************************************************************
 *  read_nwka.cpp    2020-05-20
 *  by Giorgio Bianchini
 *  This file is part of the R package TreeNode, licensed under GPLv3
 *
 *  Methods to read trees from a file/string in Newick-with-Attributes
 *  (NWKA) format and pass them to R.
 ***********************************************************************/

// [[Rcpp::plugins(cpp17)]]

#include "common.h"

using namespace Rcpp;

//Strings used in parsing NEXUS files.
static std::string BEGINstring = "begin";
static std::string ENDstring = "end";
static std::string TREESstring = "trees";
static std::string TREEstring = "tree";
static std::string TRANSLATEstring = "translate";

//Trim a string from start (in place).
//From https://stackoverflow.com/questions/216823/whats-the-best-way-to-trim-stdstring
static inline void ltrim(std::string& s)
{
  s.erase(s.begin(), std::find_if(s.begin(), s.end(), [](int ch) {
    return !std::isspace(ch);
  }));
}

//Trim a string from end (in place).
//From https://stackoverflow.com/questions/216823/whats-the-best-way-to-trim-stdstring
static inline void rtrim(std::string& s)
{
  s.erase(std::find_if(s.rbegin(), s.rend(), [](int ch)
    {
      return !std::isspace(ch);
    }).base(), s.end());
}

//Trim a string from both ends (in place).
//From https://stackoverflow.com/questions/216823/whats-the-best-way-to-trim-stdstring
static inline void trim(std::string& s)
{
  ltrim(s);
  rtrim(s);
}

//Determine whether a string can be parsed as an integer number, optionally returning the value.
static bool tryParse(std::string val, int* output = NULL)
{
  size_t p;

  bool error = true;
  int parsed = 0;

  if (val.length() > 0)
  {
    try
    {
      parsed = std::stoi(val, &p);
      error = false;
    }
    catch (...)
    {
      error = true;
    }
  }

  if (!error && p == val.length())
  {
    if (output != NULL)
    {
      *output = parsed;
    }
    return true;
  }
  else
  {
    if (output != NULL)
    {
      *output = 0;
    }
    return false;
  }
}

//Read the next non-whitespace token from a string, taking into account quotes and escape characters.
//The current position is determined by *srPosition, which should be initialised to 0. The bool
//arguments should all be initialised to false.
char nextToken(std::string* source, int* srPosition, bool* escaping, bool* escaped, bool* openQuotes, bool* openApostrophe, bool* eof)
{
  if ((size_t)(*srPosition) >= source->length())
  {
    (*eof) = true;
    (*escaped) = false;
    return -1;
  }

  char c = source->at(*srPosition);
  (*srPosition)++;

  (*eof) = false;

  if (!(*escaping))
  {
    *escaped = false;
    if (!(*openQuotes) && !(*openApostrophe))
    {
      while (std::isspace(c))
      {
        if ((size_t)(*srPosition) >= source->length())
        {
          (*eof) = true;
          (*escaped) = false;
          return -1;
        }

        c = source->at(*srPosition);
        (*srPosition)++;
      }

      switch (c)
      {
      case '\\':
        *escaping = true;
        break;
      case '"':
        *openQuotes = true;
        break;
      case '\'':
        *openApostrophe = true;
        break;
      }
    }
    else if (*openQuotes)
    {
      switch (c)
      {
      case '"':
        *openQuotes = false;
        break;
      case '\\':
        *escaping = true;
        break;
      }
    }
    else if (*openApostrophe)
    {
      switch (c)
      {
      case '\'':
        *openApostrophe = false;
        break;
      case '\\':
        *escaping = true;
        break;
      }
    }
  }
  else
  {
    *escaping = false;
    *escaped = true;
  }

  return c;
}

//Read the next non-whitespace token from a file stream, taking into account quotes and escape characters.
//The bool arguments should all be initialised to false.
char nextToken(std::fstream* source, bool* escaping, bool* escaped, bool* openQuotes, bool* openApostrophe, bool* eof)
{
  if (source->eof())
  {
    (*eof) = true;
    (*escaped) = false;
    return -1;
  }

  char c;
  source->get(c);

  (*eof) = false;

  if (!(*escaping))
  {
    *escaped = false;
    if (!(*openQuotes) && !(*openApostrophe))
    {
      while (std::isspace(c))
      {
        if (source->eof())
        {
          (*eof) = true;
          (*escaped) = false;
          return -1;
        }

        source->get(c);
      }

      switch (c)
      {
      case '\\':
        *escaping = true;
        break;
      case '"':
        *openQuotes = true;
        break;
      case '\'':
        *openApostrophe = true;
        break;
      }
    }
    else if (*openQuotes)
    {
      switch (c)
      {
      case '"':
        *openQuotes = false;
        break;
      case '\\':
        *escaping = true;
        break;
      }
    }
    else if (*openApostrophe)
    {
      switch (c)
      {
      case '\'':
        *openApostrophe = false;
        break;
      case '\\':
        *escaping = true;
        break;
      }
    }
  }
  else
  {
    *escaping = false;
    *escaped = true;
  }

  return c;
}

//Read the next word from a file stream, taking into account whitespaces, square brackets, commas and
//semicolons. *eof should be initialised to false.
std::string nextWord(std::fstream* source, bool* eof)
{
  std::stringstream sb;

  char c;
  source->get(c);

  while (!source->eof() && std::isspace((char)c))
  {
    source->get(c);
  }

  if (!source->eof())
  {
    sb << c;
  }

  if ((char)c == '[' || (char)c == ']' || (char)c == ',' || (char)c == ';')
  {
    *eof = false;
    return sb.str();
  }

  c = source->peek();

  while (!source->eof() && !std::isspace((char)c))
  {
    if ((char)c == '[' || (char)c == ']' || (char)c == ',' || (char)c == ';')
    {
      break;
    }

    source->get(c);

    sb << c;

    c = source->peek();
  }

  if (source->eof())
  {
    *eof = true;
  }
  else
  {
    *eof = false;
  }

  return sb.str();
}

//Determine whether a map contains a certain key.
bool containsKey(std::map<std::string, std::variant<std::string, double>, ci_less>* map, std::string key)
{
  return map->count(key) > 0;
}

//Parse the attributes of a NWKA node into the *attributes map.
static void parseAttributes(std::string* sr, int* srPosition, bool* eof, std::map<std::string, std::variant<std::string, double>, ci_less>* attributes, int childCount)
{
  std::stringstream attributeValue;
  std::stringstream attributeName;

  int openSquareCount = 0;
  int openCurlyCount = 0;

  bool escaping = false;
  bool escaped = false;
  bool openQuotes = false;
  bool openApostrophe = false;

  bool nameFinished = false;
  char lastSeparator = ',';

  bool start = true;
  bool closedOuterBrackets = false;

  bool withinBrackets = false;

  char expectedClosingBrackets = '\0';

  while (!(*eof))
  {
    char c2;

    if (!closedOuterBrackets)
    {
      c2 = nextToken(sr, srPosition, &escaping, &escaped, &openQuotes, &openApostrophe, eof);
    }
    else
    {
      c2 = ',';
    }

    if (start)
    {
      if (c2 == '[')
      {
        expectedClosingBrackets = ']';
        c2 = ',';
        start = false;
      }
    }

    if (c2 == '=' && !escaped && !openQuotes && !openApostrophe)
    {
      nameFinished = true;

      if (closedOuterBrackets)
      {
        closedOuterBrackets = false;
        expectedClosingBrackets = '\0';
        start = true;
        withinBrackets = false;
      }

      if (expectedClosingBrackets != '\0')
      {
        withinBrackets = true;
      }
    }
    else if ((*eof || ((c2 == ':' || c2 == '/' || c2 == ',') && openSquareCount == 0 && openCurlyCount == 0)) && !escaped && !openQuotes && !openApostrophe)
    {
      if (attributeValue.tellp() > 0)
      {
        std::string name = attributeName.str();

        if (name.rfind("&", 0) == 0)
        {
          name = name.substr(1);
        }

        if (name.rfind("!", 0) == 0)
        {
          name = name.substr(1);
        }

        if (equalCI(name, NAMEATTRIBUTE))
        {
          std::string value = attributeValue.str();

          if ((value.rfind("\"", 0) == 0 && value.find("\"", value.length() - 1) == value.length() - 1) || (value.rfind("'", 0) == 0 && value.find("'", value.length() - 1) == value.length() - 1))
          {
            value = value.substr(1, value.length() - 2);
          }

          (*attributes)["Name"] = value;
        }
        else if (equalCI(name, SUPPORTATTRIBUTE))
        {
          (*attributes)["Support"] = std::stod(attributeValue.str());
        }
        else if (equalCI(name, LENGTHATTRIBUTE))
        {
          (*attributes)["Length"] = std::stod(attributeValue.str());
        }
        else
        {
          std::string value = attributeValue.str();
          double result;
          if (tryParse(value, &result))
          {
            (*attributes)[name] = result;
          }
          else
          {
            if ((value.rfind("\"", 0) == 0 && value.find("\"", value.length() - 1) == value.length() - 1) || (value.rfind("'", 0) == 0 && value.find("'", value.length() - 1) == value.length() - 1))
            {
              value = value.substr(1, value.length() - 2);
            }
            (*attributes)[name] = value;
          }
        }
      }
      else if (attributeName.tellp() > 0)
      {
        double result;

        switch (lastSeparator)
        {
        case ':':
          if (tryParse(attributeName.str(), &result))
          {
            (*attributes)["Length"] = result;
          }
          else
          {
            std::string name = "Unknown";

            if (containsKey(&((*attributes)), name))
            {
              int ind = 2;
              std::string newName = name + std::to_string(ind);

              while (containsKey(&((*attributes)), newName))
              {
                ind++;
                newName = name + std::to_string(ind);
              }

              name = newName;
            }

            (*attributes)[name] = attributeName.str();
          }
          break;
        case '/':
          if (tryParse(attributeName.str(), &result))
          {
            (*attributes)["Support"] = result;
          }
          else
          {
            std::string name = "Unknown";

            if (containsKey(&((*attributes)), name))
            {
              int ind = 2;
              std::string newName = name + std::to_string(ind);

              while (containsKey(&((*attributes)), newName))
              {
                ind++;
                newName = name + std::to_string(ind);
              }

              name = newName;
            }

            (*attributes)[name] = attributeName.str();
          }
          break;
        case ',':
          bool isName = false;

          std::string value = attributeName.str();

          if ((value.rfind("\"", 0) == 0 && value.find("\"", value.length() - 1) == value.length() - 1) || (value.rfind("'", 0) == 0 && value.find("'", value.length() - 1) == value.length() - 1))
          {
            value = value.substr(1, value.length() - 2);
            isName = true;
          }

          if (childCount == 0 && (!containsKey(&((*attributes)), "Name") || std::get<std::string>((*attributes)["Name"]).empty()) && (!containsKey(&((*attributes)), "Length") || std::isnan(std::get<double>((*attributes)["Length"]))) && (!containsKey(&((*attributes)), "Support") || std::isnan(std::get<double>((*attributes)["Support"]))))
          {
            isName = true;
          }

          if ((!containsKey(&((*attributes)), "Name") || std::get<std::string>((*attributes)["Name"]).empty()) && !withinBrackets && !closedOuterBrackets && (isName || !tryParse(value.substr(0, 1), (int*)NULL)))
          {
            (*attributes)["Name"] = value;
          }
          else
          {
            if ((!containsKey(&((*attributes)), "Support") || std::isnan(std::get<double>((*attributes)["Support"]))) && tryParse(value, &result))
            {
              (*attributes)["Support"] = result;
            }
            else
            {

              std::string name = "Unknown";

              if (containsKey(&((*attributes)), name))
              {
                int ind = 2;
                std::string newName = name + std::to_string(ind);

                while (containsKey(&((*attributes)), newName))
                {
                  ind++;
                  newName = name + std::to_string(ind);
                }

                name = newName;
              }

              (*attributes)[name] = value;
            }
          }
          break;
        }
      }

      lastSeparator = c2;
      nameFinished = false;

      attributeName.str(std::string());
      attributeName.clear();

      attributeValue.str(std::string());
      attributeValue.clear();

      if (closedOuterBrackets)
      {
        closedOuterBrackets = false;
        expectedClosingBrackets = '\0';
        start = true;
        withinBrackets = false;
      }

      if (expectedClosingBrackets != '\0')
      {
        withinBrackets = true;
      }
    }
    else
    {
      if (closedOuterBrackets)
      {
        closedOuterBrackets = false;
        expectedClosingBrackets = '\0';
        start = true;
        withinBrackets = false;
      }

      if (expectedClosingBrackets != '\0')
      {
        withinBrackets = true;
      }

      if (c2 == '[' && !escaped && !openQuotes && !openApostrophe)
      {
        openSquareCount++;
      }
      else if (c2 == ']' && !escaped && !openQuotes && !openApostrophe)
      {
        if (openSquareCount > 0)
        {
          openSquareCount--;
        }
        else if (expectedClosingBrackets == c2)
        {
          closedOuterBrackets = true;
        }
      }
      else if (c2 == '{' && !escaped && !openQuotes && !openApostrophe)
      {
        openCurlyCount++;
      }
      else if (c2 == '}' && !escaped && !openQuotes && !openApostrophe)
      {
        if (openCurlyCount > 0)
        {
          openCurlyCount--;
        }
      }


      if (!closedOuterBrackets)
      {
        if (!nameFinished)
        {
          attributeName << c2;
        }
        else
        {
          attributeValue << c2;
        }

      }
    }
  }

  if ((!containsKey(&((*attributes)), "Support") || std::isnan(std::get<double>((*attributes)["Support"]))) && containsKey(&((*attributes)), "prob"))
  {
    std::variant<std::string, double> support = (*attributes)["prob"];
    double actualSupport;

    if (support.index() == 1)
    {
      actualSupport = std::get<double>(support);
    }
    else
    {
      tryParse(std::get<std::string>(support), &actualSupport);
    }

    (*attributes)["Support"] = actualSupport;
  }
}

//Parse a NWKA-format string into a series of vectors containing parent-child relationships between the nodes
//and node attributes. This will trim the source string in place and remove a trailing semicolon.
static int parseNWKA(std::string* source, int* currIndex, std::vector<int>* allParents, std::vector<std::vector<int>>* allChildren, std::vector<std::map<std::string, std::variant<std::string, double>, ci_less>>* allAttributes, int* tipCount, int parent = -1, bool debug = false)
{
  trim(*source);

  if (source->find(";", source->length() - 1) == source->length() - 1)
  {
    source->erase(source->end() - 1, source->end());
  }

  if (debug)
  {
    Rcpp::Rcout << "Parsing: " << *source;
  }

  if (source->rfind("(", 0) == 0)
  {
    std::stringstream childrenBuilder;

    int srPosition = 1;

    bool closed = false;
    int openCount = 0;
    int openSquareCount = 0;
    int openCurlyCount = 0;

    bool escaping = false;
    bool escaped;
    bool openQuotes = false;
    bool openApostrophe = false;
    bool eof = false;

    std::vector<int> commas;
    int position = 0;

    while (!closed && !eof)
    {
      char c = nextToken(source, &srPosition, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);

      if (!escaped)
      {
        if (!openQuotes && !openApostrophe)
        {
          switch (c)
          {
          case '(':
            openCount++;
            break;
          case ')':
            if (openCount > 0)
            {
              openCount--;
            }
            else
            {
              closed = true;
            }
            break;
          case '[':
            openSquareCount++;
            break;
          case ']':
            openSquareCount--;
            break;
          case '{':
            openCurlyCount++;
            break;
          case '}':
            openCurlyCount--;
            break;
          case ',':
            if (openCount == 0 && openSquareCount == 0 && openCurlyCount == 0)
            {
              commas.push_back(position);
            }
            break;
          }
        }
      }

      if (!closed && !eof)
      {
        childrenBuilder << c;
        position++;
      }
    }

    std::vector<std::string> children;

    if (commas.size() > 0)
    {
      std::string childrenString = childrenBuilder.str();

      for (size_t i = 0; i < commas.size(); i++)
      {
        children.push_back(childrenString.substr(i > 0 ? commas[i - 1] + 1 : 0, commas[i] - (i > 0 ? commas[i - 1] + 1 : 0)));
      }
      children.push_back(childrenString.substr(commas.back() + 1, childrenString.length() - commas.back() - 1));
    }
    else
    {
      children.push_back(childrenBuilder.str());
    }

    if (debug)
    {
      Rcpp::Rcout << "\n";
      Rcpp::Rcout << "Children:\n";
      for (size_t i = 0; i < children.size(); i++)
      {
        Rcpp::Rcout << " - " + children[i] << "\n";
      }
      Rcpp::Rcout << "\n";
    }

    int myIndex = *currIndex;
    (*currIndex)++;

    allParents->push_back(parent);
    allChildren->push_back(std::vector<int>());

    allAttributes->push_back(std::map<std::string, std::variant<std::string, double>, ci_less>());

    parseAttributes(source, &srPosition, &eof, &((*allAttributes)[myIndex]), children.size());

    if (debug)
    {
      Rcpp::Rcout << "\nAttributes:\n";

      std::map<std::string, std::variant<std::string, double>>::iterator it;

      for (it = (*allAttributes)[myIndex].begin(); it != (*allAttributes)[myIndex].end(); it++)
      {
        if (it->second.index() == 0)
        {
          Rcpp::Rcout << " - " << it->first << " = " << std::get<std::string>(it->second) << "\n";
        }
        else
        {
          Rcpp::Rcout << " - " << it->first << " = " << std::to_string(std::get<double>(it->second)) << "\n";
        }

      }

      Rcpp::Rcout << "\n";
    }

    for (size_t i = 0; i < children.size(); i++)
    {
      int childInd = parseNWKA(&(children[i]), currIndex, allParents, allChildren, allAttributes, tipCount, myIndex, debug);
      (*allChildren)[myIndex].push_back(childInd);
    }

    return myIndex;
  }
  else
  {
    int srPosition = 0;

    bool eof = false;

    int myIndex = *currIndex;
    (*currIndex)++;

    (*tipCount)++;

    allParents->push_back(parent);
    allChildren->push_back(std::vector<int>());

    allAttributes->push_back(std::map<std::string, std::variant<std::string, double>, ci_less>());
    parseAttributes(source, &srPosition, &eof, &((*allAttributes)[myIndex]), 0);

    if (debug)
    {
      Rcpp::Rcout << "\nAttributes:\n";

      std::map<std::string, std::variant<std::string, double>>::iterator it;

      for (it = (*allAttributes)[myIndex].begin(); it != (*allAttributes)[myIndex].end(); it++)
      {
        if (it->second.index() == 0)
        {
          Rcpp::Rcout << " - " << it->first << " = " << std::get<std::string>(it->second) << "\n";
        }
        else
        {
          Rcpp::Rcout << " - " << it->first << " = " << std::to_string(std::get<double>(it->second)) << "\n";
        }

      }

      Rcpp::Rcout << "\n";
    }

    return myIndex;
  }
}

//Create a phylo object from parent-child relationships between nodes and attributes.
static phylo convertToPhylo(std::vector<int>* allParents, std::vector<std::vector<int>>* allChildren, std::vector<std::map<std::string, std::variant<std::string, double>, ci_less>>* allAttributes, int tipCount)
{
  phylo tbr;

  int nodeCount = allParents->size() - tipCount;

  tbr.Nnode = nodeCount;

  if (containsKey(&((*allAttributes)[0]), "Length") && ((*allAttributes)[0]["Length"]).index() == 1 && !std::isnan(std::get<double>((*allAttributes)[0]["Length"])))
  {
    tbr.rootEdge = std::get<double>((*allAttributes)[0]["Length"]);
  }

  tbr.edgeLength = std::vector<double>(allParents->size() - 1);
  tbr.edge = std::vector<std::vector<int32_t>>(allParents->size() - 1);
  tbr.tipLabel = std::vector<std::string>(tipCount);
  tbr.nodeLabel = std::vector<std::string>(nodeCount);

  std::vector<int32_t> nodeCorresp(allParents->size());

  int tipIndex = 0;
  int nonTipIndex = 0;

  for (size_t i = 0; i < allParents->size(); i++)
  {
    if ((*allChildren)[i].size() > 0)
    {
      nonTipIndex++;
      nodeCorresp[i] = nonTipIndex + tipCount;

      if ((*allParents)[i] >= 0)
      {
        std::vector<int32_t> edge(2);
        edge[0] = nodeCorresp[(*allParents)[i]] - 1;
        edge[1] = nonTipIndex + tipCount - 1;

        tbr.edge[i - 1] = edge;
        if (containsKey(&((*allAttributes)[i]), "Length") && ((*allAttributes)[i]["Length"]).index() == 1 && !std::isnan(std::get<double>((*allAttributes)[i]["Length"])))
        {
          tbr.hasEdgeLength = true;
          tbr.edgeLength[i - 1] = std::get<double>((*allAttributes)[i]["Length"]);
        }
        else
        {
          tbr.edgeLength[i - 1] = std::nan("");
        }
      }

      std::map<std::string, std::variant<std::string, double>, ci_less>::iterator it;

      for (it = (*allAttributes)[i].begin(); it != (*allAttributes)[i].end(); it++)
      {
        bool isNumeric = it->second.index() == 1;
        Attribute attr;
        attr.AttributeName = it->first;
        attr.IsNumeric = isNumeric;

        int attrIndex = attributeIndex(&(tbr.attributes), &attr);

        if (attrIndex < 0)
        {
          tbr.attributes.push_back(attr);

          if (isNumeric)
          {
            std::vector<double> values(tipCount);
            for (int k = 0; k < tipCount; k++)
            {
              values[k] = std::nan("");
            }
            tbr.tipAttributes.push_back(values);

            values = std::vector<double>(nodeCount);
            for (int k = 0; k < nodeCount; k++)
            {
              values[k] = std::nan("");
            }
            tbr.nodeAttributes.push_back(values);
          }
          else
          {
            tbr.tipAttributes.push_back(std::vector<std::string>(tipCount));
            tbr.nodeAttributes.push_back(std::vector<std::string>(nodeCount));
          }

          attrIndex = tbr.attributes.size() - 1;
        }

        if (isNumeric)
        {
          std::get<std::vector<double>>(tbr.nodeAttributes[attrIndex])[nonTipIndex - 1] = std::get<double>(it->second);
        }
        else
        {
          std::get<std::vector<std::string>>(tbr.nodeAttributes[attrIndex])[nonTipIndex - 1] = std::get<std::string>(it->second);
        }
      }
    }
    else
    {
      tipIndex++;
      nodeCorresp[i] = tipIndex;

      if ((*allParents)[i] >= 0)
      {
        std::vector<int32_t> edge(2);
        edge[0] = nodeCorresp[(*allParents)[i]] - 1;
        edge[1] = tipIndex - 1;

        tbr.edge[i - 1] = edge;
        if (containsKey(&((*allAttributes)[i]), "Length") && ((*allAttributes)[i]["Length"]).index() == 1 && !std::isnan(std::get<double>((*allAttributes)[i]["Length"])))
        {
          tbr.hasEdgeLength = true;
          tbr.edgeLength[i - 1] = std::get<double>((*allAttributes)[i]["Length"]);
        }
        else
        {
          tbr.edgeLength[i - 1] = std::nan("");
        }
      }

      std::map<std::string, std::variant<std::string, double>, ci_less>::iterator it;

      for (it = (*allAttributes)[i].begin(); it != (*allAttributes)[i].end(); it++)
      {
        bool isNumeric = it->second.index() == 1;
        Attribute attr;
        attr.AttributeName = it->first;
        attr.IsNumeric = isNumeric;

        if (equalCI(attr.AttributeName, NAMEATTRIBUTE) && !isNumeric)
        {
          tbr.tipLabel[tipIndex - 1] = std::get<std::string>(it->second);
        }

        int attrIndex = attributeIndex(&(tbr.attributes), &attr);

        if (attrIndex < 0)
        {
          tbr.attributes.push_back(attr);

          if (isNumeric)
          {
            std::vector<double> values(tipCount);
            for (int k = 0; k < tipCount; k++)
            {
              values[k] = std::nan("");
            }
            tbr.tipAttributes.push_back(values);

            values = std::vector<double>(nodeCount);
            for (int k = 0; k < nodeCount; k++)
            {
              values[k] = std::nan("");
            }
            tbr.nodeAttributes.push_back(values);
          }
          else
          {
            tbr.tipAttributes.push_back(std::vector<std::string>(tipCount));
            tbr.nodeAttributes.push_back(std::vector<std::string>(nodeCount));
          }

          attrIndex = tbr.attributes.size() - 1;
        }

        if (isNumeric)
        {
          std::get<std::vector<double>>(tbr.tipAttributes[attrIndex])[tipIndex - 1] = std::get<double>(it->second);
        }
        else
        {
          std::get<std::vector<std::string>>(tbr.tipAttributes[attrIndex])[tipIndex - 1] = std::get<std::string>(it->second);
        }
      }
    }
  }

  Attribute nameAttr;
  nameAttr.AttributeName = "Name";
  nameAttr.IsNumeric = false;

  Attribute supportAttr;
  supportAttr.AttributeName = "Support";
  supportAttr.IsNumeric = true;

  int nameAttributeIndex = attributeIndex(&(tbr.attributes), &nameAttr);
  int supportAttributeIndex = attributeIndex(&(tbr.attributes), &supportAttr);


  bool found = false;

  if (nameAttributeIndex >= 0)
  {
    for (size_t i = 0; i < std::get<std::vector<std::string>>(tbr.nodeAttributes[nameAttributeIndex]).size(); i++)
    {
      if (std::get<std::vector<std::string>>(tbr.nodeAttributes[nameAttributeIndex])[i] != "")
      {
        found = true;
        break;
      }
    }
  }

  if (found)
  {
    tbr.nodeLabel = std::get<std::vector<std::string>>(tbr.nodeAttributes[nameAttributeIndex]);
    tbr.hasNodeLabel = true;
  }
  else if (supportAttributeIndex >= 0)
  {
    for (size_t i = 0; i < std::get<std::vector<double>>(tbr.nodeAttributes[supportAttributeIndex]).size(); i++)
    {
      if (std::get<std::vector<double>>(tbr.nodeAttributes[supportAttributeIndex])[i] > 0)
      {
        found = true;
        break;
      }
    }

    if (found)
    {
      tbr.nodeLabel = std::vector<std::string>(std::get<std::vector<double>>(tbr.nodeAttributes[supportAttributeIndex]).size());

      for (size_t i = 0; i < std::get<std::vector<double>>(tbr.nodeAttributes[supportAttributeIndex]).size(); i++)
      {
        tbr.nodeLabel[i] = std::to_string(std::get<std::vector<double>>(tbr.nodeAttributes[supportAttributeIndex])[i]);
      }

      tbr.hasNodeLabel = true;
    }
  }

  return tbr;
}

//Parse a NWKA string containing a single tree into a phylo object.
static phylo parseNWKAStringOneTree(std::string* source, bool debug)
{
  int currIndex = 0;
  std::vector<int> allParents;
  std::vector < std::vector<int>> allChildren;
  std::vector<std::map<std::string, std::variant<std::string, double>, ci_less>> allAttributes;
  int tipCount = 0;

  std::string::size_type index = source->find("(", 0);

  std::string treeName = "";

  if (index != std::string::npos)
  {
    treeName = source->substr(0, index);
    *source = source->substr(index);
  }

  parseNWKA(source, &currIndex, &allParents, &allChildren, &allAttributes, &tipCount, -1, debug);

  if (!containsKey(&(allAttributes[0]), "TreeName") && !treeName.empty())
  {
    allAttributes[0]["TreeName"] = treeName;
  }

  phylo tree = convertToPhylo(&allParents, &allChildren, &allAttributes, tipCount);

  return tree;
}

//Parse a NWKA format file (possibly containing multiple trees) into a multiPhylo object containing the
//parsed tree(s).
static multiPhylo parseNWKAFile(std::string fileName, bool debug)
{
  multiPhylo tbr;

  std::fstream file(fileName, std::ios::in);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for reading.");
  }

  bool escaping = false;
  bool escaped;
  bool openQuotes = false;
  bool openApostrophe = false;
  bool eof = false;


  Attribute treeNameAttr;
  treeNameAttr.AttributeName = "TreeName";
  treeNameAttr.IsNumeric = false;

  while (!eof)
  {
    std::stringstream sb;

    char c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);

    while (!eof && !(c == ';' && !escaped && !openQuotes && !openApostrophe))
    {
      sb << (char)c;
      c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);
    }

    std::string treeString = sb.str();

    if (treeString.length() > 0)
    {
      try
      {
        phylo tree = parseNWKAStringOneTree(&treeString, debug);
        tbr.trees.push_back(tree);

        int treeNameIndex = attributeIndex(&(tree.attributes), &treeNameAttr);

        if (treeNameIndex < 0)
        {
          tbr.treeNames.push_back("tree" + std::to_string(tbr.treeNames.size() + 1));
        }
        else
        {
          tbr.treeNames.push_back(std::get<std::vector<std::string>>(tree.nodeAttributes[treeNameIndex])[0]);
        }
      }
      catch (...)
      {
        Rcpp::warning("An error occurred while parsing tree #" + std::to_string(tbr.trees.size() + 1) + "!");
        break;
      }
    }
  }

  file.close();

  return tbr;
}

//Parse a NWKA string (possibly containing multiple trees) into a multiPhylo object containing the
//parsed tree(s).
static multiPhylo parseNWKAString(std::string* source, bool debug)
{
  multiPhylo tbr;

  int srPosition = 0;
  bool escaping = false;
  bool escaped;
  bool openQuotes = false;
  bool openApostrophe = false;
  bool eof = false;

  Attribute treeNameAttr;
  treeNameAttr.AttributeName = "TreeName";
  treeNameAttr.IsNumeric = false;

  while (!eof)
  {
    std::stringstream sb;

    char c = nextToken(source, &srPosition, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);

    while (!eof && !(c == ';' && !escaped && !openQuotes && !openApostrophe))
    {
      sb << (char)c;
      c = nextToken(source, &srPosition, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);
    }

    std::string treeString = sb.str();

    if (treeString.length() > 0)
    {
      try
      {
        phylo tree = parseNWKAStringOneTree(&treeString, debug);
        tbr.trees.push_back(tree);

        int treeNameIndex = attributeIndex(&(tree.attributes), &treeNameAttr);

        if (treeNameIndex < 0)
        {
          tbr.treeNames.push_back("tree" + std::to_string(tbr.treeNames.size() + 1));
        }
        else
        {
          tbr.treeNames.push_back(std::get<std::vector<std::string>>(tree.nodeAttributes[treeNameIndex])[0]);
        }
      }
      catch (...)
      {
        Rcpp::warning("An error occurred while parsing tree #" + std::to_string(tbr.trees.size() + 1) + "!");
        break;
      }
    }
  }

  return tbr;
}

//Possible states while reading a NEXUS file
enum class NEXUSStatus
{
  Root,
  InCommentInRoot,
  InOtherBlock,
  InCommentInOtherBlock,
  InTreeBlock,
  InTranslateStatement,
  InTreeStatement,
  InCommentInTreeBlock,
  InCommentInTranslateStatement,
  InCommentInTreeStatementName,
};

//Parse a NEXUS format file (possibly containing multiple trees) into a multiPhylo object containing the
//parsed tree(s).
static multiPhylo parseNEXUSFile(std::string fileName, bool debug)
{
  multiPhylo tbr;

  std::fstream file(fileName, std::ios::in);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for reading.");
  }

  NEXUSStatus status = NEXUSStatus::Root;

  bool eof = false;

  std::string word = nextWord(&file, &eof);

  std::map<std::string, std::string> translateDictionary;

  std::string treeName;

  while (!eof)
  {
    switch (status)
    {
    case NEXUSStatus::Root:
      if (equalCI(word, BEGINstring))
      {
        bool ignore;
        word = nextWord(&file, &ignore);

        if (equalCI(word, TREESstring))
        {
          status = NEXUSStatus::InTreeBlock;
        }
        else
        {
          status = NEXUSStatus::InOtherBlock;
        }
      }
      else if (word == "[")
      {
        status = NEXUSStatus::InCommentInRoot;
      }
      break;
    case NEXUSStatus::InCommentInRoot:
      if (word == "]")
      {
        status = NEXUSStatus::Root;
      }
      break;
    case NEXUSStatus::InOtherBlock:
      if (equalCI(word, ENDstring))
      {
        status = NEXUSStatus::Root;
      }
      else if (word == "[")
      {
        status = NEXUSStatus::InCommentInOtherBlock;
      }
      break;
    case NEXUSStatus::InCommentInOtherBlock:
      if (word == "]")
      {
        status = NEXUSStatus::InOtherBlock;
      }
      break;
    case NEXUSStatus::InTreeBlock:
      if (equalCI(word, TRANSLATEstring))
      {
        status = NEXUSStatus::InTranslateStatement;
      }
      else if (equalCI(word, TREEstring))
      {
        status = NEXUSStatus::InTreeStatement;
      }
      else if (equalCI(word, ENDstring))
      {
        status = NEXUSStatus::Root;
      }
      else if (word == "[")
      {
        status = NEXUSStatus::InCommentInTreeBlock;
      }
      break;
    case NEXUSStatus::InCommentInTreeBlock:
      if (word == "]")
      {
        status = NEXUSStatus::InTreeBlock;
      }
      break;
    case NEXUSStatus::InTranslateStatement:
      if (word == "[")
      {
        status = NEXUSStatus::InCommentInTranslateStatement;
      }
      else if (word == ";")
      {
        status = NEXUSStatus::InTreeBlock;
      }
      else if (word == ",")
      {
      }
      else
      {
        bool ignore;
        std::string name = word;
        word = nextWord(&file, &ignore);
        translateDictionary[name] = word;
      }
      break;
    case NEXUSStatus::InCommentInTranslateStatement:
      if (word == "]")
      {
        status = NEXUSStatus::InTranslateStatement;
      }
      break;
    case NEXUSStatus::InCommentInTreeStatementName:
      if (word == "]")
      {
        status = NEXUSStatus::InTreeStatement;
      }
      break;
    case NEXUSStatus::InTreeStatement:
      if (word == "[")
      {
        status = NEXUSStatus::InCommentInTreeStatementName;
      }
      else
      {
        treeName = word;
        bool escaping = false;
        bool escaped;
        bool openQuotes = false;
        bool openApostrophe = false;
        bool openComment = false;

        char c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);

        while (!eof && (c != '=' || openComment))
        {
          if (c == '[')
          {
            openComment = true;
          }

          if (c == ']')
          {
            openComment = false;
          }

          c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);
        }

        std::stringstream preComments;
        std::stringstream tree;

        c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);

        while (!(c == '(' && !openComment) && !eof)
        {
          preComments << c;

          if (c == '[')
          {
            openComment = true;
          }

          if (c == ']')
          {
            openComment = false;
          }

          c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);
        }



        while (!(c == ';' && !openComment && !escaped && !openQuotes && !openApostrophe) && !eof)
        {
          tree << c;

          if (c == '[')
          {
            openComment = true;
          }

          if (c == ']')
          {
            openComment = false;
          }


          c = nextToken(&file, &escaping, &escaped, &openQuotes, &openApostrophe, &eof);
        }

        std::string treeString = tree.str();

        phylo parsedTree = parseNWKAStringOneTree(&treeString, debug);

        Attribute treeNameAttr;
        treeNameAttr.AttributeName = "TreeName";
        treeNameAttr.IsNumeric = false;

        if (attributeIndex(&(parsedTree.attributes), &treeNameAttr) < 0)
        {
          parsedTree.attributes.push_back(treeNameAttr);
          parsedTree.tipAttributes.push_back(std::vector<std::string>(parsedTree.tipLabel.size()));
          parsedTree.nodeAttributes.push_back(std::vector<std::string>(parsedTree.Nnode));
          std::get<std::vector<std::string>>(parsedTree.nodeAttributes[parsedTree.nodeAttributes.size() - 1])[0] = treeName;
        }

        for (size_t i = 0; i < parsedTree.tipLabel.size(); i++)
        {
          if (!parsedTree.tipLabel[i].empty())
          {
            std::map<std::string, std::string>::iterator it = translateDictionary.find(parsedTree.tipLabel[i]);
            if (it != translateDictionary.end())
            {
              parsedTree.tipLabel[i] = it->second;
            }
          }
        }

        for (size_t i = 0; i < parsedTree.nodeLabel.size(); i++)
        {
          if (!parsedTree.nodeLabel[i].empty())
          {
            std::map<std::string, std::string>::iterator it = translateDictionary.find(parsedTree.nodeLabel[i]);
            if (it != translateDictionary.end())
            {
              parsedTree.nodeLabel[i] = it->second;
            }
          }
        }

        std::string preCommentsString = preComments.str();
        trim(preCommentsString);

        if (preCommentsString != "[&R]" && preCommentsString != "[&U]")
        {
          bool tempEof = false;

          int tempSrPosition = 0;

          std::map<std::string, std::variant<std::string, double>, ci_less> attributes;

          parseAttributes(&preCommentsString, &tempSrPosition, &tempEof, &attributes, 2);

          std::map<std::string, std::variant<std::string, double>, ci_less>::iterator it;

          for (it = attributes.begin(); it != attributes.end(); it++)
          {
            bool isNumeric = it->second.index() == 1;
            Attribute attr;
            attr.AttributeName = it->first;
            attr.IsNumeric = isNumeric;

            int attrIndex = attributeIndex(&(parsedTree.attributes), &attr);

            if (attrIndex < 0)
            {
              parsedTree.attributes.push_back(attr);

              if (isNumeric)
              {
                std::vector<double> values(parsedTree.tipLabel.size());
                for (size_t k = 0; k < parsedTree.tipLabel.size(); k++)
                {
                  values[k] = std::nan("");
                }
                parsedTree.tipAttributes.push_back(values);

                values = std::vector<double>(parsedTree.Nnode);
                for (int k = 0; k < parsedTree.Nnode; k++)
                {
                  values[k] = std::nan("");
                }
                parsedTree.nodeAttributes.push_back(values);
              }
              else
              {
                parsedTree.tipAttributes.push_back(std::vector<std::string>(parsedTree.tipLabel.size()));
                parsedTree.nodeAttributes.push_back(std::vector<std::string>(parsedTree.Nnode));
              }

              attrIndex = parsedTree.attributes.size() - 1;
            }

            if (isNumeric)
            {
              std::get<std::vector<double>>(parsedTree.nodeAttributes[attrIndex])[0] = std::get<double>(it->second);
            }
            else
            {
              std::get<std::vector<std::string>>(parsedTree.nodeAttributes[attrIndex])[0] = std::get<std::string>(it->second);
            }
          }
        }

        tbr.trees.push_back(parsedTree);
        tbr.treeNames.push_back(treeName);

        status = NEXUSStatus::InTreeBlock;
      }
      break;
    }

    word = nextWord(&file, &eof);
  }

  file.close();

  return tbr;
}

//Read trees in NWKA format from a string provided by R and pass them back to R.
//[[Rcpp::export]]
SEXP Rcpp_read_nwka_string(std::string source, bool debug)
{
  multiPhylo trees = parseNWKAString(&source, debug);

  return Rcpp::wrap(convertMultiPhylo(&trees));
}

//Read trees from a file in NWKA format and pass them back to R
//[[Rcpp::export]]
SEXP Rcpp_read_nwka_file(std::string fileName, bool debug)
{
  multiPhylo trees = parseNWKAFile(fileName, debug);

  return Rcpp::wrap(convertMultiPhylo(&trees));
}

//Read trees from a file in NEXUS format and pass them back to R
//[[Rcpp::export]]
SEXP Rcpp_read_nexus_file(std::string fileName, bool debug)
{
  multiPhylo trees = parseNEXUSFile(fileName, debug);

  return Rcpp::wrap(convertMultiPhylo(&trees));
}
