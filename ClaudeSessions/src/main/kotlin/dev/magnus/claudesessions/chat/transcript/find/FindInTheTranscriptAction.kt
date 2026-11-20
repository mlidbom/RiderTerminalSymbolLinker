package dev.magnus.claudesessions.chat.transcript.find

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.project.DumbAwareAction
import dev.magnus.claudesessions.chat.transcript.TranscriptPane

/**
 * Opens the find bar of the [TranscriptPane] the focus is in, as Find does in an editor, with Find's shortcuts, such as
 * Ctrl+F: plugin.xml gives it Find's. Disabled everywhere else, where Find runs.
 */
class FindInTheTranscriptAction : DumbAwareAction() {
    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.EDT

    override fun update(event: AnActionEvent) {
        event.presentation.isEnabledAndVisible = event.getData(TranscriptPane.KEY) != null
    }

    override fun actionPerformed(event: AnActionEvent) {
        event.getData(TranscriptPane.KEY)?.openFind()
    }

    companion object {
        /** The action's id in plugin.xml. */
        const val ID = "ClaudeSessions.FindInTheTranscript"
    }
}
