package com.azathrix.minipanda

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet

class MiniPandaParserDefinition : ParserDefinition {
    companion object {
        val FILE = IFileElementType(MiniPandaLanguage)
        val COMMENTS = TokenSet.create(MiniPandaTokenTypes.LINE_COMMENT, MiniPandaTokenTypes.BLOCK_COMMENT)
        val STRINGS = TokenSet.create(MiniPandaTokenTypes.STRING)
        val WHITE_SPACES = TokenSet.create(MiniPandaTokenTypes.WHITE_SPACE, MiniPandaTokenTypes.NEWLINE)
    }

    override fun createLexer(project: Project?): Lexer = MiniPandaLexer()

    override fun createParser(project: Project?): PsiParser = MiniPandaParser()

    override fun getFileNodeType(): IFileElementType = FILE

    override fun getCommentTokens(): TokenSet = COMMENTS

    override fun getStringLiteralElements(): TokenSet = STRINGS

    override fun getWhitespaceTokens(): TokenSet = WHITE_SPACES

    override fun createElement(node: ASTNode?): PsiElement = MiniPandaPsiElement(node!!)

    override fun createFile(viewProvider: FileViewProvider): PsiFile = MiniPandaFile(viewProvider)
}
