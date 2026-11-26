package dev.magnus.csl

/*
 * The JSON form of the .NET Analysis&Refactoring MCP's answers that the plugin reads, as the MCP's tools give it with
 * the argument `"format": "json"`. Gson fills these from an answer; nothing else in the plugin sees them. The MCP's
 * specifications of the form are in its repository, under tests/DotNetAnalysisAndRefactoringMcp.Specifications/JsonForm.
 * Lines and columns are 1-based.
 */

/** `list_solutions`: every solution the MCP has loaded. */
internal class SolutionsAnswer(val solutions: List<SolutionInfo>)

/** `go_to_definition`: one declaration when the name is unambiguous, several when it isn't, none when nothing has it. */
internal class DeclarationsAnswer(val declarations: List<Declaration>)

/**
 * One declaration. A compiled symbol has no [file] and no [line]. A declaration a source generator wrote has no [file]
 * either, since Rider holds the generated file in memory: its [line] and [column] are in that file, which the MCP names
 * by its generator and hint name, two fields the plugin doesn't read. A member with no source of its own, such as a
 * positional record's property, is where it is written: the parameter in the record's header. A type declared in
 * parts is at the part in the file named after it, where there is one.
 */
internal class Declaration(
    val kind: String,
    val qualifiedName: String,
    val file: String?,
    val line: Int?,
    val column: Int?,
)

/** `browse_namespace`: one entry for each namespace asked for, in the order asked. */
internal class NamespacesAnswer(val namespaces: List<NamespaceListing>)

/** A namespace's child namespaces, by their qualified names, and its types; or an [error], such as that it doesn't exist. */
internal class NamespaceListing(
    val name: String,
    val childNamespaces: List<String>,
    val types: List<TypeListing>,
    val error: String?,
) {
    /** Whether it is the global namespace, which has no name to write. */
    val isGlobal: Boolean get() = name == GLOBAL

    companion object {
        /** The global namespace's name, as `browse_namespace` names it. */
        const val GLOBAL = ""
    }
}

/**
 * A type, with its [members] when they were asked for, and the types nested in it. [name] is its bare C# identifier,
 * [qualifiedName] that after its namespace's and the types' it is nested in. [file], [line] and [column] are where its
 * name is declared, as a [Declaration]'s are: no [file] for a type a source generator wrote.
 */
internal class TypeListing(
    val kind: String,
    val name: String,
    val qualifiedName: String,
    val file: String?,
    val line: Int?,
    val column: Int?,
    val members: List<MemberListing>?,
    val nestedTypes: List<TypeListing>?,
)

/**
 * A member a type declares. [kind] is a C# keyword where there is one: `method`, `property`, `field` (constants and
 * enum members too), `event`, `constructor`, `indexer`, `operator`. A C# 14 extension block is a member of
 * [kind] `extension`, with no [name], and its own [members].
 */
internal class MemberListing(
    val kind: String,
    val name: String?,
    val members: List<MemberListing>?,
)
