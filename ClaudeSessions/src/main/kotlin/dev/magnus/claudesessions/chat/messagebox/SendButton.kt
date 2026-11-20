package dev.magnus.claudesessions.chat.messagebox

import com.intellij.ui.JBColor
import com.intellij.util.ui.GraphicsUtil
import com.intellij.util.ui.JBUI
import java.awt.BasicStroke
import java.awt.Color
import java.awt.Cursor
import java.awt.Dimension
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.awt.geom.Ellipse2D
import java.awt.geom.Path2D
import java.awt.geom.RoundRectangle2D
import javax.swing.JComponent

/**
 * The round button at the right of the chat's input box, as in Claude's desktop app and claude.ai: an arrow that sends
 * what the box holds, as Ctrl+Enter does, or, while Claude is busy and the box is empty, a square that interrupts
 * Claude, as Escape does. It does nothing while the box is empty and Claude isn't busy.
 */
internal class SendButton(private val send: () -> Unit, private val interrupt: () -> Unit) : JComponent() {
    /** What a click does, as the button shows it. */
    enum class Does { SEND, STOP, NOTHING }

    var does: Does = Does.NOTHING
        private set

    init {
        addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(event: MouseEvent) = click()
        })
        update()
    }

    /** Shows what a click does, as the box holds no text, or some, when [boxIsEmpty], and Claude [isBusy] or not. */
    fun show(boxIsEmpty: Boolean, isBusy: Boolean) {
        does = when {
            !boxIsEmpty -> Does.SEND
            isBusy -> Does.STOP
            else -> Does.NOTHING
        }
        update()
    }

    /** Does what a click does: sends, stops Claude, or nothing. */
    fun click() = when (does) {
        Does.SEND -> send()
        Does.STOP -> interrupt()
        Does.NOTHING -> {}
    }

    private fun update() {
        cursor = Cursor.getPredefinedCursor(if (does == Does.NOTHING) Cursor.DEFAULT_CURSOR else Cursor.HAND_CURSOR)
        toolTipText = when (does) {
            Does.SEND -> "Send: Ctrl+Enter"
            Does.STOP -> "Stop Claude: Escape"
            Does.NOTHING -> null
        }
        repaint()
    }

    override fun getPreferredSize(): Dimension = JBUI.size(DIAMETER, DIAMETER)

    override fun getMinimumSize(): Dimension = preferredSize

    override fun paintComponent(graphics: Graphics) {
        val g = graphics.create() as Graphics2D
        try {
            GraphicsUtil.setupAAPainting(g)
            val diameter = minOf(width, height).toFloat()
            g.color = if (does == Does.NOTHING) JBColor.border() else CLAUDE
            g.fill(Ellipse2D.Float(0f, 0f, diameter, diameter))
            g.color = Color.WHITE
            if (does == Does.STOP) paintASquare(g, diameter) else paintAnArrowUp(g, diameter)
        } finally {
            g.dispose()
        }
    }

    private fun paintAnArrowUp(g: Graphics2D, diameter: Float) {
        val middle = diameter / 2
        val reach = diameter * 0.26f
        g.stroke = BasicStroke(JBUI.scale(2).toFloat(), BasicStroke.CAP_ROUND, BasicStroke.JOIN_ROUND)
        g.draw(Path2D.Float().apply {
            moveTo(middle, middle + reach)
            lineTo(middle, middle - reach)
            moveTo(middle - reach * 0.8f, middle - reach * 0.2f)
            lineTo(middle, middle - reach)
            lineTo(middle + reach * 0.8f, middle - reach * 0.2f)
        })
    }

    private fun paintASquare(g: Graphics2D, diameter: Float) {
        val side = diameter * 0.36f
        val corner = (diameter - side) / 2
        g.fill(RoundRectangle2D.Float(corner, corner, side, side, side / 4, side / 4))
    }

    private companion object {
        /** How wide and tall the button is, unscaled. */
        const val DIAMETER = 26

        /** The orange of Claude's own apps' send button. */
        val CLAUDE = JBColor(Color(0xD97757), Color(0xD97757))
    }
}
