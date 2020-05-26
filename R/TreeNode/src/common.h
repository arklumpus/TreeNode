/***********************************************************************
 *  common.h    2020-05-20
 *  by Giorgio Bianchini
 *  This file is part of the R package TreeNode, licensed under GPLv3
 *
 *  Common definitions used by multiple files in the package.
 ***********************************************************************/

#include <Rcpp.h>
#include <fstream>
#include <variant>

//Unsigned byte
typedef unsigned char byte;

//Standard attribute names
static std::string LENGTHATTRIBUTE = "length";
static std::string SUPPORTATTRIBUTE = "support";
static std::string NAMEATTRIBUTE = "name";
static std::string TREENAMEATTRIBUTE = "treename";

//Represents a node's attributes
struct Attribute
{
    std::string AttributeName;
    bool IsNumeric = false;
};

//Represents a phylogenetic tree in a format similar to the one used by the R package APE
struct phylo
{
    int32_t Nnode = -1;
    double rootEdge = std::nan("");
    std::vector<std::vector<int32_t>> edge;
    std::vector<std::string> tipLabel;
    std::vector<std::string> nodeLabel;
    std::vector<double> edgeLength;
    std::vector<std::variant<std::vector<std::string>, std::vector<double>>> tipAttributes;
    std::vector<std::variant<std::vector<std::string>, std::vector<double>>> nodeAttributes;
    std::vector<Attribute> attributes;
    bool hasEdgeLength = false;
    bool hasNodeLabel = false;
};

//Represents a list of phylogenetic trees with names
struct multiPhylo
{
    std::vector<phylo> trees;
    std::vector<std::string> treeNames;
};

//From https://stackoverflow.com/questions/1801892/how-can-i-make-the-mapfind-operation-case-insensitive
/************************************************************************/
/* Comparator for case-insensitive comparison in STL assos. containers  */
/************************************************************************/
struct ci_less
{
    struct nocase_compare
    {
        //In common.cpp
        bool operator() (const unsigned char& c1, const unsigned char& c2) const;
    };

    //In common.cpp
    bool operator() (const std::string& s1, const std::string& s2) const;
};

//In common.cpp [see comments there]
bool equalCI(std::string& str1, std::string& str2);
Rcpp::List convertPhylo(phylo tree);
Rcpp::List convertMultiPhylo(multiPhylo* trees);
bool tryParse(std::string val, double* output = NULL);
int attributeIndex(std::vector<Attribute>* attributes, Attribute* attribute);
int32_t addChildren(std::vector<std::vector<int32_t>>* children, std::vector<int32_t>* sortedParents, std::vector<std::vector<int32_t>>* sortedChildren, std::vector<int32_t>* sortedNodes, int32_t* currIndex, int32_t currNonSortedIndex, int32_t currSortedParent);
void setAttributes(phylo* tree);
std::vector<std::variant<std::vector<std::string>, std::vector<double>>> convertAttributeList(Rcpp::List* attributes, std::vector<Attribute>* allAttributes);
phylo convertTree(Rcpp::List* tree);
void setTreeName(phylo* tree, std::string name);
multiPhylo convertTrees(Rcpp::List* trees);
