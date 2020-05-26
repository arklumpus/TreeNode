########################################################################
#  read_nwka.R    2020-05-20
#  by Giorgio Bianchini
#  This file is part of the R package TreeNode, licensed under GPLv3
#
#  Functions to read trees in Newick-with-Attributes (NWKA) format.
########################################################################



#' Read Tree File in NWKA Format
#'
#' This function reads a file containing one or more trees in Newick-with-Attributes (NWKA) format.
#'
#' @param file A file name.
#' @param text A variable of mode character containing the tree(s) to parse. By default, this is set to \code{NULL} and
#'        ignored (i.e. the tree is read from the file specified by the \code{file} argument); otherwise, the file argument
#'        is ignored and the trees are read from the \code{text} argument.
#' @param tree.names A vector of mode character containing names for the trees that are read from the file;
#'        if \code{NULL} (the default), the trees will be named as \code{"tree1"}, \code{"tree2"}, ...
#' @param keep.multi If \code{TRUE}, this function will return an object of class \code{"multiPhylo"} even
#'        when the tree file contains only a single tree. Defaults to \code{FALSE}, which means that if the
#'        file contains a single tree, an object of class \code{"phylo"} is returned.
#' @param debug A logical value indicating whether to enable verbose debug output while parsing the tree. If this is \code{TRUE},
#'        the function will print information about each node in the tree as it parses it.
#'
#' @return An object of class \code{"phylo"} or \code{"multiPhylo"}, compatible with the \code{\link[ape]{ape}}
#'         package.
#'
#'         In addition to the elements described in the documentation for the \code{\link[ape]{read.tree}}
#'         function of the \code{\link[ape]{ape}} package, a \code{"phylo"} object produced by this function
#'         will also have the following components:
#'         \item{\code{tip.attributes}}{A named list of attributes for the tips of the tree. Each element of
#'                                      this list is a vector of mode character or numeric (depending on the attribute).}
#'         \item{\code{node.attributes}}{A named list of attributes for the internal nodes of the tree. Each element of
#'                                       this list is a vector of mode character or numeric (depending on the attribute).}
#'
#' @details The Newick-with-Attributes format parsed by this function is backwards compatible with the Newick/New Hampshire
#'          format and some of its extensions (e.g. Extended Newick, New Hampshire X).
#'
#'          Node attributes (e.g. support values, rates, ages...) are parsed by this function and returned in the
#'          \code{tip.attributes} and \code{node.attributes} elements of the returned \code{"phylo"} objects. If the nodes
#'          contain a \code{prob} attribute, its value will also be copied to the \code{Support} attribute.
#'
#'          Attribute names may appear in any kind of casing (e.g. \code{Name}, \code{name} or \code{NAME}), but they
#'          should be treated using case-insensitive comparisons.
#'
#'          Setting the \code{debug} argument to \code{TRUE} can be useful when analysing malformed trees (to understand
#'          at which point in the tree the problem lies).
#'
#' @author Giorgio Bianchini
#'
#' @family functions to read trees
#'
#' @seealso \code{\link[ape]{ape}}, \code{\link[ape]{read.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/NWKA.md}
#'
#' @examples
#' # Parse a tree string
#' # Topology from https://www.ncbi.nlm.nih.gov/Taxonomy/Browser/wwwtax.cgi?id=207598
#' tree <- read_nwka_tree(text="(((('Homo sapiens'[rank=species])'Homo'[rank=genus])
#' 'Hominina'[rank=subtribe],(('Pan paniscus'[rank=species],'Pan troglodytes'
#' [rank=species])'Pan'[rank=genus])'Panina'[rank=subtribe])'Hominini'[rank=tribe],
#' (('Gorilla gorilla'[rank=species],'Gorilla beringei'[rank=species])'Gorilla'
#' [rank=genus])'Gorillini'[rank=tribe])'Homininae'[rank=subfamily];")
#'
#' # Show the tree's structure
#' str(tree)
#'
#' # Plot the tree with node labels
#' ape::plot.phylo(tree, show.node.label = TRUE, node.depth = 2)
#'
#' # Add taxonomic rank (stored in the "rank" attribute of the tree)
#' tree$tip.label = paste(tree$tip.attributes$rank, tree$tip.attributes$Name, sep="\n")
#' tree$node.label = paste(tree$node.attributes$rank, tree$node.attributes$Name, sep="\n")
#'
#' # Plot again
#' ape::plot.phylo(tree, show.node.label = TRUE, node.depth = 2, y.lim=c(0.5, 5.5))
#'
#' @export
read_nwka_tree <- function(file = "", text = NULL, tree.names = NULL, keep.multi = FALSE, debug = FALSE)
{
  trees <- NULL

  if (is.character(text))
  {
    trees <- Rcpp_read_nwka_string(text, debug)
  }
  else
  {
    trees <- Rcpp_read_nwka_file(file, debug)
  }

  if (!is.null(tree.names))
  {
    names(trees) <- tree.names
  }

  if (length(trees) == 1 && !keep.multi)
  {
    trees <- trees[[1]]
  }

  return(trees)
}

