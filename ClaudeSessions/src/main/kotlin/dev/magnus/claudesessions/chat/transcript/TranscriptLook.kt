package dev.magnus.claudesessions.chat.transcript

import androidx.compose.ui.graphics.Color
import com.intellij.openapi.editor.colors.EditorColors
import com.intellij.openapi.editor.colors.EditorColorsManager
import com.intellij.ui.JBColor
import com.intellij.util.ui.JBUI
import com.intellij.util.ui.NamedColorUtil
import com.intellij.util.ui.UIUtil
import org.jetbrains.jewel.bridge.toComposeColor

/**
 * The colors the [TranscriptPane] shows the conversation in, the IDE's own, so that it follows the IDE's theme: read
 * anew each time, as the theme can change.
 */
object TranscriptLook {
    /** Behind the conversation: the editor's, so that it reads as a document. */
    val background: Color get() = EditorColorsManager.getInstance().globalScheme.defaultBackground.toComposeColor()

    /** Behind what the user said, and behind a tool call's body. */
    val shaded: Color get() = JBUI.CurrentTheme.ActionButton.hoverBackground().toComposeColor()

    /** Of what is said about the conversation rather than in it: tool calls' headers, thinking, captions, dividers. */
    val secondaryText: Color get() = UIUtil.getContextHelpForeground().toComposeColor()

    /** Of a tool call that failed. */
    val error: Color get() = NamedColorUtil.getErrorForeground().toComposeColor()

    /** Behind each place Find found what it seeks: the editor's. */
    val found: Color get() = (EditorColorsManager.getInstance().globalScheme.getAttributes(EditorColors.SEARCH_RESULT_ATTRIBUTES)?.backgroundColor ?: JBColor.YELLOW).toComposeColor()

    /** Behind the place Find found that the user is at: the editor's selection, as Find selects it in an editor. */
    val foundCurrent: Color get() = (EditorColorsManager.getInstance().globalScheme.getColor(EditorColors.SELECTION_BACKGROUND_COLOR) ?: JBColor.ORANGE).toComposeColor()

    /** Of a link. */
    val link: Color get() = JBUI.CurrentTheme.Link.Foreground.ENABLED.toComposeColor()
}
