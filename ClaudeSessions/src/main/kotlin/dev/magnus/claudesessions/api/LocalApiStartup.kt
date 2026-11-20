package dev.magnus.claudesessions.api

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

/** Starts the IDE's [LocalApi] with the first project that opens. */
class LocalApiStartup : ProjectActivity {
    override suspend fun execute(project: Project) {
        LocalApi.start()
    }
}
