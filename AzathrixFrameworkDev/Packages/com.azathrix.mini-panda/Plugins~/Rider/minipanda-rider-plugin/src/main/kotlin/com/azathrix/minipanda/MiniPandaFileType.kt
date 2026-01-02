package com.azathrix.minipanda

import com.intellij.openapi.fileTypes.LanguageFileType
import javax.swing.Icon

object MiniPandaFileType : LanguageFileType(MiniPandaLanguage) {
    override fun getName(): String = "MiniPanda"
    override fun getDescription(): String = "MiniPanda script file"
    override fun getDefaultExtension(): String = "panda"
    override fun getIcon(): Icon? = MiniPandaIcons.FILE
}
