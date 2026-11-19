package dev.magnus.claudesessions

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.project.Project

/** What Claude Sessions tells the user in the IDE's notifications: its notification group, which plugin.xml registers. */
object ClaudeSessionsNotifications {
    private const val GROUP = "Claude Sessions"

    /** Tells the user [content], under [title], in [project]'s window, or the IDE's active one when [project] is null. */
    fun tell(project: Project?, title: String, content: String, type: NotificationType) {
        NotificationGroupManager.getInstance().getNotificationGroup(GROUP).createNotification(title, content, type).notify(project)
    }
}
