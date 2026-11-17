package dev.magnus.claudesessions.observation

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/** Starts the [ObservationChannel] with the first project opened. The channel listens only in the sandbox IDE. */
class ObservationStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        ObservationChannel.getInstance()
    }
}
