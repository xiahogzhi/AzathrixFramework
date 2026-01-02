package com.azathrix.minipanda

import com.intellij.extapi.psi.ASTWrapperPsiElement
import com.intellij.extapi.psi.PsiFileBase
import com.intellij.lang.ASTNode
import com.intellij.openapi.fileTypes.FileType
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement

class MiniPandaPsiElement(node: ASTNode) : ASTWrapperPsiElement(node)

class MiniPandaFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, MiniPandaLanguage) {
    override fun getFileType(): FileType = MiniPandaFileType
    override fun toString(): String = "MiniPanda File"
}
