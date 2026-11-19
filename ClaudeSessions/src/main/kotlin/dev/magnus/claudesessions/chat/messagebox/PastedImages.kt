package dev.magnus.claudesessions.chat.messagebox

import com.intellij.openapi.application.PathManager
import com.intellij.util.ui.ImageUtil
import java.awt.Image
import java.nio.file.Files
import java.nio.file.Path
import java.time.Duration
import java.time.Instant
import java.util.UUID
import javax.imageio.ImageIO

/**
 * The files the images pasted into a [MessageBox] are saved to, as PNG, so that Claude Code can be handed each one's
 * path: `claude-sessions/pasted-images` in the IDE's system folder.
 *
 * Claude Code reads an image as its path is pasted, so a file is needed only until the message is sent. Each file is
 * kept a week all the same, as a message may wait in the box, and then deleted as the next image is saved.
 */
object PastedImages {
    private val folder: Path get() = Path.of(PathManager.getSystemPath(), "claude-sessions", "pasted-images")

    private val KEPT_FOR: Duration = Duration.ofDays(7)

    /** Saves [image] to a file of its own, and returns the file. */
    fun save(image: Image): Path {
        Files.createDirectories(folder)
        deleteThoseKeptLongEnough()
        val file = folder.resolve("${Instant.now().toEpochMilli()}-${UUID.randomUUID()}.png")
        ImageIO.write(ImageUtil.toBufferedImage(image), "png", file.toFile())
        return file
    }

    private fun deleteThoseKeptLongEnough() {
        val before = Instant.now().minus(KEPT_FOR)
        Files.newDirectoryStream(folder, "*.png").use { files -> files.filter { Files.getLastModifiedTime(it).toInstant().isBefore(before) }.forEach(Files::deleteIfExists) }
    }
}
