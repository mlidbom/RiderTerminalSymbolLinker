package dev.magnus.claudesessions.chat.messagebox

import com.intellij.openapi.fileEditor.OpenFileDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.LocalFileSystem
import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import com.intellij.util.ui.UIUtil
import dev.magnus.claudesessions.chat.claudecode.PastedImage
import java.awt.BorderLayout
import java.awt.Cursor
import java.awt.FlowLayout
import java.awt.Image
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.nio.file.Files
import java.nio.file.Path
import javax.imageio.ImageIO
import javax.swing.Icon
import javax.swing.ImageIcon
import javax.swing.JComponent
import javax.swing.JPanel
import javax.swing.SwingConstants

/**
 * The strip above a [MessageBox]'s editor that shows the images the box holds, each as a thumbnail with its
 * place-holder's text beneath it, such as `[Image #1]`, in the order the place-holders are in. Clicking one opens its
 * image in the IDE's image viewer, at full size. Magnus, 2026-11-20: "Could thumbnails appear above the text editor
 * itself with the names beneath them "[Image #1]" and clicking them shows them full size?"
 *
 * It takes no room while the box holds no image.
 *
 * Its methods run on the EDT.
 */
class PastedImageThumbnails(private val project: Project) {
    val component: JComponent = JPanel(FlowLayout(FlowLayout.LEFT, JBUI.scale(6), JBUI.scale(4))).apply {
        isOpaque = false
        isVisible = false
    }

    /** The images the strip shows a thumbnail of, in their order. */
    var shown: List<PastedImage> = emptyList()
        private set

    /** Each image's thumbnail, once read from its file: null when the file is gone, as a week after it was pasted. */
    private val thumbnailsOf = mutableMapOf<Path, Icon?>()

    /** Shows a thumbnail of each of [images], in their order, and of no other. */
    fun show(images: List<PastedImage>) {
        if (images == shown) return
        shown = images
        thumbnailsOf.keys.retainAll(images.map { it.file }.toSet())
        component.removeAll()
        images.forEach { component.add(thumbnailOf(it)) }
        component.isVisible = images.isNotEmpty()
        component.revalidate()
        component.repaint()
    }

    /** Opens [image] in the IDE's image viewer, at full size, as clicking its thumbnail does. */
    fun open(image: PastedImage) {
        val file = LocalFileSystem.getInstance().refreshAndFindFileByNioFile(image.file) ?: return
        OpenFileDescriptor(project, file).navigate(true)
    }

    /** The thumbnail of [image], with its place-holder's text beneath it, which opens it when clicked. */
    private fun thumbnailOf(image: PastedImage): JComponent = JPanel(BorderLayout()).apply {
        isOpaque = false
        add(JBLabel().apply { icon = thumbnailsOf.getOrPut(image.file) { readTheThumbnailOf(image.file) } }, BorderLayout.CENTER)
        add(JBLabel(image.text, SwingConstants.CENTER).apply { foreground = UIUtil.getContextHelpForeground() }, BorderLayout.SOUTH)
        cursor = Cursor.getPredefinedCursor(Cursor.HAND_CURSOR)
        toolTipText = "Click to see it full size"
        addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(event: MouseEvent) = open(image)
        })
    }

    /** The image in [file], scaled down to fit a thumbnail, or null when the file is gone. */
    private fun readTheThumbnailOf(file: Path): Icon? {
        if (!Files.isRegularFile(file)) return null
        val image = checkNotNull(ImageIO.read(file.toFile())) { "$file, which the box saved a pasted image to, holds no image" }
        val side = JBUI.scale(THUMBNAIL_SIDE)
        val scale = minOf(side.toDouble() / image.width, side.toDouble() / image.height, 1.0)
        return ImageIcon(image.getScaledInstance((image.width * scale).toInt().coerceAtLeast(1), (image.height * scale).toInt().coerceAtLeast(1), Image.SCALE_SMOOTH))
    }

    private companion object {
        /** How wide and tall a thumbnail is at most, unscaled. */
        const val THUMBNAIL_SIDE = 56
    }
}
