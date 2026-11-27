package dev.magnus.csl

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.project.Project

/**
 * What a click on a terminal link tells the user when it opens nothing: a balloon, which it also reports to
 * [LinkClickOutcomes].
 */
internal object LinkNotices {
    /** That the link leads nowhere: [message], such as `No symbol "Circle.Area" found`. */
    fun tellNothingFound(project: Project, message: String) = tell(project, message, LinkClickOutcome.NothingFound(message))

    /** That what the link leads to couldn't be opened: [message] says why. */
    fun tellNotOpened(project: Project, message: String) = tell(project, message, LinkClickOutcome.NotOpened(message))

    private fun tell(project: Project, message: String, outcome: LinkClickOutcome) {
        NotificationGroupManager.getInstance()
            .getNotificationGroup("RiderTerminalSymbolLinker")
            .createNotification(message, NotificationType.INFORMATION)
            .notify(project)
        LinkClickOutcomes.report(outcome)
    }
}
