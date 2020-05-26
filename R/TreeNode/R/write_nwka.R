########################################################################
#  write_nwka.R    2020-05-20
#  by Giorgio Bianchini
#  This file is part of the R package TreeNode, licensed under GPLv3
#
#  Functions to write trees in Newick-with-Attributes (NWKA) format.
########################################################################



#' Write Tree File in NWKA format
#'
#' This function writes one or more trees in Newick-with-Attributes (NWKA) format to a file or to the standard output.
#'
#'
#' @param trees An object of class \code{"phylo"} or \code{"multiPhylo"}.
#' @param file A file name. If this is \code{""} (the default), the tree will be written on the standard
#'             output.
#' @param append If this is \code{FALSE} (the default), the output file (if it exists already) is truncated
#'               before writing trees (i.e. overwritten). If this is \code{TRUE}, the trees are appended at
#'               the end of the output file.
#' @param nwka If this is \code{TRUE} (the default), the tree will be written in Newick-with-Attributes
#'             (NWKA) format. Otherwise, the tree will be written in Newick format (and attributes that
#'             cannot be represented in this format will be lost).
#' @param quotes If \code{nwka = FALSE}, this argument determines whether names in the tree file will be
#'               enclosed within single quotes (if this is \code{TRUE}) or not (if this is \code{FALSE}).
#'
#' @details All of the available attributes are written to the file if \code{nwka = TRUE}. Otherwise, (if
#'          available) the tip names and lenghts are always written, as well as the internal nodes' lenghts and support
#'          values. If nodes have a name, this is only included if they do not have a support value as well.
#'
#'          The tip names can be specified either as a \code{Name} element in the tree's \code{tip.attributes}
#'          element, or as the \code{tip.label} element of the tree. If both are specified, the values stored in
#'          the \code{Name} attribute take precedence (this allows backward compatibility for trees created
#'          using \code{\link[ape]{ape}}).
#'
#'          The node names and support values can similarly be specified either with a \code{Name} or \code{Support}
#'          element in the tree's \code{node.attributes}, or as the tree's \code{node.label}. If all the node labels
#'          can be parsed as numbers, they will be assumed to contain support values; otherwise, they will be
#'          assumed to contain node names. If the \code{node.attributes} already contain a \code{Name} or \code{Support}
#'          element, the node labels will be ignored.
#'
#'          No attempt is made to fix problematic labels. Thus, if the tip or node names contain special characters,
#'          an invalid output may be produced. For example, if the labels contain spaces or commas and \code{enwk} and
#'          \code{quotes} are both \code{FALSE}, the output tree may not be parsed correctly. If you wish to produce a
#'          tree conforming to the Newick format while fixing problematic tip labels, you should look into the
#'          \code{\link[ape]{write.tree}} function of the \code{\link[ape]{ape}} package.
#'
#' @author Giorgio Bianchini
#'
#' @family functions to write trees
#'
#' @seealso \code{\link[ape]{ape}}, \code{\link[ape]{write.tree}}
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
#' # Print the tree to the standard output in NWKA format
#' cat(write_nwka_tree(tree))
#'
#' # Print the tree to the standard output in Newick format without quotes
#' cat(write_nwka_tree(tree, nwka = FALSE))
#'
#' # Print the tree to the standard output in Newick format with quotes
#' cat(write_nwka_tree(tree, nwka = FALSE, quotes = TRUE))
#'
#' @export
write_nwka_tree <- function(trees, file = "", append = FALSE, nwka = TRUE, quotes = FALSE)
{
  if (!inherits(trees, c("phylo", "multiPhylo")))
  {
    stop("Expecting a \"phylo\" or \"multiPhylo\" object!");
  }

  if (!inherits(trees, "multiPhylo"))
  {
    realTrees <- list()
    realTrees[["tree"]] <- trees
    trees <- realTrees
  }

  if (file == "")
  {
    Rcpp_multiPhylo_to_string(trees, nwka, quotes)
  }
  else
  {
    Rcpp_multiPhylo_to_file(trees, file, nwka, quotes, append)
  }
}

#' Write Tree File in NEXUS format
#'
#' This function writes one or more trees to a NEXUS format file. Within the NEXUS file, the trees are stored in the Newick-with-Attributes (NWKA) format.
#'
#'
#' @param trees An object of class \code{"phylo"} or \code{"multiPhylo"}.
#' @param file A file name.
#' @param translate If this is \code{TRUE} (the default), the produced nexus tree will contain, in addition to the \code{Trees} block,
#'                  a \code{Taxa} block containing the taxon labels, as well as a \code{Translate} instruction in the \code{Trees} block.
#'                  Otherwise, it will only contain a \code{Trees} block without a \code{Translate} instruction.
#' @param translate_quotes If this is \code{TRUE} (the default), the entries in the \code{Taxa} block and in the \code{Translate} instruction
#'                         will be placed within single quotes. Otherwise, they will be written without single quotes.
#'
#' @details Only the tip labels are included in the \code{Taxa} block and the \code{Translate} instruction (if applicable).
#'
#'          The trees inside the NEXUS file will be stored in NWKA format, including all of the available attributes. This is compatible
#'          with the NEXUS specification, because attributes that cannot be represented in standard Newick format are enclosed within
#'          square brackets (\code{[]});
#'
#'          The tip names can be specified either as a \code{Name} element in the tree's \code{tip.attributes}
#'          element, or as the \code{tip.label} element of the tree. If both are specified, the values stored in
#'          the \code{Name} attribute take precedence (this allows backward compatibility for trees created
#'          using \code{\link[ape]{ape}}).
#'
#'          The node names and support values can similarly be specified either with a \code{Name} or \code{Support}
#'          element in the tree's \code{node.attributes}, or as the tree's \code{node.label}. If all the node labels
#'          can be parsed as numbers, they will be assumed to contain support values; otherwise, they will be
#'          assumed to contain node names. If the \code{node.attributes} already contain a \code{Name} or \code{Support}
#'          element, the node labels will be ignored.
#'
#' @author Giorgio Bianchini
#'
#' @family functions to write trees
#'
#' @seealso \code{\link[ape]{ape}}, \code{\link[ape]{write.nexus}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/NWKA.md}
#'
#'
#' @export
write_nwka_nexus <- function(trees, file, translate = TRUE, translate_quotes = TRUE)
{
  if (!inherits(trees, c("phylo", "multiPhylo")))
  {
    stop("Expecting a \"phylo\" or \"multiPhylo\" object!");
  }

  if (!inherits(trees, "multiPhylo"))
  {
    realTrees <- list()
    realTrees[["tree"]] <- trees
    trees <- realTrees
  }

  Rcpp_multiPhylo_to_nexus(trees, file, translate, translate_quotes)
}
