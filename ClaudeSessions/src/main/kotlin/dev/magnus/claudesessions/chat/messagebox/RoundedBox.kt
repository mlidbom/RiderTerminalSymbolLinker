package dev.magnus.claudesessions.chat.messagebox

import com.intellij.ui.JBColor
import com.intellij.util.ui.GraphicsUtil
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Color
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.geom.RoundRectangle2D
import javax.swing.JPanel

/**
 * The frame of the chat's input box: a box with rounded corners, filled with [fill], and outlined, in the theme's focus
 * color while [isFocused], as the composer of Claude's desktop app is. Magnus, 2026-11-20, of that: "Could it get to
 * look something like this?"
 */
internal class RoundedBox(private val fill: Color, private val isFocused: () -> Boolean) : JPanel(BorderLayout(JBUI.scale(6), 0)) {
    init {
        isOpaque = false
        border = JBUI.Borders.empty(8, 12, 6, 6)
    }

    override fun paintComponent(graphics: Graphics) {
        val g = graphics.create() as Graphics2D
        try {
            GraphicsUtil.setupAAPainting(g)
            val arc = JBUI.scale(ARC).toFloat()
            val outline = RoundRectangle2D.Float(0.5f, 0.5f, width - 1f, height - 1f, arc, arc)
            g.color = fill
            g.fill(outline)
            g.color = if (isFocused()) JBUI.CurrentTheme.Focus.focusColor() else JBColor.border()
            g.draw(outline)
        } finally {
            g.dispose()
        }
    }

    private companion object {
        /** How round its corners are, unscaled: the diameter of the arc. */
        const val ARC = 18
    }
}
