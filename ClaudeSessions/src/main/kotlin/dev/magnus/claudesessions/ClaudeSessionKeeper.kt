package dev.magnus.claudesessions

import com.intellij.openapi.application.EDT
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.coroutines.coroutineContext
import kotlin.time.Duration.Companion.seconds

/**
 * Keeps a project's [SavedClaudeSessions] current: every two seconds it looks at which session each tab of the
 * [ClaudeSessionsView] runs, and saves them when they changed, and it saves them one last time as the project closes. So
 * the saved sessions are those that ran when the IDE exits, or within two seconds of when it crashes.
 *
 * It starts once the view has resumed the saved sessions, so that it never saves the view before they're in it, and
 * stops as the project starts closing: the IDE then ends the tabs' processes.
 */
@Service(Service.Level.PROJECT)
class ClaudeSessionKeeper(private val project: Project, private val scope: CoroutineScope) {
    @Volatile
    private var keeping: Job? = null

    /** Whether it keeps the saved sessions current now, which it does from when the view has opened until the project closes. */
    val isKeeping: Boolean get() = keeping?.isActive == true

    fun startKeeping() {
        if (isKeeping) return
        keeping = scope.launch { keep() }
    }

    /**
     * Saves the sessions the view's tabs run now, and stops: the project is closing, or the plugin unloading, and the
     * tabs are about to go. Saved now, a session started since the last look is saved too. Unless it doesn't keep them,
     * as before the view has resumed the saved sessions, when they're left as they are.
     */
    fun saveOneLastTimeAndStop() {
        if (!isKeeping) return
        keeping?.cancel()
        SavedClaudeSessions(project).write(ClaudeSessionsView.getInstance(project).tabs.mapNotNull { it.lookAtTheSession() })
    }

    private suspend fun keep() {
        val saved = SavedClaudeSessions(project)
        var lastSaved = saved.read()
        while (true) {
            val sessions = sessionsInTheView()
            if (sessions != lastSaved) {
                coroutineContext.ensureActive() // The project may have started closing while this looked.
                saved.write(sessions)
                lastSaved = sessions
            }
            delay(HOW_OFTEN_IT_LOOKS)
        }
    }

    private suspend fun sessionsInTheView(): List<ClaudeSession> {
        val tabs = withContext(Dispatchers.EDT) { ClaudeSessionsView.getInstance(project).tabs }
        return withContext(Dispatchers.IO) { tabs.mapNotNull { it.lookAtTheSession() } }
    }

    companion object {
        /** How often it looks at the sessions in the view. */
        private val HOW_OFTEN_IT_LOOKS = 2.seconds

        fun getInstance(project: Project): ClaudeSessionKeeper = project.service()
    }
}
