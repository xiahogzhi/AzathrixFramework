package com.azathrix.minipanda

import com.intellij.psi.tree.IElementType

class MiniPandaTokenType(debugName: String) : IElementType(debugName, MiniPandaLanguage) {
    override fun toString(): String = "MiniPandaTokenType.${super.toString()}"
}

class MiniPandaElementType(debugName: String) : IElementType(debugName, MiniPandaLanguage)

object MiniPandaTokenTypes {
    // Keywords
    @JvmField val VAR = MiniPandaTokenType("VAR")
    @JvmField val FUNC = MiniPandaTokenType("FUNC")
    @JvmField val CLASS = MiniPandaTokenType("CLASS")
    @JvmField val IF = MiniPandaTokenType("IF")
    @JvmField val ELSE = MiniPandaTokenType("ELSE")
    @JvmField val WHILE = MiniPandaTokenType("WHILE")
    @JvmField val FOR = MiniPandaTokenType("FOR")
    @JvmField val IN = MiniPandaTokenType("IN")
    @JvmField val RETURN = MiniPandaTokenType("RETURN")
    @JvmField val BREAK = MiniPandaTokenType("BREAK")
    @JvmField val CONTINUE = MiniPandaTokenType("CONTINUE")
    @JvmField val TRUE = MiniPandaTokenType("TRUE")
    @JvmField val FALSE = MiniPandaTokenType("FALSE")
    @JvmField val NULL = MiniPandaTokenType("NULL")
    @JvmField val THIS = MiniPandaTokenType("THIS")
    @JvmField val SUPER = MiniPandaTokenType("SUPER")
    @JvmField val IMPORT = MiniPandaTokenType("IMPORT")
    @JvmField val AS = MiniPandaTokenType("AS")

    // Literals
    @JvmField val NUMBER = MiniPandaTokenType("NUMBER")
    @JvmField val STRING = MiniPandaTokenType("STRING")
    @JvmField val IDENTIFIER = MiniPandaTokenType("IDENTIFIER")

    // Operators
    @JvmField val PLUS = MiniPandaTokenType("PLUS")
    @JvmField val MINUS = MiniPandaTokenType("MINUS")
    @JvmField val STAR = MiniPandaTokenType("STAR")
    @JvmField val SLASH = MiniPandaTokenType("SLASH")
    @JvmField val PERCENT = MiniPandaTokenType("PERCENT")
    @JvmField val EQ = MiniPandaTokenType("EQ")
    @JvmField val EQEQ = MiniPandaTokenType("EQEQ")
    @JvmField val BANGEQ = MiniPandaTokenType("BANGEQ")
    @JvmField val LT = MiniPandaTokenType("LT")
    @JvmField val GT = MiniPandaTokenType("GT")
    @JvmField val LTEQ = MiniPandaTokenType("LTEQ")
    @JvmField val GTEQ = MiniPandaTokenType("GTEQ")
    @JvmField val AMPAMP = MiniPandaTokenType("AMPAMP")
    @JvmField val PIPEPIPE = MiniPandaTokenType("PIPEPIPE")
    @JvmField val BANG = MiniPandaTokenType("BANG")
    @JvmField val ARROW = MiniPandaTokenType("ARROW")

    // Delimiters
    @JvmField val LPAREN = MiniPandaTokenType("LPAREN")
    @JvmField val RPAREN = MiniPandaTokenType("RPAREN")
    @JvmField val LBRACE = MiniPandaTokenType("LBRACE")
    @JvmField val RBRACE = MiniPandaTokenType("RBRACE")
    @JvmField val LBRACKET = MiniPandaTokenType("LBRACKET")
    @JvmField val RBRACKET = MiniPandaTokenType("RBRACKET")
    @JvmField val COMMA = MiniPandaTokenType("COMMA")
    @JvmField val DOT = MiniPandaTokenType("DOT")
    @JvmField val COLON = MiniPandaTokenType("COLON")
    @JvmField val SEMICOLON = MiniPandaTokenType("SEMICOLON")

    // Comments
    @JvmField val LINE_COMMENT = MiniPandaTokenType("LINE_COMMENT")
    @JvmField val BLOCK_COMMENT = MiniPandaTokenType("BLOCK_COMMENT")

    // Other
    @JvmField val NEWLINE = MiniPandaTokenType("NEWLINE")
    @JvmField val WHITE_SPACE = MiniPandaTokenType("WHITE_SPACE")
    @JvmField val BAD_CHARACTER = MiniPandaTokenType("BAD_CHARACTER")
}
