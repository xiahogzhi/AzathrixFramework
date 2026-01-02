package com.azathrix.minipanda

import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage
import javax.swing.Icon

class MiniPandaColorSettingsPage : ColorSettingsPage {
    companion object {
        private val DESCRIPTORS = arrayOf(
            AttributesDescriptor("Keyword", MiniPandaHighlightingColors.KEYWORD),
            AttributesDescriptor("Number", MiniPandaHighlightingColors.NUMBER),
            AttributesDescriptor("String", MiniPandaHighlightingColors.STRING),
            AttributesDescriptor("Identifier", MiniPandaHighlightingColors.IDENTIFIER),
            AttributesDescriptor("Line comment", MiniPandaHighlightingColors.LINE_COMMENT),
            AttributesDescriptor("Block comment", MiniPandaHighlightingColors.BLOCK_COMMENT),
            AttributesDescriptor("Operator", MiniPandaHighlightingColors.OPERATOR),
            AttributesDescriptor("Braces", MiniPandaHighlightingColors.BRACES),
            AttributesDescriptor("Brackets", MiniPandaHighlightingColors.BRACKETS),
            AttributesDescriptor("Parentheses", MiniPandaHighlightingColors.PARENTHESES),
        )
    }

    override fun getIcon(): Icon? = MiniPandaIcons.FILE

    override fun getHighlighter(): SyntaxHighlighter = MiniPandaSyntaxHighlighter()

    override fun getDemoText(): String = """
        // MiniPanda 示例
        var x = 42
        var name = "hello"

        func add(a, b) {
            return a + b
        }

        class Player {
            func init(name) {
                this.name = name
                this.hp = 100
            }
        }

        /* 多行注释 */
        if x > 0 {
            print(x)
        }
    """.trimIndent()

    override fun getAdditionalHighlightingTagToDescriptorMap(): Map<String, TextAttributesKey>? = null

    override fun getAttributeDescriptors(): Array<AttributesDescriptor> = DESCRIPTORS

    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY

    override fun getDisplayName(): String = "MiniPanda"
}
