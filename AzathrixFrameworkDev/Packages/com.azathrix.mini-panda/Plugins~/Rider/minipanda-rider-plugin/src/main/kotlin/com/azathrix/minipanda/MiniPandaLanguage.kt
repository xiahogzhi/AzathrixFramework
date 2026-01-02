package com.azathrix.minipanda

import com.intellij.lang.Language

object MiniPandaLanguage : Language("MiniPanda") {
    override fun getDisplayName(): String = "MiniPanda"
    override fun isCaseSensitive(): Boolean = true
}