#' Read Tree File in NEXUS Format with NWKA Trees
#'
#' This function reads a file containing one or more trees in NEXUS format. Each tree is parsed according to the Newick-with-Attributes (NWKA) format.
#'
#' @param file A file name.
#' @param tree.names A vector of mode character containing names for the trees that are read from the file;
#'        if \code{NULL} (the default), the trees will be named according to the names in the tree file or,
#'        if these are missing, as \code{"tree1"}, \code{"tree2"}, ...
#' @param force.multi If \code{TRUE}, this function will return an object of class \code{"multiPhylo"} even
#'        when the tree file contains only a single tree. Defaults to \code{FALSE}, which means that if the
#'        file contains a single tree, an object of class \code{"phylo"} is returned.
#' @param debug A logical value indicating whether to enable verbose debug output while parsing the tree. If this is \code{TRUE},
#'        the function will print information about each node in the each tree as it parses it.
#'
#' @return An object of class \code{"phylo"} or \code{"multiPhylo"}, compatible with the \code{\link[ape]{ape}}
#'         package.
#'
#'         In addition to the elements described in the documentation for the \code{\link[ape]{read.tree}}
#'         function of the \code{\link[ape]{ape}} package, a \code{"phylo"} object produced by this function
#'         will also have the following components:
#'         \item{\code{tip.attributes}}{A named list of attributes for the tips of the tree. Each element of
#'                                      this list is a vector of mode character or numeric (depending on the attribute).}
#'         \item{\code{node.attributes}}{A named list of attributes for the internal nodes of the tree. Each element of
#'                                       this list is a vector of mode character or numeric (depending on the attribute).}
#'
#' @details Only the \code{Trees} block of the NEXUS file is parsed.
#'
#'          Node attributes (e.g. support values, rates, ages...) are parsed by this function and returned in the
#'          \code{tip.attributes} and \code{node.attributes} elements of the returned \code{"phylo"} objects. If the nodes
#'          contain a \code{prob} attribute, its value will also be copied to the \code{Support} attribute.
#'
#'          The translation table (if any) of the \code{Trees} block is used to translate the names of both tips and internal
#'          nodes. However, if the untranslated names of internal nodes are numbers, these may be interpreted as support
#'          values (and thus, not translated).
#'
#'          Attribute names may appear in any kind of casing (e.g. \code{Name}, \code{name} or \code{NAME}), but they
#'          should be treated using case-insensitive comparisons.
#'
#'          Setting the \code{debug} argument to \code{TRUE} can be useful when analysing malformed trees (to understand
#'          at which point in the tree the problem lies).
#'
#' @author Giorgio Bianchini
#'
#' @family functions to read trees
#'
#' @seealso \code{\link[ape]{ape}}, \code{\link[ape]{read.tree}}, \code{\link[ape]{read.nexus}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/NWKA.md}
#'
#' @export
read_nwka_nexus <- function(file, tree.names = NULL, force.multi = FALSE, debug = FALSE)
{
  trees <- Rcpp_read_nexus_file(file, debug)

  if (!is.null(tree.names))
  {
    names(trees) <- tree.names
  }

  if (length(trees) == 1 && !force.multi)
  {
    trees <- trees[[1]]
  }

  return(trees)
}
