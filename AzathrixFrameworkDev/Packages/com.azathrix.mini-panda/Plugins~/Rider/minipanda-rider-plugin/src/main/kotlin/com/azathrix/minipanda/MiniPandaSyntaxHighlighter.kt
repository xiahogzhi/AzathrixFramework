package com.azathrix.minipanda

import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.HighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.editor.colors.TextAttributesKey.createTextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterBase
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.tree.IElementType

object MiniPandaHighlightingColors {
    val KEYWORD = createTextAttributesKey("MINIPANDA_KEYWORD", DefaultLanguageHighlighterColors.KEYWORD)
    val NUMBER = createTextAttributesKey("MINIPANDA_NUMBER", DefaultLanguageHighlighterColors.NUMBER)
    val STRING = createTextAttributesKey("MINIPANDA_STRING", DefaultLanguageHighlighterColors.STRING)
    val IDENTIFIER = createTextAttributesKey("MINIPANDA_IDENTIFIER", DefaultLanguageHighlighterColors.IDENTIFIER)
    val LINE_COMMENT = createTextAttributesKey("MINIPANDA_LINE_COMMENT", DefaultLanguageHighlighterColors.LINE_COMMENT)
    val BLOCK_COMMENT = createTextAttributesKey("MINIPANDA_BLOCK_COMMENT", DefaultLanguageHighlighterColors.BLOCK_COMMENT)
    val OPERATOR = createTextAttributesKey("MINIPANDA_OPERATOR", DefaultLanguageHighlighterColors.OPERATION_SIGN)
    val BRACES = createTextAttributesKey("MINIPANDA_BRACES", DefaultLanguageHighlighterColors.BRACES)
    val BRACKETS = createTextAttributesKey("MINIPANDA_BRACKETS", DefaultLanguageHighlighterColors.BRACKETS)
    val PARENTHESES = createTextAttributesKey("MINIPANDA_PARENTHESES", DefaultLanguageHighlighterColors.PARENTHESES)
    val COMMA = createTextAttributesKey("MINIPANDA_COMMA", DefaultLanguageHighlighterColors.COMMA)
    val DOT = createTextAttributesKey("MINIPANDA_DOT", DefaultLanguageHighlighterColors.DOT)
    val BAD_CHARACTER = createTextAttributesKey("MINIPANDA_BAD_CHARACTER", HighlighterColors.BAD_CHARACTER)
}

class MiniPandaSyntaxHighlighter : SyntaxHighlighterBase() {
    override fun getHighlightingLexer(): Lexer = MiniPandaLexer()

    override fun getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey> {
        return when (tokenType) {
            MiniPandaTokenTypes.VAR, MiniPandaTokenTypes.FUNC, MiniPandaTokenTypes.CLASS,
            MiniPandaTokenTypes.IF, MiniPandaTokenTypes.ELSE, MiniPandaTokenTypes.WHILE,
            MiniPandaTokenTypes.FOR, MiniPandaTokenTypes.IN, MiniPandaTokenTypes.RETURN,
            MiniPandaTokenTypes.BREAK, MiniPandaTokenTypes.CONTINUE,
            MiniPandaTokenTypes.TRUE, MiniPandaTokenTypes.FALSE, MiniPandaTokenTypes.NULL,
            MiniPandaTokenTypes.THIS, MiniPandaTokenTypes.SUPER,
            MiniPandaTokenTypes.IMPORT, MiniPandaTokenTypes.AS
            -> arrayOf(MiniPandaHighlightingColors.KEYWORD)

            MiniPandaTokenTypes.NUMBER -> arrayOf(MiniPandaHighlightingColors.NUMBER)
            MiniPandaTokenTypes.STRING -> arrayOf(MiniPandaHighlightingColors.STRING)
            MiniPandaTokenTypes.IDENTIFIER -> arrayOf(MiniPandaHighlightingColors.IDENTIFIER)
            MiniPandaTokenTypes.LINE_COMMENT -> arrayOf(MiniPandaHighlightingColors.LINE_COMMENT)
            MiniPandaTokenTypes.BLOCK_COMMENT -> arrayOf(MiniPandaHighlightingColors.BLOCK_COMMENT)

            MiniPandaTokenTypes.PLUS, MiniPandaTokenTypes.MINUS, MiniPandaTokenTypes.STAR,
            MiniPandaTokenTypes.SLASH, MiniPandaTokenTypes.PERCENT, MiniPandaTokenTypes.EQ,
            MiniPandaTokenTypes.EQEQ, MiniPandaTokenTypes.BANGEQ, MiniPandaTokenTypes.LT,
            MiniPandaTokenTypes.GT, MiniPandaTokenTypes.LTEQ, MiniPandaTokenTypes.GTEQ,
            MiniPandaTokenTypes.AMPAMP, MiniPandaTokenTypes.PIPEPIPE, MiniPandaTokenTypes.BANG,
            MiniPandaTokenTypes.ARROW
            -> arrayOf(MiniPandaHighlightingColors.OPERATOR)

            MiniPandaTokenTypes.LBRACE, MiniPandaTokenTypes.RBRACE -> arrayOf(MiniPandaHighlightingColors.BRACES)
            MiniPandaTokenTypes.LBRACKET, MiniPandaTokenTypes.RBRACKET -> arrayOf(MiniPandaHighlightingColors.BRACKETS)
            MiniPandaTokenTypes.LPAREN, MiniPandaTokenTypes.RPAREN -> arrayOf(MiniPandaHighlightingColors.PARENTHESES)
            MiniPandaTokenTypes.COMMA -> arrayOf(MiniPandaHighlightingColors.COMMA)
            MiniPandaTokenTypes.DOT -> arrayOf(MiniPandaHighlightingColors.DOT)

            MiniPandaTokenTypes.BAD_CHARACTER -> arrayOf(MiniPandaHighlightingColors.BAD_CHARACTER)
            else -> emptyArray()
        }
    }
}

class MiniPandaSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter {
        return MiniPandaSyntaxHighlighter()
    }
}
