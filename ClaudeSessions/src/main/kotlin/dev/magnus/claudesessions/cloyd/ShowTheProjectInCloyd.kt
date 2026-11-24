package dev.magnus.claudesessions.cloyd

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runInterruptible

/** As a project opens, has Cloyd show its window, unless its Projects Dashboard is open: [ProjectWindowInCloyd]. */
class ShowTheProjectInCloyd : ProjectActivity {
    override suspend fun execute(project: Project) = runInterruptible(Dispatchers.IO) { ProjectWindowInCloyd(project).show(WhyTheWindowIsAskedFor.IDE_OPENED_THE_PROJECT) }
}
