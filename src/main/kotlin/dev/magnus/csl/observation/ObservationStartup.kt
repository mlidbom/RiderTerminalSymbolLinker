package dev.magnus.csl.observation

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/** Starts the [ObservationChannel] with the first project opened. The channel listens only in the sandbox Rider. */
class ObservationStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        ObservationChannel.getInstance()
    }
}
