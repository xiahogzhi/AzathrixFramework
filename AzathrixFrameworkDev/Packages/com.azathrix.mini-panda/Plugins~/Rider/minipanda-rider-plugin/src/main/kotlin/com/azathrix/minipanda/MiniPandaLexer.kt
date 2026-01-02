package com.azathrix.minipanda

import com.intellij.lexer.LexerBase
import com.intellij.psi.tree.IElementType

class MiniPandaLexer : LexerBase() {
    private var buffer: CharSequence = ""
    private var startOffset = 0
    private var endOffset = 0
    private var position = 0
    private var tokenStart = 0
    private var tokenEnd = 0
    private var tokenType: IElementType? = null

    private val keywords = mapOf(
        "var" to MiniPandaTokenTypes.VAR,
        "func" to MiniPandaTokenTypes.FUNC,
        "class" to MiniPandaTokenTypes.CLASS,
        "if" to MiniPandaTokenTypes.IF,
        "else" to MiniPandaTokenTypes.ELSE,
        "while" to MiniPandaTokenTypes.WHILE,
        "for" to MiniPandaTokenTypes.FOR,
        "in" to MiniPandaTokenTypes.IN,
        "return" to MiniPandaTokenTypes.RETURN,
        "break" to MiniPandaTokenTypes.BREAK,
        "continue" to MiniPandaTokenTypes.CONTINUE,
        "true" to MiniPandaTokenTypes.TRUE,
        "false" to MiniPandaTokenTypes.FALSE,
        "null" to MiniPandaTokenTypes.NULL,
        "this" to MiniPandaTokenTypes.THIS,
        "super" to MiniPandaTokenTypes.SUPER,
        "import" to MiniPandaTokenTypes.IMPORT,
        "as" to MiniPandaTokenTypes.AS
    )

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.startOffset = startOffset
        this.endOffset = endOffset
        this.position = startOffset
        advance()
    }

    override fun getState(): Int = 0
    override fun getTokenType(): IElementType? = tokenType
    override fun getTokenStart(): Int = tokenStart
    override fun getTokenEnd(): Int = tokenEnd
    override fun getBufferSequence(): CharSequence = buffer
    override fun getBufferEnd(): Int = endOffset

    override fun advance() {
        tokenStart = position
        if (position >= endOffset) {
            tokenType = null
            return
        }

        val c = buffer[position]

        when {
            c == '/' && peek(1) == '/' -> scanLineComment()
            c == '/' && peek(1) == '*' -> scanBlockComment()
            c.isWhitespace() && c != '\n' -> scanWhitespace()
            c == '\n' -> { position++; tokenType = MiniPandaTokenTypes.NEWLINE }
            c.isDigit() -> scanNumber()
            c == '"' -> scanString()
            c.isLetter() || c == '_' -> scanIdentifier()
            else -> scanOperator()
        }

        tokenEnd = position
    }

    private fun peek(offset: Int): Char? {
        val idx = position + offset
        return if (idx < endOffset) buffer[idx] else null
    }

    private fun scanWhitespace() {
        while (position < endOffset && buffer[position].isWhitespace() && buffer[position] != '\n') {
            position++
        }
        tokenType = MiniPandaTokenTypes.WHITE_SPACE
    }

    private fun scanLineComment() {
        position += 2
        while (position < endOffset && buffer[position] != '\n') {
            position++
        }
        tokenType = MiniPandaTokenTypes.LINE_COMMENT
    }

    private fun scanBlockComment() {
        position += 2
        while (position < endOffset - 1) {
            if (buffer[position] == '*' && buffer[position + 1] == '/') {
                position += 2
                break
            }
            position++
        }
        tokenType = MiniPandaTokenTypes.BLOCK_COMMENT
    }

    private fun scanNumber() {
        while (position < endOffset && (buffer[position].isDigit() || buffer[position] == '.')) {
            position++
        }
        tokenType = MiniPandaTokenTypes.NUMBER
    }

    private fun scanString() {
        position++ // skip opening quote
        while (position < endOffset && buffer[position] != '"') {
            if (buffer[position] == '\\' && position + 1 < endOffset) {
                position += 2
            } else {
                position++
            }
        }
        if (position < endOffset) position++ // skip closing quote
        tokenType = MiniPandaTokenTypes.STRING
    }

    private fun scanIdentifier() {
        while (position < endOffset && (buffer[position].isLetterOrDigit() || buffer[position] == '_')) {
            position++
        }
        val text = buffer.subSequence(tokenStart, position).toString()
        tokenType = keywords[text] ?: MiniPandaTokenTypes.IDENTIFIER
    }

    private fun scanOperator() {
        val c = buffer[position]
        val next = peek(1)

        tokenType = when {
            c == '=' && next == '=' -> { position += 2; MiniPandaTokenTypes.EQEQ }
            c == '!' && next == '=' -> { position += 2; MiniPandaTokenTypes.BANGEQ }
            c == '<' && next == '=' -> { position += 2; MiniPandaTokenTypes.LTEQ }
            c == '>' && next == '=' -> { position += 2; MiniPandaTokenTypes.GTEQ }
            c == '&' && next == '&' -> { position += 2; MiniPandaTokenTypes.AMPAMP }
            c == '|' && next == '|' -> { position += 2; MiniPandaTokenTypes.PIPEPIPE }
            c == '=' && next == '>' -> { position += 2; MiniPandaTokenTypes.ARROW }
            else -> {
                position++
                when (c) {
                    '+' -> MiniPandaTokenTypes.PLUS
                    '-' -> MiniPandaTokenTypes.MINUS
                    '*' -> MiniPandaTokenTypes.STAR
                    '/' -> MiniPandaTokenTypes.SLASH
                    '%' -> MiniPandaTokenTypes.PERCENT
                    '=' -> MiniPandaTokenTypes.EQ
                    '<' -> MiniPandaTokenTypes.LT
                    '>' -> MiniPandaTokenTypes.GT
                    '!' -> MiniPandaTokenTypes.BANG
                    '(' -> MiniPandaTokenTypes.LPAREN
                    ')' -> MiniPandaTokenTypes.RPAREN
                    '{' -> MiniPandaTokenTypes.LBRACE
                    '}' -> MiniPandaTokenTypes.RBRACE
                    '[' -> MiniPandaTokenTypes.LBRACKET
                    ']' -> MiniPandaTokenTypes.RBRACKET
                    ',' -> MiniPandaTokenTypes.COMMA
                    '.' -> MiniPandaTokenTypes.DOT
                    ':' -> MiniPandaTokenTypes.COLON
                    ';' -> MiniPandaTokenTypes.SEMICOLON
                    else -> MiniPandaTokenTypes.BAD_CHARACTER
                }
            }
        }
    }
}
