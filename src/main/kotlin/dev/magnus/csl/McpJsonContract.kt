package dev.magnus.csl

/*
 * The JSON form of the .NET Analysis&Refactoring MCP's answers that the plugin reads, as the MCP's tools give it with
 * the argument `"format": "json"`. Gson fills these from an answer; nothing else in the plugin sees them. The MCP's
 * specifications of the form are in its repository, under tests/DotNetAnalysisAndRefactoringMcp.Specifications/JsonForm.
 * Lines and columns are 1-based.
 */

/** `GET /health`, which needs no MCP session: the solutions the MCP serves, by their solution files' paths. */
internal class HealthAnswer(val solutions: List<ServedSolution>)

/** A solution the MCP serves: [path] is its solution file's absolute path. */
internal class ServedSolution(val path: String)

/** `go_to_definition`: one declaration when the name is unambiguous, several when it isn't, none when nothing has it. */
internal class DeclarationsAnswer(val declarations: List<Declaration>)

/**
 * Where the MCP says a symbol is declared, in a [Declaration] or a [TypeListing]. A compiled symbol has no [file] and no
 * [line]. A declaration a source generator wrote has no [file] either, since Rider holds the generated file in memory:
 * its [line] and [column] are in that file, which [generator], the generator's full type name, wrote and named
 * [hintName]. A member with no source of its own, such as a positional record's property, is where it is written: the
 * parameter in the record's header. A type declared in parts is at the part in the file named after it, where there is
 * one.
 */
internal interface DeclaredSymbol {
    val kind: String
    val qualifiedName: String
    val file: String?
    val generator: String?
    val hintName: String?
    val line: Int?
    val column: Int?

    /** The [SymbolHit] a link opens, or null for a compiled symbol, which has no source to open. */
    fun toSymbolHit(): SymbolHit? =
        SourceFile.of(file, generator, hintName)?.let { SymbolHit(kind, QualifiedName(qualifiedName), it, line!!, column!!) }
}

/** One declaration of the name `go_to_definition` was asked for. */
internal class Declaration(
    override val kind: String,
    override val qualifiedName: String,
    override val file: String?,
    override val generator: String?,
    override val hintName: String?,
    override val line: Int?,
    override val column: Int?,
) : DeclaredSymbol

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
 * [qualifiedName] that after its namespace's and the types' it is nested in.
 */
internal class TypeListing(
    override val kind: String,
    val name: String,
    override val qualifiedName: String,
    override val file: String?,
    override val generator: String?,
    override val hintName: String?,
    override val line: Int?,
    override val column: Int?,
    val members: List<MemberListing>?,
    val nestedTypes: List<TypeListing>?,
) : DeclaredSymbol

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
