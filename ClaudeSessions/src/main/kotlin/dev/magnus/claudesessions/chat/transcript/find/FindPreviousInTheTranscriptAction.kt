package dev.magnus.claudesessions.chat.transcript.find

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.project.DumbAwareAction
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * Goes to the place before in what Find found in the [TranscriptPane] the focus is in, as Find Previous does in an
 * editor, with its shortcuts, such as Shift+F3: plugin.xml gives it Find Previous's. Disabled while Find seeks nothing
 * there, and everywhere else.
 */
class FindPreviousInTheTranscriptAction : DumbAwareAction() {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.getData(TranscriptPane.KEY)?.find?.query?.isNotEmpty() == true
    }

    override fun actionPerformed(event: AnActionEvent) {
        val pane = event.getData(TranscriptPane.KEY) ?: return
        pane.find.previous()
        pane.findBar.update()
    }

    companion object {
        /** The action's id in plugin.xml. */
        const val ID = "ClaudeSessions.FindPreviousInTheTranscript"
    }
}
