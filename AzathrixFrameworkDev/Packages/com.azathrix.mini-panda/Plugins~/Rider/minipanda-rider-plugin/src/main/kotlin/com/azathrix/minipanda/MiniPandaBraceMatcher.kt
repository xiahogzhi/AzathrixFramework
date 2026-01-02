package com.azathrix.minipanda

import com.intellij.lang.BracePair
import com.intellij.lang.PairedBraceMatcher
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType

class MiniPandaBraceMatcher : PairedBraceMatcher {
    override fun getPairs(): Array<BracePair> = arrayOf(
        BracePair(MiniPandaTokenTypes.LBRACE, MiniPandaTokenTypes.RBRACE, true),
        BracePair(MiniPandaTokenTypes.LBRACKET, MiniPandaTokenTypes.RBRACKET, false),
        BracePair(MiniPandaTokenTypes.LPAREN, MiniPandaTokenTypes.RPAREN, false)
    )

    override fun isPairedBracesAllowedBeforeType(lbraceType: IElementType, contextType: IElementType?): Boolean = true

    override fun getCodeConstructStart(file: PsiFile?, openingBraceOffset: Int): Int = openingBraceOffset
}
